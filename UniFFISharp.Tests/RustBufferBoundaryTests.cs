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
        Assert.NotNull(stream);
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
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var stream = new BigEndianStream(ms);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.CheckRemaining(-1));
    }

    [Fact]
    public void TestBigEndianStreamNegativeReadBytesThrows()
    {
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        var stream = new BigEndianStream(ms);

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.ReadBytes(-5));
    }

    [Fact]
    public void TestBigEndianStreamNegativeStringLengthThrows()
    {
        // Encoded Int32 length = -1 (0xFF, 0xFF, 0xFF, 0xFF) followed by dummy payload
        byte[] payload = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x41, 0x42 };
        using var ms = new MemoryStream(payload);
        var stream = new BigEndianStream(ms);

        Assert.Throws<StreamUnderflowException>(() => stream.ReadString());
    }
}
