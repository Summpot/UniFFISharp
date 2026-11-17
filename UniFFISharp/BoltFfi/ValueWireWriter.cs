using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace UniFFISharp.BoltFfi;

public delegate void ValueWireWriterFunc<in T>(ref ValueWireWriter writer, T value);

/// <summary>
/// Stack-first ref-struct binary encoder for BoltFFI wire payloads.
/// Uses caller-provided stack buffer and falls back to <see cref="ArrayPool{T}.Shared"/> when exceeded.
/// Always call <see cref="Dispose"/> (e.g. <c>using var writer = new ValueWireWriter(stackalloc byte[256]);</c>).
/// </summary>
public unsafe ref struct ValueWireWriter
{
    private Span<byte> _buf;
    private byte[]? _rented;
    private int _pos;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueWireWriter(Span<byte> initialBuffer)
    {
        _buf = initialBuffer;
        _rented = null;
        _pos = 0;
    }

    public readonly int Length => _pos;
    public readonly ReadOnlySpan<byte> WrittenSpan => _buf.Slice(0, _pos);
    public readonly ReadOnlySpan<byte> RawBufferSpan => _buf;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        byte[]? rented = _rented;
        if (rented != null)
        {
            _rented = null;
            _buf = default;
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int count)
    {
        if ((uint)count > (uint)(_buf.Length - _pos))
        {
            Grow(count);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additional)
    {
        int needed = checked(_pos + additional);
        int newCap = Math.Max(Math.Max(_buf.Length * 2, 256), needed);
        byte[] newBuf = ArrayPool<byte>.Shared.Rent(newCap);
        if (_pos > 0)
        {
            _buf.Slice(0, _pos).CopyTo(newBuf);
        }
        byte[]? old = _rented;
        _rented = newBuf;
        _buf = newBuf;
        if (old != null)
        {
            ArrayPool<byte>.Shared.Return(old);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteBool(bool val)
    {
        EnsureCapacity(1);
        Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos++) = val ? (byte)1 : (byte)0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI8(sbyte val)
    {
        EnsureCapacity(1);
        Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos++) = (byte)val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU8(byte val)
    {
        EnsureCapacity(1);
        Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos++) = val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI16(short val)
    {
        EnsureCapacity(2);
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), val);
        else
            BinaryPrimitives.WriteInt16LittleEndian(_buf.Slice(_pos), val);
        _pos += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU16(ushort val) => WriteI16((short)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI32(int val)
    {
        EnsureCapacity(4);
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), val);
        else
            BinaryPrimitives.WriteInt32LittleEndian(_buf.Slice(_pos), val);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU32(uint val) => WriteI32((int)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteI64(long val)
    {
        EnsureCapacity(8);
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), val);
        else
            BinaryPrimitives.WriteInt64LittleEndian(_buf.Slice(_pos), val);
        _pos += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteU64(ulong val) => WriteI64((long)val);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteF32(float val)
    {
        EnsureCapacity(4);
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), val);
        else
            BinaryPrimitives.WriteSingleLittleEndian(_buf.Slice(_pos), val);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteF64(double val)
    {
        EnsureCapacity(8);
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), val);
        else
            BinaryPrimitives.WriteDoubleLittleEndian(_buf.Slice(_pos), val);
        _pos += 8;
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
        int maxBytes = checked(4 + str.Length * 3);
        EnsureCapacity(maxBytes);
        int bytesWritten = Encoding.UTF8.GetBytes(str.AsSpan(), _buf.Slice(_pos + 4));
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), (uint)bytesWritten);
        else
            BinaryPrimitives.WriteUInt32LittleEndian(_buf.Slice(_pos, 4), (uint)bytesWritten);
        _pos += 4 + bytesWritten;
    }

    public void WriteBytes(byte[]? bytes)
    {
        if (bytes == null || bytes.Length == 0)
        {
            WriteU32(0);
            return;
        }
        WriteBytes(bytes.AsSpan());
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            WriteU32(0);
            return;
        }
        EnsureCapacity(checked(4 + bytes.Length));
        if (BitConverter.IsLittleEndian)
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(_buf), _pos), (uint)bytes.Length);
        else
            BinaryPrimitives.WriteUInt32LittleEndian(_buf.Slice(_pos, 4), (uint)bytes.Length);
        _pos += 4;
        bytes.CopyTo(_buf.Slice(_pos));
        _pos += bytes.Length;
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
        guid.TryWriteBytes(_buf.Slice(_pos, 16), bigEndian: true, out _);
        _pos += 16;
    }

    public void WriteUri(Uri uri) => WriteString(uri.ToString());

    public void WriteArray<T>(T[]? items, ValueWireWriterFunc<T> elementWriter)
    {
        if (items == null || items.Length == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Length);
        ReadOnlySpan<T> span = items;
        for (int i = 0; i < span.Length; i++)
        {
            elementWriter(ref this, span[i]);
        }
    }

    public void WriteList<T>(List<T>? items, ValueWireWriterFunc<T> elementWriter)
    {
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Count);
        ReadOnlySpan<T> span = CollectionsMarshal.AsSpan(items);
        for (int i = 0; i < span.Length; i++)
        {
            elementWriter(ref this, span[i]);
        }
    }

    public void WriteList<T>(IReadOnlyList<T>? items, ValueWireWriterFunc<T> elementWriter)
    {
        if (items is List<T> list)
        {
            WriteList(list, elementWriter);
            return;
        }
        if (items is T[] array)
        {
            WriteArray(array, elementWriter);
            return;
        }
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        int count = items.Count;
        WriteU32((uint)count);
        for (int i = 0; i < count; i++)
        {
            elementWriter(ref this, items[i]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUnmanaged<T>(in T value) where T : unmanaged
    {
        int size = Unsafe.SizeOf<T>();
        EnsureCapacity(size);
        MemoryMarshal.Write(_buf.Slice(_pos, size), in value);
        _pos += size;
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
            MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(list)).CopyTo(_buf.Slice(_pos, byteSize));
        }
        else if (items is T[] array)
        {
            MemoryMarshal.AsBytes(array.AsSpan()).CopyTo(_buf.Slice(_pos, byteSize));
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                T val = items[i];
                MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in val)).CopyTo(_buf.Slice(_pos + i * elemSize, elemSize));
            }
        }
        _pos += byteSize;
    }

    public void WriteMap<TKey, TValue>(Dictionary<TKey, TValue>? map, ValueWireWriterFunc<TKey> keyWriter, ValueWireWriterFunc<TValue> valueWriter)
        where TKey : notnull
    {
        if (map == null || map.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)map.Count);
        foreach (var kvp in map)
        {
            keyWriter(ref this, kvp.Key);
            valueWriter(ref this, kvp.Value);
        }
    }

    public void WriteMap<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? map, ValueWireWriterFunc<TKey> keyWriter, ValueWireWriterFunc<TValue> valueWriter)
    {
        if (map == null || map.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)map.Count);
        foreach (var kvp in map)
        {
            keyWriter(ref this, kvp.Key);
            valueWriter(ref this, kvp.Value);
        }
    }

    public void WriteSet<T>(HashSet<T>? items, ValueWireWriterFunc<T> elementWriter)
    {
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Count);
        foreach (var item in items)
        {
            elementWriter(ref this, item);
        }
    }

    public void WriteSet<T>(IReadOnlyCollection<T>? items, ValueWireWriterFunc<T> elementWriter)
    {
        if (items is HashSet<T> set)
        {
            WriteSet(set, elementWriter);
            return;
        }
        if (items == null || items.Count == 0)
        {
            WriteU32(0);
            return;
        }
        WriteU32((uint)items.Count);
        foreach (var item in items)
        {
            elementWriter(ref this, item);
        }
    }

    public void WriteOption<T>(T? value, ValueWireWriterFunc<T> valueWriter) where T : class
    {
        WriteBool(value != null);
        if (value != null) valueWriter(ref this, value);
    }

    public void WriteOptionValue<T>(T? value, ValueWireWriterFunc<T> valueWriter) where T : struct
    {
        WriteBool(value.HasValue);
        if (value.HasValue) valueWriter(ref this, value.Value);
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

    public readonly byte[] ToArray() => _buf.Slice(0, _pos).ToArray();
}
