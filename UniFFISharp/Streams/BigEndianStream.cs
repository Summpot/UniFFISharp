using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using UniFFISharp.Exceptions;

namespace UniFFISharp.Streams;

public unsafe ref struct BigEndianStream
{
    private readonly byte* _ptr;
    private readonly long _len;
    private long _pos;

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
            throw new StreamUnderflowException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt8(byte value)
    {
        if (_pos + 1 > _len) throw new StreamUnderflowException();
        _ptr[_pos++] = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt8(sbyte value) => WriteUInt8((byte)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        if (_pos + 2 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
        _pos += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value)
    {
        if (_pos + 2 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
        _pos += 2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteUInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64(ulong value)
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat32(float value)
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteSingleBigEndian(new Span<byte>(_ptr + _pos, 4), value);
        _pos += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat64(double value)
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        BinaryPrimitives.WriteDoubleBigEndian(new Span<byte>(_ptr + _pos, 8), value);
        _pos += 8;
    }

    public void WriteString(string value)
    {
        value ??= string.Empty;
        int byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt32(byteCount);
        if (_pos + byteCount > _len) throw new StreamUnderflowException();
        Encoding.UTF8.GetBytes(value.AsSpan(), new Span<byte>(_ptr + _pos, byteCount));
        _pos += byteCount;
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

        if (_pos + count > _len) throw new StreamUnderflowException();
        new ReadOnlySpan<byte>(bytes, offset, count).CopyTo(new Span<byte>(_ptr + _pos, count));
        _pos += count;
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return;
        if (_pos + bytes.Length > _len) throw new StreamUnderflowException();
        bytes.CopyTo(new Span<byte>(_ptr + _pos, bytes.Length));
        _pos += bytes.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (_pos >= _len) throw new StreamUnderflowException();
        return _ptr[_pos++];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8() => (sbyte)ReadUInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        if (_pos + 2 > _len) throw new StreamUnderflowException();
        ushort val = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
        _pos += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        if (_pos + 2 > _len) throw new StreamUnderflowException();
        short val = BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
        _pos += 2;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        uint val = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        int val = BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        ulong val = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        long val = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat32()
    {
        if (_pos + 4 > _len) throw new StreamUnderflowException();
        float val = BinaryPrimitives.ReadSingleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
        _pos += 4;
        return val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadFloat64()
    {
        if (_pos + 8 > _len) throw new StreamUnderflowException();
        double val = BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
        _pos += 8;
        return val;
    }

    public string ReadString()
    {
        int length = ReadInt32();
        if (length < 0) throw new StreamUnderflowException("Negative string length prefix encountered.");
        if (length == 0) return string.Empty;
        if (_pos + length > _len) throw new StreamUnderflowException();
        string str = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(_ptr + _pos, length));
        _pos += length;
        return str;
    }

    public byte[] ReadBytes(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Read length cannot be negative.");
        if (length == 0) return Array.Empty<byte>();
        if (_pos + length > _len) throw new StreamUnderflowException();
        byte[] result = new byte[length];
        new ReadOnlySpan<byte>(_ptr + _pos, length).CopyTo(result);
        _pos += length;
        return result;
    }
}
