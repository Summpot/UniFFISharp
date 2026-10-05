using System;

namespace UniFFISharp.Generator.Metadata;

public static class Checksum
{
    public static ushort Calculate(byte[] bytes, int length) => Calculate(bytes, 0, length);

    public static ushort Calculate(byte[] bytes, int start, int length)
    {
        const ulong INITIAL_STATE = 0xcbf29ce484222325;
        const ulong PRIME = 0x100000001b3;

        ulong hash = INITIAL_STATE;
        int end = Math.Min(start + length, bytes.Length);
        for (int i = start; i < end; i++)
        {
            hash ^= bytes[i];
            hash *= PRIME;
        }

        return (ushort)(hash ^ (hash >> 16) ^ (hash >> 32) ^ (hash >> 48));
    }
}

