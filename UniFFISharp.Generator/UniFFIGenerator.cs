using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // 1. Monitor AdditionalFiles with UniFFI="true" or native binary extension
        var additionalFilesProvider = context.AdditionalTextsProvider
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Where(pair =>
            {
                var (text, options) = pair;
                if (options.GetOptions(text).TryGetValue("build_metadata.AdditionalFiles.UniFFI", out var val) &&
                    string.Equals(val, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                var p = text.Path;
                return p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                       p.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) ||
                       p.EndsWith(".a", StringComparison.OrdinalIgnoreCase) ||
                       p.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                       p.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase);
            })
            .Select((pair, _) => pair.Left.Path);

        // 2. Monitor MSBuild properties UniFFIRustLibrary, UniFFIRustStaticLibrary, UniFFILinkMode, PublishAot
        var configProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                bool isStatic = false;
                if (options.GlobalOptions.TryGetValue("build_property.UniFFILinkMode", out var linkMode) &&
                    string.Equals(linkMode, "Static", StringComparison.OrdinalIgnoreCase))
                {
                    isStatic = true;
                }
                else if (options.GlobalOptions.TryGetValue("build_property.PublishAot", out var aot) &&
                    string.Equals(aot, "true", StringComparison.OrdinalIgnoreCase))
                {
                    isStatic = true;
                }

                var list = new List<string>();
                if (isStatic)
                {
                    if (options.GlobalOptions.TryGetValue("build_property.UniFFIRustStaticLibrary", out var staticLib) &&
                        !string.IsNullOrWhiteSpace(staticLib))
                    {
                        list.Add(staticLib.Trim());
                    }
                    else if (options.GlobalOptions.TryGetValue("build_property.UniFFIRustLibrary", out var lib) &&
                        !string.IsNullOrWhiteSpace(lib) &&
                        (lib.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) || lib.EndsWith(".a", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(lib.Trim());
                    }
                }
                else
                {
                    if (options.GlobalOptions.TryGetValue("build_property.UniFFIRustLibrary", out var libPath) &&
                        !string.IsNullOrWhiteSpace(libPath))
                    {
                        list.Add(libPath.Trim());
                    }
                    if (options.GlobalOptions.TryGetValue("build_property.UniFFIRustStaticLibrary", out var staticLibPath) &&
                        !string.IsNullOrWhiteSpace(staticLibPath))
                    {
                        list.Add(staticLibPath.Trim());
                    }
                }

                return (IsStatic: isStatic, Paths: list);
            });

        // Combine both sources
        var allLibPaths = additionalFilesProvider
            .Collect()
            .Combine(configProvider)
            .SelectMany((pair, _) =>
            {
                var (fromAdditional, config) = pair;
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var propPath in config.Paths)
                {
                    if (!string.IsNullOrWhiteSpace(propPath))
                    {
                        try { set.Add(Path.GetFullPath(propPath.Trim())); }
                        catch { set.Add(propPath.Trim()); }
                    }
                }

                foreach (var f in fromAdditional)
                {
                    if (!string.IsNullOrWhiteSpace(f))
                    {
                        string path;
                        try { path = Path.GetFullPath(f.Trim()); }
                        catch { path = f.Trim(); }

                        if (config.IsStatic)
                        {
                            if (path.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) ||
                                path.EndsWith(".a", StringComparison.OrdinalIgnoreCase))
                            {
                                set.Add(path);
                            }
                        }
                        else
                        {
                            set.Add(path);
                        }
                    }
                }

                if (config.IsStatic)
                {
                    var staticOnly = set.Where(p => p.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".a", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (staticOnly.Count > 0)
                    {
                        return staticOnly;
                    }
                }

                return set.ToList();
            });

        // 3. Monitor MSBuild property UniFFINamespace
        var namespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property.UniFFINamespace", out var ns) &&
                    !string.IsNullOrWhiteSpace(ns))
                {
                    return ns.Trim();
                }
                return null;
            });

        // Combine all sources
        var combinedProvider = allLibPaths.Collect().Combine(namespaceProvider);

        // Generate C# bindings
        context.RegisterSourceOutput(combinedProvider, (productionContext, pair) =>
        {
            var (libPaths, customNamespace) = pair;
            var generatedCrates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var libPath in libPaths)
            {
                bool exists = !string.IsNullOrEmpty(libPath) && File.Exists(libPath);
                if (!exists) continue;

                var ci = PeMetadataExtractor.Extract(libPath!);
                if (ci == null) continue;

                if (!string.IsNullOrWhiteSpace(customNamespace))
                {
                    ci.Namespace = customNamespace!;
                }

                int fnCount = ci.Functions.Count;
                int recCount = ci.Records.Count;
                int enmCount = ci.Enums.Count;
                int objCount = ci.Objects.Count;
                int cbiCount = ci.CallbackInterfaces.Count;

                if (fnCount == 0 && recCount == 0 && enmCount == 0 && objCount == 0 && cbiCount == 0)
                {
                    continue;
                }

                string crateNorm = ci.CrateName.Replace('-', '_');
                if (!generatedCrates.Add(crateNorm))
                {
                    continue;
                }

                string libFileName = Path.GetFileName(libPath);
                string source = CodeGenerator.Generate(ci, libFileName);
                string hintName = $"UniFFIBindings.{crateNorm}.g.cs";

                productionContext.AddSource(hintName, SourceText.From(source, System.Text.Encoding.UTF8));
            }
        });
    }
}
