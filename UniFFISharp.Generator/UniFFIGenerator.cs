using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.PeParser;

[assembly: InternalsVisibleTo("UniFFISharp.Tests")]

namespace UniFFISharp.Generator;

[Generator]
public class UniFFIGenerator : IIncrementalGenerator
{
    static UniFFIGenerator()
    {
        try
        {
            var assembly = typeof(UniFFIGenerator).Assembly;
            var alcType = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader")
                       ?? Type.GetType("System.Runtime.Loader.AssemblyLoadContext");
            if (alcType != null)
            {
                var getLoadContext = alcType.GetMethod("GetLoadContext", new[] { typeof(Assembly) });
                var alc = getLoadContext?.Invoke(null, new object[] { assembly });
                if (alc != null)
                {
                    var loadFromStreamMethod = alcType.GetMethod("LoadFromStream", new[] { typeof(Stream) });
                    var preloadOrder = new[] { "AsmResolver.dll", "AsmResolver.PE.File.dll", "AsmResolver.PE.dll" };
                    foreach (var name in preloadOrder)
                    {
                        var resName = assembly.GetManifestResourceNames()
                            .FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
                        if (resName != null)
                        {
                            using var stream = assembly.GetManifestResourceStream(resName);
                            if (stream != null)
                            {
                                loadFromStreamMethod?.Invoke(alc, new object[] { stream });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
        }

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var requestedName = new AssemblyName(args.Name).Name;
            var assembly = typeof(UniFFIGenerator).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith($"{requestedName}.dll", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    return Assembly.Load(data);
                }
            }
            return null;
        };
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Stamp files are marked UniFFI=true. Content is "libraryPath|utcTicks".
        var stampProvider = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(pair =>
            {
                var (text, options) = pair;
                return options.GetOptions(text).TryGetValue("build_metadata.AdditionalFiles.UniFFI", out var marker) &&
                       string.Equals(marker, "true", StringComparison.OrdinalIgnoreCase);
            })
            .Select((pair, cancellationToken) => ReadStamp(pair.Left, cancellationToken));

        var namespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property._UniFFINamespaceEncoded", out var encoded) &&
                    !string.IsNullOrWhiteSpace(encoded))
                {
                    return encoded.Trim();
                }

                if (options.GlobalOptions.TryGetValue("build_property.UniFFINamespace", out var ns) &&
                    !string.IsNullOrWhiteSpace(ns))
                {
                    return ns.Trim();
                }

                return string.Empty;
            });

        var combinedProvider = stampProvider.Collect().Combine(namespaceProvider);

        context.RegisterSourceOutput(combinedProvider, (productionContext, pair) =>
        {
            var (stamps, customNamespace) = pair;
            var generatedCrates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nsConfig = new NamespaceConfig(customNamespace);

            foreach (var stamp in stamps)
            {
                var libPath = LibraryPathFromStamp(stamp);
                if (libPath == null || !File.Exists(libPath))
                    continue;

                var ciList = PeMetadataExtractor.ExtractAll(libPath);
                if (ciList == null || ciList.Count == 0)
                    continue;

                var validCis = ciList.Where(c => !c.IsEmpty).ToList();
                if (validCis.Count == 0)
                    continue;

                for (int i = 0; i < validCis.Count; i++)
                {
                    bool isRoot = (i == 0);
                    validCis[i].Namespace = nsConfig.ResolveNamespace(validCis[i].CrateName, isRoot);
                }

                var crateToNs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in validCis)
                {
                    crateToNs[c.CrateName.Replace('-', '_')] = c.Namespace;
                }

                Func<string, string> nsResolver = crate =>
                {
                    string norm = crate.Replace('-', '_');
                    if (crateToNs.TryGetValue(norm, out var resolvedNs))
                    {
                        return resolvedNs;
                    }
                    return nsConfig.ResolveNamespace(norm, false);
                };

                foreach (var ci in validCis)
                {
                    string crateNorm = ci.CrateName.Replace('-', '_');
                    if (!generatedCrates.Add(crateNorm))
                        continue;

                    string source = CodeGenerator.Generate(ci, Path.GetFileName(libPath), nsResolver);
                    productionContext.AddSource(
                        $"UniFFIBindings.{crateNorm}.g.cs",
                        SourceText.From(source, System.Text.Encoding.UTF8));
                }
            }
        });
    }

    private static string ReadStamp(AdditionalText text, CancellationToken cancellationToken)
    {
        try
        {
            return text.GetText(cancellationToken)?.ToString()?.Trim() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string? LibraryPathFromStamp(string? stamp)
    {
        if (string.IsNullOrWhiteSpace(stamp))
            return null;

        int separator = stamp!.IndexOf('|');
        string path = (separator >= 0 ? stamp.Substring(0, separator) : stamp).Trim();
        return path.Length == 0 ? null : path;
    }
}

internal sealed class NamespaceConfig
{
    public string RootNamespace { get; }
    public Dictionary<string, string> CrateMappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public NamespaceConfig(string rawConfig)
    {
        RootNamespace = string.Empty;
        if (string.IsNullOrWhiteSpace(rawConfig)) return;

        rawConfig = DecodeRawConfig(rawConfig.Trim());

        var tokens = rawConfig.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (trimmed.Length == 0) continue;

            int eqIndex = trimmed.IndexOf('=');
            if (eqIndex < 0)
            {
                if (string.IsNullOrEmpty(RootNamespace))
                {
                    RootNamespace = trimmed;
                }
            }
            else
            {
                string crateKey = trimmed.Substring(0, eqIndex).Trim().Replace('-', '_');
                string nsVal = trimmed.Substring(eqIndex + 1).Trim();
                CrateMappings[crateKey] = nsVal;
            }
        }
    }

    public string ResolveNamespace(string crateName, bool isRootCrate)
    {
        string crateNorm = crateName.Replace('-', '_');
        if (CrateMappings.TryGetValue(crateNorm, out var mappedVal))
        {
            if (mappedVal.StartsWith("global::", StringComparison.OrdinalIgnoreCase))
            {
                return mappedVal.Substring("global::".Length).Trim();
            }
            if (string.IsNullOrEmpty(mappedVal))
            {
                return RootNamespace;
            }
            if (!string.IsNullOrEmpty(RootNamespace))
            {
                return $"{RootNamespace}.{mappedVal.TrimStart('.')}";
            }
            return mappedVal.TrimStart('.');
        }

        if (isRootCrate)
        {
            return !string.IsNullOrEmpty(RootNamespace) ? RootNamespace : TypeHelper.ToPascalCase(crateName);
        }

        string subNs = TypeHelper.ToPascalCase(crateName);
        return !string.IsNullOrEmpty(RootNamespace) ? $"{RootNamespace}.{subNs}" : subNs;
    }

    private static string DecodeRawConfig(string raw)
    {
        if (raw.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var base64Part = raw.Substring("base64:".Length).Trim();
                var bytes = Convert.FromBase64String(base64Part);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return raw;
            }
        }

        if (raw.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var hexPart = raw.Substring("hex:".Length).Trim();
                if (hexPart.Length % 2 == 0)
                {
                    var bytes = new byte[hexPart.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = Convert.ToByte(hexPart.Substring(i * 2, 2), 16);
                    }
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
            }
            catch
            {
                return raw;
            }
        }

        if (raw.IndexOf('%') >= 0)
        {
            try
            {
                return Uri.UnescapeDataString(raw);
            }
            catch
            {
                return raw;
            }
        }

        return raw;
    }
}
