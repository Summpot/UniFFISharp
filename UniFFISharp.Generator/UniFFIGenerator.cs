using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.PeParser;

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

            foreach (var stamp in stamps)
            {
                var libPath = LibraryPathFromStamp(stamp);
                if (libPath == null || !File.Exists(libPath))
                    continue;

                var ci = PeMetadataExtractor.Extract(libPath);
                if (ci == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(customNamespace))
                    ci.Namespace = customNamespace;

                if (ci.Functions.Count == 0 &&
                    ci.Records.Count == 0 &&
                    ci.Enums.Count == 0 &&
                    ci.Objects.Count == 0 &&
                    ci.CallbackInterfaces.Count == 0)
                {
                    continue;
                }

                string crateNorm = ci.CrateName.Replace('-', '_');
                if (!generatedCrates.Add(crateNorm))
                    continue;

                string source = CodeGenerator.Generate(ci, Path.GetFileName(libPath));
                productionContext.AddSource(
                    $"UniFFIBindings.{crateNorm}.g.cs",
                    SourceText.From(source, System.Text.Encoding.UTF8));
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
