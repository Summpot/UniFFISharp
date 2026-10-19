using System;
using System.IO;
using Xunit;
using UniFFISharp.Exceptions;
using UniFFISharp.Streams;
using UniFFISharp.Types;

namespace UniFFISharp.Tests;

public class RustBufferBoundaryTests
{
    [Fact]
    public void TestRustBufferLengthExceedsCapacityThrows()
    {
        var buf = new RustBuffer
        {
            capacity = 10,
            len = 20,
            data = new IntPtr(12345)
        };

        var ex = Assert.Throws<ArgumentException>(() => buf.AsStream());
        Assert.Contains("cannot exceed capacity", ex.Message);
    }

    [Fact]
    public void TestRustBufferNullDataWithNonZeroLengthThrows()
    {
        var buf = new RustBuffer
        {
            capacity = 10,
            len = 5,
            data = IntPtr.Zero
        };

        var ex = Assert.Throws<ArgumentException>(() => buf.AsStream());
        Assert.Contains("cannot be null", ex.Message);
    }

    [Fact]
    public void TestRustBufferZeroLengthWithNullDataSucceeds()
    {
        var buf = new RustBuffer
        {
            capacity = 0,
            len = 0,
            data = IntPtr.Zero
        };

        var stream = buf.AsStream();
        Assert.Equal(0, stream.Length);
        Assert.False(stream.HasRemaining());
    }

    [Fact]
    public void TestRustBufferWritableNullDataWithNonZeroCapacityThrows()
    {
        var buf = new RustBuffer
        {
            capacity = 10,
            len = 0,
            data = IntPtr.Zero
        };

        var ex = Assert.Throws<ArgumentException>(() => buf.AsWriteableStream());
        Assert.Contains("cannot be null", ex.Message);
    }

    [Fact]
    public void TestBigEndianStreamNegativeCheckRemainingThrows()
    {
        byte[] buffer = new byte[] { 1, 2, 3 };
        unsafe
        {
            fixed (byte* p = buffer)
            {
                var stream = new BigEndianStream(p, buffer.Length);
                bool threw = false;
                try { stream.CheckRemaining(-1); }
                catch (ArgumentOutOfRangeException) { threw = true; }
                Assert.True(threw);
            }
        }
    }

    [Fact]
    public void TestBigEndianStreamNegativeReadBytesThrows()
    {
        byte[] buffer = new byte[] { 1, 2, 3 };
        unsafe
        {
            fixed (byte* p = buffer)
            {
                var stream = new BigEndianStream(p, buffer.Length);
                bool threw = false;
                try { stream.ReadBytes(-5); }
                catch (ArgumentOutOfRangeException) { threw = true; }
                Assert.True(threw);
            }
        }
    }

    [Fact]
    public void TestBigEndianStreamNegativeStringLengthThrows()
    {
        // Encoded Int32 length = -1 (0xFF, 0xFF, 0xFF, 0xFF) followed by dummy payload
        byte[] payload = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x41, 0x42 };
        unsafe
        {
            fixed (byte* p = payload)
            {
                var stream = new BigEndianStream(p, payload.Length);
                bool threw = false;
                try { stream.ReadString(); }
                catch (StreamUnderflowException) { threw = true; }
                Assert.True(threw);
            }
        }
    }

    [Fact]
    public void TestBigEndianStream_BatchInt32List_RoundtripsCorrectly()
    {
        var input = new System.Collections.Generic.List<int> { 0, 1, -1, int.MaxValue, int.MinValue, 42, 1337 };
        byte[] buffer = new byte[4 + input.Count * sizeof(int)];
        unsafe
        {
            fixed (byte* p = buffer)
            {
                var writeStream = new BigEndianStream(p, buffer.Length);
                writeStream.WriteInt32List(input);
                Assert.Equal(buffer.Length, writeStream.Position);

                var readStream = new BigEndianStream(p, buffer.Length);
                var output = readStream.ReadInt32List();
                Assert.Equal(input, output);
            }
        }
    }

    [Fact]
    public void TestBigEndianStream_BatchDoubleList_RoundtripsCorrectly()
    {
        var input = new System.Collections.Generic.List<double> { 0.0, 3.1415926535, -2.71828, double.MaxValue, double.MinValue };
        byte[] buffer = new byte[4 + input.Count * sizeof(double)];
        unsafe
        {
            fixed (byte* p = buffer)
            {
                var writeStream = new BigEndianStream(p, buffer.Length);
                writeStream.WriteFloat64List(input);
                Assert.Equal(buffer.Length, writeStream.Position);

                var readStream = new BigEndianStream(p, buffer.Length);
                var output = readStream.ReadFloat64List();
                Assert.Equal(input, output);
            }
        }
    }

    [Fact]
    public void TestBigEndianStream_CorruptCountExceedingRemaining_ThrowsUniffiException()
    {
        byte[] payload = new byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(0, 4), 1_000_000);
        unsafe
        {
            fixed (byte* p = payload)
            {
                var stream = new BigEndianStream(p, payload.Length);
                bool threw = false;
                try
                {
                    stream.ReadInt32List();
                }
                catch (UniffiException ex)
                {
                    threw = true;
                    Assert.Contains("exceeds remaining buffer bytes", ex.Message);
                }
                Assert.True(threw);
            }
        }
    }
}
