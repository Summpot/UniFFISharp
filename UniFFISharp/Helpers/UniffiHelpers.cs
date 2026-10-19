using System;
using System.Runtime.CompilerServices;
using System.Text;
using UniFFISharp.Exceptions;
using UniFFISharp.Types;

namespace UniFFISharp.Helpers;

public interface CallStatusErrorHandler<E> where E : Exception
{
    E Lift(RustBuffer errorBuf);
}

public sealed class NullCallStatusErrorHandler : CallStatusErrorHandler<UniffiException>
{
    public static readonly NullCallStatusErrorHandler INSTANCE = new();

    public UniffiException Lift(RustBuffer errorBuf)
    {
        return new UndeclaredErrorException("Library returned an error not declared in the UniFFI interface.");
    }
}

public static class UniffiHelpers
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCallStatus<E>(ref UniffiRustCallStatus status, CallStatusErrorHandler<E> errorHandler)
        where E : Exception
    {
        if (status.IsError())
        {
            throw errorHandler.Lift(status.error_buf);
        }

        if (status.IsPanic())
        {
            string msg = "Rust panic";
            if (status.error_buf.len > 0 && status.error_buf.data != IntPtr.Zero)
            {
                try
                {
                    unsafe
                    {
                        var stream = status.error_buf.AsStream();
                        msg = stream.ReadString();
                    }
                }
                catch (Exception ex)
                {
                    msg = $"Rust panic (failed to decode panic message: {ex.Message})";
                }
            }
            throw new PanicException(msg);
        }

        throw new InternalException($"Unknown rust call status: {status.code}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCallStatus(ref UniffiRustCallStatus status)
    {
        ThrowCallStatus(ref status, NullCallStatusErrorHandler.INSTANCE);
    }
}
