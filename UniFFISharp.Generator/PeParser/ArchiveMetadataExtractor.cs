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

    /// <summary>
    /// Maximum allowed symbols or members in an archive linker member to prevent excessive memory allocation.
    /// Default is 1,000,000.
    /// </summary>
    public static int MaxArchiveSymbols { get; set; } = 1_000_000;

    public static bool IsArchive(byte[] data)
    {
        if (data == null || data.Length < 8) return false;
        for (int i = 0; i < 8; i++)
        {
            if (data[i] != ArchiveMagic[i]) return false;
        }
        return true;
    }

    public static bool IsArchive(Stream stream)
    {
        if (stream == null || stream.Length < 8) return false;
        long origPos = stream.Position;
        byte[] magic = new byte[8];
        int read = ReadFully(stream, magic, 0, 8);
        stream.Position = origPos;
        if (read < 8) return false;
        return IsArchive(magic);
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

    public static ComponentInterface? Extract(Stream stream)
    {
        var all = ExtractAll(stream);
        return all.Count > 0 ? all[0] : null;
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(string filePath)
    {
        if (!File.Exists(filePath)) return Array.Empty<ComponentInterface>();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return ExtractAll(stream);
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 8) return Array.Empty<ComponentInterface>();
        using var ms = new MemoryStream(fileBytes, false);
        return ExtractAll(ms);
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(Stream stream)
    {
        if (!IsArchive(stream))
        {
            return Array.Empty<ComponentInterface>();
        }

        var aggregator = new MetadataAggregator();
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);
        byte[] headerBuf = new byte[60];

        // Find member offsets containing UNIFFI_META symbols
        HashSet<long> targetMemberOffsets;
        try
        {
            targetMemberOffsets = FindUniffiMemberOffsets(stream);
            if (targetMemberOffsets.Count == 0)
            {
                // Fallback: scan all members in the archive
                targetMemberOffsets = new HashSet<long>(GetAllMemberOffsets(stream));
            }
        }
        catch
        {
            targetMemberOffsets = new HashSet<long>();
        }

        foreach (long memberOffset in targetMemberOffsets)
        {
            if (memberOffset + 60 > stream.Length) continue;

            stream.Position = memberOffset;
            if (ReadFully(stream, headerBuf, 0, 60) < 60) continue;

            string name = Encoding.ASCII.GetString(headerBuf, 0, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(headerBuf, 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int memberSize) || memberSize <= 0) continue;

            long objStart = memberOffset + 60;
            int bsdNameLen = 0;
            if (name.StartsWith("#1/") && int.TryParse(name.Substring(3).Trim(), out int nLen) && nLen > 0 && nLen < memberSize)
            {
                bsdNameLen = nLen;
                objStart += bsdNameLen;
                memberSize -= bsdNameLen;
            }

            if (objStart + memberSize > stream.Length) continue;

            byte[] objBytes = new byte[memberSize];
            stream.Position = objStart;
            if (ReadFully(stream, objBytes, 0, memberSize) < memberSize) continue;

            // Extract symbols from COFF, ELF, or Mach-O object file
            List<(string, byte[])> extractedSymbols;
            try
            {
                extractedSymbols = ExtractFromObject(objBytes);
            }
            catch
            {
                continue;
            }

            foreach (var (symName, symData) in extractedSymbols)
            {
                aggregator.AllExports.Add(symName);

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
        }

        return aggregator.BuildAll();
    }

    internal static HashSet<long> FindUniffiMemberOffsets(Stream stream)
    {
        var result = new HashSet<long>();
        long pos = 8; // skip !<arch>\n
        byte[] headerBuf = new byte[60];

        while (pos + 60 <= stream.Length)
        {
            stream.Position = pos;
            if (ReadFully(stream, headerBuf, 0, 60) < 60) break;

            string name = Encoding.ASCII.GetString(headerBuf, 0, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(headerBuf, 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int size)) break;

            long bodyPos = pos + 60;
            if (bodyPos + size > stream.Length) break;

            if (name == "/")
            {
                byte[] linkerData = new byte[size];
                stream.Position = bodyPos;
                if (ReadFully(stream, linkerData, 0, size) == size)
                {
                    if (pos == 8)
                    {
                        // 1st Linker member: big-endian
                        ParseFirstLinkerMember(linkerData, 0, size, result);
                    }
                    else
                    {
                        // 2nd Linker member: little-endian
                        ParseSecondLinkerMember(linkerData, 0, size, result);
                        if (result.Count > 0)
                        {
                            return result;
                        }
                    }
                }
            }
            else if (name.StartsWith("__.SYMDEF") || (name.StartsWith("#1/") && pos == 8))
            {
                byte[] linkerData = new byte[size];
                stream.Position = bodyPos;
                if (ReadFully(stream, linkerData, 0, size) == size)
                {
                    int bsdNameLen = 0;
                    if (name.StartsWith("#1/") && int.TryParse(name.Substring(3).Trim(), out int nLen) && nLen > 0 && nLen < size)
                    {
                        bsdNameLen = nLen;
                    }
                    ParseBsdLinkerMember(linkerData, bsdNameLen, size - bsdNameLen, result);
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

    internal static HashSet<uint> FindUniffiMemberOffsets(byte[] archive)
    {
        using var ms = new MemoryStream(archive, false);
        var longOffsets = FindUniffiMemberOffsets(ms);
        var res = new HashSet<uint>();
        foreach (var o in longOffsets)
        {
            res.Add((uint)o);
        }
        return res;
    }

    private static void ParseFirstLinkerMember(byte[] archive, int bodyPos, int size, HashSet<long> result)
    {
        if (size < 4) return;
        uint numSymbols = BinaryPrimitives.ReadUInt32BigEndian(archive.AsSpan(bodyPos, 4));
        int offsetsStart = bodyPos + 4;
        long requiredOffsetBytes = (long)numSymbols * 4L;
        if (requiredOffsetBytes > size - 4 || numSymbols > (uint)MaxArchiveSymbols) return;

        int stringsStart = (int)(offsetsStart + requiredOffsetBytes);
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

    private static void ParseSecondLinkerMember(byte[] archive, int bodyPos, int size, HashSet<long> result)
    {
        if (size < 4) return;
        uint numMembers = BitConverter.ToUInt32(archive, bodyPos);
        long memberOffsetsBytes = (long)numMembers * 4L;
        if (memberOffsetsBytes > size - 4 || numMembers > (uint)MaxArchiveSymbols) return;

        int memberOffsetsStart = bodyPos + 4;
        long numSymbolsPosLong = (long)memberOffsetsStart + memberOffsetsBytes;
        if (numSymbolsPosLong + 4 > bodyPos + size) return;
        int numSymbolsPos = (int)numSymbolsPosLong;

        var memberOffsets = new uint[numMembers];
        for (int i = 0; i < numMembers; i++)
        {
            memberOffsets[i] = BitConverter.ToUInt32(archive, memberOffsetsStart + i * 4);
        }

        uint numSymbols = BitConverter.ToUInt32(archive, numSymbolsPos);
        long indicesBytes = (long)numSymbols * 2L;
        if (indicesBytes > (bodyPos + size) - (numSymbolsPos + 4) || numSymbols > (uint)MaxArchiveSymbols) return;

        int indicesStart = numSymbolsPos + 4;
        long stringsStartLong = (long)indicesStart + indicesBytes;
        if (stringsStartLong > bodyPos + size) return;
        int stringsStart = (int)stringsStartLong;

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

    private static void ParseBsdLinkerMember(byte[] archive, int bodyPos, int size, HashSet<long> result)
    {
        if (size < 4) return;
        uint ranlibBytes = BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(bodyPos, 4));
        if (ranlibBytes > (uint)Math.Max(0, size - 4)) return;
        int ranlibCount = (int)(ranlibBytes / 8);
        if (ranlibCount > MaxArchiveSymbols) return;
        int offsetsStart = bodyPos + 4;
        long strSizePosLong = (long)offsetsStart + (long)ranlibCount * 8L;

        if (strSizePosLong + 4 <= bodyPos + size)
        {
            int strSizePos = (int)strSizePosLong;
            uint strBytes = BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(strSizePos, 4));
            int stringsStart = strSizePos + 4;

            if ((long)stringsStart + (long)strBytes <= bodyPos + size)
            {
                for (int i = 0; i < ranlibCount; i++)
                {
                    int entryPos = offsetsStart + i * 8;
                    uint strx = BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(entryPos, 4));
                    uint off = BinaryPrimitives.ReadUInt32LittleEndian(archive.AsSpan(entryPos + 4, 4));

                    long curStrLong = (long)stringsStart + (long)strx;
                    if (curStrLong >= bodyPos + size) continue;
                    int curStr = (int)curStrLong;

                    int nextNull = Array.IndexOf<byte>(archive, 0, curStr);
                    if (nextNull < 0 || nextNull > bodyPos + size) nextNull = bodyPos + size;

                    string rawName = Encoding.ASCII.GetString(archive, curStr, nextNull - curStr);
                    string sym = rawName.StartsWith("_") ? rawName.Substring(1) : rawName;

                    if (sym.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) &&
                        !sym.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(off);
                    }
                }
                return;
            }
        }

        // Try 64-bit BSD ranlib if 32-bit was invalid
        if (size >= 16)
        {
            ulong ranlibBytes64 = BinaryPrimitives.ReadUInt64LittleEndian(archive.AsSpan(bodyPos, 8));
            if (ranlibBytes64 > (ulong)Math.Max(0, size - 8)) return;
            ulong ranlibCount64 = ranlibBytes64 / 16;
            if (ranlibCount64 > (ulong)MaxArchiveSymbols) return;
            int offsetsStart64 = bodyPos + 8;
            long strSizePos64Long = (long)offsetsStart64 + (long)ranlibCount64 * 16L;

            if (strSizePos64Long + 8 <= bodyPos + size)
            {
                int strSizePos64 = (int)strSizePos64Long;
                ulong strBytes64 = BinaryPrimitives.ReadUInt64LittleEndian(archive.AsSpan(strSizePos64, 8));
                int stringsStart64 = strSizePos64 + 8;

                if ((long)stringsStart64 + (long)strBytes64 <= bodyPos + size)
                {
                    for (int i = 0; i < (int)ranlibCount64; i++)
                    {
                        int entryPos = offsetsStart64 + i * 16;
                        ulong strx = BinaryPrimitives.ReadUInt64LittleEndian(archive.AsSpan(entryPos, 8));
                        ulong off = BinaryPrimitives.ReadUInt64LittleEndian(archive.AsSpan(entryPos + 8, 8));

                        long curStrLong = (long)stringsStart64 + (long)strx;
                        if (curStrLong >= bodyPos + size) continue;
                        int curStr = (int)curStrLong;

                        int nextNull = Array.IndexOf<byte>(archive, 0, curStr);
                        if (nextNull < 0 || nextNull > bodyPos + size) nextNull = bodyPos + size;

                        string rawName = Encoding.ASCII.GetString(archive, curStr, nextNull - curStr);
                        string sym = rawName.StartsWith("_") ? rawName.Substring(1) : rawName;

                        if (sym.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase) &&
                            !sym.StartsWith("__imp_", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add((long)off);
                        }
                    }
                }
            }
        }
    }

    internal static List<long> GetAllMemberOffsets(Stream stream)
    {
        var offsets = new List<long>();
        long pos = 8;
        byte[] headerBuf = new byte[60];
        while (pos + 60 <= stream.Length)
        {
            stream.Position = pos;
            if (ReadFully(stream, headerBuf, 0, 60) < 60) break;

            string name = Encoding.ASCII.GetString(headerBuf, 0, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(headerBuf, 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int size)) break;

            if (name != "/" && name != "//" && !name.StartsWith("__.SYMDEF"))
            {
                offsets.Add(pos);
            }

            pos += 60 + size;
            if (pos % 2 != 0) pos++;
        }
        return offsets;
    }

    internal static List<uint> GetAllMemberOffsets(byte[] archive)
    {
        using var ms = new MemoryStream(archive, false);
        var longOffsets = GetAllMemberOffsets(ms);
        var res = new List<uint>();
        foreach (var o in longOffsets)
        {
            res.Add((uint)o);
        }
        return res;
    }

    private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = stream.Read(buffer, offset + totalRead, count - totalRead);
            if (bytesRead <= 0) break;
            totalRead += bytesRead;
        }
        return totalRead;
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

        // Check if Mach-O object file
        if (MachOMetadataExtractor.IsMachO(objBytes))
        {
            ExtractFromMachO(objBytes, results);
            return results;
        }

        // COFF object file
        ExtractFromCoff(objBytes, results);
        return results;
    }

    private static void ExtractFromMachO(byte[] objBytes, List<(string, byte[])> results)
    {
        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(objBytes.AsSpan(0, 4));
        bool isLE = (magic == 0xFEEDFACF || magic == 0xFEEDFACE);
        results.AddRange(MachOMetadataExtractor.ExtractSymbolsMachO64(objBytes, isLE));
    }

    private static void ExtractFromCoff(byte[] objBytes, List<(string, byte[])> results)
    {
        if (objBytes.Length < 20) return;

        ushort numSections = BitConverter.ToUInt16(objBytes, 2);
        uint symTableOffset = BitConverter.ToUInt32(objBytes, 8);
        uint numSyms = BitConverter.ToUInt32(objBytes, 12);
        ushort optHeaderSize = BitConverter.ToUInt16(objBytes, 16);

        long secHeadersStartLong = 20L + (long)optHeaderSize;
        if (secHeadersStartLong + (long)numSections * 40L > objBytes.Length) return;
        int secHeadersStart = (int)secHeadersStartLong;

        var secHeaders = new (string name, uint rawDataPtr, uint rawDataSize)[numSections];
        for (int i = 0; i < numSections; i++)
        {
            int secOffset = secHeadersStart + i * 40;
            string secName = Encoding.ASCII.GetString(objBytes, secOffset, 8).TrimEnd('\0');
            uint rawDataSize = BitConverter.ToUInt32(objBytes, secOffset + 16);
            uint rawDataPtr = BitConverter.ToUInt32(objBytes, secOffset + 20);
            secHeaders[i] = (secName, rawDataPtr, rawDataSize);
        }

        long symTableEnd = (long)symTableOffset + (long)numSyms * 18L;
        if (symTableEnd > objBytes.Length || numSyms > (uint)MaxArchiveSymbols) return;

        long strTableOffsetLong = symTableEnd;

        for (int i = 0; i < numSyms; i++)
        {
            long entryOffsetLong = (long)symTableOffset + (long)i * 18L;
            if (entryOffsetLong + 18 > objBytes.Length) break;
            int entryOffset = (int)entryOffsetLong;

            uint nameZero = BitConverter.ToUInt32(objBytes, entryOffset);
            string symName;
            if (nameZero == 0)
            {
                uint strOffset = BitConverter.ToUInt32(objBytes, entryOffset + 4);
                long fullStrOffset = strTableOffsetLong + (long)strOffset;
                if (fullStrOffset >= 0 && fullStrOffset < objBytes.Length)
                {
                    int nullPos = Array.IndexOf<byte>(objBytes, 0, (int)fullStrOffset);
                    if (nullPos < 0) nullPos = objBytes.Length;
                    symName = Encoding.ASCII.GetString(objBytes, (int)fullStrOffset, nullPos - (int)fullStrOffset);
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
                    long dataStart = (long)sec.rawDataPtr + (long)symValue;
                    if (dataStart < objBytes.Length && symValue <= sec.rawDataSize)
                    {
                        long availLong = Math.Min((long)sec.rawDataSize - (long)symValue, (long)objBytes.Length - dataStart);
                        if (availLong > 0)
                        {
                            int available = (int)Math.Min(availLong, 10 * 1024 * 1024);
                            byte[] symData = new byte[available];
                            Array.Copy(objBytes, (int)dataStart, symData, 0, available);
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
