using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.PeParser;

/// <summary>
/// Extracts UniFFI metadata from static library archives (.lib on Windows / .a on Unix).
/// Supports both COFF archives (MSVC / Windows) and ELF object members (Unix / Linux).
/// </summary>
public static class ArchiveMetadataExtractor
{
    private static readonly byte[] ArchiveMagic = Encoding.ASCII.GetBytes("!<arch>\n");

    public static bool IsArchive(byte[] data)
    {
        if (data == null || data.Length < 8) return false;
        for (int i = 0; i < 8; i++)
        {
            if (data[i] != ArchiveMagic[i]) return false;
        }
        return true;
    }

    public static ComponentInterface? Extract(byte[] fileBytes)
    {
        if (!IsArchive(fileBytes))
        {
            return null;
        }

        var aggregator = new MetadataAggregator();
        var ci = aggregator.ComponentInterface;
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);

        // Find member offsets containing UNIFFI_META symbols
        var targetMemberOffsets = FindUniffiMemberOffsets(fileBytes);
        if (targetMemberOffsets.Count == 0)
        {
            // Fallback: scan all members in the archive
            targetMemberOffsets = new HashSet<uint>(GetAllMemberOffsets(fileBytes));
        }

        foreach (uint memberOffset in targetMemberOffsets)
        {
            if (memberOffset + 60 > fileBytes.Length) continue;

            string sizeStr = Encoding.ASCII.GetString(fileBytes, (int)memberOffset + 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int memberSize) || memberSize <= 0) continue;

            int objStart = (int)memberOffset + 60;
            if (objStart + memberSize > fileBytes.Length) continue;

            byte[] objBytes = new byte[memberSize];
            Array.Copy(fileBytes, objStart, objBytes, 0, memberSize);

            // Extract symbols from COFF or ELF object file
            var extractedSymbols = ExtractFromObject(objBytes);

            foreach (var (symName, symData) in extractedSymbols)
            {
                ci.AllExports.Add(symName);

                if (!symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (symName.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
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
        }

        aggregator.Build();

        return ci;
    }

    internal static HashSet<uint> FindUniffiMemberOffsets(byte[] archive)
    {
        var result = new HashSet<uint>();
        int pos = 8; // skip !<arch>\n

        while (pos + 60 <= archive.Length)
        {
            string name = Encoding.ASCII.GetString(archive, pos, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(archive, pos + 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int size)) break;

            int bodyPos = pos + 60;
            if (bodyPos + size > archive.Length) break;

            if (name == "/")
            {
                if (pos == 8)
                {
                    // 1st Linker member: big-endian
                    ParseFirstLinkerMember(archive, bodyPos, size, result);
                }
                else
                {
                    // 2nd Linker member: little-endian
                    ParseSecondLinkerMember(archive, bodyPos, size, result);
                    if (result.Count > 0)
                    {
                        return result;
                    }
                }
            }

            pos = bodyPos + size;
            if (pos % 2 != 0) pos++;
        }

        return result;
    }

    private static void ParseFirstLinkerMember(byte[] archive, int bodyPos, int size, HashSet<uint> result)
    {
        if (size < 4) return;
        uint numSymbols = BinaryPrimitives.ReadUInt32BigEndian(archive.AsSpan(bodyPos, 4));
        int offsetsStart = bodyPos + 4;
        int stringsStart = offsetsStart + (int)numSymbols * 4;

        if (stringsStart > bodyPos + size) return;

        var offsets = new uint[numSymbols];
        for (int i = 0; i < numSymbols; i++)
        {
            offsets[i] = BinaryPrimitives.ReadUInt32BigEndian(archive.AsSpan(offsetsStart + i * 4, 4));
        }

        int curStr = stringsStart;
        for (int i = 0; i < numSymbols && curStr < bodyPos + size; i++)
        {
            int nextNull = Array.IndexOf<byte>(archive, 0, curStr);
            if (nextNull < 0 || nextNull > bodyPos + size) break;

            string sym = Encoding.ASCII.GetString(archive, curStr, nextNull - curStr);
            curStr = nextNull + 1;

            if (sym.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) &&
                !sym.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(offsets[i]);
            }
        }
    }

    private static void ParseSecondLinkerMember(byte[] archive, int bodyPos, int size, HashSet<uint> result)
    {
        if (size < 4) return;
        uint numMembers = BitConverter.ToUInt32(archive, bodyPos);
        int memberOffsetsStart = bodyPos + 4;
        int numSymbolsPos = memberOffsetsStart + (int)numMembers * 4;

        if (numSymbolsPos + 4 > bodyPos + size) return;

        var memberOffsets = new uint[numMembers];
        for (int i = 0; i < numMembers; i++)
        {
            memberOffsets[i] = BitConverter.ToUInt32(archive, memberOffsetsStart + i * 4);
        }

        uint numSymbols = BitConverter.ToUInt32(archive, numSymbolsPos);
        int indicesStart = numSymbolsPos + 4;
        int stringsStart = indicesStart + (int)numSymbols * 2;

        if (stringsStart > bodyPos + size) return;

        var symbolIndices = new ushort[numSymbols];
        for (int i = 0; i < numSymbols; i++)
        {
            symbolIndices[i] = BitConverter.ToUInt16(archive, indicesStart + i * 2);
        }

        int curStr = stringsStart;
        for (int i = 0; i < numSymbols && curStr < bodyPos + size; i++)
        {
            int nextNull = Array.IndexOf<byte>(archive, 0, curStr);
            if (nextNull < 0 || nextNull > bodyPos + size) break;

            string sym = Encoding.ASCII.GetString(archive, curStr, nextNull - curStr);
            curStr = nextNull + 1;

            if (sym.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) &&
                !sym.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
            {
                int memberIdx = symbolIndices[i] - 1;
                if (memberIdx >= 0 && memberIdx < memberOffsets.Length)
                {
                    result.Add(memberOffsets[memberIdx]);
                }
            }
        }
    }

    internal static List<uint> GetAllMemberOffsets(byte[] archive)
    {
        var offsets = new List<uint>();
        int pos = 8;
        while (pos + 60 <= archive.Length)
        {
            string name = Encoding.ASCII.GetString(archive, pos, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(archive, pos + 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int size)) break;

            if (name != "/" && name != "//")
            {
                offsets.Add((uint)pos);
            }

            pos += 60 + size;
            if (pos % 2 != 0) pos++;
        }
        return offsets;
    }

    internal static List<(string Name, byte[] Data)> ExtractFromObject(byte[] objBytes)
    {
        var results = new List<(string, byte[])>();
        if (objBytes.Length < 20) return results;

        // Check if ELF object file (0x7F 'E' 'L' 'F')
        if (objBytes[0] == 0x7F && objBytes[1] == (byte)'E' && objBytes[2] == (byte)'L' && objBytes[3] == (byte)'F')
        {
            ExtractFromElf(objBytes, results);
            return results;
        }

        // COFF object file
        ExtractFromCoff(objBytes, results);
        return results;
    }

    private static void ExtractFromCoff(byte[] objBytes, List<(string, byte[])> results)
    {
        if (objBytes.Length < 20) return;

        ushort numSections = BitConverter.ToUInt16(objBytes, 2);
        uint symTableOffset = BitConverter.ToUInt32(objBytes, 8);
        uint numSyms = BitConverter.ToUInt32(objBytes, 12);
        ushort optHeaderSize = BitConverter.ToUInt16(objBytes, 16);

        int secHeadersStart = 20 + optHeaderSize;
        if (secHeadersStart + numSections * 40 > objBytes.Length) return;

        var secHeaders = new (string name, uint rawDataPtr, uint rawDataSize)[numSections];
        for (int i = 0; i < numSections; i++)
        {
            int secOffset = secHeadersStart + i * 40;
            string secName = Encoding.ASCII.GetString(objBytes, secOffset, 8).TrimEnd('\0');
            uint rawDataSize = BitConverter.ToUInt32(objBytes, secOffset + 16);
            uint rawDataPtr = BitConverter.ToUInt32(objBytes, secOffset + 20);
            secHeaders[i] = (secName, rawDataPtr, rawDataSize);
        }

        if (symTableOffset + numSyms * 18 > objBytes.Length) return;

        int strTableOffset = (int)symTableOffset + (int)numSyms * 18;

        for (int i = 0; i < numSyms; i++)
        {
            int entryOffset = (int)symTableOffset + i * 18;
            uint nameZero = BitConverter.ToUInt32(objBytes, entryOffset);
            string symName;
            if (nameZero == 0)
            {
                uint strOffset = BitConverter.ToUInt32(objBytes, entryOffset + 4);
                if (strTableOffset + (int)strOffset < objBytes.Length)
                {
                    int nullPos = Array.IndexOf<byte>(objBytes, 0, strTableOffset + (int)strOffset);
                    if (nullPos < 0) nullPos = objBytes.Length;
                    symName = Encoding.ASCII.GetString(objBytes, strTableOffset + (int)strOffset, nullPos - (strTableOffset + (int)strOffset));
                }
                else
                {
                    symName = string.Empty;
                }
            }
            else
            {
                symName = Encoding.ASCII.GetString(objBytes, entryOffset, 8).TrimEnd('\0');
            }

            uint symValue = BitConverter.ToUInt32(objBytes, entryOffset + 8);
            short secNum = BitConverter.ToInt16(objBytes, entryOffset + 12);
            byte numAux = objBytes[entryOffset + 17];
            i += numAux; // skip aux entries

            if (symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) &&
                !symName.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
            {
                if (secNum > 0 && secNum <= secHeaders.Length)
                {
                    var sec = secHeaders[secNum - 1];
                    if (sec.rawDataPtr + symValue < objBytes.Length)
                    {
                        int available = (int)Math.Min(sec.rawDataSize - symValue, objBytes.Length - (sec.rawDataPtr + symValue));
                        if (available > 0)
                        {
                            byte[] symData = new byte[available];
                            Array.Copy(objBytes, (int)sec.rawDataPtr + (int)symValue, symData, 0, available);
                            results.Add((symName, symData));
                        }
                    }
                }
            }
        }
    }

    private static void ExtractFromElf(byte[] objBytes, List<(string, byte[])> results)
    {
        if (objBytes.Length < 64) return;
        bool is64 = objBytes[4] == 2; // ELFCLASS64
        if (!is64) return;

        // ELF64 Header
        ulong shoff = BitConverter.ToUInt64(objBytes, 40);
        ushort shentsize = BitConverter.ToUInt16(objBytes, 58);
        ushort shnum = BitConverter.ToUInt16(objBytes, 60);
        ushort shstrndx = BitConverter.ToUInt16(objBytes, 62);

        if (shoff + (ulong)(shnum * shentsize) > (ulong)objBytes.Length) return;

        // Read sections
        var sections = new (uint type, ulong offset, ulong size, uint link)[shnum];
        for (int i = 0; i < shnum; i++)
        {
            int sPos = (int)shoff + i * shentsize;
            uint type = BitConverter.ToUInt32(objBytes, sPos + 4);
            ulong offset = BitConverter.ToUInt64(objBytes, sPos + 24);
            ulong size = BitConverter.ToUInt64(objBytes, sPos + 32);
            uint link = BitConverter.ToUInt32(objBytes, sPos + 40);
            sections[i] = (type, offset, size, link);
        }

        // Find symbol table (.symtab has type SHT_SYMTAB = 2)
        for (int s = 0; s < shnum; s++)
        {
            if (sections[s].type != 2) continue;

            var symsec = sections[s];
            int strsecIdx = (int)symsec.link;
            if (strsecIdx >= shnum) continue;
            var strsec = sections[strsecIdx];

            int symEntrySize = 24; // sizeof(Elf64_Sym)
            int numSyms = (int)(symsec.size / (ulong)symEntrySize);

            for (int i = 0; i < numSyms; i++)
            {
                int entryPos = (int)symsec.offset + i * symEntrySize;
                uint st_name = BitConverter.ToUInt32(objBytes, entryPos);
                ushort st_shndx = BitConverter.ToUInt16(objBytes, entryPos + 6);
                ulong st_value = BitConverter.ToUInt64(objBytes, entryPos + 8);
                ulong st_size = BitConverter.ToUInt64(objBytes, entryPos + 16);

                if (st_name == 0 || (int)strsec.offset + (int)st_name >= objBytes.Length) continue;

                int nullPos = Array.IndexOf<byte>(objBytes, 0, (int)strsec.offset + (int)st_name);
                if (nullPos < 0) continue;
                string symName = Encoding.ASCII.GetString(objBytes, (int)strsec.offset + (int)st_name, nullPos - ((int)strsec.offset + (int)st_name));

                if (symName.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
                {
                    if (st_shndx < shnum)
                    {
                        var targetSec = sections[st_shndx];
                        int dataPos = (int)targetSec.offset + (int)st_value;
                        int dataLen = (int)st_size;
                        if (dataLen == 0) dataLen = (int)(targetSec.size - st_value);

                        if (dataPos + dataLen <= objBytes.Length && dataLen > 0)
                        {
                            byte[] symData = new byte[dataLen];
                            Array.Copy(objBytes, dataPos, symData, 0, dataLen);
                            results.Add((symName, symData));
                        }
                    }
                }
            }
        }
    }
}
