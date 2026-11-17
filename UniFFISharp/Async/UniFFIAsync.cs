using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using UniFFISharp.Callbacks;
using UniFFISharp.Collections;
using UniFFISharp.Helpers;
using UniFFISharp.Types;

namespace UniFFISharp.Async;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void UniFfiFutureCallback(ulong continuationHandle, byte pollResult);

public sealed class UniffiForeignFutureHandle : IDisposable
{
    private const int StatePending = 0;
    private const int StateCompleted = 1;
    private const int StateDropped = 2;

    private CancellationTokenSource? _cts;
    private int _state;

    public CancellationTokenSource Cts
    {
        get
        {
            var existing = Volatile.Read(ref _cts);
            if (existing != null) return existing;

            var created = new CancellationTokenSource();
            var race = Interlocked.CompareExchange(ref _cts, created, null);
            if (race != null)
            {
                created.Dispose();
                return race;
            }

            if (Volatile.Read(ref _state) == StateDropped)
            {
                created.Cancel();
            }
            return created;
        }
    }

    public bool IsDropped => Volatile.Read(ref _state) == StateDropped;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryClaimCallback() => Interlocked.CompareExchange(ref _state, StateCompleted, StatePending) == StatePending;

    public void MarkDropped()
    {
        if (Interlocked.CompareExchange(ref _state, StateDropped, StatePending) == StatePending)
        {
            Volatile.Read(ref _cts)?.Cancel();
        }
    }

    public void InvokeCallbackOnce(Action invoke)
    {
        if (TryClaimCallback())
        {
            invoke();
        }
    }

    public void Dispose()
    {
        var cts = Interlocked.Exchange(ref _cts, null);
        cts?.Dispose();
    }
}

public static class _UniFFIAsync
{
    public const byte UNIFFI_RUST_FUTURE_POLL_READY = 0;
    public const byte UNIFFI_RUST_FUTURE_POLL_WAKE = 1;

    public delegate F CompleteFuncDelegate<F>(ulong handle, ref UniffiRustCallStatus status);
    public delegate void CompleteActionDelegate(ulong handle, ref UniffiRustCallStatus status);

    private sealed class UniffiFutureState : IValueTaskSource<byte>
    {
        private ManualResetValueTaskSourceCore<byte> _core = new() { RunContinuationsAsynchronously = true };
        public ulong RustFuture;
        public Action<ulong>? CancelFunc;
        public CancellationToken CancellationToken;
        public int PollSignaled;
        public int Canceled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<byte> AsValueTask() => new(this, _core.Version);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PrepareForPoll()
        {
            Volatile.Write(ref PollSignaled, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetForNextPoll()
        {
            _core.Reset();
            Volatile.Write(ref PollSignaled, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(byte result)
        {
            if (Interlocked.Exchange(ref PollSignaled, 1) == 0)
            {
                _core.SetResult(result);
            }
        }

        public void SetCanceled()
        {
            Volatile.Write(ref Canceled, 1);
            try { CancelFunc?.Invoke(RustFuture); } catch { }
            if (Interlocked.Exchange(ref PollSignaled, 1) == 0)
            {
                try { _core.SetException(new OperationCanceledException(CancellationToken)); } catch { }
            }
        }

        public void ResetForPool()
        {
            RustFuture = 0;
            CancelFunc = null;
            CancellationToken = default;
            PollSignaled = 0;
            Canceled = 0;
            _core.Reset();
        }

        public byte GetResult(short token) => _core.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);
    }

    private const int SharedPoolCapacity = 32;
    private static readonly UniffiFutureState?[] s_sharedPool = new UniffiFutureState?[SharedPoolCapacity];
    private static int s_sharedPoolHint;

    [ThreadStatic]
    private static UniffiFutureState? t_cachedState;

    private static readonly ConcurrentHandleMap<UniffiFutureState> _asyncHandleMap = new();

    public static readonly ConcurrentHandleMap<UniffiForeignFutureHandle> ForeignFuturesMap = new();

    public static unsafe IntPtr ContinuationCallbackPointer => (IntPtr)(delegate* unmanaged[Cdecl]<ulong, byte, void>)&OnContinuationCallback;
    public static unsafe IntPtr DroppedCallbackPointer => (IntPtr)(delegate* unmanaged[Cdecl]<ulong, void>)&OnForeignFutureDropped;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UniffiFutureState RentState(ulong rustFuture, Action<ulong>? cancelFunc, CancellationToken cancellationToken)
    {
        var state = t_cachedState;
        if (state != null)
        {
            t_cachedState = null;
        }
        else
        {
            int idx = (s_sharedPoolHint++ & (SharedPoolCapacity - 1));
            state = Interlocked.Exchange(ref s_sharedPool[idx], null) ?? new UniffiFutureState();
        }

        state.RustFuture = rustFuture;
        state.CancelFunc = cancelFunc;
        state.CancellationToken = cancellationToken;
        state.PollSignaled = 0;
        state.Canceled = 0;
        return state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnState(UniffiFutureState state)
    {
        state.ResetForPool();
        if (t_cachedState == null)
        {
            t_cachedState = state;
            return;
        }
        int idx = (s_sharedPoolHint++ & (SharedPoolCapacity - 1));
        Interlocked.CompareExchange(ref s_sharedPool[idx], state, null);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnForeignFutureDropped(ulong handle)
    {
        try
        {
            if (ForeignFuturesMap.Remove(handle, out var futureHandle) && futureHandle != null)
            {
                futureHandle.MarkDropped();
            }
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnContinuationCallback(ulong continuationHandle, byte pollResult)
    {
        try
        {
            if (_asyncHandleMap.TryGet(continuationHandle, out UniffiFutureState? state) && state != null)
            {
                state.SetResult(pollResult);
            }
        }
        catch
        {
        }
    }

    public static Task<T> UniffiRustCallAsync<T, F, E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteFuncDelegate<F> completeFunc,
        Action<ulong> freeFunc,
        Func<F, T> liftFunc,
        CallStatusErrorHandler<E> errorHandler,
        Action<ulong>? cancelFunc = null,
        CancellationToken cancellationToken = default
    ) where E : Exception =>
        UniffiRustCallValueTaskAsync(
            rustFuture, pollFunc, completeFunc, freeFunc, liftFunc, errorHandler, cancelFunc, cancellationToken
        ).AsTask();

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<T> UniffiRustCallValueTaskAsync<T, F, E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteFuncDelegate<F> completeFunc,
        Action<ulong> freeFunc,
        Func<F, T> liftFunc,
        CallStatusErrorHandler<E> errorHandler,
        Action<ulong>? cancelFunc = null,
        CancellationToken cancellationToken = default
    ) where E : Exception
    {
        var state = RentState(rustFuture, cancelFunc, cancellationToken);
        ulong mapEntry = _asyncHandleMap.Insert(state);
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static s => ((UniffiFutureState)s!).SetCanceled(), state);
        }

        bool pollCompletedNormally = false;
        try
        {
            byte pollResult;
            do
            {
                state.PrepareForPoll();
                cancellationToken.ThrowIfCancellationRequested();
                pollFunc(rustFuture, ContinuationCallbackPointer, mapEntry);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != UNIFFI_RUST_FUTURE_POLL_READY)
                {
                    state.ResetForNextPoll();
                }
            }
            while (pollResult != UNIFFI_RUST_FUTURE_POLL_READY);

            pollCompletedNormally = Volatile.Read(ref state.Canceled) == 0;

            var status = new UniffiRustCallStatus();
            var result = completeFunc(rustFuture, ref status);
            if (!status.IsSuccess()) UniffiHelpers.ThrowCallStatus(ref status, errorHandler);
            return liftFunc(result);
        }
        finally
        {
            reg.Dispose();
            _asyncHandleMap.Remove(mapEntry, out _);
            freeFunc(rustFuture);
            if (pollCompletedNormally)
            {
                ReturnState(state);
            }
        }
    }

    public static Task UniffiRustCallAsync<E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteActionDelegate completeFunc,
        Action<ulong> freeFunc,
        CallStatusErrorHandler<E> errorHandler,
        Action<ulong>? cancelFunc = null,
        CancellationToken cancellationToken = default
    ) where E : Exception =>
        UniffiRustCallValueTaskAsync(
            rustFuture, pollFunc, completeFunc, freeFunc, errorHandler, cancelFunc, cancellationToken
        ).AsTask();

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public static async ValueTask UniffiRustCallValueTaskAsync<E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteActionDelegate completeFunc,
        Action<ulong> freeFunc,
        CallStatusErrorHandler<E> errorHandler,
        Action<ulong>? cancelFunc = null,
        CancellationToken cancellationToken = default
    ) where E : Exception
    {
        var state = RentState(rustFuture, cancelFunc, cancellationToken);
        ulong mapEntry = _asyncHandleMap.Insert(state);
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static s => ((UniffiFutureState)s!).SetCanceled(), state);
        }

        bool pollCompletedNormally = false;
        try
        {
            byte pollResult;
            do
            {
                state.PrepareForPoll();
                cancellationToken.ThrowIfCancellationRequested();
                pollFunc(rustFuture, ContinuationCallbackPointer, mapEntry);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != UNIFFI_RUST_FUTURE_POLL_READY)
                {
                    state.ResetForNextPoll();
                }
            }
            while (pollResult != UNIFFI_RUST_FUTURE_POLL_READY);

            pollCompletedNormally = Volatile.Read(ref state.Canceled) == 0;

            var status = new UniffiRustCallStatus();
            completeFunc(rustFuture, ref status);
            if (!status.IsSuccess()) UniffiHelpers.ThrowCallStatus(ref status, errorHandler);
        }
        finally
        {
            reg.Dispose();
            _asyncHandleMap.Remove(mapEntry, out _);
            freeFunc(rustFuture);
            if (pollCompletedNormally)
            {
                ReturnState(state);
            }
        }
    }
}
