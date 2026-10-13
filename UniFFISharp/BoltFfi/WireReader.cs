using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

public unsafe sealed class WireReader
{
    private readonly byte* pointer;
    private readonly int length;
    private int position;

    public WireReader(FfiBuf buf)
    {
        pointer = (byte*)buf.ptr;
        length = checked((int)buf.len);
        position = 0;
    }

    public WireReader(ReadOnlySpan<byte> span)
    {
        fixed (byte* ptr = span)
        {
            pointer = ptr;
            length = span.Length;
            position = 0;
        }
    }

    public WireReader(byte* ptr, int len)
    {
        pointer = ptr;
        length = len;
        position = 0;
    }

    public int Position => position;
    public int Length => length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Require(int count, string typeName)
    {
        if (position + count > length)
        {
            throw new EndOfStreamException($"Corrupt wire buffer: unexpected end of stream reading {typeName} (at offset {position}, required {count}, buffer length {length})");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        Require(1, "bool");
        return pointer[position++] != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadI8()
    {
        Require(1, "i8");
        return (sbyte)pointer[position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadU8()
    {
        Require(1, "u8");
        return pointer[position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadI16()
    {
        Require(2, "i16");
        short val = BinaryPrimitives.ReadInt16LittleEndian(new ReadOnlySpan<byte>(pointer + position, 2));
        position += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadU16() => unchecked((ushort)ReadI16());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadI32()
    {
        Require(4, "i32");
        int val = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(pointer + position, 4));
        position += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadU32() => unchecked((uint)ReadI32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadI64()
    {
        Require(8, "i64");
        long val = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(pointer + position, 8));
        position += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadU64() => unchecked((ulong)ReadI64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadF32()
    {
        Require(4, "f32");
        float val = BinaryPrimitives.ReadSingleLittleEndian(new ReadOnlySpan<byte>(pointer + position, 4));
        position += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadF64()
    {
        Require(8, "f64");
        double val = BinaryPrimitives.ReadDoubleLittleEndian(new ReadOnlySpan<byte>(pointer + position, 8));
        position += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nint ReadNInt() => checked((nint)ReadI64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nuint ReadNUInt() => checked((nuint)ReadU64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString()
    {
        int strLen = checked((int)ReadU32());
        if (strLen == 0) return string.Empty;
        Require(strLen, "string payload");
        string val = Encoding.UTF8.GetString(pointer + position, strLen);
        position += strLen;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes()
    {
        int count = checked((int)ReadU32());
        if (count == 0) return Array.Empty<byte>();
        Require(count, "bytes payload");
        byte[] bytes = new byte[count];
        Marshal.Copy((nint)(pointer + position), bytes, 0, count);
        position += count;
        return bytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan ReadDuration()
    {
        long seconds = ReadI64();
        int nanos = ReadI32();
        if (seconds < 0 || nanos < 0 || nanos >= 1_000_000_000)
            throw new InvalidOperationException("Corrupt wire: invalid duration");
        return new TimeSpan(checked(seconds * TimeSpan.TicksPerSecond + nanos / 100));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTimeOffset ReadDateTime()
    {
        long seconds = ReadI64();
        int nanos = ReadI32();
        if (nanos < 0 || nanos >= 1_000_000_000)
            throw new InvalidOperationException("Corrupt wire: invalid date-time");
        return DateTimeOffset.UnixEpoch.AddTicks(checked(seconds * TimeSpan.TicksPerSecond + nanos / 100));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Guid ReadGuid()
    {
        Require(16, "guid");
        var bytes = new ReadOnlySpan<byte>(pointer + position, 16);
        position += 16;
        return new Guid(bytes, bigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uri ReadUri() => new Uri(ReadString());

    public T[] ReadArray<T>(Func<WireReader, T> elementReader)
    {
        int count = checked((int)ReadU32());
        if (count == 0) return Array.Empty<T>();
        var items = new T[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = elementReader(this);
        }
        return items;
    }

    public List<T> ReadList<T>(Func<WireReader, T> elementReader)
    {
        int count = checked((int)ReadU32());
        if (count == 0) return new List<T>(0);
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(elementReader(this));
        }
        return list;
    }

    public Dictionary<TKey, TValue> ReadMap<TKey, TValue>(Func<WireReader, TKey> keyReader, Func<WireReader, TValue> valueReader)
        where TKey : notnull
    {
        int count = checked((int)ReadU32());
        var map = new Dictionary<TKey, TValue>(count);
        for (int i = 0; i < count; i++)
        {
            TKey key = keyReader(this);
            TValue val = valueReader(this);
            map[key] = val;
        }
        return map;
    }

    public HashSet<T> ReadSet<T>(Func<WireReader, T> elementReader)
        where T : notnull
    {
        int count = checked((int)ReadU32());
        var set = new HashSet<T>(count);
        for (int i = 0; i < count; i++)
        {
            set.Add(elementReader(this));
        }
        return set;
    }

    public T? ReadOption<T>(Func<WireReader, T> valueReader) where T : class
    {
        return ReadBool() ? valueReader(this) : null;
    }

    public T? ReadOptionValue<T>(Func<WireReader, T> valueReader) where T : struct
    {
        return ReadBool() ? valueReader(this) : null;
    }

    public bool? ReadOptionBool() => ReadBool() ? ReadBool() : null;
    public sbyte? ReadOptionI8() => ReadBool() ? ReadI8() : null;
    public byte? ReadOptionU8() => ReadBool() ? ReadU8() : null;
    public short? ReadOptionI16() => ReadBool() ? ReadI16() : null;
    public ushort? ReadOptionU16() => ReadBool() ? ReadU16() : null;
    public int? ReadOptionI32() => ReadBool() ? ReadI32() : null;
    public uint? ReadOptionU32() => ReadBool() ? ReadU32() : null;
    public long? ReadOptionI64() => ReadBool() ? ReadI64() : null;
    public ulong? ReadOptionU64() => ReadBool() ? ReadU64() : null;
    public float? ReadOptionF32() => ReadBool() ? ReadF32() : null;
    public double? ReadOptionF64() => ReadBool() ? ReadF64() : null;
    public string? ReadOptionString() => ReadBool() ? ReadString() : null;
    public byte[]? ReadOptionBytes() => ReadBool() ? ReadBytes() : null;
    public TimeSpan? ReadOptionDuration() => ReadBool() ? ReadDuration() : null;
    public DateTimeOffset? ReadOptionDateTime() => ReadBool() ? ReadDateTime() : null;
    public Guid? ReadOptionGuid() => ReadBool() ? ReadGuid() : null;
}
