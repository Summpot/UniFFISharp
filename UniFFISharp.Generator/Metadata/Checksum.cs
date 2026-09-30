namespace UniFFISharp.Generator.Metadata;

public static class Checksum
{
    public static ushort Calculate(byte[] bytes, int length)
    {
        const ulong INITIAL_STATE = 0xcbf29ce484222325;
        const ulong PRIME = 0x100000001b3;

        ulong hash = INITIAL_STATE;
        for (int i = 0; i < length && i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= PRIME;
        }

        return (ushort)(hash ^ (hash >> 16) ^ (hash >> 32) ^ (hash >> 48));
    }
}
