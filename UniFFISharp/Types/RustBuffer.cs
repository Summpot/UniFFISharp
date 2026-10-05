using System;
using System.IO;
using System.Runtime.InteropServices;
using UniFFISharp.Streams;

namespace UniFFISharp.Types;

[StructLayout(LayoutKind.Sequential)]
public struct RustBuffer
{
    public ulong capacity;
    public ulong len;
    public IntPtr data;

    public static BigEndianStream MemoryStream(IntPtr data, long length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Stream length cannot be negative.");
        }
        if (length > 0 && data == IntPtr.Zero)
        {
            throw new ArgumentException("Data pointer cannot be null when length is greater than zero.", nameof(data));
        }
        if (length == 0)
        {
            return new BigEndianStream(Stream.Null);
        }

        unsafe
        {
            return new BigEndianStream(new UnmanagedMemoryStream((byte*)data.ToPointer(), length));
        }
    }

    public BigEndianStream AsStream()
    {
        if (len > capacity)
        {
            throw new ArgumentException($"RustBuffer length ({len}) cannot exceed capacity ({capacity}).");
        }
        if (len > 0 && data == IntPtr.Zero)
        {
            throw new ArgumentException("RustBuffer data pointer cannot be null when length is greater than zero.");
        }
        if (len > (ulong)long.MaxValue)
        {
            throw new OverflowException("RustBuffer length exceeds maximum supported stream length.");
        }
        if (len == 0)
        {
            return new BigEndianStream(Stream.Null);
        }

        unsafe
        {
            return new BigEndianStream(
                new UnmanagedMemoryStream((byte*)data.ToPointer(), Convert.ToInt64(len))
            );
        }
    }

    public BigEndianStream AsWriteableStream()
    {
        if (capacity > 0 && data == IntPtr.Zero)
        {
            throw new ArgumentException("RustBuffer data pointer cannot be null when capacity is greater than zero.");
        }
        if (capacity > (ulong)long.MaxValue)
        {
            throw new OverflowException("RustBuffer capacity exceeds maximum supported stream length.");
        }
        if (capacity == 0)
        {
            return new BigEndianStream(Stream.Null);
        }

        unsafe
        {
            return new BigEndianStream(
                new UnmanagedMemoryStream(
                    (byte*)data.ToPointer(),
                    Convert.ToInt64(capacity),
                    Convert.ToInt64(capacity),
                    FileAccess.Write
                )
            );
        }
    }
}
