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
        unsafe
        {
            return new BigEndianStream(new UnmanagedMemoryStream((byte*)data.ToPointer(), length));
        }
    }

    public BigEndianStream AsStream()
    {
        unsafe
        {
            return new BigEndianStream(
                new UnmanagedMemoryStream((byte*)data.ToPointer(), Convert.ToInt64(len))
            );
        }
    }

    public BigEndianStream AsWriteableStream()
    {
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
