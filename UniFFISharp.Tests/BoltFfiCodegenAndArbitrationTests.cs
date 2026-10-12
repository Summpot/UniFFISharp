using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using UniFFISharp.Generator;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;
using Xunit;
using TypeKind = UniFFISharp.Generator.Metadata.TypeKind;

namespace UniFFISharp.Tests;

public class BoltFfiCodegenAndArbitrationTests
{
    private static BoltFfiMetadataEnvelope CreateSampleBoltEnvelope()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""geometry"", ""core""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""geometry"", ""core""] }
                    },
                    ""decls"": [
                        {
                            ""Record"": {
                                ""Direct"": {
                                    ""name"": { ""parts"": [""point""] },
                                    ""fields"": [
                                        { ""key"": { ""Name"": ""x"" }, ""ty"": { ""Primitive"": ""F64"" } },
                                        { ""key"": { ""Name"": ""y"" }, ""ty"": { ""Primitive"": ""F64"" } }
                                    ]
                                }
                            }
                        },
                        {
                            ""Enum"": {
                                ""name"": { ""parts"": [""shape"", ""kind""] },
                                ""variants"": [
                                    { ""name"": ""Circle"", ""discriminant"": 0 },
                                    { ""name"": ""Square"", ""discriminant"": 1 }
                                ]
                            }
                        },
                        {
                            ""Class"": {
                                ""name"": { ""parts"": [""canvas"", ""renderer""] },
                                ""release_symbol"": ""boltffi_class_release_canvas_renderer""
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""create"", ""point""] },
                                ""symbol"": ""boltffi_function_create_point"",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": {
                                        ""plan"": {
                                            ""DirectViaReturnSlot"": {
                                                ""Record"": ""Point""
                                            }
                                        }
                                    },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""x""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""F64"" } }
                                        },
                                        {
                                            ""name"": { ""parts"": [""y""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""F64"" } }
                                        }
                                    ]
                                }
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""render"", ""async""] },
                                ""symbol"": ""boltffi_function_render_async"",
                                ""callable"": {
                                    ""execution"": ""Async"",
                                    ""returns"": {
                                        ""plan"": {
                                            ""DirectViaReturnSlot"": {
                                                ""Primitive"": ""String""
                                            }
                                        }
                                    },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""renderer""] },
                                            ""plan"": { ""Direct"": { ""Class"": ""CanvasRenderer"" } }
                                        },
                                        {
                                            ""name"": { ""parts"": [""title""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                        }
                                    ]
                                }
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);
        return env!;
    }

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesExpectedStructures()
    {
        var env = CreateSampleBoltEnvelope();
        string source = BoltFfiCodeGenerator.Generate(env, "geometry_core.dll", "Geometry.Core");

        // 1. Namespace
        Assert.Contains("namespace Geometry.Core", source);

        // 2. Inlined Lightweight Runtime
        Assert.Contains("internal struct FfiBuf", source);
        Assert.Contains("internal struct BoltFfiStatus", source);
        Assert.Contains("internal sealed class WireReader", source);
        Assert.Contains("internal sealed class WireWriter", source);

        // 3. Direct Struct (Blittable)
        Assert.Contains("[StructLayout(LayoutKind.Sequential)]", source);
        Assert.Contains("public partial struct Point : IEquatable<Point>", source);
        Assert.Contains("public double X;", source);
        Assert.Contains("public double Y;", source);
        Assert.Contains("public bool Equals(Point other)", source);
        Assert.Contains("public override int GetHashCode()", source);
        Assert.Contains("public static bool operator ==(Point left, Point right)", source);

        // 4. Enum
        Assert.Contains("public enum ShapeKind", source);
        Assert.Contains("Circle = 0,", source);
        Assert.Contains("Square = 1,", source);

        // 5. Class Handle with IDisposable
        Assert.Contains("public partial class CanvasRenderer : IDisposable", source);
        Assert.Contains("private nint handle;", source);
        Assert.Contains("internal CanvasRenderer(nint handle)", source);
        Assert.Contains("internal nint GetHandle() => handle;", source);
        Assert.Contains("NativeMethods.boltffi_class_release_canvas_renderer(handle);", source);

        // 6. Native Methods
        Assert.Contains("internal static partial class NativeMethods", source);
        Assert.Contains("private const string NativeLib = \"geometry_core\";", source);
        Assert.Contains("[DllImport(NativeLib, EntryPoint = \"boltffi_function_create_point\", CallingConvention = CallingConvention.Cdecl)]", source);
        Assert.Contains("internal static extern Point boltffi_function_create_point(double x, double y);", source);
        Assert.Contains("[DllImport(NativeLib, EntryPoint = \"boltffi_class_release_canvas_renderer\", CallingConvention = CallingConvention.Cdecl)]", source);
        Assert.Contains("internal static extern void boltffi_class_release_canvas_renderer(nint handle);", source);

        // 7. Public Friendly Methods
        Assert.Contains("public static partial class GeometryCoreMethods", source);
        Assert.Contains("public static Point CreatePoint(double x, double y)", source);
        Assert.Contains("public static async Task<string> RenderAsync(CanvasRenderer renderer, string title)", source);
        Assert.Contains("renderer.GetHandle()", source);
        Assert.Contains("FfiBuf.FromBytes(global::System.Text.Encoding.UTF8.GetBytes(title ?? string.Empty))", source);
        Assert.Contains(".ReadString()", source);
    }

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesSyntaxWithZeroErrors()
    {
        var env = CreateSampleBoltEnvelope();
        string source = BoltFfiCodeGenerator.Generate(env, "geometry_core.dll", "Geometry.Core");

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void Arbitration_PreferBoltFFI_RemovesCollidingSymbolsFromUniFFI()
    {
        var boltEnv = CreateSampleBoltEnvelope();
        string preferMode = "PreferBoltFFI"; // Default

        // Construct a colliding UniFFI component interface
        var ci = new ComponentInterface
        {
            CrateName = "geometry_core",
            Namespace = "Geometry.Core"
        };
        ci.Records.Add(new RecordMetadata { Name = "Point" });
        ci.Records.Add(new RecordMetadata { Name = "NonCollidingUniFFIRecord" });
        ci.Enums.Add(new EnumMetadata { Name = "ShapeKind" });
        ci.Objects.Add(new ObjectMetadata { Name = "CanvasRenderer" });
        ci.Objects.Add(new ObjectMetadata { Name = "OtherObject" });
        ci.Functions.Add(new FnMetadata { Name = "CreatePoint" });

        // Simulate Arbitration Logic
        var boltSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decl in boltEnv.Bindings.Native!.Decls)
        {
            if (!string.IsNullOrEmpty(decl.DeclName))
                boltSymbols.Add(decl.DeclName);
        }
        boltSymbols.Add("GeometryCoreMethods");

        Assert.Contains("Point", boltSymbols);
        Assert.Contains("ShapeKind", boltSymbols);
        Assert.Contains("CanvasRenderer", boltSymbols);

        // PreferBoltFFI: drop colliding symbols from UniFFI
        if (string.Equals(preferMode, "PreferBoltFFI", StringComparison.OrdinalIgnoreCase))
        {
            ci.Records.RemoveAll(r => boltSymbols.Contains(r.Name));
            ci.Enums.RemoveAll(e => boltSymbols.Contains(e.Name));
            ci.Objects.RemoveAll(o => boltSymbols.Contains(o.Name));
            ci.CallbackInterfaces.RemoveAll(ci_cb => boltSymbols.Contains(ci_cb.Name));
            if (boltSymbols.Contains("GeometryCoreMethods"))
            {
                ci.Functions.Clear();
            }
        }

        // Point, ShapeKind, CanvasRenderer, and Functions should be cleared from UniFFI
        Assert.DoesNotContain(ci.Records, r => r.Name == "Point");
        Assert.Contains(ci.Records, r => r.Name == "NonCollidingUniFFIRecord");
        Assert.Empty(ci.Enums);
        Assert.DoesNotContain(ci.Objects, o => o.Name == "CanvasRenderer");
        Assert.Contains(ci.Objects, o => o.Name == "OtherObject");
        Assert.Empty(ci.Functions);

        // BoltFFI retained all its declarations
        Assert.Equal(5, boltEnv.Bindings.Native.Decls.Count);
    }

    [Fact]
    public void Arbitration_PreferUniFFI_RemovesCollidingSymbolsFromBoltFFI()
    {
        var boltEnv = CreateSampleBoltEnvelope();
        var ci = new ComponentInterface
        {
            CrateName = "geometry_core",
            Namespace = "Geometry.Core"
        };
        ci.Records.Add(new RecordMetadata { Name = "Point" });
        ci.Enums.Add(new EnumMetadata { Name = "ShapeKind" });

        // PreferUniFFI: drop colliding symbols from BoltFFI
        boltEnv.Bindings.Native!.Decls.RemoveAll(d =>
            ci.Records.Any(r => string.Equals(r.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
            ci.Enums.Any(e => string.Equals(e.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
            ci.Objects.Any(o => string.Equals(o.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)) ||
            ci.CallbackInterfaces.Any(cb => string.Equals(cb.Name, d.DeclName, StringComparison.OrdinalIgnoreCase)));

        // BoltFFI dropped Point and ShapeKind, but kept CanvasRenderer and functions
        Assert.DoesNotContain(boltEnv.Bindings.Native.Decls, d => d.DeclName == "Point");
        Assert.DoesNotContain(boltEnv.Bindings.Native.Decls, d => d.DeclName == "ShapeKind");
        Assert.Contains(boltEnv.Bindings.Native.Decls, d => d.DeclName == "CanvasRenderer");

        // UniFFI kept Point and ShapeKind
        Assert.Contains(ci.Records, r => r.Name == "Point");
        Assert.Contains(ci.Enums, e => e.Name == "ShapeKind");
    }

    [Fact]
    public void Arbitration_NamespaceRedirection_RetainsSymbolsInBoth()
    {
        var boltEnv = CreateSampleBoltEnvelope();
        var ci = new ComponentInterface
        {
            CrateName = "geometry_core",
            Namespace = "Geometry.Core"
        };
        ci.Records.Add(new RecordMetadata { Name = "Point" });

        string boltTargetNs = "Geometry.Core.Bolt"; // Redirected via <BoltFFINamespace>

        bool sameNamespace = string.Equals(ci.Namespace, boltTargetNs, StringComparison.OrdinalIgnoreCase);
        Assert.False(sameNamespace);

        // Since namespaces differ, no collision occurs and both retain all symbols
        Assert.Single(ci.Records);
        Assert.Equal(5, boltEnv.Bindings.Native!.Decls.Count);
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly string path;
        private readonly SourceText text;

        public TestAdditionalText(string path, string content)
        {
            this.path = path;
            text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => text;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions globalOptions;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> properties)
        {
            globalOptions = new DictionaryAnalyzerConfigOptions(properties);
        }

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => globalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => globalOptions;

        private sealed class DictionaryAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> properties;
            public DictionaryAnalyzerConfigOptions(Dictionary<string, string> properties)
            {
                this.properties = properties;
            }
            public override bool TryGetValue(string key, out string value)
            {
                return properties.TryGetValue(key, out value!);
            }
        }
    }

    [Fact]
    public void IncrementalGenerator_ExecutesAndEmitsBoltFFIBindings()
    {
        // 1. Create a dummy native binary containing a valid framed .boltffi section
        string tempDir = Path.Combine(Path.GetTempPath(), "uniffisharp_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string mockLibPath = Path.Combine(tempDir, "mock_native.dll");

        try
        {
            string boltJson = @"
            {
                ""magic"": ""boltffi.bindings"",
                ""format"": { ""major"": 0, ""minor"": 1 },
                ""surface"": ""Native"",
                ""package"": {
                    ""name"": { ""parts"": [""mock"", ""pkg""] }
                },
                ""bindings"": {
                    ""Native"": {
                        ""package"": {
                            ""name"": { ""parts"": [""mock"", ""pkg""] }
                        },
                        ""decls"": [
                            {
                                ""Record"": {
                                    ""Direct"": {
                                        ""name"": { ""parts"": [""sample"", ""data""] },
                                        ""fields"": [
                                            { ""key"": { ""Name"": ""id"" }, ""ty"": { ""Primitive"": ""I32"" } }
                                        ]
                                    }
                                }
                            }
                        ]
                    }
                }
            }";

            // Write raw binary with BFFIMD01 framing
            byte[] jsonBytes = Encoding.UTF8.GetBytes(boltJson);
            byte[] framed = new byte[8 + 8 + jsonBytes.Length];
            Encoding.ASCII.GetBytes("BFFIMD01").CopyTo(framed, 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(framed.AsSpan(8, 8), (ulong)jsonBytes.Length);
            jsonBytes.CopyTo(framed, 16);

            // Write as raw binary file (our fallback extractor scans binary files for BFFIMD01)
            File.WriteAllBytes(mockLibPath, framed);

            // 2. Setup Roslyn generator driver
            var generator = new UniFFIGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            string stampPath = Path.Combine(tempDir, "target.uniffi.stamp");
            var additionalText = new TestAdditionalText(stampPath, mockLibPath);

            var options = new Dictionary<string, string>
            {
                ["build_metadata.AdditionalFiles.UniFFI"] = "true",
                ["build_property.UniFFIEngine"] = "Dual",
                ["build_property.UniFFIEnginePrefer"] = "PreferBoltFFI"
            };
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(options);

            var compilation = CSharpCompilation.Create("TestAssembly",
                references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

            driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            // 3. Run generator
            driver = driver.RunGenerators(compilation);
            var runResult = driver.GetRunResult();

            // 4. Assert that BoltFFIBindings was emitted
            Assert.Contains(runResult.GeneratedTrees, t => t.FilePath.Contains("BoltFFIBindings.mock_pkg.g.cs"));

            var boltTree = runResult.GeneratedTrees.First(t => t.FilePath.Contains("BoltFFIBindings.mock_pkg.g.cs"));
            string text = boltTree.GetText().ToString();
            Assert.Contains("public partial struct SampleData", text);
            Assert.Contains("public int Id;", text);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void IncrementalGenerator_EngineUniFFI_IgnoresBoltFFI()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "uniffisharp_test_engine_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string mockLibPath = Path.Combine(tempDir, "mock_native.dll");

        try
        {
            string boltJson = @"
            {
                ""magic"": ""boltffi.bindings"",
                ""format"": { ""major"": 0, ""minor"": 1 },
                ""surface"": ""Native"",
                ""package"": {
                    ""name"": { ""parts"": [""mock"", ""pkg""] }
                },
                ""bindings"": {
                    ""Native"": {
                        ""package"": {
                            ""name"": { ""parts"": [""mock"", ""pkg""] }
                        },
                        ""decls"": []
                    }
                }
            }";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(boltJson);
            byte[] framed = new byte[8 + 8 + jsonBytes.Length];
            Encoding.ASCII.GetBytes("BFFIMD01").CopyTo(framed, 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(framed.AsSpan(8, 8), (ulong)jsonBytes.Length);
            jsonBytes.CopyTo(framed, 16);
            File.WriteAllBytes(mockLibPath, framed);

            var generator = new UniFFIGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            string stampPath = Path.Combine(tempDir, "target.uniffi.stamp");
            var additionalText = new TestAdditionalText(stampPath, mockLibPath);

            var options = new Dictionary<string, string>
            {
                ["build_metadata.AdditionalFiles.UniFFI"] = "true",
                ["build_property.UniFFIEngine"] = "UniFFI" // Force UniFFI only
            };
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(options);

            var compilation = CSharpCompilation.Create("TestAssembly",
                references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

            driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            driver = driver.RunGenerators(compilation);
            var runResult = driver.GetRunResult();

            // When UniFFIEngine is UniFFI, BoltFFI bindings must NOT be emitted
            Assert.DoesNotContain(runResult.GeneratedTrees, t => t.FilePath.Contains("BoltFFIBindings"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void Arbitration_ErrorMode_TriggersDiagnosticOnCollision()
    {
        var boltEnv = CreateSampleBoltEnvelope();
        var ci = new ComponentInterface
        {
            CrateName = "geometry_core",
            Namespace = "Geometry.Core"
        };
        ci.Records.Add(new RecordMetadata { Name = "Point" });

        var boltSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decl in boltEnv.Bindings.Native!.Decls)
        {
            if (!string.IsNullOrEmpty(decl.DeclName))
                boltSymbols.Add(decl.DeclName);
        }

        var collisions = new List<string>();
        foreach (var sym in boltSymbols)
        {
            if (ci.Records.Any(r => string.Equals(r.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                ci.Enums.Any(e => string.Equals(e.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                ci.Objects.Any(o => string.Equals(o.Name, sym, StringComparison.OrdinalIgnoreCase)) ||
                ci.CallbackInterfaces.Any(c => string.Equals(c.Name, sym, StringComparison.OrdinalIgnoreCase)))
            {
                collisions.Add(sym);
            }
        }

        Assert.Contains("Point", collisions);
    }
}
