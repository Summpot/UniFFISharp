using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using UniFFISharp.Exceptions;

namespace UniFFISharp.Streams;

public class BigEndianStream
{
    private readonly Stream _stream;

    public BigEndianStream(Stream stream)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public Stream BaseStream => _stream;

    public long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    public long Length => _stream.Length;

    public bool HasRemaining() => _stream.Position < _stream.Length;

    public void CheckRemaining(int bytesToRead)
    {
        if (_stream.Position + bytesToRead > _stream.Length)
        {
            throw new StreamUnderflowException();
        }
    }

    public void WriteUInt8(byte value)
    {
        _stream.WriteByte(value);
    }

    public void WriteInt8(sbyte value)
    {
        _stream.WriteByte((byte)value);
    }

    public void WriteUInt16(ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt16(short value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUInt32(uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt32(int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteUInt64(ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteInt64(long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteFloat32(float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteFloat64(double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        _stream.Write(buffer);
    }

    public void WriteString(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteInt32(bytes.Length);
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteBytes(byte[] bytes)
    {
        _stream.Write(bytes, 0, bytes.Length);
    }

    public void WriteBytes(byte[] bytes, int offset, int count)
    {
        _stream.Write(bytes, offset, count);
    }

    public byte ReadUInt8()
    {
        CheckRemaining(1);
        int val = _stream.ReadByte();
        if (val == -1) throw new StreamUnderflowException();
        return (byte)val;
    }

    public sbyte ReadInt8()
    {
        return (sbyte)ReadUInt8();
    }

    public ushort ReadUInt16()
    {
        CheckRemaining(2);
        Span<byte> buffer = stackalloc byte[2];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public short ReadInt16()
    {
        CheckRemaining(2);
        Span<byte> buffer = stackalloc byte[2];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    public uint ReadUInt32()
    {
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt32BigEndian(buffer);
    }

    public int ReadInt32()
    {
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public ulong ReadUInt64()
    {
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadUInt64BigEndian(buffer);
    }

    public long ReadInt64()
    {
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public float ReadFloat32()
    {
        CheckRemaining(4);
        Span<byte> buffer = stackalloc byte[4];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadSingleBigEndian(buffer);
    }

    public double ReadFloat64()
    {
        CheckRemaining(8);
        Span<byte> buffer = stackalloc byte[8];
        _stream.ReadExactly(buffer);
        return BinaryPrimitives.ReadDoubleBigEndian(buffer);
    }

    public string ReadString()
    {
        int length = ReadInt32();
        if (length == 0) return string.Empty;
        CheckRemaining(length);
        byte[] buffer = new byte[length];
        _stream.ReadExactly(buffer, 0, length);
        return Encoding.UTF8.GetString(buffer);
    }

    public byte[] ReadBytes(int length)
    {
        if (length == 0) return Array.Empty<byte>();
        CheckRemaining(length);
        byte[] buffer = new byte[length];
        _stream.ReadExactly(buffer, 0, length);
        return buffer;
    }
}
