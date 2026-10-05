using System;
using System.IO;
using Xunit;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Tests;

public class MetadataReaderRobustnessTests
{
    [Fact]
    public void TestIterativeNestedTypeParsing()
    {
        // Option (12) -> Sequence (17) -> UInt8 (0)
        // Note: Vec<u8> is optimized to Bytes
        byte[] data = new byte[] { 12, 17, 0 };
        var reader = new MetadataReader(data);
        var type = reader.ReadType();

        Assert.Equal(TypeKind.Option, type.Kind);
        Assert.NotNull(type.InnerType);
        Assert.Equal(TypeKind.Bytes, type.InnerType.Kind);
    }

    [Fact]
    public void TestIterativeMapTypeParsing()
    {
        // Map (18) -> String (11) -> Int32 (6)
        byte[] data = new byte[] { 18, 11, 6 };
        var reader = new MetadataReader(data);
        var type = reader.ReadType();

        Assert.Equal(TypeKind.Map, type.Kind);
        Assert.NotNull(type.KeyType);
        Assert.Equal(TypeKind.String, type.KeyType.Kind);
        Assert.NotNull(type.ValueType);
        Assert.Equal(TypeKind.Int32, type.ValueType.Kind);
    }

    [Fact]
    public void TestTypeDepthLimitThrowsGracefully()
    {
        // Construct 100 nested Option (0x0C) followed by UInt8 (0x00)
        byte[] data = new byte[101];
        for (int i = 0; i < 100; i++)
        {
            data[i] = 12; // TYPE_OPTION
        }
        data[100] = 0; // TYPE_UINT8

        var reader = new MetadataReader(data);
        var ex = Assert.Throws<InvalidOperationException>(() => reader.ReadType());
        Assert.Contains("depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TestIterativeNestedLiteralSome()
    {
        // LIT_SOME (5) -> LIT_SOME (5) -> LIT_INT (1) -> len 1 -> "7"
        byte[] data = new byte[] { 5, 5, 1, 1, (byte)'7' };
        var reader = new MetadataReader(data);
        var type = new UniFFIType { Kind = TypeKind.Int32 };
        string? result = reader.ReadLiteral(type);

        Assert.Equal("7", result);
    }

    [Fact]
    public void TestIterativeSkipLiteral()
    {
        // LIT_SOME (5) -> LIT_SOME (5) -> LIT_STR (0) -> len 4 -> "test" -> followed by next byte 42
        byte[] data = new byte[] { 5, 5, 0, 4, (byte)'t', (byte)'e', (byte)'s', (byte)'t', 42 };
        var reader = new MetadataReader(data);
        reader.SkipLiteral();

        Assert.Equal(42, reader.ReadUInt8());
    }

    [Fact]
    public void TestStrictUtf8RejectsInvalidBytes()
    {
        // 0xFF is an invalid initial byte in UTF-8
        byte[] data = new byte[] { 1, 0xFF };
        var reader = new MetadataReader(data);

        Assert.ThrowsAny<Exception>(() => reader.ReadString());
    }
}
