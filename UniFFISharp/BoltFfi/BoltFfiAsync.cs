using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using UniFFISharp.Collections;

namespace UniFFISharp.BoltFfi;

public static class BoltFfiAsync
{
    private const sbyte PollReady = 0;
    private const int StatusOk = 0;
    private const int StatusCancelled = 4;

    public static unsafe nint ContinuationPointer => (nint)(delegate* unmanaged[Cdecl]<ulong, sbyte, void>)&OnFutureContinuation;

    private sealed class BoltFfiFutureState : IValueTaskSource<sbyte>
    {
        private ManualResetValueTaskSourceCore<sbyte> _core = new() { RunContinuationsAsynchronously = true };
        public nint FutureHandle;
        public Action<nint>? CancelAction;
        public CancellationToken CancellationToken;
        public int PollSignaled;
        public int Canceled;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<sbyte> AsValueTask() => new(this, _core.Version);

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
        public void SetResult(sbyte result)
        {
            if (Interlocked.Exchange(ref PollSignaled, 1) == 0)
            {
                _core.SetResult(result);
            }
        }

        public void SetCanceled()
        {
            Volatile.Write(ref Canceled, 1);
            nint handle = FutureHandle;
            if (handle != 0)
            {
                try { CancelAction?.Invoke(handle); } catch { }
            }
            if (Interlocked.Exchange(ref PollSignaled, 1) == 0)
            {
                try { _core.SetException(new OperationCanceledException(CancellationToken)); } catch { }
            }
        }

        public void ResetForPool()
        {
            FutureHandle = 0;
            CancelAction = null;
            CancellationToken = default;
            PollSignaled = 0;
            Canceled = 0;
            _core.Reset();
        }

        public sbyte GetResult(short token) => _core.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);
    }

    private const int SharedPoolCapacity = 32;
    private static readonly BoltFfiFutureState?[] s_sharedPool = new BoltFfiFutureState?[SharedPoolCapacity];
    private static int s_sharedPoolHint;

    [ThreadStatic]
    private static BoltFfiFutureState? t_cachedState;

    private static readonly ConcurrentHandleMap<BoltFfiFutureState> Continuations = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BoltFfiFutureState RentState(nint futureHandle, Action<nint>? cancelAction, CancellationToken cancellationToken)
    {
        var state = t_cachedState;
        if (state != null)
        {
            t_cachedState = null;
        }
        else
        {
            int idx = (s_sharedPoolHint++ & (SharedPoolCapacity - 1));
            state = Interlocked.Exchange(ref s_sharedPool[idx], null) ?? new BoltFfiFutureState();
        }

        state.FutureHandle = futureHandle;
        state.CancelAction = cancelAction;
        state.CancellationToken = cancellationToken;
        state.PollSignaled = 0;
        state.Canceled = 0;
        return state;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReturnState(BoltFfiFutureState state)
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

    public static Task<T> CallAsync<T>(
        Func<nint> start,
        Action<nint, ulong, nint> poll,
        Func<nint, T> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default) =>
        CallAsync<T>(start(), poll, complete, cancel, free, cancellationToken);

    public static Task<T> CallAsync<T>(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Func<nint, T> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default)
    {
        if (futureHandle == 0) ThrowNullFutureHandle();
        return CallAsyncCore(futureHandle, poll, static (future, state, _) => ((Func<nint, T>)state!)(future), complete, cancel, free, cancellationToken);
    }

    public static Task<T> CallAsync<T>(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Func<nint, CancellationToken, T> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default)
    {
        if (futureHandle == 0) ThrowNullFutureHandle();
        return CallAsyncCore(futureHandle, poll, static (future, state, ct) => ((Func<nint, CancellationToken, T>)state!)(future, ct), complete, cancel, free, cancellationToken);
    }

    private static async Task<T> CallAsyncCore<T>(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Func<nint, object?, CancellationToken, T> invokeComplete,
        object? completeState,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken)
    {
        var state = RentState(futureHandle, cancel, cancellationToken);
        ulong callbackData = Continuations.Insert(state);
        CancellationTokenRegistration cancellationRegistration = default;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(static s => ((BoltFfiFutureState)s!).SetCanceled(), state);
        }

        bool pollCompletedNormally = false;
        try
        {
            sbyte pollResult;
            do
            {
                state.PrepareForPoll();
                cancellationToken.ThrowIfCancellationRequested();
                poll(futureHandle, callbackData, ContinuationPointer);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != PollReady)
                {
                    state.ResetForNextPoll();
                }
            }
            while (pollResult != PollReady);

            pollCompletedNormally = Volatile.Read(ref state.Canceled) == 0;
            cancellationRegistration.Dispose();
            cancellationRegistration = default;

            return invokeComplete(futureHandle, completeState, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            cancel(futureHandle);
            throw;
        }
        finally
        {
            cancellationRegistration.Dispose();
            Continuations.Remove(callbackData, out _);
            free(futureHandle);
            if (pollCompletedNormally)
            {
                ReturnState(state);
            }
        }
    }

    public static Task CallAsyncVoid(
        Func<nint> start,
        Action<nint, ulong, nint> poll,
        Action<nint> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default) =>
        CallAsyncVoid(start(), poll, complete, cancel, free, cancellationToken);

    public static Task CallAsyncVoid(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Action<nint> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default)
    {
        if (futureHandle == 0) ThrowNullFutureHandle();
        return CallAsyncVoidCore(futureHandle, poll, static (future, state, _) => ((Action<nint>)state!)(future), complete, cancel, free, cancellationToken);
    }

    public static Task CallAsyncVoid(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Action<nint, CancellationToken> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default)
    {
        if (futureHandle == 0) ThrowNullFutureHandle();
        return CallAsyncVoidCore(futureHandle, poll, static (future, state, ct) => ((Action<nint, CancellationToken>)state!)(future, ct), complete, cancel, free, cancellationToken);
    }

    private static async Task CallAsyncVoidCore(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Action<nint, object?, CancellationToken> invokeComplete,
        object? completeState,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken)
    {
        var state = RentState(futureHandle, cancel, cancellationToken);
        ulong callbackData = Continuations.Insert(state);
        CancellationTokenRegistration cancellationRegistration = default;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(static s => ((BoltFfiFutureState)s!).SetCanceled(), state);
        }

        bool pollCompletedNormally = false;
        try
        {
            sbyte pollResult;
            do
            {
                state.PrepareForPoll();
                cancellationToken.ThrowIfCancellationRequested();
                poll(futureHandle, callbackData, ContinuationPointer);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != PollReady)
                {
                    state.ResetForNextPoll();
                }
            }
            while (pollResult != PollReady);

            pollCompletedNormally = Volatile.Read(ref state.Canceled) == 0;
            cancellationRegistration.Dispose();
            cancellationRegistration = default;

            invokeComplete(futureHandle, completeState, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            cancel(futureHandle);
            throw;
        }
        finally
        {
            cancellationRegistration.Dispose();
            Continuations.Remove(callbackData, out _);
            free(futureHandle);
            if (pollCompletedNormally)
            {
                ReturnState(state);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNullFutureHandle() =>
        throw new InvalidOperationException("Failed to start async operation: future handle is null");

    public static void ThrowIfStatus(FfiStatus status, CancellationToken cancellationToken = default)
    {
        if (status.code == StatusOk) return;
        ThrowStatusError(status.code, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowStatusError(int code, CancellationToken cancellationToken)
    {
        if (code == StatusCancelled) throw new OperationCanceledException(cancellationToken);
        throw new BoltFfiException(code, $"FFI async completion failed with status code {code}");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnFutureContinuation(ulong callbackData, sbyte pollResult)
    {
        try
        {
            if (Continuations.TryGet(callbackData, out var state) && state != null)
            {
                state.SetResult(pollResult);
            }
        }
        catch
        {
        }
    }
}
