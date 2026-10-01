using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.PeParser;

/// <summary>
/// Extracts UniFFI metadata from Mach-O dynamic libraries (.dylib) on macOS.
/// Supports both single-architecture Mach-O (64-bit) and Universal / Fat binaries.
/// </summary>
public static class MachOMetadataExtractor
{
    private const uint MH_MAGIC_64 = 0xFEEDFACF;
    private const uint MH_CIGAM_64 = 0xCFFAEDFE;
    private const uint FAT_MAGIC = 0xCAFEBABE;
    private const uint FAT_CIGAM = 0xBEBAFECA;

    private const uint LC_SEGMENT_64 = 0x19;
    private const uint LC_SYMTAB = 0x02;

    public static bool IsMachO(byte[] data)
    {
        if (data == null || data.Length < 4) return false;
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
        uint magicBE = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));

        return magic == MH_MAGIC_64 || magic == MH_CIGAM_64 ||
               magicBE == FAT_MAGIC || magicBE == FAT_CIGAM;
    }

    public static ComponentInterface? Extract(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var fi = new FileInfo(filePath);
        if (fi.Length > int.MaxValue) return null;
        byte[] fileBytes = File.ReadAllBytes(filePath);
        return Extract(fileBytes);
    }

    public static ComponentInterface? Extract(byte[] fileBytes)
    {
        if (!IsMachO(fileBytes) || fileBytes.Length < 32)
        {
            return null;
        }

        byte[] machOData = fileBytes;
        uint magicBE = BinaryPrimitives.ReadUInt32BigEndian(fileBytes.AsSpan(0, 4));

        // Check if Universal / Fat binary
        if (magicBE == FAT_MAGIC || magicBE == FAT_CIGAM)
        {
            machOData = ExtractFromFatBinary(fileBytes);
            if (machOData == null || machOData.Length < 32)
            {
                return null;
            }
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(machOData.AsSpan(0, 4));
        bool isLE = (magic == MH_MAGIC_64);

        var aggregator = new MetadataAggregator();
        var ci = aggregator.ComponentInterface;
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);

        var symbols = ExtractSymbolsMachO64(machOData, isLE);

        foreach (var (symName, symData) in symbols)
        {
            ci.AllExports.Add(symName);

            if (!symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) ||
                symName.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seenSymbols.Add(symName))
            {
                continue;
            }

            try
            {
                var metadataReader = new MetadataReader(symData);
                object? item = metadataReader.ReadItem();
                if (item == null)
                {
                    ci.DiscoveredSymbols.Add($"{symName} (item is null)");
                    continue;
                }

                ci.DiscoveredSymbols.Add($"{symName} (parsed {item.GetType().Name})");

                aggregator.AddItem(item);
            }
            catch (Exception ex)
            {
                ci.DiscoveredSymbols.Add($"{symName} (read ex: {ex.Message})");
            }
        }

        aggregator.Build();

        return ci;
    }

    private static byte[] ExtractFromFatBinary(byte[] data)
    {
        if (data.Length < 8) return data;
        uint nfat = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4));

        int bestOffset = 0;
        int bestSize = 0;

        for (int i = 0; i < nfat; i++)
        {
            int entryPos = 8 + i * 20;
            if (entryPos + 20 > data.Length) break;

            uint cpuType = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(entryPos, 4));
            uint offset = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(entryPos + 8, 4));
            uint size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(entryPos + 12, 4));

            // CPU_TYPE_ARM64 = 0x0100000C, CPU_TYPE_X86_64 = 0x01000007
            if (cpuType == 0x0100000C || (cpuType == 0x01000007 && bestOffset == 0))
            {
                bestOffset = (int)offset;
                bestSize = (int)size;
                if (cpuType == 0x0100000C) break; // Prefer ARM64
            }
        }

        if (bestOffset > 0 && bestOffset + bestSize <= data.Length)
        {
            byte[] slice = new byte[bestSize];
            Array.Copy(data, bestOffset, slice, 0, bestSize);
            return slice;
        }

        return data;
    }

    internal static List<(string Name, byte[] Data)> ExtractSymbolsMachO64(byte[] data, bool isLE)
    {
        var results = new List<(string, byte[])>();
        if (data.Length < 32) return results;

        uint ncmds = ReadUInt32(data, 16, isLE);
        int cmdOffset = 32;

        var sections = new List<(ulong addr, ulong size, uint offset)>();
        (uint symoff, uint nsyms, uint stroff, uint strsize)? symtab = null;

        for (int i = 0; i < ncmds && cmdOffset + 8 <= data.Length; i++)
        {
            uint cmd = ReadUInt32(data, cmdOffset, isLE);
            uint cmdsize = ReadUInt32(data, cmdOffset + 4, isLE);
            if (cmdsize < 8 || cmdOffset + (int)cmdsize > data.Length) break;

            if (cmd == LC_SEGMENT_64 && cmdOffset + 72 <= data.Length)
            {
                uint nsects = ReadUInt32(data, cmdOffset + 64, isLE);
                int secStart = cmdOffset + 72;

                for (int s = 0; s < nsects && secStart + 80 <= data.Length; s++)
                {
                    int secPos = secStart + s * 80;
                    ulong addr = ReadUInt64(data, secPos + 32, isLE);
                    ulong size = ReadUInt64(data, secPos + 40, isLE);
                    uint offset = ReadUInt32(data, secPos + 48, isLE);
                    sections.Add((addr, size, offset));
                }
            }
            else if (cmd == LC_SYMTAB && cmdOffset + 24 <= data.Length)
            {
                uint symoff = ReadUInt32(data, cmdOffset + 8, isLE);
                uint nsyms = ReadUInt32(data, cmdOffset + 12, isLE);
                uint stroff = ReadUInt32(data, cmdOffset + 16, isLE);
                uint strsize = ReadUInt32(data, cmdOffset + 20, isLE);
                symtab = (symoff, nsyms, stroff, strsize);
            }

            cmdOffset += (int)cmdsize;
        }

        if (symtab == null) return results;

        var st = symtab.Value;
        int symEntrySize = 16; // struct nlist_64
        if ((long)st.symoff + (long)st.nsyms * symEntrySize > data.Length) return results;

        for (int i = 0; i < st.nsyms; i++)
        {
            int entryPos = (int)st.symoff + i * symEntrySize;
            uint n_strx = ReadUInt32(data, entryPos, isLE);
            byte n_sect = data[entryPos + 5]; // 1-based section index
            ulong n_value = ReadUInt64(data, entryPos + 8, isLE);

            if (n_strx == 0 || (long)st.stroff + n_strx >= data.Length) continue;

            int nullPos = Array.IndexOf<byte>(data, 0, (int)st.stroff + (int)n_strx);
            if (nullPos < 0) continue;

            string rawName = Encoding.ASCII.GetString(data, (int)st.stroff + (int)n_strx, nullPos - ((int)st.stroff + (int)n_strx));
            string symName = rawName.StartsWith("_") ? rawName.Substring(1) : rawName;

            if (symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
            {
                if (n_sect > 0 && n_sect <= sections.Count)
                {
                    var sec = sections[n_sect - 1];
                    if (n_value >= sec.addr && n_value < sec.addr + sec.size)
                    {
                        int dataPos = (int)sec.offset + (int)(n_value - sec.addr);
                        int dataLen = (int)(sec.size - (n_value - sec.addr));

                        if (dataPos >= 0 && dataPos + dataLen <= data.Length && dataLen > 0)
                        {
                            byte[] symData = new byte[dataLen];
                            Array.Copy(data, dataPos, symData, 0, dataLen);
                            results.Add((symName, symData));
                        }
                    }
                }
            }
        }

        return results;
    }

    private static uint ReadUInt32(byte[] data, int offset, bool isLE) =>
        isLE ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
             : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static ulong ReadUInt64(byte[] data, int offset, bool isLE) =>
        isLE ? BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8))
             : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8));
}
