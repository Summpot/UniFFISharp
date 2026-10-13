using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

[StructLayout(LayoutKind.Sequential)]
public struct FfiBuf
{
    public nint ptr;
    public nuint len;
    public nuint cap;
    public nuint align;

    public static FfiBuf Empty => default;

    public static FfiBuf FromString(string? str)
    {
        if (string.IsNullOrEmpty(str)) return FromBytes(Array.Empty<byte>());
        return FromBytes(Encoding.UTF8.GetBytes(str));
    }

    public static FfiBuf FromBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            nint emptyMem = Marshal.AllocHGlobal(4);
            Marshal.WriteInt32(emptyMem, 0);
            return new FfiBuf { ptr = emptyMem, len = 4, cap = 4, align = 1 };
        }
        nint mem = Marshal.AllocHGlobal(4 + bytes.Length);
        Marshal.WriteInt32(mem, bytes.Length);
        Marshal.Copy(bytes, 0, mem + 4, bytes.Length);
        return new FfiBuf { ptr = mem, len = (nuint)(4 + bytes.Length), cap = (nuint)(4 + bytes.Length), align = 1 };
    }

    public static void Free(ref FfiBuf buf)
    {
        if (buf.ptr != 0)
        {
            Marshal.FreeHGlobal(buf.ptr);
            buf = default;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ToArray()
    {
        if (ptr == 0 || len == 0) return Array.Empty<byte>();
        unsafe
        {
            if (len >= 4)
            {
                uint byteLen = *(uint*)ptr;
                if (byteLen == 0) return Array.Empty<byte>();
                return new ReadOnlySpan<byte>((byte*)ptr + 4, (int)byteLen).ToArray();
            }
            return new ReadOnlySpan<byte>((byte*)ptr, checked((int)len)).ToArray();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        if (ptr == 0 || len == 0) return string.Empty;
        unsafe
        {
            if (len >= 4)
            {
                uint strLen = *(uint*)ptr;
                if (strLen == 0) return string.Empty;
                return Encoding.UTF8.GetString((byte*)ptr + 4, (int)strLen);
            }
            return Encoding.UTF8.GetString((byte*)ptr, checked((int)len));
        }
    }
}
