using System;
using System.Collections.Generic;
using System.IO;
using AsmResolver.PE;
using AsmResolver.PE.Exports;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.PeParser;

public static class PeMetadataExtractor
{
    public static ComponentInterface? Extract(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            return null;
        }

        if (dllPath.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) ||
            dllPath.EndsWith(".a", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveMetadataExtractor.Extract(dllPath);
        }

        // If a corresponding .lib or .a static archive exists alongside the dynamic library,
        // extract metadata from the archive to ensure reliable parsing without PE export reflection issues.
        string libCandidate = Path.ChangeExtension(dllPath, ".lib");
        if (File.Exists(libCandidate))
        {
            try
            {
                var archiveCi = ArchiveMetadataExtractor.Extract(libCandidate);
                if (archiveCi != null && (archiveCi.Functions.Count > 0 || archiveCi.Records.Count > 0 || archiveCi.Objects.Count > 0))
                {
                    return archiveCi;
                }
            }
            catch
            {
                // Fallback to PE extraction
            }
        }

        string aCandidate = Path.ChangeExtension(dllPath, ".a");
        if (File.Exists(aCandidate))
        {
            try
            {
                var archiveCi = ArchiveMetadataExtractor.Extract(aCandidate);
                if (archiveCi != null && (archiveCi.Functions.Count > 0 || archiveCi.Records.Count > 0 || archiveCi.Objects.Count > 0))
                {
                    return archiveCi;
                }
            }
            catch
            {
                // Fallback to PE extraction
            }
        }

        // Read minimal header to determine format
        byte[] header = new byte[64];
        int headerRead = 0;
        using (var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            headerRead = fs.Read(header, 0, header.Length);
        }

        if (ArchiveMetadataExtractor.IsArchive(header))
        {
            return ArchiveMetadataExtractor.Extract(dllPath);
        }

        if (ElfMetadataExtractor.IsElf(header))
        {
            return ElfMetadataExtractor.Extract(dllPath);
        }

        if (MachOMetadataExtractor.IsMachO(header))
        {
            return MachOMetadataExtractor.Extract(dllPath);
        }

        // PE format (.dll, .exe) parsed via AsmResolver from file
        try
        {
            var peImage = PEImage.FromFile(dllPath);
            return ExtractFromPeImage(peImage);
        }
        catch
        {
            // If File-based PE read fails or file is not standard PE, fallback to Extract(byte[]) if file size is small (< 100MB)
            var fileInfo = new FileInfo(dllPath);
            if (fileInfo.Length < 100 * 1024 * 1024)
            {
                try
                {
                    byte[] fileBytes = File.ReadAllBytes(dllPath);
                    return Extract(fileBytes);
                }
                catch { }
            }
            return null;
        }
    }

    public static ComponentInterface? Extract(byte[] fileBytes)
    {
        if (ArchiveMetadataExtractor.IsArchive(fileBytes))
        {
            return ArchiveMetadataExtractor.Extract(fileBytes);
        }

        if (ElfMetadataExtractor.IsElf(fileBytes))
        {
            return ElfMetadataExtractor.Extract(fileBytes);
        }

        if (MachOMetadataExtractor.IsMachO(fileBytes))
        {
            return MachOMetadataExtractor.Extract(fileBytes);
        }

        // PE format (.dll, .exe) parsed via AsmResolver
        PEImage peImage;
        try
        {
            peImage = PEImage.FromBytes(fileBytes);
        }
        catch
        {
            return null;
        }

        return ExtractFromPeImage(peImage);
    }

    private static ComponentInterface? ExtractFromPeImage(PEImage peImage)
    {
        if (peImage.Exports == null || peImage.Exports.Entries.Count == 0)
        {
            return null;
        }

        var aggregator = new MetadataAggregator();
        var ci = aggregator.ComponentInterface;
        var seenSymbols = new HashSet<string>(StringComparer.Ordinal);

        foreach (var export in peImage.Exports.Entries)
        {
            string? rawName = export.Name;
            if (string.IsNullOrEmpty(rawName)) continue;
            string name = rawName!;

            // Handle Darwin/macOS style leading underscore if present
            if (name.StartsWith("_"))
            {
                name = name.Substring(1);
            }

            ci.AllExports.Add(name);

            if (!name.StartsWith("UNIFFI_META", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seenSymbols.Add(name))
            {
                continue;
            }

            try
            {
                if (export.Address == null || !export.Address.CanRead)
                {
                    ci.DiscoveredSymbols.Add($"{name} (cannot read address)");
                    continue;
                }

                var reader = export.Address.CreateReader();
                byte[] data = reader.ReadToEnd();

                if (data.Length == 0)
                {
                    ci.DiscoveredSymbols.Add($"{name} (len=0)");
                    continue;
                }

                var metadataReader = new MetadataReader(data);
                object? item = metadataReader.ReadItem();
                if (item == null)
                {
                    ci.DiscoveredSymbols.Add($"{name} (item is null, code={data[0]})");
                    continue;
                }
                ci.DiscoveredSymbols.Add($"{name} (parsed {item.GetType().Name})");

                aggregator.AddItem(item);
            }
            catch (Exception ex)
            {
                ci.DiscoveredSymbols.Add($"{name} (read/parse ex: {ex.Message})");
            }
        }

        aggregator.Build();

        if (string.IsNullOrEmpty(ci.CrateName) && (ci.Functions.Count > 0 || ci.Records.Count > 0 || ci.Objects.Count > 0))
        {
            // Derive crate name from functions or objects module path
            if (ci.Functions.Count > 0)
            {
                ci.CrateName = ci.Functions[0].ModulePath;
            }
            else if (ci.Records.Count > 0)
            {
                ci.CrateName = ci.Records[0].ModulePath;
            }
            else if (ci.Objects.Count > 0)
            {
                ci.CrateName = ci.Objects[0].ModulePath;
            }
            ci.Namespace = ci.CrateName;
        }

        return ci;
    }
}
