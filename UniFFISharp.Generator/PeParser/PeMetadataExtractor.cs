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
        var all = ExtractAll(dllPath);
        return all.Count > 0 ? all[0] : null;
    }

    public static ComponentInterface? Extract(byte[] fileBytes)
    {
        var all = ExtractAll(fileBytes);
        return all.Count > 0 ? all[0] : null;
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(string dllPath)
    {
        if (!File.Exists(dllPath))
        {
            return Array.Empty<ComponentInterface>();
        }

        if (dllPath.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) ||
            dllPath.EndsWith(".a", StringComparison.OrdinalIgnoreCase))
        {
            return ArchiveMetadataExtractor.ExtractAll(dllPath);
        }

        // If a corresponding .lib or .a static archive exists alongside the dynamic library,
        // extract metadata from the archive to ensure reliable parsing without PE export reflection issues.
        string libCandidate = Path.ChangeExtension(dllPath, ".lib");
        if (File.Exists(libCandidate))
        {
            try
            {
                var archiveCis = ArchiveMetadataExtractor.ExtractAll(libCandidate);
                if (archiveCis.Count > 0)
                {
                    return archiveCis;
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
                var archiveCis = ArchiveMetadataExtractor.ExtractAll(aCandidate);
                if (archiveCis.Count > 0)
                {
                    return archiveCis;
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
            return ArchiveMetadataExtractor.ExtractAll(dllPath);
        }

        if (ElfMetadataExtractor.IsElf(header))
        {
            return ElfMetadataExtractor.ExtractAll(dllPath);
        }

        if (MachOMetadataExtractor.IsMachO(header))
        {
            return MachOMetadataExtractor.ExtractAll(dllPath);
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
                    return ExtractAll(fileBytes);
                }
                catch { }
            }
            return Array.Empty<ComponentInterface>();
        }
    }

    public static IReadOnlyList<ComponentInterface> ExtractAll(byte[] fileBytes)
    {
        if (ArchiveMetadataExtractor.IsArchive(fileBytes))
        {
            return ArchiveMetadataExtractor.ExtractAll(fileBytes);
        }

        if (ElfMetadataExtractor.IsElf(fileBytes))
        {
            return ElfMetadataExtractor.ExtractAll(fileBytes);
        }

        if (MachOMetadataExtractor.IsMachO(fileBytes))
        {
            return MachOMetadataExtractor.ExtractAll(fileBytes);
        }

        // PE format (.dll, .exe) parsed via AsmResolver
        PEImage peImage;
        try
        {
            peImage = PEImage.FromBytes(fileBytes);
        }
        catch
        {
            return Array.Empty<ComponentInterface>();
        }

        return ExtractFromPeImage(peImage);
    }

    private static IReadOnlyList<ComponentInterface> ExtractFromPeImage(PEImage peImage)
    {
        if (peImage.Exports == null || peImage.Exports.Entries.Count == 0)
        {
            return Array.Empty<ComponentInterface>();
        }

        var aggregator = new MetadataAggregator();
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

            aggregator.AllExports.Add(name);

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
                    aggregator.DiscoveredSymbols.Add($"{name} (cannot read address)");
                    continue;
                }

                var reader = export.Address.CreateReader();
                byte[] data = reader.ReadToEnd();

                if (data.Length == 0)
                {
                    aggregator.DiscoveredSymbols.Add($"{name} (len=0)");
                    continue;
                }

                var metadataReader = new MetadataReader(data);
                object? item = metadataReader.ReadItem();
                if (item == null)
                {
                    aggregator.DiscoveredSymbols.Add($"{name} (item is null, code={data[0]})");
                    continue;
                }
                aggregator.DiscoveredSymbols.Add($"{name} (parsed {item.GetType().Name})");

                aggregator.AddItem(item);
            }
            catch (Exception ex)
            {
                aggregator.DiscoveredSymbols.Add($"{name} (read/parse ex: {ex.Message})");
            }
        }

        return aggregator.BuildAll();
    }
}
