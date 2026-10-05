using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.PeParser;

/// <summary>
/// Extracts UniFFI metadata from ELF shared libraries (.so).
/// Parses ELF64 and ELF32 dynamic symbol tables and sections.
/// </summary>
public static class ElfMetadataExtractor
{
    public static bool IsElf(byte[] data)
    {
        return data != null &&
               data.Length >= 4 &&
               data[0] == 0x7F &&
               data[1] == (byte)'E' &&
               data[2] == (byte)'L' &&
               data[3] == (byte)'F';
    }

    public static ComponentInterface? Extract(string filePath)
    {
        var all = ExtractAll(filePath);
        return all.Count > 0 ? all[0] : null;
    }

    public static ComponentInterface? Extract(byte[] fileBytes)
    {
        var all = ExtractAll(fileBytes);
        return all.Count > 0 ? all[0] : null;
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(string filePath)
    {
        if (!File.Exists(filePath)) return Array.Empty<ComponentInterface>();
        var fi = new FileInfo(filePath);
        if (fi.Length > int.MaxValue) return Array.Empty<ComponentInterface>();
        byte[] fileBytes = File.ReadAllBytes(filePath);
        return ExtractAll(fileBytes);
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(byte[] fileBytes)
    {
        if (!IsElf(fileBytes) || fileBytes.Length < 52)
        {
            return Array.Empty<ComponentInterface>();
        }

        bool is64 = fileBytes[4] == 2;
        bool isLE = fileBytes[5] == 1;

        var aggregator = new MetadataAggregator();
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);

        var symbols = is64
            ? ExtractSymbolsElf64(fileBytes, isLE)
            : ExtractSymbolsElf32(fileBytes, isLE);

        foreach (var (symName, symData) in symbols)
        {
            aggregator.AllExports.Add(symName);

            if (!symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) ||
                symName.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seenSymbols.Add(symName))
            {
                continue;
            }

            if (symData.Count == 0)
            {
                aggregator.DiscoveredSymbols.Add($"{symName} (empty data)");
                continue;
            }

            try
            {
                var metadataReader = new MetadataReader(symData.Array!, symData.Offset, symData.Count);
                object? item = metadataReader.ReadItem();
                if (item == null)
                {
                    aggregator.DiscoveredSymbols.Add($"{symName} (item is null)");
                    continue;
                }

                aggregator.DiscoveredSymbols.Add($"{symName} (parsed {item.GetType().Name})");

                aggregator.AddItem(item);
            }
            catch (Exception ex)
            {
                aggregator.DiscoveredSymbols.Add($"{symName} (read ex: {ex.Message})");
            }
        }

        return aggregator.BuildAll();
    }

    private static List<(string Name, ArraySegment<byte> Data)> ExtractSymbolsElf64(byte[] data, bool isLE)
    {
        var results = new List<(string, ArraySegment<byte>)>();
        if (data.Length < 64) return results;

        ulong shoff = ReadUInt64(data, 40, isLE);
        ushort shentsize = ReadUInt16(data, 58, isLE);
        ushort shnum = ReadUInt16(data, 60, isLE);

        if (shentsize < 64 || shoff + (ulong)(shnum * shentsize) > (ulong)data.Length)
        {
            return results;
        }

        var sections = new (uint type, ulong addr, ulong offset, ulong size, uint link)[shnum];
        for (int i = 0; i < shnum; i++)
        {
            int sPos = (int)shoff + i * shentsize;
            uint type = ReadUInt32(data, sPos + 4, isLE);
            ulong addr = ReadUInt64(data, sPos + 16, isLE);
            ulong offset = ReadUInt64(data, sPos + 24, isLE);
            ulong size = ReadUInt64(data, sPos + 32, isLE);
            uint link = ReadUInt32(data, sPos + 40, isLE);
            sections[i] = (type, addr, offset, size, link);
        }

        // Search both SHT_DYNSYM (11) and SHT_SYMTAB (2)
        for (int s = 0; s < shnum; s++)
        {
            if (sections[s].type != 11 && sections[s].type != 2) continue;

            var symsec = sections[s];
            int strsecIdx = (int)symsec.link;
            if (strsecIdx >= shnum) continue;
            var strsec = sections[strsecIdx];

            int symEntrySize = 24; // sizeof(Elf64_Sym)
            int numSyms = (int)(symsec.size / (ulong)symEntrySize);

            for (int i = 0; i < numSyms; i++)
            {
                int entryPos = (int)symsec.offset + i * symEntrySize;
                if (entryPos + symEntrySize > data.Length) break;

                uint st_name = ReadUInt32(data, entryPos, isLE);
                ushort st_shndx = ReadUInt16(data, entryPos + 6, isLE);
                ulong st_value = ReadUInt64(data, entryPos + 8, isLE);
                ulong st_size = ReadUInt64(data, entryPos + 16, isLE);

                if (st_name == 0 || (int)strsec.offset + (int)st_name >= data.Length) continue;

                int nullPos = Array.IndexOf<byte>(data, 0, (int)strsec.offset + (int)st_name);
                if (nullPos < 0) continue;
                string symName = Encoding.ASCII.GetString(data, (int)strsec.offset + (int)st_name, nullPos - ((int)strsec.offset + (int)st_name));

                if (symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
                {
                    if (st_shndx < shnum)
                    {
                        var targetSec = sections[st_shndx];
                        int dataPos = (targetSec.addr > 0 && st_value >= targetSec.addr)
                            ? (int)targetSec.offset + (int)(st_value - targetSec.addr)
                            : (int)targetSec.offset + (int)st_value;

                        int dataLen = (int)st_size;
                        if (dataLen <= 0)
                        {
                            dataLen = (int)(targetSec.size - (ulong)(dataPos - (int)targetSec.offset));
                        }

                        if (dataPos >= 0 && dataPos + dataLen <= data.Length && dataLen > 0)
                        {
                            results.Add((symName, new ArraySegment<byte>(data, dataPos, dataLen)));
                        }
                    }
                }
            }
        }

        return results;
    }

    private static List<(string Name, ArraySegment<byte> Data)> ExtractSymbolsElf32(byte[] data, bool isLE)
    {
        var results = new List<(string, ArraySegment<byte>)>();
        if (data.Length < 52) return results;

        uint shoff = ReadUInt32(data, 32, isLE);
        ushort shentsize = ReadUInt16(data, 46, isLE);
        ushort shnum = ReadUInt16(data, 48, isLE);

        if (shentsize < 40 || (int)shoff + shnum * shentsize > data.Length)
        {
            return results;
        }

        var sections = new (uint type, uint addr, uint offset, uint size, uint link)[shnum];
        for (int i = 0; i < shnum; i++)
        {
            int sPos = (int)shoff + i * shentsize;
            uint type = ReadUInt32(data, sPos + 4, isLE);
            uint addr = ReadUInt32(data, sPos + 12, isLE);
            uint offset = ReadUInt32(data, sPos + 16, isLE);
            uint size = ReadUInt32(data, sPos + 20, isLE);
            uint link = ReadUInt32(data, sPos + 24, isLE);
            sections[i] = (type, addr, offset, size, link);
        }

        for (int s = 0; s < shnum; s++)
        {
            if (sections[s].type != 11 && sections[s].type != 2) continue;

            var symsec = sections[s];
            int strsecIdx = (int)symsec.link;
            if (strsecIdx >= shnum) continue;
            var strsec = sections[strsecIdx];

            int symEntrySize = 16; // sizeof(Elf32_Sym)
            int numSyms = (int)(symsec.size / (uint)symEntrySize);

            for (int i = 0; i < numSyms; i++)
            {
                int entryPos = (int)symsec.offset + i * symEntrySize;
                if (entryPos + symEntrySize > data.Length) break;

                uint st_name = ReadUInt32(data, entryPos, isLE);
                uint st_value = ReadUInt32(data, entryPos + 4, isLE);
                uint st_size = ReadUInt32(data, entryPos + 8, isLE);
                ushort st_shndx = ReadUInt16(data, entryPos + 14, isLE);

                if (st_name == 0 || (int)strsec.offset + (int)st_name >= data.Length) continue;

                int nullPos = Array.IndexOf<byte>(data, 0, (int)strsec.offset + (int)st_name);
                if (nullPos < 0) continue;
                string symName = Encoding.ASCII.GetString(data, (int)strsec.offset + (int)st_name, nullPos - ((int)strsec.offset + (int)st_name));

                if (symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
                {
                    if (st_shndx < shnum)
                    {
                        var targetSec = sections[st_shndx];
                        int dataPos = (targetSec.addr > 0 && st_value >= targetSec.addr)
                            ? (int)targetSec.offset + (int)(st_value - targetSec.addr)
                            : (int)targetSec.offset + (int)st_value;

                        int dataLen = (int)st_size;
                        if (dataLen <= 0)
                        {
                            dataLen = (int)(targetSec.size - (uint)(dataPos - (int)targetSec.offset));
                        }

                        if (dataPos >= 0 && dataPos + dataLen <= data.Length && dataLen > 0)
                        {
                            results.Add((symName, new ArraySegment<byte>(data, dataPos, dataLen)));
                        }
                    }
                }
            }
        }

        return results;
    }

    private static ushort ReadUInt16(byte[] data, int offset, bool isLE) =>
        isLE ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2))
             : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] data, int offset, bool isLE) =>
        isLE ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
             : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));

    private static ulong ReadUInt64(byte[] data, int offset, bool isLE) =>
        isLE ? BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset, 8))
             : BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8));
}
