using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniFFISharp.Callbacks;
using UniFFISharp.Collections;
using UniFFISharp.Helpers;
using UniFFISharp.Types;

namespace UniFFISharp.Async;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void UniFfiFutureCallback(ulong continuationHandle, byte pollResult);

public sealed class UniffiForeignFutureHandle : IDisposable
{
    public CancellationTokenSource Cts { get; } = new();
    private readonly object _lock = new();
    private bool _callbackInvoked = false;

    public void MarkDropped()
    {
        lock (_lock)
        {
            if (!_callbackInvoked)
            {
                Cts.Cancel();
            }
        }
    }

    public void InvokeCallbackOnce(Action invoke)
    {
        bool shouldInvoke;
        lock (_lock)
        {
            shouldInvoke = !_callbackInvoked;
            if (shouldInvoke) _callbackInvoked = true;
        }
        if (shouldInvoke) invoke();
    }

    public void Dispose()
    {
        Cts.Dispose();
    }
}

public static class _UniFFIAsync
{
    public const byte UNIFFI_RUST_FUTURE_POLL_READY = 0;
    public const byte UNIFFI_RUST_FUTURE_POLL_WAKE = 1;

    public delegate F CompleteFuncDelegate<F>(ulong handle, ref UniffiRustCallStatus status);
    public delegate void CompleteActionDelegate(ulong handle, ref UniffiRustCallStatus status);

    private static readonly ConcurrentHandleMap<TaskCompletionSource<byte>> _asyncHandleMap = new();
    private static readonly UniFfiFutureCallback _continuationCallback = OnContinuationCallback;
    private static readonly IntPtr _continuationCallbackPtr = Marshal.GetFunctionPointerForDelegate(_continuationCallback);

    public static readonly ConcurrentHandleMap<UniffiForeignFutureHandle> ForeignFuturesMap = new();
    private static readonly UniffiForeignFutureDroppedCallback _droppedCallback = OnForeignFutureDropped;
    private static readonly IntPtr _droppedCallbackPtr = Marshal.GetFunctionPointerForDelegate(_droppedCallback);

    public static IntPtr ContinuationCallbackPointer => _continuationCallbackPtr;
    public static IntPtr DroppedCallbackPointer => _droppedCallbackPtr;

    private static void OnForeignFutureDropped(ulong handle)
    {
        if (ForeignFuturesMap.Remove(handle, out var futureHandle) && futureHandle != null)
        {
            futureHandle.MarkDropped();
        }
    }

    private static void OnContinuationCallback(ulong continuationHandle, byte pollResult)
    {
        if (_asyncHandleMap.Remove(continuationHandle, out TaskCompletionSource<byte>? task) && task != null)
        {
            task.SetResult(pollResult);
        }
    }

    private static async Task PollFuture(ulong rustFuture, Action<ulong, IntPtr, ulong> pollFunc)
    {
        byte pollResult;
        do
        {
            var tcs = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
            ulong mapEntry = _asyncHandleMap.Insert(tcs);
            pollFunc(rustFuture, _continuationCallbackPtr, mapEntry);
            pollResult = await tcs.Task.ConfigureAwait(false);
        }
        while (pollResult != UNIFFI_RUST_FUTURE_POLL_READY);
    }

    public static async Task<T> UniffiRustCallAsync<T, F, E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteFuncDelegate<F> completeFunc,
        Action<ulong> freeFunc,
        Func<F, T> liftFunc,
        CallStatusErrorHandler<E> errorHandler
    ) where E : Exception
    {
        try
        {
            await PollFuture(rustFuture, pollFunc).ConfigureAwait(false);
            var status = new UniffiRustCallStatus();
            var result = completeFunc(rustFuture, ref status);
            if (!status.IsSuccess()) UniffiHelpers.ThrowCallStatus(ref status, errorHandler);
            return liftFunc(result);
        }
        finally
        {
            freeFunc(rustFuture);
        }
    }

    public static async Task UniffiRustCallAsync<E>(
        ulong rustFuture,
        Action<ulong, IntPtr, ulong> pollFunc,
        CompleteActionDelegate completeFunc,
        Action<ulong> freeFunc,
        CallStatusErrorHandler<E> errorHandler
    ) where E : Exception
    {
        try
        {
            await PollFuture(rustFuture, pollFunc).ConfigureAwait(false);
            var status = new UniffiRustCallStatus();
            completeFunc(rustFuture, ref status);
            if (!status.IsSuccess()) UniffiHelpers.ThrowCallStatus(ref status, errorHandler);
        }
        finally
        {
            freeFunc(rustFuture);
        }
    }
}
