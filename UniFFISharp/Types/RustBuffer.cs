using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UniFFISharp.Streams;

namespace UniFFISharp.Types;

[StructLayout(LayoutKind.Sequential)]
public struct RustBuffer
{
    public ulong capacity;
    public ulong len;
    public IntPtr data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigEndianStream MemoryStream(IntPtr data, long length)
    {
        if (length < 0)
        {
            ThrowNegativeStreamLength();
        }
        if (length > 0 && data == IntPtr.Zero)
        {
            ThrowNullDataPointer(nameof(data));
        }
        return new BigEndianStream(data, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BigEndianStream AsStream()
    {
        if (len > capacity)
        {
            ThrowLengthExceedsCapacity(len, capacity);
        }
        if (len > 0 && data == IntPtr.Zero)
        {
            ThrowNullRustBufferData("length");
        }
        if (len > (ulong)long.MaxValue)
        {
            ThrowExceedsMaxLength("length");
        }
        return new BigEndianStream(data, (long)len);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BigEndianStream AsWriteableStream()
    {
        if (capacity > (ulong)long.MaxValue)
        {
            ThrowExceedsMaxLength("capacity");
        }
        if (capacity > 0 && data == IntPtr.Zero)
        {
            ThrowNullRustBufferData("capacity");
        }
        return new BigEndianStream(data, (long)capacity);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNegativeStreamLength() =>
        throw new ArgumentOutOfRangeException("length", "Stream length cannot be negative.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNullDataPointer(string paramName) =>
        throw new ArgumentException("Data pointer cannot be null when length is greater than zero.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowLengthExceedsCapacity(ulong length, ulong cap) =>
        throw new ArgumentException($"RustBuffer length ({length}) cannot exceed capacity ({cap}).");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNullRustBufferData(string fieldName) =>
        throw new ArgumentException($"RustBuffer data pointer cannot be null when {fieldName} is greater than zero.");

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowExceedsMaxLength(string fieldName) =>
        throw new OverflowException($"RustBuffer {fieldName} exceeds maximum supported stream length.");
}
