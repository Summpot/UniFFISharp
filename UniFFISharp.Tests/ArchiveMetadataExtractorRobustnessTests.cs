using System;
using System.IO;
using System.Text;
using Xunit;
using UniFFISharp.Generator.PeParser;

namespace UniFFISharp.Tests;

public class ArchiveMetadataExtractorRobustnessTests
{
    [Fact]
    public void TestNonArchiveReturnsEmpty()
    {
        byte[] dummy = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var list = ArchiveMetadataExtractor.ExtractAll(dummy);
        Assert.Empty(list);
    }

    [Fact]
    public void TestMalformedArchiveHeaderGracefullyHandled()
    {
        // Valid "!<arch>\n" magic + truncated/corrupt member header
        byte[] data = new byte[68];
        Encoding.ASCII.GetBytes("!<arch>\n").CopyTo(data, 0);
        // Put invalid size string
        Encoding.ASCII.GetBytes("/               ").CopyTo(data, 8);
        Encoding.ASCII.GetBytes("invalid   ").CopyTo(data, 56);

        var list = ArchiveMetadataExtractor.ExtractAll(data);
        Assert.Empty(list);
    }

    [Fact]
    public void TestLinkerMemberOversizedSymbolsCountGracefullyHandled()
    {
        // Valid "!<arch>\n" magic + member header stating size 100
        // but member body contains numSymbols = 0x3FFFFFFF (claims huge symbol count)
        byte[] data = new byte[8 + 60 + 100];
        Encoding.ASCII.GetBytes("!<arch>\n").CopyTo(data, 0);

        // Header: name="/" (16), mtime (12), uid (6), gid (6), mode (8), size "100       " (10), end "\x60\x0A" (2)
        Encoding.ASCII.GetBytes("/               ").CopyTo(data, 8);
        Encoding.ASCII.GetBytes("0           ").CopyTo(data, 24);
        Encoding.ASCII.GetBytes("0     ").CopyTo(data, 36);
        Encoding.ASCII.GetBytes("0     ").CopyTo(data, 42);
        Encoding.ASCII.GetBytes("0       ").CopyTo(data, 48);
        Encoding.ASCII.GetBytes("100       ").CopyTo(data, 56);
        data[66] = 0x60;
        data[67] = 0x0A;

        // Body: body starts at pos 68. Put numSymbols = 0x3FFFFFFF in Big Endian
        data[68] = 0x3F;
        data[69] = 0xFF;
        data[70] = 0xFF;
        data[71] = 0xFF;

        // Should return empty list safely without throwing OutOfMemoryException or IndexOutOfRangeException
        var list = ArchiveMetadataExtractor.ExtractAll(data);
        Assert.Empty(list);
    }

    [Fact]
    public void TestMaxArchiveSymbolsPropertyConfigurable()
    {
        int original = ArchiveMetadataExtractor.MaxArchiveSymbols;
        try
        {
            ArchiveMetadataExtractor.MaxArchiveSymbols = 500;
            Assert.Equal(500, ArchiveMetadataExtractor.MaxArchiveSymbols);
        }
        finally
        {
            ArchiveMetadataExtractor.MaxArchiveSymbols = original;
        }
    }
}
