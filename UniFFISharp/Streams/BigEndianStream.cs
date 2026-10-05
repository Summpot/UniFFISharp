using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UniFFISharp.Exceptions;

namespace UniFFISharp.Streams;

public unsafe class BigEndianStream
{
    private readonly Stream? _stream;
    private readonly byte* _ptr;
    private readonly long _len;
    private long _pos;
    private readonly bool _isDirectPointer;

    public BigEndianStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _isDirectPointer = false;
        _ptr = null;
        _len = 0;
        _pos = 0;
    }

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
        _isDirectPointer = true;
        _stream = null;
    }

    public Stream BaseStream => _stream ?? throw new NotSupportedException("Direct pointer stream does not have an underlying Stream.");

    public long Position
    {
        get => _isDirectPointer ? _pos : _stream!.Position;
        set
        {
            if (_isDirectPointer)
            {
                if (value < 0 || value > _len) throw new ArgumentOutOfRangeException(nameof(value));
                _pos = value;
            }
            else
            {
                _stream!.Position = value;
            }
        }
    }

    public long Length => _isDirectPointer ? _len : _stream!.Length;

    public bool HasRemaining() => _isDirectPointer ? _pos < _len : _stream!.Position < _stream!.Length;

    public void CheckRemaining(int bytesToRead)
    {
        if (bytesToRead < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytesToRead), "Bytes to read cannot be negative.");
        }
        long rem = _isDirectPointer ? (_len - _pos) : (_stream!.Length - _stream!.Position);
        if (rem < bytesToRead)
        {
            throw new StreamUnderflowException();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt8(byte value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 1 > _len) throw new StreamUnderflowException();
            _ptr[_pos++] = value;
            return;
        }
        _stream!.WriteByte(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt8(sbyte value) => WriteUInt8((byte)value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt16(ushort value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 2 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
            _pos += 2;
            return;
        }
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt16(short value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 2 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteInt16BigEndian(new Span<byte>(_ptr + _pos, 2), value);
            _pos += 2;
            return;
        }
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt32(uint value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteUInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
            _pos += 4;
            return;
        }
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt32(int value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(_ptr + _pos, 4), value);
            _pos += 4;
            return;
        }
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteUInt64(ulong value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
            _pos += 8;
            return;
        }
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteInt64(long value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteInt64BigEndian(new Span<byte>(_ptr + _pos, 8), value);
            _pos += 8;
            return;
        }
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat32(float value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteSingleBigEndian(new Span<byte>(_ptr + _pos, 4), value);
            _pos += 4;
            return;
        }
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteFloat64(double value)
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            BinaryPrimitives.WriteDoubleBigEndian(new Span<byte>(_ptr + _pos, 8), value);
            _pos += 8;
            return;
        }
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        _stream!.Write(buffer);
    }

    public void WriteString(string value)
    {
        value ??= string.Empty;
        if (_isDirectPointer)
        {
            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt32(byteCount);
            if (_pos + byteCount > _len) throw new StreamUnderflowException();
            Encoding.UTF8.GetBytes(value.AsSpan(), new Span<byte>(_ptr + _pos, byteCount));
            _pos += byteCount;
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(bytes.Length);
        _stream!.Write(bytes, 0, bytes.Length);
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

        if (_isDirectPointer)
        {
            if (_pos + count > _len) throw new StreamUnderflowException();
            new ReadOnlySpan<byte>(bytes, offset, count).CopyTo(new Span<byte>(_ptr + _pos, count));
            _pos += count;
            return;
        }
        _stream!.Write(bytes, offset, count);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadUInt8()
    {
        if (_isDirectPointer)
        {
            if (_pos >= _len) throw new StreamUnderflowException();
            return _ptr[_pos++];
        }
        CheckRemaining(1);
        int val = _stream!.ReadByte();
        if (val == -1) throw new StreamUnderflowException();
        return (byte)val;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadInt8() => (sbyte)ReadUInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        if (_isDirectPointer)
        {
            if (_pos + 2 > _len) throw new StreamUnderflowException();
            ushort val = BinaryPrimitives.ReadUInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
            _pos += 2;
            return val;
        }
        CheckRemaining(2);
        Span<byte> buffer = stackalloc byte[2];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        if (_isDirectPointer)
        {
            if (_pos + 2 > _len) throw new StreamUnderflowException();
            short val = BinaryPrimitives.ReadInt16BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 2));
            _pos += 2;
            return val;
        }
        CheckRemaining(2);
        Span<byte> buffer = stackalloc byte[2];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            uint val = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
            _pos += 4;
            return val;
        }
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            int val = BinaryPrimitives.ReadInt32BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
            _pos += 4;
            return val;
        }
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            ulong val = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
            _pos += 8;
            return val;
        }
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            long val = BinaryPrimitives.ReadInt64BigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
            _pos += 8;
            return val;
        }
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat32()
    {
        if (_isDirectPointer)
        {
            if (_pos + 4 > _len) throw new StreamUnderflowException();
            float val = BinaryPrimitives.ReadSingleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 4));
            _pos += 4;
            return val;
        }
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleBigEndian(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadFloat64()
    {
        if (_isDirectPointer)
        {
            if (_pos + 8 > _len) throw new StreamUnderflowException();
            double val = BinaryPrimitives.ReadDoubleBigEndian(new ReadOnlySpan<byte>(_ptr + _pos, 8));
            _pos += 8;
            return val;
        }
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream!.ReadExactly(buffer);
        return BinaryPrimitives.ReadDoubleBigEndian(buffer);
    }

    public string ReadString()
    {
        int length = ReadInt32();
        if (length < 0) throw new StreamUnderflowException("Negative string length prefix encountered.");
        if (length == 0) return string.Empty;
        if (_isDirectPointer)
        {
            if (_pos + length > _len) throw new StreamUnderflowException();
            string str = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(_ptr + _pos, length));
            _pos += length;
            return str;
        }
        CheckRemaining(length);
        byte[] buffer = new byte[length];
        _stream!.ReadExactly(buffer, 0, length);
        return Encoding.UTF8.GetString(buffer);
    }

    public byte[] ReadBytes(int length)
    {
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Read length cannot be negative.");
        if (length == 0) return Array.Empty<byte>();
        if (_isDirectPointer)
        {
            if (_pos + length > _len) throw new StreamUnderflowException();
            byte[] result = new byte[length];
            new ReadOnlySpan<byte>(_ptr + _pos, length).CopyTo(result);
            _pos += length;
            return result;
        }
        CheckRemaining(length);
        byte[] buffer = new byte[length];
        _stream!.ReadExactly(buffer, 0, length);
        return buffer;
    }
}
