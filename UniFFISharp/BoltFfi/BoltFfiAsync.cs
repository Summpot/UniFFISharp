using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UniFFISharp.Collections;

namespace UniFFISharp.BoltFfi;

public static class BoltFfiAsync
{
    private const sbyte PollReady = 0;
    private const int StatusOk = 0;
    private const int StatusCancelled = 4;

    public static unsafe nint ContinuationPointer => (nint)(delegate* unmanaged[Cdecl]<ulong, sbyte, void>)&OnFutureContinuation;

    private static readonly ConcurrentHandleMap<TaskCompletionSource<sbyte>> Continuations = new();

    public static Task<T> CallAsync<T>(
        Func<nint> start,
        Action<nint, ulong, nint> poll,
        Func<nint, T> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default) =>
        CallAsync<T>(start(), poll, complete, cancel, free, cancellationToken);

    public static async Task<T> CallAsync<T>(
        nint futureHandle,
        Action<nint, ulong, nint> poll,
        Func<nint, T> complete,
        Action<nint> cancel,
        Action<nint> free,
        CancellationToken cancellationToken = default)
    {
        var future = new RustFutureOwner(futureHandle, cancel, free);
        CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
            static state => ((RustFutureOwner)state!).Cancel(), future);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (true)
            {
                sbyte pollResult = await PollOnceAsync(future, poll, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (pollResult == PollReady) break;
            }
            cancellationRegistration.Dispose();
            return complete(future.Handle);
        }
        catch (OperationCanceledException)
        {
            future.Cancel();
            throw;
        }
        finally
        {
            cancellationRegistration.Dispose();
            future.Release();
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
        CancellationToken cancellationToken = default) =>
        CallAsync<object?>(
            futureHandle,
            poll,
            future => { complete(future); return null; },
            cancel,
            free,
            cancellationToken);

    public static void ThrowIfStatus(FfiStatus status, CancellationToken cancellationToken = default)
    {
        switch (status.code)
        {
            case StatusOk: return;
            case StatusCancelled: throw new OperationCanceledException(cancellationToken);
            default: throw new BoltFfiException(status.code, $"FFI async completion failed with status code {status.code}");
        }
    }

    private static Task<sbyte> PollOnceAsync(
        RustFutureOwner future,
        Action<nint, ulong, nint> poll,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<sbyte>(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong callbackData = RegisterContinuation(completion);
        try
        {
            poll(future.Handle, callbackData, ContinuationPointer);
        }
        catch
        {
            Continuations.Remove(callbackData, out _);
            throw;
        }
        var state = new PollState(callbackData, cancellationToken);
        CancellationTokenRegistration registration = cancellationToken.Register(static value =>
        {
            var current = (PollState)value!;
            if (Continuations.Remove(current.CallbackData, out var pending) && pending != null)
                pending.TrySetCanceled(current.CancellationToken);
        }, state);
        return AwaitPollAsync(completion.Task, registration);
    }

    private static async Task<sbyte> AwaitPollAsync(
        Task<sbyte> pollTask,
        CancellationTokenRegistration registration)
    {
        try { return await pollTask.ConfigureAwait(false); }
        finally { registration.Dispose(); }
    }

    private static ulong RegisterContinuation(TaskCompletionSource<sbyte> completion) =>
        Continuations.Insert(completion);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnFutureContinuation(ulong callbackData, sbyte pollResult)
    {
        try
        {
            if (Continuations.Remove(callbackData, out var completion) && completion != null)
                completion.TrySetResult(pollResult);
        }
        catch
        {
        }
    }

    private sealed class PollState
    {
        internal PollState(ulong callbackData, CancellationToken cancellationToken)
        {
            CallbackData = callbackData;
            CancellationToken = cancellationToken;
        }

        internal ulong CallbackData { get; }
        internal CancellationToken CancellationToken { get; }
    }

    private sealed class RustFutureOwner
    {
        private readonly Action<nint> cancel;
        private readonly Action<nint> free;
        private nint handle;

        internal RustFutureOwner(nint handle, Action<nint> cancel, Action<nint> free)
        {
            if (handle == 0) throw new InvalidOperationException("Failed to start async operation: future handle is null");
            this.handle = handle;
            this.cancel = cancel;
            this.free = free;
        }

        internal nint Handle => handle;

        internal void Cancel()
        {
            nint current = handle;
            if (current != 0) cancel(current);
        }

        internal void Release()
        {
            nint current = Interlocked.Exchange(ref handle, 0);
            if (current != 0) free(current);
        }
    }
}
