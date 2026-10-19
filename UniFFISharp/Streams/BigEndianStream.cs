using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UniFFISharp.Exceptions;

namespace UniFFISharp.Streams;

public unsafe ref struct BigEndianStream
{
    private readonly byte* _ptr;
    private readonly long _len;
    private long _pos;

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnderflow(string? message = null)
    {
        if (message != null) throw new StreamUnderflowException(message);
        throw new StreamUnderflowException();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BigEndianStream(IntPtr data, long length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Stream length cannot be negative.");
        }
        if (length > 0 && data == IntPtr.Zero)
        {
            throw new ArgumentException("Data pointer cannot be null when length is greater than zero.", nameof(data));
        }
        _ptr = (byte*)data.ToPointer();
        _len = length;
        _pos = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BigEndianStream(byte* ptr, long length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Stream length cannot be negative.");
        }
        if (length > 0 && ptr == null)
        {
            throw new ArgumentException("Data pointer cannot be null when length is greater than zero.", nameof(ptr));
        }
        _ptr = ptr;
        _len = length;
        _pos = 0;
    }

    public long Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pos;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (value < 0 || value > _len) throw new ArgumentOutOfRangeException(nameof(value));
            _pos = value;
        }
    }

    public long Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _len;
    }

    public long Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _len - _pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasRemaining() => _pos < _len;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CheckRemaining(int bytesToRead)
    {
        if (bytesToRead < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesToRead), "Bytes to read cannot be negative.");
        }
        if (_len - _pos < bytesToRead)
        {
            ThrowUnderflow();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt8(byte value)
    {
        if (_pos + 1 > _len) ThrowUnderflow();
        _ptr[_pos++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt8(sbyte value) => WriteUInt8((byte)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        if (_pos + 2 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
        _pos += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value)
    {
        if (_pos + 2 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
        _pos += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteUInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64(ulong value)
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat32(float value)
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteSingleBigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat64(double value)
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        BinaryPrimitives.WriteDoubleBigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    public void WriteString(string value)
    {
        value ??= string.Empty;
        if (_pos + 4 > _len) ThrowUnderflow();
        if (value.Length == 0)
        {
            WriteInt32(0);
            return;
        }

        long remaining = _len - (_pos + 4);
        if (remaining < 0) ThrowUnderflow();
        int available = remaining > int.MaxValue ? int.MaxValue : (int)remaining;

        if (!Encoding.UTF8.TryGetBytes(value.AsSpan(), new Span<byte>(_ptr + _pos + 4, available), out int bytesWritten))
        {
            ThrowUnderflow();
        }

        BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_ptr + _pos, 4), bytesWritten);
        _pos += 4 + bytesWritten;
    }

    public void WriteBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        WriteBytes(bytes, 0, bytes.Length);
    }

    public void WriteBytes(byte[] bytes, int offset, int count)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || count < 0 || offset + count > bytes.Length) throw new ArgumentOutOfRangeException();
        if (count == 0) return;

        if (_pos + count > _len) ThrowUnderflow();
        new ReadOnlySpan<byte>(bytes, offset, count).CopyTo(new Span<byte>(_ptr + _pos, count));
        _pos += count;
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        if (_pos + bytes.Length > _len) ThrowUnderflow();
        bytes.CopyTo(new Span<byte>(_ptr + _pos, bytes.Length));
        _pos += bytes.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (_pos >= _len) ThrowUnderflow();
        return _ptr[_pos++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8() => (sbyte)ReadUInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        if (_pos + 2 > _len) ThrowUnderflow();
        ushort val = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
        _pos += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        if (_pos + 2 > _len) ThrowUnderflow();
        short val = BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
        _pos += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        uint val = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        int val = BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        ulong val = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        long val = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat32()
    {
        if (_pos + 4 > _len) ThrowUnderflow();
        float val = BinaryPrimitives.ReadSingleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadFloat64()
    {
        if (_pos + 8 > _len) ThrowUnderflow();
        double val = BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    public string ReadString()
    {
        int length = ReadInt32();
        if (length < 0) ThrowUnderflow("Negative string length prefix encountered.");
        if (length == 0) return string.Empty;
        if (_pos + length > _len) ThrowUnderflow();
        string str = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(_ptr + _pos, length));
        _pos += length;
        return str;
    }

    public byte[] ReadBytes(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Read length cannot be negative.");
        if (length == 0) return Array.Empty<byte>();
        if (_pos + length > _len) ThrowUnderflow();
        byte[] result = new byte[length];
        new ReadOnlySpan<byte>(_ptr + _pos, length).CopyTo(result);
        _pos += length;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt8List(List<byte>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        if (_pos + count > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        src.CopyTo(new Span<byte>(_ptr + _pos, count));
        _pos += count;
    }

    public List<byte> ReadUInt8List()
    {
        int count = ReadInt32();
        if (count < 0 || count > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<byte>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        var src = new ReadOnlySpan<byte>(_ptr + _pos, count);
        var dst = CollectionsMarshal.AsSpan(list);
        src.CopyTo(dst);
        _pos += count;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt8List(List<sbyte>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        if (_pos + count > _len) ThrowUnderflow();
        var src = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(value));
        src.CopyTo(new Span<byte>(_ptr + _pos, count));
        _pos += count;
    }

    public List<sbyte> ReadInt8List()
    {
        int count = ReadInt32();
        if (count < 0 || count > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<sbyte>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        var src = new ReadOnlySpan<byte>(_ptr + _pos, count);
        var dst = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(list));
        src.CopyTo(dst);
        _pos += count;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16List(List<ushort>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(ushort));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, ushort>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<ushort> ReadUInt16List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(ushort) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<ushort>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(ushort);
        var src = MemoryMarshal.Cast<byte, ushort>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16List(List<short>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(short));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, short>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<short> ReadInt16List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(short) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<short>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(short);
        var src = MemoryMarshal.Cast<byte, short>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32List(List<uint>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(uint));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, uint>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<uint> ReadUInt32List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(uint) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<uint>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(uint);
        var src = MemoryMarshal.Cast<byte, uint>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32List(List<int>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(int));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, int>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<int> ReadInt32List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(int) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<int>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(int);
        var src = MemoryMarshal.Cast<byte, int>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64List(List<ulong>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(ulong));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, ulong>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<ulong> ReadUInt64List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(ulong) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<ulong>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(ulong);
        var src = MemoryMarshal.Cast<byte, ulong>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64List(List<long>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(long));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = CollectionsMarshal.AsSpan(value);
        var dst = MemoryMarshal.Cast<byte, long>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<long> ReadInt64List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(long) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<long>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(long);
        var src = MemoryMarshal.Cast<byte, long>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = CollectionsMarshal.AsSpan(list);
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat32List(List<float>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(float));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = MemoryMarshal.Cast<float, int>(CollectionsMarshal.AsSpan(value));
        var dst = MemoryMarshal.Cast<byte, int>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<float> ReadFloat32List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(float) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<float>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(float);
        var src = MemoryMarshal.Cast<byte, int>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = MemoryMarshal.Cast<float, int>(CollectionsMarshal.AsSpan(list));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat64List(List<double>? value)
    {
        if (value == null) { WriteInt32(0); return; }
        int count = value.Count;
        WriteInt32(count);
        if (count == 0) return;
        int bytesNeeded = checked(count * sizeof(double));
        if (_pos + bytesNeeded > _len) ThrowUnderflow();
        var src = MemoryMarshal.Cast<double, long>(CollectionsMarshal.AsSpan(value));
        var dst = MemoryMarshal.Cast<byte, long>(new Span<byte>(_ptr + _pos, bytesNeeded));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
    }

    public List<double> ReadFloat64List()
    {
        int count = ReadInt32();
        if (count < 0 || (long)count * sizeof(double) > Remaining)
        {
            throw new UniffiException($"Corrupt stream: sequence count {count} exceeds remaining buffer bytes {Remaining}");
        }
        var list = new List<double>(count);
        if (count == 0) return list;
        CollectionsMarshal.SetCount(list, count);
        int bytesNeeded = count * sizeof(double);
        var src = MemoryMarshal.Cast<byte, long>(new ReadOnlySpan<byte>(_ptr + _pos, bytesNeeded));
        var dst = MemoryMarshal.Cast<double, long>(CollectionsMarshal.AsSpan(list));
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.ReverseEndianness(src, dst);
        }
        else
        {
            src.CopyTo(dst);
        }
        _pos += bytesNeeded;
        return list;
    }
}
