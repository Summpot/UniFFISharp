using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AsmResolver.PE.File;
using UniFFISharp.Generator.Metadata.BoltFfi;

namespace UniFFISharp.Generator.PeParser;

/// <summary>
/// Extracts and parses BoltFFI binding metadata embedded in compiled binaries.
/// Reads .boltffi (PE / ELF / COFF) or __DATA,__boltffi (Mach-O) linker sections,
/// validates the BFFIMD01 frame header, and deserializes JSON envelopes.
/// </summary>
public static class BoltFfiMetadataExtractor
{
    private static readonly byte[] RecordMagic = Encoding.ASCII.GetBytes("BFFIMD01");
    private static readonly ConcurrentDictionary<string, (long Ticks, long Length, IReadOnlyList<BoltFfiMetadataEnvelope> Envelopes)> s_cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Extracts all BoltFFI metadata envelopes from a library or archive file path.
    public static bool IsCached(string filePath, long ticks, long length)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);
            return s_cache.TryGetValue(fullPath, out var cached) && cached.Ticks == ticks && cached.Length == length;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<BoltFfiMetadataEnvelope> ExtractAll(string filePath, byte[]? fileBytes = null)
    {
        if (!File.Exists(filePath)) return Array.Empty<BoltFfiMetadataEnvelope>();

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            var fi = new FileInfo(fullPath);
            long ticks = fi.LastWriteTimeUtc.Ticks;
            long len = fi.Length;

            if (s_cache.TryGetValue(fullPath, out var cached) && cached.Ticks == ticks && cached.Length == len)
            {
                return cached.Envelopes;
            }

            var results = fileBytes != null ? ExtractAll(fileBytes) : ExtractAllInternal(fullPath);
            s_cache[fullPath] = (ticks, len, results);
            return results;
        }
        catch
        {
            return Array.Empty<BoltFfiMetadataEnvelope>();
        }
    }

    private static IReadOnlyList<BoltFfiMetadataEnvelope> ExtractAllInternal(string filePath)
    {
        if (!File.Exists(filePath)) return Array.Empty<BoltFfiMetadataEnvelope>();

        try
        {
            if (filePath.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) ||
                filePath.EndsWith(".a", StringComparison.OrdinalIgnoreCase))
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (ArchiveMetadataExtractor.IsArchive(fs))
                {
                    var rawSections = ExtractSectionsFromArchive(fs);
                    var envelopes = new List<BoltFfiMetadataEnvelope>();
                    foreach (var sec in rawSections)
                    {
                        envelopes.AddRange(DecodeSectionBytes(sec));
                    }
                    return envelopes;
                }
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);
            return ExtractAll(fileBytes);
        }
        catch
        {
            return Array.Empty<BoltFfiMetadataEnvelope>();
        }
    }

    /// <summary>
    /// Extracts all BoltFFI metadata envelopes from memory bytes of a library or archive.
    /// </summary>
    public static IReadOnlyList<BoltFfiMetadataEnvelope> ExtractAll(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 16)
        {
            return Array.Empty<BoltFfiMetadataEnvelope>();
        }

        var results = new List<BoltFfiMetadataEnvelope>();

        // 1. Static library / archive (.lib / .a)
        if (ArchiveMetadataExtractor.IsArchive(fileBytes))
        {
            var rawSections = ExtractSectionsFromArchive(fileBytes);
            foreach (var sec in rawSections)
            {
                results.AddRange(DecodeSectionBytes(sec));
            }
            return results;
        }

        // 2. Direct section bytes passed (starts with BFFIMD01)
        if (StartsWithRecordMagic(fileBytes))
        {
            return DecodeSectionBytes(fileBytes);
        }

        // 3. Single binary (PE, ELF, or Mach-O)
        byte[]? sectionData = ExtractSectionFromAny(fileBytes);
        if (sectionData != null && sectionData.Length >= 16)
        {
            results.AddRange(DecodeSectionBytes(sectionData));
        }

        return results;
    }

    /// <summary>
    /// Decodes all self-delimiting BFFIMD01 records from the raw bytes of a .boltffi section.
    /// </summary>
    public static List<BoltFfiMetadataEnvelope> DecodeSectionBytes(byte[] sectionBytes)
    {
        var envelopes = new List<BoltFfiMetadataEnvelope>();
        if (sectionBytes == null || sectionBytes.Length < 16) return envelopes;

        int offset = 0;
        while (offset + 16 <= sectionBytes.Length)
        {
            // Verify 8-byte magic: BFFIMD01
            bool magicMatch = true;
            for (int i = 0; i < 8; i++)
            {
                if (sectionBytes[offset + i] != RecordMagic[i])
                {
                    magicMatch = false;
                    break;
                }
            }

            if (!magicMatch)
            {
                // Advance 1 byte and scan ahead in case of alignment padding
                offset++;
                continue;
            }

            ulong payloadLen = BinaryPrimitives.ReadUInt64LittleEndian(sectionBytes.AsSpan(offset + 8, 8));
            int payloadStart = offset + 16;
            if (payloadLen == 0 || (ulong)payloadStart + payloadLen > (ulong)sectionBytes.Length)
            {
                break;
            }

            try
            {
                string json = Encoding.UTF8.GetString(sectionBytes, payloadStart, (int)payloadLen);
                var envelope = BoltFfiMetadataEnvelope.Parse(json);
                if (envelope != null && envelope.IsValid)
                {
                    envelopes.Add(envelope);
                }
            }
            catch
            {
                // Ignore parse errors on individual record
            }

            offset = payloadStart + (int)payloadLen;
        }

        return envelopes;
    }

    private static bool StartsWithRecordMagic(byte[] data)
    {
        if (data.Length < 8) return false;
        for (int i = 0; i < 8; i++)
        {
            if (data[i] != RecordMagic[i]) return false;
        }
        return true;
    }

    private static byte[]? ExtractSectionFromAny(byte[] data)
    {
        if (data == null || data.Length < 16) return null;

        // ELF (0x7F 'E' 'L' 'F')
        if (ElfMetadataExtractor.IsElf(data))
        {
            return ExtractSectionFromElf(data);
        }

        // Mach-O
        if (MachOMetadataExtractor.IsMachO(data))
        {
            return ExtractSectionFromMachO(data);
        }

        // PE / COFF
        return ExtractSectionFromPeOrCoff(data);
    }

    private static byte[]? ExtractSectionFromPeOrCoff(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data, false);
            var peFile = PEFile.FromStream(ms);
            foreach (var section in peFile.Sections)
            {
                string name = section.Name?.ToString().TrimEnd('\0') ?? string.Empty;
                if (name.Equals(".boltffi", StringComparison.OrdinalIgnoreCase))
                {
                    if (section.Contents is AsmResolver.IReadableSegment readable)
                    {
                        var reader = readable.CreateReader(readable.Offset, readable.GetPhysicalSize());
                        return reader.ReadBytes((int)reader.Length);
                    }
                }
            }
        }
        catch
        {
            // Fallback: raw COFF object section parsing
            return ExtractSectionFromCoffObject(data);
        }

        return null;
    }

    private static byte[]? ExtractSectionFromCoffObject(byte[] objBytes)
    {
        if (objBytes.Length < 20) return null;

        ushort numSections = BitConverter.ToUInt16(objBytes, 2);
        uint symTableOffset = BitConverter.ToUInt32(objBytes, 8);
        uint numSyms = BitConverter.ToUInt32(objBytes, 12);
        ushort optHeaderSize = BitConverter.ToUInt16(objBytes, 16);

        long secHeadersStartLong = 20L + (long)optHeaderSize;
        if (secHeadersStartLong + (long)numSections * 40L > objBytes.Length) return null;
        int secHeadersStart = (int)secHeadersStartLong;

        long strTableOffsetLong = (long)symTableOffset + (long)numSyms * 18L;

        for (int i = 0; i < numSections; i++)
        {
            int secOffset = secHeadersStart + i * 40;
            string secName = Encoding.ASCII.GetString(objBytes, secOffset, 8).TrimEnd('\0');

            if (secName.StartsWith("/") && int.TryParse(secName.Substring(1), out int strOffset))
            {
                long namePos = strTableOffsetLong + strOffset;
                if (namePos < objBytes.Length)
                {
                    int nullPos = Array.IndexOf(objBytes, (byte)0, (int)namePos);
                    if (nullPos > namePos)
                    {
                        secName = Encoding.ASCII.GetString(objBytes, (int)namePos, nullPos - (int)namePos);
                    }
                }
            }

            if (secName.Equals(".boltffi", StringComparison.OrdinalIgnoreCase))
            {
                uint rawDataSize = BitConverter.ToUInt32(objBytes, secOffset + 16);
                uint rawDataPtr = BitConverter.ToUInt32(objBytes, secOffset + 20);

                if (rawDataPtr + rawDataSize <= (uint)objBytes.Length && rawDataSize > 0)
                {
                    byte[] slice = new byte[rawDataSize];
                    Array.Copy(objBytes, (int)rawDataPtr, slice, 0, (int)rawDataSize);
                    return slice;
                }
            }
        }

        return null;
    }

    private static byte[]? ExtractSectionFromElf(byte[] data)
    {
        if (data.Length < 64) return null;
        bool is64 = data[4] == 2;
        bool isLE = data[5] == 1;

        if (is64)
        {
            ulong shoff = ReadUInt64(data, 40, isLE);
            ushort shentsize = ReadUInt16(data, 58, isLE);
            ushort shnum = ReadUInt16(data, 60, isLE);
            ushort shstrndx = ReadUInt16(data, 62, isLE);

            if (shentsize < 64 || shstrndx >= shnum || shoff + (ulong)(shnum * shentsize) > (ulong)data.Length)
                return null;

            int strSecPos = (int)shoff + shstrndx * shentsize;
            ulong strSecOffset = ReadUInt64(data, strSecPos + 24, isLE);
            ulong strSecSize = ReadUInt64(data, strSecPos + 32, isLE);
            if (strSecOffset + strSecSize > (ulong)data.Length) return null;

            for (int i = 0; i < shnum; i++)
            {
                int sPos = (int)shoff + i * shentsize;
                uint sh_name = ReadUInt32(data, sPos, isLE);
                ulong offset = ReadUInt64(data, sPos + 24, isLE);
                ulong size = ReadUInt64(data, sPos + 32, isLE);

                if ((ulong)sh_name < strSecSize)
                {
                    int strStart = (int)strSecOffset + (int)sh_name;
                    int nullIdx = Array.IndexOf(data, (byte)0, strStart);
                    if (nullIdx > strStart)
                    {
                        string secName = Encoding.ASCII.GetString(data, strStart, nullIdx - strStart);
                        if (secName.Equals(".boltffi", StringComparison.OrdinalIgnoreCase))
                        {
                            if (offset + size <= (ulong)data.Length && size > 0)
                            {
                                byte[] result = new byte[size];
                                Array.Copy(data, (int)offset, result, 0, (int)size);
                                return result;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    private static byte[]? ExtractSectionFromMachO(byte[] data)
    {
        if (data.Length < 32) return null;
        uint magicBE = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4));
        byte[] machOData = data;
        if (magicBE == 0xCAFEBABE || magicBE == 0xBEBAFECA)
        {
            machOData = ExtractFromFatBinary(data);
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(machOData.AsSpan(0, 4));
        bool isLE = (magic == 0xFEEDFACF || magic == 0xFEEDFACE);
        uint ncmds = ReadUInt32(machOData, 16, isLE);
        int cmdOffset = 32;

        for (int i = 0; i < ncmds && cmdOffset + 8 <= machOData.Length; i++)
        {
            uint cmd = ReadUInt32(machOData, cmdOffset, isLE);
            uint cmdsize = ReadUInt32(machOData, cmdOffset + 4, isLE);
            if (cmdsize < 8 || cmdOffset + (int)cmdsize > machOData.Length) break;

            if (cmd == 0x19 /* LC_SEGMENT_64 */ && cmdOffset + 72 <= machOData.Length)
            {
                string segName = Encoding.ASCII.GetString(machOData, cmdOffset + 8, 16).TrimEnd('\0');
                uint nsects = ReadUInt32(machOData, cmdOffset + 64, isLE);
                int secStart = cmdOffset + 72;

                for (int s = 0; s < nsects && secStart + 80 <= machOData.Length; s++)
                {
                    string sectName = Encoding.ASCII.GetString(machOData, secStart, 16).TrimEnd('\0');
                    uint offset = ReadUInt32(machOData, secStart + 48, isLE);
                    ulong size = ReadUInt64(machOData, secStart + 40, isLE);

                    if (sectName.Equals("__boltffi", StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(segName) || segName.Equals("__DATA", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (offset + size <= (ulong)machOData.Length && size > 0)
                        {
                            byte[] result = new byte[size];
                            Array.Copy(machOData, (int)offset, result, 0, (int)size);
                            return result;
                        }
                    }

                    secStart += 80;
                }
            }

            cmdOffset += (int)cmdsize;
        }

        return null;
    }

    private static List<byte[]> ExtractSectionsFromArchive(byte[] archiveBytes)
    {
        using var stream = new MemoryStream(archiveBytes, false);
        return ExtractSectionsFromArchive(stream);
    }

    private static List<byte[]> ExtractSectionsFromArchive(Stream stream)
    {
        var list = new List<byte[]>();
        var memberOffsets = ArchiveMetadataExtractor.GetAllMemberOffsets(stream);
        byte[] headerBuf = new byte[60];

        foreach (long memberOffset in memberOffsets)
        {
            if (memberOffset + 60 > stream.Length) continue;
            stream.Position = memberOffset;
            if (stream.Read(headerBuf, 0, 60) < 60) continue;

            string name = Encoding.ASCII.GetString(headerBuf, 0, 16).Trim();
            string sizeStr = Encoding.ASCII.GetString(headerBuf, 48, 10).Trim();
            if (!int.TryParse(sizeStr, out int memberSize) || memberSize <= 0) continue;

            long objStart = memberOffset + 60;
            if (name.StartsWith("#1/") && int.TryParse(name.Substring(3).Trim(), out int nLen) && nLen > 0 && nLen < memberSize)
            {
                objStart += nLen;
                memberSize -= nLen;
            }

            if (objStart + memberSize > stream.Length) continue;

            byte[] objBytes = new byte[memberSize];
            stream.Position = objStart;
            if (stream.Read(objBytes, 0, memberSize) < memberSize) continue;

            var secData = ExtractSectionFromAny(objBytes);
            if (secData != null && secData.Length >= 16)
            {
                list.Add(secData);
            }
        }

        return list;
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

            if (cpuType == 0x0100000C || (cpuType == 0x01000007 && bestOffset == 0))
            {
                bestOffset = (int)offset;
                bestSize = (int)size;
                if (cpuType == 0x0100000C) break;
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
