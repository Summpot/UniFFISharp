using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

public sealed class WireWriter
{
    [ThreadStatic]
    private static WireWriter? t_cached;

    private byte[] buffer;
    private int position;

    public static WireWriter GetOrCreate()
    {
        var w = t_cached;
        if (w == null)
        {
            w = new WireWriter(128);
            t_cached = w;
        }
        else
        {
            w.position = 0;
        }
        return w;
    }

    public WireWriter(int initialCapacity = 64)
    {
        buffer = new byte[initialCapacity];
        position = 0;
    }

    public int Length => position;
    public byte[] Buffer => buffer;
    public ReadOnlySpan<byte> WrittenSpan => buffer.AsSpan(0, position);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int count)
    {
        if (position + count > buffer.Length)
        {
            int newCap = Math.Max(buffer.Length * 2, position + count);
            var newBuf = new byte[newCap];
            global::System.Buffer.BlockCopy(buffer, 0, newBuf, 0, position);
            buffer = newBuf;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool val)
    {
        EnsureCapacity(1);
        buffer[position++] = val ? (byte)1 : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI8(sbyte val)
    {
        EnsureCapacity(1);
        buffer[position++] = (byte)val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU8(byte val)
    {
        EnsureCapacity(1);
        buffer[position++] = val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI16(short val)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(position), val);
        position += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU16(ushort val) => WriteI16((short)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI32(int val)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(position), val);
        position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU32(uint val) => WriteI32((int)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI64(long val)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(position), val);
        position += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU64(ulong val) => WriteI64((long)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteF32(float val)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(position), val);
        position += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteF64(double val)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(position), val);
        position += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNInt(nint val) => WriteI64(val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNUInt(nuint val) => WriteU64(val);

    public void WriteString(string? str)
    {
        if (string.IsNullOrEmpty(str))
        {
            WriteU32(0);
            return;
        }
        int utf8Len = Encoding.UTF8.GetByteCount(str);
        EnsureCapacity(4 + utf8Len);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(position), (uint)utf8Len);
        position += 4;
        Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, position);
        position += utf8Len;
    }

    public void WriteBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            WriteU32(0);
            return;
        }
        EnsureCapacity(4 + bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(position), (uint)bytes.Length);
        position += 4;
        global::System.Buffer.BlockCopy(bytes, 0, buffer, position, bytes.Length);
        position += bytes.Length;
    }

    public void WriteDuration(TimeSpan duration)
    {
        long seconds = duration.Ticks / TimeSpan.TicksPerSecond;
        int nanos = (int)((duration.Ticks % TimeSpan.TicksPerSecond) * 100);
        WriteI64(seconds);
        WriteI32(nanos);
    }

    public void WriteDateTime(DateTimeOffset dateTime)
    {
        long ticks = (dateTime - DateTimeOffset.UnixEpoch).Ticks;
        long seconds = ticks / TimeSpan.TicksPerSecond;
        int nanos = (int)((ticks % TimeSpan.TicksPerSecond) * 100);
        WriteI64(seconds);
        WriteI32(nanos);
    }

    public void WriteGuid(Guid guid)
    {
        EnsureCapacity(16);
        guid.TryWriteBytes(buffer.AsSpan(position, 16), bigEndian: true, out _);
        position += 16;
    }

    public void WriteUri(Uri uri) => WriteString(uri.ToString());

    public void WriteArray<T>(T[]? items, Action<WireWriter, T> elementWriter)
    {
        if (items == null || items.Length == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Length);
        foreach (var item in items)
        {
            elementWriter(this, item);
        }
    }

    public void WriteList<T>(IReadOnlyList<T>? items, Action<WireWriter, T> elementWriter)
    {
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            elementWriter(this, items[i]);
        }
    }

    public void WriteMap<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? map, Action<WireWriter, TKey> keyWriter, Action<WireWriter, TValue> valueWriter)
    {
        if (map == null || map.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)map.Count);
        foreach (var kvp in map)
        {
            keyWriter(this, kvp.Key);
            valueWriter(this, kvp.Value);
        }
    }

    public void WriteSet<T>(IReadOnlyCollection<T>? items, Action<WireWriter, T> elementWriter)
    {
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Count);
        foreach (var item in items)
        {
            elementWriter(this, item);
        }
    }

    public void WriteOption<T>(T? value, Action<WireWriter, T> valueWriter) where T : class
    {
        WriteBool(value != null);
        if (value != null) valueWriter(this, value);
    }

    public void WriteOptionValue<T>(T? value, Action<WireWriter, T> valueWriter) where T : struct
    {
        WriteBool(value.HasValue);
        if (value.HasValue) valueWriter(this, value.Value);
    }

    public void WriteOptionBool(bool? val) { WriteBool(val.HasValue); if (val.HasValue) WriteBool(val.Value); }
    public void WriteOptionI8(sbyte? val) { WriteBool(val.HasValue); if (val.HasValue) WriteI8(val.Value); }
    public void WriteOptionU8(byte? val) { WriteBool(val.HasValue); if (val.HasValue) WriteU8(val.Value); }
    public void WriteOptionI16(short? val) { WriteBool(val.HasValue); if (val.HasValue) WriteI16(val.Value); }
    public void WriteOptionU16(ushort? val) { WriteBool(val.HasValue); if (val.HasValue) WriteU16(val.Value); }
    public void WriteOptionI32(int? val) { WriteBool(val.HasValue); if (val.HasValue) WriteI32(val.Value); }
    public void WriteOptionU32(uint? val) { WriteBool(val.HasValue); if (val.HasValue) WriteU32(val.Value); }
    public void WriteOptionI64(long? val) { WriteBool(val.HasValue); if (val.HasValue) WriteI64(val.Value); }
    public void WriteOptionU64(ulong? val) { WriteBool(val.HasValue); if (val.HasValue) WriteU64(val.Value); }
    public void WriteOptionF32(float? val) { WriteBool(val.HasValue); if (val.HasValue) WriteF32(val.Value); }
    public void WriteOptionF64(double? val) { WriteBool(val.HasValue); if (val.HasValue) WriteF64(val.Value); }
    public void WriteOptionString(string? val) { WriteBool(val != null); if (val != null) WriteString(val); }
    public void WriteOptionBytes(byte[]? val) { WriteBool(val != null); if (val != null) WriteBytes(val); }
    public void WriteOptionDuration(TimeSpan? val) { WriteBool(val.HasValue); if (val.HasValue) WriteDuration(val.Value); }
    public void WriteOptionDateTime(DateTimeOffset? val) { WriteBool(val.HasValue); if (val.HasValue) WriteDateTime(val.Value); }
    public void WriteOptionGuid(Guid? val) { WriteBool(val.HasValue); if (val.HasValue) WriteGuid(val.Value); }

    public byte[] ToArray()
    {
        var res = new byte[position];
        global::System.Buffer.BlockCopy(buffer, 0, res, 0, position);
        return res;
    }
}
