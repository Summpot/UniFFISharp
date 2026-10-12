using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata.BoltFfi;
using UniFFISharp.Generator.PeParser;

[assembly: InternalsVisibleTo("UniFFISharp.Tests")]

namespace UniFFISharp.Generator;

[Generator]
public class UniFFIGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor SymbolCollisionDescriptor = new(
        "UFFI0020",
        "Symbol collision between UniFFI and BoltFFI",
        "Symbol '{0}' is exported by both UniFFI and BoltFFI in namespace '{1}'. Configure <UniFFIEnginePrefer> to choose one or <BoltFFINamespace> to redirect.",
        "UniFFISharp",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

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

        var boltNamespaceProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property._BoltFFINamespaceEncoded", out var encoded) &&
                    !string.IsNullOrWhiteSpace(encoded))
                {
                    return encoded.Trim();
                }

                if (options.GlobalOptions.TryGetValue("build_property.BoltFFINamespace", out var ns) &&
                    !string.IsNullOrWhiteSpace(ns))
                {
                    return ns.Trim();
                }

                return string.Empty;
            });

        var engineProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property.UniFFIEngine", out var engine) &&
                    !string.IsNullOrWhiteSpace(engine))
                {
                    return engine.Trim();
                }
                return "Auto";
            });

        var preferProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property.UniFFIEnginePrefer", out var prefer) &&
                    !string.IsNullOrWhiteSpace(prefer))
                {
                    return prefer.Trim();
                }
                return "PreferBoltFFI";
            });

        var useNativeUnionsProvider = context.AnalyzerConfigOptionsProvider
            .Select((options, _) =>
            {
                if (options.GlobalOptions.TryGetValue("build_property.UniFFIUseNativeUnions", out var raw) &&
                    !string.IsNullOrWhiteSpace(raw))
                {
                    if (bool.TryParse(raw.Trim(), out var parsed))
                    {
                        return (bool?)parsed;
                    }
                }
                return null;
            });

        var langVersionProvider = context.ParseOptionsProvider
            .Select((options, _) => options is CSharpParseOptions csOptions ? csOptions.LanguageVersion : LanguageVersion.Default);

        var unionSupportProvider = context.CompilationProvider
            .Select((compilation, _) => compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute") != null);

        var configProvider = namespaceProvider
            .Combine(boltNamespaceProvider)
            .Combine(engineProvider)
            .Combine(preferProvider)
            .Combine(useNativeUnionsProvider);

        var langAndSupportProvider = langVersionProvider.Combine(unionSupportProvider);

        var combinedProvider = stampProvider.Collect()
            .Combine(configProvider)
            .Combine(langAndSupportProvider);

        context.RegisterSourceOutput(combinedProvider, (productionContext, pair) =>
        {
            var ((stamps, ((((customNamespace, boltNamespace), engineMode), preferMode), useNativeUnions)), (langVersion, hasUnionSupport)) = pair;
            var generatedUniFFICrates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generatedBoltFFICrates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var nsConfig = new NamespaceConfig(customNamespace);
            var boltNsConfig = string.IsNullOrWhiteSpace(boltNamespace) ? nsConfig : new NamespaceConfig(boltNamespace);

            var codeGenOptions = new CodeGeneratorOptions
            {
                LanguageVersion = langVersion,
                HasUnionAttributeSupport = hasUnionSupport,
                UseNativeUnions = useNativeUnions
            };

            bool enableUniFFI = !string.Equals(engineMode, "BoltFFI", StringComparison.OrdinalIgnoreCase);
            bool enableBoltFFI = !string.Equals(engineMode, "UniFFI", StringComparison.OrdinalIgnoreCase);

            foreach (var stamp in stamps)
            {
                var libPath = LibraryPathFromStamp(stamp);
                if (libPath == null || !File.Exists(libPath))
                    continue;

                var ciList = enableUniFFI ? PeMetadataExtractor.ExtractAll(libPath) : Array.Empty<Metadata.ComponentInterface>();
                var boltEnvelopes = enableBoltFFI ? BoltFfiMetadataExtractor.ExtractAll(libPath) : Array.Empty<BoltFfiMetadataEnvelope>();

                if ((ciList == null || ciList.Count == 0) && (boltEnvelopes == null || boltEnvelopes.Count == 0))
                    continue;

                var validCis = ciList != null ? ciList.Where(c => !c.IsEmpty).ToList() : new List<Metadata.ComponentInterface>();

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

                // Collision detection and arbitration
                if (boltEnvelopes != null && boltEnvelopes.Count > 0)
                {
                    foreach (var env in boltEnvelopes)
                    {
                        if (env.Bindings.Native == null) continue;
                        string crateNorm = env.Package.PackageName.Replace('-', '_');
                        string boltTargetNs = boltNsConfig.ResolveNamespace(crateNorm, true);

                        var boltSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var decl in env.Bindings.Native.Decls)
                        {
                            if (!string.IsNullOrEmpty(decl.DeclName))
                            {
                                boltSymbols.Add(decl.DeclName);
                            }
                        }
                        boltSymbols.Add($"{TypeHelper.ToPascalCase(crateNorm)}Methods");

                        // Check collision against UniFFI CI
                        var matchedCi = validCis.FirstOrDefault(c => string.Equals(c.CrateName.Replace('-', '_'), crateNorm, StringComparison.OrdinalIgnoreCase));
                        if (matchedCi != null)
                        {
                            bool sameNamespace = string.Equals(matchedCi.Namespace, boltTargetNs, StringComparison.OrdinalIgnoreCase);
                            if (sameNamespace)
                            {
                                if (string.Equals(preferMode, "Error", StringComparison.OrdinalIgnoreCase))
                                {
                                    foreach (var sym in boltSymbols)
                                    {
                                        if (matchedCi.Records.Any(r => string.Equals(r.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                                            matchedCi.Enums.Any(e => string.Equals(e.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                                            matchedCi.Objects.Any(o => string.Equals(o.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                                            matchedCi.CallbackInterfaces.Any(ci => string.Equals(ci.Name, sym, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            productionContext.ReportDiagnostic(Diagnostic.Create(
                                                SymbolCollisionDescriptor,
                                                Location.None,
                                                sym,
                                                matchedCi.Namespace));
                                        }
                                    }
                                }
                                else if (string.Equals(preferMode, "PreferUniFFI", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Drop colliding symbols from BoltFFI
                                    env.Bindings.Native.Decls.RemoveAll(d =>
                                        matchedCi.Records.Any(r => string.Equals(r.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
                                        matchedCi.Enums.Any(e => string.Equals(e.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
                                        matchedCi.Objects.Any(o => string.Equals(o.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
                                        matchedCi.CallbackInterfaces.Any(ci => string.Equals(ci.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)));
                                }
                                else
                                {
                                    // Default: PreferBoltFFI -> drop colliding symbols from UniFFI
                                    matchedCi.Records.RemoveAll(r => boltSymbols.Contains(r.Name));
                                    matchedCi.Enums.RemoveAll(e => boltSymbols.Contains(e.Name));
                                    matchedCi.Objects.RemoveAll(o => boltSymbols.Contains(o.Name));
                                    matchedCi.CallbackInterfaces.RemoveAll(ci => boltSymbols.Contains(ci.Name));
                                    if (boltSymbols.Contains($"{TypeHelper.ToPascalCase(crateNorm)}Methods"))
                                    {
                                        matchedCi.Functions.Clear();
                                    }
                                }
                            }
                        }

                        // Emit BoltFFI bindings
                        if (generatedBoltFFICrates.Add(crateNorm))
                        {
                            string boltSource = BoltFfiCodeGenerator.Generate(env, Path.GetFileName(libPath), boltTargetNs);
                            if (!string.IsNullOrEmpty(boltSource))
                            {
                                productionContext.AddSource(
                                    $"BoltFFIBindings.{crateNorm}.g.cs",
                                    SourceText.From(boltSource, Encoding.UTF8));
                            }
                        }
                    }
                }

                // Emit remaining UniFFI bindings
                foreach (var ci in validCis)
                {
                    if (ci.IsEmpty) continue;
                    string crateNorm = ci.CrateName.Replace('-', '_');
                    if (!generatedUniFFICrates.Add(crateNorm))
                        continue;

                    string source = CodeGenerator.Generate(ci, Path.GetFileName(libPath), nsResolver, codeGenOptions);
                    productionContext.AddSource(
                        $"UniFFIBindings.{crateNorm}.g.cs",
                        SourceText.From(source, Encoding.UTF8));
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
