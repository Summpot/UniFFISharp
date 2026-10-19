using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

public delegate T WireReaderFunc<out T>(ref WireReader reader);

public unsafe ref struct WireReader
{
    private readonly ReadOnlySpan<byte> span;
    private int position;

    public WireReader(FfiBuf buf)
    {
        span = buf.ptr != 0 ? new ReadOnlySpan<byte>((byte*)buf.ptr, checked((int)buf.len)) : ReadOnlySpan<byte>.Empty;
        position = 0;
    }

    public WireReader(ReadOnlySpan<byte> span)
    {
        this.span = span;
        this.position = 0;
    }

    public WireReader(byte* ptr, int len)
    {
        span = ptr != null && len > 0 ? new ReadOnlySpan<byte>(ptr, len) : ReadOnlySpan<byte>.Empty;
        position = 0;
    }

    public int Position => position;
    public int Length => span.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Require(int count, string typeName)
    {
        if (position + count > span.Length)
        {
            throw new EndOfStreamException($"Corrupt wire buffer: unexpected end of stream reading {typeName} (at offset {position}, required {count}, buffer length {span.Length})");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBool()
    {
        Require(1, "bool");
        return span[position++] != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadI8()
    {
        Require(1, "i8");
        return (sbyte)span[position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadU8()
    {
        Require(1, "u8");
        return span[position++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadI16()
    {
        Require(2, "i16");
        short val = BinaryPrimitives.ReadInt16LittleEndian(span.Slice(position, 2));
        position += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadU16() => unchecked((ushort)ReadI16());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadI32()
    {
        Require(4, "i32");
        int val = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(position, 4));
        position += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadU32() => unchecked((uint)ReadI32());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadI64()
    {
        Require(8, "i64");
        long val = BinaryPrimitives.ReadInt64LittleEndian(span.Slice(position, 8));
        position += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadU64() => unchecked((ulong)ReadI64());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadF32()
    {
        Require(4, "f32");
        float val = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(position, 4));
        position += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadF64()
    {
        Require(8, "f64");
        double val = BinaryPrimitives.ReadDoubleLittleEndian(span.Slice(position, 8));
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
        string val = Encoding.UTF8.GetString(span.Slice(position, strLen));
        position += strLen;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes()
    {
        int count = checked((int)ReadU32());
        if (count == 0) return Array.Empty<byte>();
        Require(count, "bytes payload");
        byte[] bytes = span.Slice(position, count).ToArray();
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
        var bytes = span.Slice(position, 16);
        position += 16;
        return new Guid(bytes, bigEndian: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uri ReadUri() => new Uri(ReadString());

    public T[] ReadArray<T>(WireReaderFunc<T> elementReader)
    {
        int count = checked((int)ReadU32());
        if (count == 0) return Array.Empty<T>();
        var items = new T[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = elementReader(ref this);
        }
        return items;
    }

    public List<T> ReadList<T>(WireReaderFunc<T> elementReader)
    {
        int count = checked((int)ReadU32());
        if (count == 0) return new List<T>(0);
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(elementReader(ref this));
        }
        return list;
    }

    public List<T> ReadDirectList<T>() where T : unmanaged
    {
        int remainingBytes = span.Length - position;
        int elemSize = Unsafe.SizeOf<T>();
        if (remainingBytes == 0 || elemSize == 0) return new List<T>(0);
        int count = remainingBytes / elemSize;
        var list = new List<T>(count);
        var srcSpan = MemoryMarshal.Cast<byte, T>(span.Slice(position, count * elemSize));
        for (int i = 0; i < count; i++)
        {
            list.Add(srcSpan[i]);
        }
        position += count * elemSize;
        return list;
    }

    public Dictionary<TKey, TValue> ReadMap<TKey, TValue>(WireReaderFunc<TKey> keyReader, WireReaderFunc<TValue> valueReader)
        where TKey : notnull
    {
        int count = checked((int)ReadU32());
        var map = new Dictionary<TKey, TValue>(count);
        for (int i = 0; i < count; i++)
        {
            TKey key = keyReader(ref this);
            TValue val = valueReader(ref this);
            map[key] = val;
        }
        return map;
    }

    public HashSet<T> ReadSet<T>(WireReaderFunc<T> elementReader)
        where T : notnull
    {
        int count = checked((int)ReadU32());
        var set = new HashSet<T>(count);
        for (int i = 0; i < count; i++)
        {
            set.Add(elementReader(ref this));
        }
        return set;
    }

    public T? ReadOption<T>(WireReaderFunc<T> valueReader) where T : class
    {
        return ReadBool() ? valueReader(ref this) : null;
    }

    public T? ReadOptionValue<T>(WireReaderFunc<T> valueReader) where T : struct
    {
        return ReadBool() ? valueReader(ref this) : null;
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
