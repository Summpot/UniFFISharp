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
    private CancellationTokenSource? _cts;
    private int _callbackInvoked;

    public CancellationTokenSource Cts => _cts ??= new();

    public void MarkDropped()
    {
        if (Interlocked.CompareExchange(ref _callbackInvoked, 1, 0) == 0)
        {
            _cts?.Cancel();
        }
    }

    public void InvokeCallbackOnce(Action invoke)
    {
        if (Interlocked.CompareExchange(ref _callbackInvoked, 1, 0) == 0)
        {
            invoke();
        }
    }

    public void Dispose()
    {
        _cts?.Dispose();
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

        public ValueTask<byte> AsValueTask() => new(this, _core.Version);

        public void Reset() => _core.Reset();

        public void SetResult(byte result) => _core.SetResult(result);

        public void SetCanceled()
        {
            try { CancelFunc?.Invoke(RustFuture); } catch { }
            try { _core.SetException(new OperationCanceledException(CancellationToken)); } catch { }
        }

        public byte GetResult(short token) => _core.GetResult(token);
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _core.OnCompleted(continuation, state, token, flags);
    }

    private static readonly ConcurrentHandleMap<UniffiFutureState> _asyncHandleMap = new();

    public static readonly ConcurrentHandleMap<UniffiForeignFutureHandle> ForeignFuturesMap = new();

    public static unsafe IntPtr ContinuationCallbackPointer => (IntPtr)(delegate* unmanaged[Cdecl]<ulong, byte, void>)&OnContinuationCallback;
    public static unsafe IntPtr DroppedCallbackPointer => (IntPtr)(delegate* unmanaged[Cdecl]<ulong, void>)&OnForeignFutureDropped;

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

    public static async Task<T> UniffiRustCallAsync<T, F, E>(
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
        var state = new UniffiFutureState
        {
            RustFuture = rustFuture,
            CancelFunc = cancelFunc,
            CancellationToken = cancellationToken
        };
        ulong mapEntry = _asyncHandleMap.Insert(state);
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static s => ((UniffiFutureState)s!).SetCanceled(), state);
        }

        try
        {
            byte pollResult;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                pollFunc(rustFuture, ContinuationCallbackPointer, mapEntry);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != UNIFFI_RUST_FUTURE_POLL_READY)
                {
                    state.Reset();
                }
            }
            while (pollResult != UNIFFI_RUST_FUTURE_POLL_READY);

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
        }
    }

    public static async Task UniffiRustCallAsync<E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteActionDelegate completeFunc,
        Action<ulong> freeFunc,
        CallStatusErrorHandler<E> errorHandler,
        Action<ulong>? cancelFunc = null,
        CancellationToken cancellationToken = default
    ) where E : Exception
    {
        var state = new UniffiFutureState
        {
            RustFuture = rustFuture,
            CancelFunc = cancelFunc,
            CancellationToken = cancellationToken
        };
        ulong mapEntry = _asyncHandleMap.Insert(state);
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(static s => ((UniffiFutureState)s!).SetCanceled(), state);
        }

        try
        {
            byte pollResult;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                pollFunc(rustFuture, ContinuationCallbackPointer, mapEntry);
                pollResult = await state.AsValueTask().ConfigureAwait(false);
                if (pollResult != UNIFFI_RUST_FUTURE_POLL_READY)
                {
                    state.Reset();
                }
            }
            while (pollResult != UNIFFI_RUST_FUTURE_POLL_READY);

            var status = new UniffiRustCallStatus();
            completeFunc(rustFuture, ref status);
            if (!status.IsSuccess()) UniffiHelpers.ThrowCallStatus(ref status, errorHandler);
        }
        finally
        {
            reg.Dispose();
            _asyncHandleMap.Remove(mapEntry, out _);
            freeFunc(rustFuture);
        }
    }
}
