using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

public sealed class WireWriter
{
    private const int MaxCachedBufferCapacity = 64 * 1024;
    private const int PoolCapacity = 4;

    [ThreadStatic]
    private static WireWriter?[]? t_pool;
    [ThreadStatic]
    private static int t_poolCount;

    private byte[] buffer;
    private int position;
    private bool _fromArrayPool;

    /// <summary>
    /// Rents a reusable WireWriter instance from the thread-local pool.
    /// Caller MUST return the writer using <see cref="Return"/> (e.g. via finally block).
    /// </summary>
    public static WireWriter Rent()
    {
        var pool = t_pool;
        if (pool != null && t_poolCount > 0)
        {
            t_poolCount--;
            var w = pool[t_poolCount]!;
            pool[t_poolCount] = null;
            w.position = 0;
            return w;
        }
        return new WireWriter(128);
    }

    /// <summary>
    /// Returns a rented WireWriter back to the thread-local pool.
    /// </summary>
    public static void Return(WireWriter? writer)
    {
        if (writer == null) return;
        if (writer._fromArrayPool)
        {
            ArrayPool<byte>.Shared.Return(writer.buffer);
            writer.buffer = Array.Empty<byte>();
            writer._fromArrayPool = false;
            return;
        }
        if (writer.buffer.Length > MaxCachedBufferCapacity) return;

        var pool = t_pool;
        if (pool == null)
        {
            pool = new WireWriter?[PoolCapacity];
            t_pool = pool;
            t_poolCount = 0;
        }

        if (t_poolCount < PoolCapacity)
        {
            writer.position = 0;
            pool[t_poolCount++] = writer;
        }
    }

    /// <summary>
    /// Backwards compatibility method. Delegates to Rent().
    /// </summary>
    public static WireWriter GetOrCreate() => Rent();

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
            byte[] newBuf;
            bool wasFromPool = _fromArrayPool;
            if (newCap > MaxCachedBufferCapacity)
            {
                newBuf = ArrayPool<byte>.Shared.Rent(newCap);
                _fromArrayPool = true;
            }
            else
            {
                newBuf = new byte[newCap];
                _fromArrayPool = false;
            }
            global::System.Buffer.BlockCopy(buffer, 0, newBuf, 0, position);
            if (wasFromPool)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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
        int maxBytes = 4 + checked(str.Length * 3);
        EnsureCapacity(maxBytes);
        int bytesWritten = Encoding.UTF8.GetBytes(str.AsSpan(), buffer.AsSpan(position + 4, buffer.Length - (position + 4)));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(position, 4), (uint)bytesWritten);
        position += 4 + bytesWritten;
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

    public void WriteUnmanaged<T>(T value) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        EnsureCapacity(size);
        MemoryMarshal.Write(buffer.AsSpan(position, size), in value);
        position += size;
    }

    public void WriteUnmanagedList<T>(IReadOnlyList<T>? items) where T : unmanaged
    {
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        int count = items.Count;
        WriteU32((uint)count);
        int elemSize = Unsafe.SizeOf<T>();
        int byteSize = checked(count * elemSize);
        EnsureCapacity(byteSize);
        if (items is List<T> list)
        {
            var src = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(list));
            src.CopyTo(buffer.AsSpan(position, byteSize));
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                T val = items[i];
                MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in val)).CopyTo(buffer.AsSpan(position + i * elemSize, elemSize));
            }
        }
        position += byteSize;
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
