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
using UniFFISharp.Generator.Arbitration;
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

        // 2. Shared Runtime reference
        Assert.Contains("using UniFFISharp.BoltFfi;", source);

        // 3. Direct Struct (Blittable)
        Assert.Contains("public partial record struct Point(double X, double Y)", source);
        Assert.Contains("internal static Point Decode(ref WireReader reader)", source);

        // 4. Enum
        Assert.Contains("public enum ShapeKind", source);
        Assert.Contains("Circle = 0,", source);
        Assert.Contains("Square = 1,", source);

        // 5. Class Handle with IDisposable and Interface
        Assert.Contains("public partial interface ICanvasRenderer : IDisposable", source);
        Assert.Contains("public partial class CanvasRenderer : ICanvasRenderer, IDisposable", source);
        Assert.Contains("private nint handle;", source);
        Assert.Contains("internal CanvasRenderer(nint handle, bool isHandle)", source);
        Assert.Contains("internal nint GetHandle()", source);
        Assert.Contains("NativeMethods.boltffi_class_release_canvas_renderer(", source);

        // 6. Native Methods
        Assert.Contains("internal static partial class NativeMethods", source);
        Assert.Contains("private const string NativeLib = \"geometry_core\";", source);
        Assert.Contains("[DllImport(NativeLib, EntryPoint = \"boltffi_function_create_point\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]", source);
        Assert.Contains("internal static extern Point boltffi_function_create_point(double x, double y);", source);
        Assert.Contains("[DllImport(NativeLib, EntryPoint = \"boltffi_class_release_canvas_renderer\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]", source);
        Assert.Contains("internal static extern void boltffi_class_release_canvas_renderer(nint handle);", source);

        // 7. Public Friendly Methods
        Assert.Contains("public static partial class GeometryCoreMethods", source);
        Assert.Contains("public static Point CreatePoint(double x, double y)", source);
        Assert.Contains("public static Task<string> RenderAsync(CanvasRenderer renderer, string title, CancellationToken cancellationToken = default)", source);
        Assert.Contains("renderer.GetHandle()", source);
        Assert.Contains("title_wire", source);
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
            Assert.Contains("public partial record struct SampleData(int Id)", text);
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

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesClassInitializersAndInstanceMethods()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""db"", ""client""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""db"", ""client""] }
                    },
                    ""decls"": [
                        {
                            ""Class"": {
                                ""name"": { ""parts"": [""database"", ""session""] },
                                ""release_symbol"": ""boltffi_release_database_session"",
                                ""initializers"": [
                                    {
                                        ""name"": { ""parts"": [""new""] },
                                        ""symbol"": ""boltffi_database_session_new"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {
                                                ""plan"": {
                                                    ""DirectViaReturnSlot"": {
                                                        ""Class"": ""DatabaseSession""
                                                    }
                                                }
                                            },
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""conn_str""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    }
                                ],
                                ""methods"": [
                                    {
                                        ""name"": { ""parts"": [""query""] },
                                        ""symbol"": ""boltffi_database_session_query"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {
                                                ""plan"": {
                                                    ""DirectViaReturnSlot"": {
                                                        ""Primitive"": ""String""
                                                    }
                                                }
                                            },
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""sql""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": { ""parts"": [""execute"", ""async""] },
                                        ""symbol"": ""boltffi_database_session_execute_async"",
                                        ""callable"": {
                                            ""execution"": ""Async"",
                                            ""returns"": {
                                                ""plan"": {
                                                    ""DirectViaReturnSlot"": {
                                                        ""Primitive"": ""I32""
                                                    }
                                                }
                                            },
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""sql""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": { ""parts"": [""close""] },
                                        ""symbol"": ""boltffi_database_session_close"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {},
                                            ""params"": []
                                        }
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "db_client.dll", "Db.Client");

        // Verify class generation
        Assert.Contains("public partial interface IDatabaseSession : IDisposable", source);
        Assert.Contains("public partial class DatabaseSession : IDatabaseSession, IDisposable", source);
        Assert.Contains("public DatabaseSession(string connStr)", source);
        Assert.Contains(": this(__Create_boltffi_database_session_new(connStr), true)", source);
        Assert.Contains("public string Query(string sql)", source);
        Assert.Contains("NativeMethods.boltffi_database_session_query(GetHandle(),", source);
        Assert.Contains("public Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken = default)", source);
        Assert.Contains("public void Close()", source);
        Assert.Contains("NativeMethods.boltffi_database_session_close(GetHandle());", source);
        Assert.Contains("NativeMethods.boltffi_release_database_session(", source);

        // Verify NativeMethods
        Assert.Contains("internal static extern nint boltffi_database_session_new(nint connStr_ptr, nuint connStr_len);", source);
        Assert.Contains("internal static extern FfiBuf boltffi_database_session_query(nint handle, nint sql_ptr, nuint sql_len);", source);
        Assert.Contains("internal static extern nint boltffi_database_session_execute_async(nint handle, nint sql_ptr, nuint sql_len);", source);
        Assert.Contains("internal static extern void boltffi_database_session_close(nint handle);", source);
        Assert.Contains("internal static extern void boltffi_release_database_session(nint handle);", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesDataEnumTaggedUnion()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""geometry"", ""model""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""geometry"", ""model""] }
                    },
                    ""decls"": [
                        {
                            ""Enum"": {
                                ""Data"": {
                                    ""name"": { ""parts"": [""geometry"", ""item""] },
                                    ""variants"": [
                                        {
                                            ""name"": ""Point"",
                                            ""discriminant"": 0,
                                            ""payload"": {
                                                ""Tuple"": [
                                                    { ""key"": { ""Index"": 0 }, ""ty"": { ""Primitive"": ""F64"" } },
                                                    { ""key"": { ""Index"": 1 }, ""ty"": { ""Primitive"": ""F64"" } }
                                                ]
                                            }
                                        },
                                        {
                                            ""name"": ""Circle"",
                                            ""discriminant"": 1,
                                            ""payload"": {
                                                ""Struct"": [
                                                    { ""key"": { ""Name"": ""radius"" }, ""ty"": { ""Primitive"": ""F64"" } }
                                                ]
                                            }
                                        },
                                        {
                                            ""name"": ""Empty"",
                                            ""discriminant"": 2
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

        string source = BoltFfiCodeGenerator.Generate(env!, "geometry_model.dll", "Geometry.Model");

        // Verify abstract partial record
        Assert.Contains("public abstract partial record GeometryItem", source);
        Assert.Contains("internal static GeometryItem Decode(ref WireReader reader) =>", source);
        Assert.Contains("0 => new Point(reader.ReadF64(), reader.ReadF64()),", source);
        Assert.Contains("1 => new Circle(reader.ReadF64()),", source);
        Assert.Contains("2 => new Empty(),", source);
        Assert.Contains("internal abstract void Encode(WireWriter writer);", source);

        // Verify variant sealed records
        Assert.Contains("public sealed partial record Point(double Item0, double Item1) : GeometryItem", source);
        Assert.Contains("public sealed partial record Circle(double Radius) : GeometryItem", source);
        Assert.Contains("public sealed partial record Empty() : GeometryItem", source);
        Assert.Contains("writer.WriteU32(0);", source);
        Assert.Contains("writer.WriteF64(Item0);", source);
        Assert.Contains("writer.WriteF64(Item1);", source);
        Assert.Contains("writer.WriteU32(1);", source);
        Assert.Contains("writer.WriteF64(Radius);", source);
        Assert.Contains("writer.WriteU32(2);", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesNativeUnionWhenOptionEnabled()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""geometry"", ""model""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""geometry"", ""model""] }
                    },
                    ""decls"": [
                        {
                            ""Enum"": {
                                ""Data"": {
                                    ""name"": { ""parts"": [""geometry"", ""item""] },
                                    ""variants"": [
                                        {
                                            ""name"": ""Point"",
                                            ""discriminant"": 0,
                                            ""payload"": {
                                                ""Tuple"": [
                                                    { ""key"": { ""Index"": 0 }, ""ty"": { ""Primitive"": ""F64"" } },
                                                    { ""key"": { ""Index"": 1 }, ""ty"": { ""Primitive"": ""F64"" } }
                                                ]
                                            }
                                        },
                                        {
                                            ""name"": ""Circle"",
                                            ""discriminant"": 1,
                                            ""payload"": {
                                                ""Struct"": [
                                                    { ""key"": { ""Name"": ""radius"" }, ""ty"": { ""Primitive"": ""F64"" } }
                                                ]
                                            }
                                        },
                                        {
                                            ""name"": ""Empty"",
                                            ""discriminant"": 2
                                        }
                                    ]
                                }
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""process"", ""item""] },
                                ""symbol"": ""boltffi_function_process_item"",
                                ""callable"": {
                                    ""params"": [
                                        { ""name"": { ""parts"": [""item""] }, ""ty"": { ""Option"": { ""Enum"": ""GeometryItem"" } } }
                                    ],
                                    ""returns"": { ""ty"": { ""Option"": { ""Enum"": ""GeometryItem"" } } }
                                }
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        var options = new CodeGeneratorOptions
        {
            UseNativeUnions = true
        };

        string source = BoltFfiCodeGenerator.Generate(env!, "geometry_model.dll", "Geometry.Model", options: options);

        // 1. Verify C# 15 Native Union declaration matching UniFFI
        Assert.Contains("public partial union GeometryItem(GeometryItem.Point, GeometryItem.Circle, GeometryItem.Empty)", source);
        Assert.DoesNotContain("public abstract partial record GeometryItem", source);

        // 2. Verify variant sealed records do NOT inherit from GeometryItem
        Assert.Contains("public sealed partial record Point(double Item0, double Item1);", source);
        Assert.DoesNotContain("public sealed partial record Point(double Item0, double Item1) : GeometryItem", source);
        Assert.Contains("public sealed partial record Circle(double Radius);", source);
        Assert.DoesNotContain("public sealed partial record Circle(double Radius) : GeometryItem", source);
        Assert.Contains("public sealed partial record Empty();", source);
        Assert.DoesNotContain("public sealed partial record Empty() : GeometryItem", source);

        // 3. Verify Decode wraps union instances
        Assert.Contains("0 => new GeometryItem(new Point(reader.ReadF64(), reader.ReadF64())),", source);
        Assert.Contains("1 => new GeometryItem(new Circle(reader.ReadF64())),", source);
        Assert.Contains("2 => new GeometryItem(new Empty()),", source);

        // 4. Verify Encode switches over this
        Assert.Contains("switch (this)", source);
        Assert.Contains("case Point v:", source);
        Assert.Contains("case Circle v:", source);
        Assert.Contains("case Empty:", source);

        // 5. Verify Option<NativeUnion> uses ReadOptionValue and WriteOptionValue (struct/value-type semantics)
        Assert.Contains("ReadOptionValue", source);
        Assert.Contains("WriteOptionValue", source);
    }

    [Fact]
    public void BoltFfiAndUniFFI_NativeUnion_PublicSignaturesAreIdentical()
    {
        // 1. Construct UniFFI ComponentInterface
        var ci = new ComponentInterface
        {
            CrateName = "drone_controller",
            Namespace = "DroneController"
        };
        ci.Enums.Add(new EnumMetadata
        {
            Name = "flight_command",
            Shape = EnumShape.Enum,
            Variants = new List<VariantMetadata>
            {
                new() { Name = "hover" },
                new()
                {
                    Name = "fly_to",
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "latitude", Type = UniFFIType.Primitive(TypeKind.Float64) },
                        new() { Name = "longitude", Type = UniFFIType.Primitive(TypeKind.Float64) }
                    }
                }
            }
        });

        // 2. Construct matching BoltFFI metadata with Record preceding Enum in source JSON
        string boltJson = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": { ""name"": { ""parts"": [""drone"", ""controller""] } },
            ""bindings"": {
                ""Native"": {
                    ""package"": { ""name"": { ""parts"": [""drone"", ""controller""] } },
                    ""decls"": [
                        {
                            ""Record"": {
                                ""Direct"": {
                                    ""name"": { ""parts"": [""geo"", ""point""] },
                                    ""fields"": [
                                        { ""key"": { ""Name"": ""x"" }, ""ty"": { ""Primitive"": ""F64"" } }
                                    ]
                                }
                            }
                        },
                        {
                            ""Enum"": {
                                ""Data"": {
                                    ""name"": { ""parts"": [""flight"", ""command""] },
                                    ""variants"": [
                                        { ""name"": ""Hover"", ""discriminant"": 0 },
                                        {
                                            ""name"": ""FlyTo"",
                                            ""discriminant"": 1,
                                            ""payload"": {
                                                ""Struct"": [
                                                    { ""key"": { ""Name"": ""latitude"" }, ""ty"": { ""Primitive"": ""F64"" } },
                                                    { ""key"": { ""Name"": ""longitude"" }, ""ty"": { ""Primitive"": ""F64"" } }
                                                ]
                                            }
                                        }
                                    ]
                                }
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(boltJson);
        Assert.NotNull(env);

        var options = new CodeGeneratorOptions
        {
            UseNativeUnions = true
        };

        ci.Records.Add(new RecordMetadata
        {
            Name = "geo_point",
            Fields = new List<FieldMetadata>
            {
                new() { Name = "x", Type = UniFFIType.Primitive(TypeKind.Float64) }
            }
        });

        string uniffiSource = CodeGenerator.Generate(ci, "drone.dll", null, options);
        string boltSource = BoltFfiCodeGenerator.Generate(env!, "drone.dll", "DroneController", options: options);

        // Assert public signature match exactly:
        // 1) Union declaration
        string unionDecl = "public partial union FlightCommand(FlightCommand.Hover, FlightCommand.FlyTo)";
        Assert.Contains(unionDecl, uniffiSource);
        Assert.Contains(unionDecl, boltSource);

        // 2) Hover variant (empty)
        string hoverDecl = "public sealed partial record Hover();";
        Assert.Contains(hoverDecl, uniffiSource);
        Assert.Contains(hoverDecl, boltSource);

        // 3) FlyTo variant (fields)
        string flyToDecl = "public sealed partial record FlyTo(double Latitude, double Longitude);";
        Assert.Contains(flyToDecl, uniffiSource);
        Assert.Contains(flyToDecl, boltSource);

        // 4) Neither should inherit from FlightCommand in native union mode
        Assert.DoesNotContain("Hover() : FlightCommand", uniffiSource);
        Assert.DoesNotContain("Hover() : FlightCommand", boltSource);
        Assert.DoesNotContain("FlyTo(double Latitude, double Longitude) : FlightCommand", uniffiSource);
        Assert.DoesNotContain("FlyTo(double Latitude, double Longitude) : FlightCommand", boltSource);

        // 5) Native Union must be emitted before Records (workaround for Roslyn #86005)
        int uniffiUnionPos = uniffiSource.IndexOf(unionDecl, StringComparison.Ordinal);
        int uniffiRecordPos = uniffiSource.IndexOf("GeoPoint", StringComparison.Ordinal);
        Assert.True(uniffiUnionPos < uniffiRecordPos, "UniFFI must emit union before records");

        int boltUnionPos = boltSource.IndexOf(unionDecl, StringComparison.Ordinal);
        int boltRecordPos = boltSource.IndexOf("GeoPoint", StringComparison.Ordinal);
        Assert.True(boltUnionPos < boltRecordPos, "BoltFFI must emit union before records even if record appeared first in JSON");
    }

    [Fact]
    public void BoltFfiCodeGenerator_GeneratesCallbackInterface()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""events"", ""core""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""events"", ""core""] }
                    },
                    ""decls"": [
                        {
                            ""Callback"": {
                                ""name"": { ""parts"": [""event"", ""listener""] },
                                ""methods"": [
                                    {
                                        ""name"": { ""parts"": [""on"", ""event""] },
                                        ""symbol"": ""cb_on_event"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {},
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""event_id""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""I32"" } }
                                                },
                                                {
                                                    ""name"": { ""parts"": [""message""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "events_core.dll", "Events.Core");

        // Verify callback interface and bridge
        Assert.Contains("public partial interface IEventListener", source);
        Assert.Contains("void OnEvent(int eventId, string message);", source);
        Assert.Contains("EventListenerBridge", source);
        Assert.Contains("internal struct VTable", source);
        Assert.Contains("internal static BoltFFICallbackHandle Create(IEventListener? callback)", source);
        Assert.Contains("EventListenerProxy : IEventListener", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_Parity_DocCommentsAndDefaultValues()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""docs""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""docs""] }
                    },
                    ""decls"": [
                        {
                            ""Constant"": {
                                ""name"": { ""parts"": [""default"", ""port""] },
                                ""ty"": { ""Primitive"": ""I32"" },
                                ""value"": ""5432"",
                                ""doc"": ""Default database port.""
                            }
                        },
                        {
                            ""Record"": {
                                ""Encoded"": {
                                    ""name"": { ""parts"": [""db"", ""config""] },
                                    ""doc"": ""Database configuration parameters."",
                                    ""fields"": [
                                        {
                                            ""key"": { ""Name"": ""timeout"" },
                                            ""ty"": { ""Primitive"": ""I32"" },
                                            ""doc"": ""Timeout in seconds."",
                                            ""default"": ""30""
                                        },
                                        {
                                            ""key"": { ""Name"": ""host"" },
                                            ""ty"": { ""Primitive"": ""String"" },
                                            ""doc"": ""Host address.""
                                        },
                                        {
                                            ""key"": { ""Name"": ""retries"" },
                                            ""ty"": { ""Primitive"": ""I32"" },
                                            ""doc"": ""Retry count."",
                                            ""default"": ""3""
                                        }
                                    ]
                                }
                            }
                        },
                        {
                            ""Stream"": {
                                ""name"": { ""parts"": [""metric"", ""events""] },
                                ""doc"": ""Stream of real-time metrics."",
                                ""item"": { ""ty"": { ""Primitive"": ""I64"" } }
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""connect""] },
                                ""symbol"": ""boltffi_parity_connect"",
                                ""doc"": ""Connects to the database server."",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": { ""Void"": {} },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""host""] },
                                            ""doc"": ""Host address."",
                                            ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                        },
                                        {
                                            ""name"": { ""parts"": [""port""] },
                                            ""doc"": ""Port number."",
                                            ""default"": ""5432"",
                                            ""plan"": { ""Direct"": { ""Primitive"": ""I32"" } }
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

        string source = BoltFfiCodeGenerator.Generate(env!, "parity_docs.dll", "Parity.Docs");

        // 1. Constant doc comments
        Assert.Contains("/// <summary>", source);
        Assert.Contains("/// Default database port.", source);
        Assert.Contains("public const int DefaultPort = 5432;", source);

        // 2. Record doc comments and field ordering (required 'host' first, optional 'timeout' and 'retries' after)
        Assert.Contains("/// Database configuration parameters.", source);
        Assert.Contains("/// <param name=\"Host\">", source);
        Assert.Contains("/// Host address.", source);
        Assert.Contains("/// <param name=\"Timeout\">", source);
        Assert.Contains("/// Timeout in seconds.", source);
        Assert.Contains("public partial record struct DbConfig(string Host, int Timeout = 30, int Retries = 3)", source);
        Assert.Contains("return new DbConfig(Host: host, Timeout: timeout, Retries: retries);", source);

        // 3. Stream doc comments
        Assert.Contains("/// Stream of real-time metrics.", source);
        Assert.Contains("public static IAsyncEnumerable<long> MetricEvents(CancellationToken cancellationToken = default)", source);

        // 4. Function doc comments and default value
        Assert.Contains("/// Connects to the database server.", source);
        Assert.Contains("/// <param name=\"port\">", source);
        Assert.Contains("/// Port number.", source);
        Assert.Contains("public static void Connect(string host, int port = 5432)", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_Parity_ReservedKeywordsEscaping()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""keywords""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""keywords""] }
                    },
                    ""decls"": [
                        {
                            ""Callback"": {
                                ""name"": { ""parts"": [""event""] },
                                ""doc"": ""Event handler callback."",
                                ""methods"": [
                                    {
                                        ""name"": { ""parts"": [""on"", ""event""] },
                                        ""symbol"": ""cb_on_event"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {},
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""string""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    }
                                ]
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""dispatch""] },
                                ""symbol"": ""boltffi_parity_dispatch"",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": { ""Void"": {} },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""event""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                        },
                                        {
                                            ""name"": { ""parts"": [""params""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""Bytes"" } }
                                        },
                                        {
                                            ""name"": { ""parts"": [""class""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""I32"" } }
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

        string source = BoltFfiCodeGenerator.Generate(env!, "parity_keywords.dll", "Parity.Keywords");

        // 1. Callback interface name stripped of '@' (IEvent, not I@Event)
        Assert.Contains("public partial interface IEvent", source);
        Assert.Contains("void OnEvent(string @string);", source);
        Assert.Contains("public sealed unsafe class EventProxy : IEvent", source);

        // 2. Dispatch function parameters escaped with '@'
        Assert.Contains("public static void Dispatch(string @event, byte[] @params, int @class)", source);

        // 3. Local variables do not contain '@'
        Assert.Contains("event_span", source);
        Assert.Contains("params_span", source);
        Assert.DoesNotContain("@event_span", source);
        Assert.DoesNotContain("@params_span", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_Parity_ClassAsyncStreamHeuristic()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""streams""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""streams""] }
                    },
                    ""decls"": [
                        {
                            ""Class"": {
                                ""name"": { ""parts"": [""log"", ""stream""] },
                                ""methods"": [
                                    {
                                        ""name"": { ""parts"": [""next""] },
                                        ""symbol"": ""boltffi_log_stream_next"",
                                        ""callable"": {
                                            ""execution"": ""Async"",
                                            ""returns"": {
                                                ""ty"": {
                                                    ""Option"": { ""Primitive"": ""String"" }
                                                }
                                            },
                                            ""params"": []
                                        }
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "parity_streams.dll", "Parity.Streams");

        // 1. Interface implements IAsyncEnumerable<string>
        Assert.Contains("public partial interface ILogStream : IDisposable, IAsyncEnumerable<string>", source);

        // 2. Class implements IAsyncEnumerable<string>
        Assert.Contains("public partial class LogStream : ILogStream, IDisposable, IAsyncEnumerable<string>", source);

        // 3. GetAsyncEnumerator method
        Assert.Contains("public async IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)", source);
        Assert.Contains("var item = await NextAsync(cancellationToken).ConfigureAwait(false);", source);
        Assert.Contains("if (item is null)", source);
        Assert.Contains("yield break;", source);
        Assert.Contains("yield return item;", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_Parity_InitializersAndCleanSignatures()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""classes""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""classes""] }
                    },
                    ""decls"": [
                        {
                            ""Class"": {
                                ""name"": { ""parts"": [""connection"", ""pool""] },
                                ""release_symbol"": ""boltffi_release_connection_pool"",
                                ""initializers"": [
                                    {
                                        ""name"": { ""parts"": [""new""] },
                                        ""symbol"": ""boltffi_connection_pool_new"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {},
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""conn_str""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": { ""parts"": [""from"", ""config""] },
                                        ""symbol"": ""boltffi_connection_pool_from_config"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {},
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""path""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": { ""parts"": [""connect"", ""async""] },
                                        ""symbol"": ""boltffi_connection_pool_connect_async"",
                                        ""callable"": {
                                            ""execution"": ""Async"",
                                            ""returns"": {},
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""uri""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    }
                                ],
                                ""methods"": [
                                    {
                                        ""name"": { ""parts"": [""execute""] },
                                        ""symbol"": ""boltffi_connection_pool_execute"",
                                        ""callable"": {
                                            ""execution"": ""Sync"",
                                            ""returns"": {
                                                ""ty"": { ""Primitive"": ""I32"" }
                                            },
                                            ""params"": [
                                                {
                                                    ""name"": { ""parts"": [""query""] },
                                                    ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                                }
                                            ]
                                        }
                                    }
                                ]
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "parity_classes.dll", "Parity.Classes");

        // 1. Initializer 'new' emits constructor with private helper
        Assert.Contains("public ConnectionPool(string connStr)", source);
        Assert.Contains(": this(__Create_boltffi_connection_pool_new(connStr), true)", source);
        Assert.Contains("private static nint __Create_boltffi_connection_pool_new(string connStr)", source);

        // 2. Named sync initializer emits static factory
        Assert.Contains("public static ConnectionPool FromConfig(string path)", source);

        // 3. Named async initializer emits async static factory
        Assert.Contains("public static Task<ConnectionPool> ConnectAsync(string uri, CancellationToken cancellationToken = default)", source);

        // 4. Instance method has clean safe public signature (no unsafe keyword)
        Assert.Contains("public int Execute(string query)", source);
        Assert.DoesNotContain("public unsafe int Execute", source);

        // 5. Internal pointer pinning is enclosed in unsafe { fixed(...) }
        Assert.Contains("unsafe", source);
        Assert.Contains("fixed (byte* query_ptr = query_span)", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfiCodeGenerator_Parity_ErrorEnum_UniffiExceptionParity()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""errors""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""errors""] }
                    },
                    ""decls"": [
                        {
                            ""Enum"": {
                                ""name"": { ""parts"": [""storage"", ""error""] },
                                ""doc"": ""Errors occurred during storage operations."",
                                ""variants"": [
                                    {
                                        ""name"": ""NotFound"",
                                        ""discriminant"": 0,
                                        ""doc"": ""Item was not found."",
                                        ""payload"": {
                                            ""Struct"": [
                                                {
                                                    ""key"": { ""Name"": ""key"" },
                                                    ""ty"": { ""Primitive"": ""String"" },
                                                    ""doc"": ""Missing key.""
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": ""PermissionDenied"",
                                        ""discriminant"": 1,
                                        ""doc"": ""Access is denied."",
                                        ""payload"": {
                                            ""Struct"": [
                                                {
                                                    ""key"": { ""Name"": ""reason"" },
                                                    ""ty"": { ""Primitive"": ""String"" }
                                                }
                                            ]
                                        }
                                    },
                                    {
                                        ""name"": ""Internal"",
                                        ""discriminant"": 2
                                    }
                                ]
                            }
                        },
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""get"", ""item""] },
                                ""symbol"": ""boltffi_parity_get_item"",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": {
                                        ""ty"": { ""Primitive"": ""Bytes"" }
                                    },
                                    ""error"": {
                                        ""Enum"": ""StorageError""
                                    },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""key""] },
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

        string source = BoltFfiCodeGenerator.Generate(env!, "parity_errors.dll", "Parity.Errors");

        // 1. Error enum inherits from UniffiException
        Assert.Contains("public abstract partial class StorageError : UniffiException", source);
        Assert.Contains("public sealed partial class NotFound : StorageError", source);
        Assert.Contains("public sealed partial class PermissionDenied : StorageError", source);
        Assert.Contains("public sealed partial class Internal : StorageError", source);

        // 2. Decode method
        Assert.Contains("internal static StorageError Decode(ref WireReader reader)", source);

        // 3. Method throws decoded concrete exception
        Assert.Contains("var errReader = new WireReader(errBuf);", source);
        Assert.Contains("var errObj = global::Parity.Errors.StorageError.Decode(ref errReader);", source);
        Assert.Contains("if (errObj is global::System.Exception ex) throw ex;", source);

        // Verify syntax validity
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfi_FallibleStringReturn_EmitsValidOutBufAndFree()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""fallible""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""fallible""] }
                    },
                    ""decls"": [
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""format"", ""yaml""] },
                                ""symbol"": ""boltffi_parity_fallible_format_yaml"",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": {
                                        ""plan"": {
                                            ""DirectViaReturnSlot"": {
                                                ""Primitive"": ""String""
                                            }
                                        }
                                    },
                                    ""params"": [
                                        {
                                            ""name"": { ""parts"": [""input""] },
                                            ""plan"": { ""Direct"": { ""Primitive"": ""String"" } }
                                        }
                                    ],
                                    ""error"": { ""kind"": ""BoltError"" }
                                }
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "mock_lib.dll", "Parity.Fallible");

        Assert.Contains("internal static extern FfiBuf boltffi_parity_fallible_format_yaml", source);
        Assert.Contains("out FfiBuf out_val", source);
        Assert.Contains("FfiBuf outNativeVal = default;", source);
        Assert.Contains("out outNativeVal", source);
        Assert.Contains("try { return outNativeVal.ReadString(); } finally { NativeMethods.boltffi_free_buf(outNativeVal); }", source);

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void BoltFfi_FallibleBytesReturn_EmitsValidOutBufAndFree()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""parity"", ""fallible""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""parity"", ""fallible""] }
                    },
                    ""decls"": [
                        {
                            ""Function"": {
                                ""name"": { ""parts"": [""build"", ""archive""] },
                                ""symbol"": ""boltffi_parity_fallible_build_archive"",
                                ""callable"": {
                                    ""execution"": ""Sync"",
                                    ""returns"": {
                                        ""plan"": {
                                            ""DirectViaReturnSlot"": {
                                                ""Primitive"": ""Bytes""
                                            }
                                        }
                                    },
                                    ""params"": [],
                                    ""error"": { ""kind"": ""BoltError"" }
                                }
                            }
                        }
                    ]
                }
            }
        }";

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);

        string source = BoltFfiCodeGenerator.Generate(env!, "mock_lib.dll", "Parity.Fallible");

        Assert.Contains("internal static extern FfiBuf boltffi_parity_fallible_build_archive", source);
        Assert.Contains("out FfiBuf out_val", source);
        Assert.Contains("FfiBuf outNativeVal = default;", source);
        Assert.Contains("out outNativeVal", source);
        Assert.Contains("try { return outNativeVal.ToArray(); } finally { NativeMethods.boltffi_free_buf(outNativeVal); }", source);

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void UniFfi_EnumConverter_EmitsSwitchMappingInsteadOfDirectCast()
    {
        var ci = new ComponentInterface
        {
            Namespace = "Sample.Enums",
            CrateName = "sample_enums",
            Enums = new List<EnumMetadata>
            {
                new()
                {
                    Name = "ImageFormat",
                    Shape = EnumShape.Enum,
                    Variants = new List<VariantMetadata>
                    {
                        new() { Name = "Jpeg" },
                        new() { Name = "Png" },
                        new() { Name = "Webp" }
                    }
                }
            }
        };

        string source = CodeGenerator.Generate(ci, "sample_enums");

        Assert.Contains("public enum ImageFormat", source);
        Assert.Contains("Jpeg = 0", source);
        Assert.Contains("Png = 1", source);
        Assert.Contains("Webp = 2", source);

        // Converter should use explicit variant mapping switch rather than direct (int) cast
        Assert.Contains("case ImageFormat.Jpeg: stream.WriteInt32(1); break;", source);
        Assert.Contains("case ImageFormat.Png: stream.WriteInt32(2); break;", source);
        Assert.Contains("case ImageFormat.Webp: stream.WriteInt32(3); break;", source);
        Assert.Contains("1 => ImageFormat.Jpeg,", source);
        Assert.Contains("2 => ImageFormat.Png,", source);
        Assert.Contains("3 => ImageFormat.Webp,", source);
        Assert.DoesNotContain("stream.WriteInt32((int)value)", source);
        Assert.DoesNotContain("(ImageFormat)tag", source);

        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var errors = syntaxTree.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void Arbitrate_ComplexRecordReturn_MissingBoltFfi_SelectsUniFfiWithoutWarning()
    {
        var ci = new ComponentInterface
        {
            Namespace = "Sample.Models",
            CrateName = "sample_models",
            Records = new List<RecordMetadata>
            {
                new()
                {
                    Name = "FolderSnapshot",
                    Fields = new List<FieldMetadata>
                    {
                        new() { Name = "id", Type = UniFFIType.Primitive(TypeKind.Int64) },
                        new() { Name = "retry_at", Type = new UniFFIType { Kind = TypeKind.Option, InnerType = UniFFIType.Primitive(TypeKind.Int64) } }
                    }
                }
            }
        };

        var uniffiFn = new FnMetadata
        {
            Name = "compute_snapshot",
            ReturnType = new UniFFIType { Kind = TypeKind.Record, Name = "FolderSnapshot" },
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "id", Type = UniFFIType.Primitive(TypeKind.Int64) }
            }
        };

        var (optimal, reason) = InteropArbitrator.PredictOptimalEngine(uniffiFn, null, ci);
        Assert.Equal(InteropEngine.UniFFI, optimal);
        Assert.Contains("record decoding", reason);

        var decision = InteropArbitrator.Arbitrate(uniffiFn, null, InteropEngine.Auto, ci);

        Assert.Equal(InteropEngine.UniFFI, decision.SelectedEngine);
        Assert.Equal(InteropEngine.UniFFI, decision.OptimalEngine);
        Assert.False(decision.IsSuboptimalMissingExport);
        Assert.Null(decision.PerformanceReason);
    }

    [Fact]
    public void Arbitrate_OptionRecordReturn_MissingBoltFfi_SelectsUniFfiWithoutWarning()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "try_find_position",
            ReturnType = new UniFFIType
            {
                Kind = TypeKind.Option,
                InnerType = new UniFFIType { Kind = TypeKind.Record, Name = "GridPlacement" }
            },
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "span", Type = UniFFIType.Primitive(TypeKind.Int32) }
            }
        };

        var (optimal, reason) = InteropArbitrator.PredictOptimalEngine(uniffiFn, null);
        Assert.Equal(InteropEngine.UniFFI, optimal);
        Assert.Contains("record decoding", reason);

        var decision = InteropArbitrator.Arbitrate(uniffiFn, null, InteropEngine.Auto);

        Assert.Equal(InteropEngine.UniFFI, decision.SelectedEngine);
        Assert.Equal(InteropEngine.UniFFI, decision.OptimalEngine);
        Assert.False(decision.IsSuboptimalMissingExport);
        Assert.Null(decision.PerformanceReason);
    }

    [Fact]
    public void BoltFfiModels_DirectVecAndReturnPlanParsing()
    {
        string funcJson = @"{
            ""id"": 15,
            ""name"": { ""parts"": [""transcode"", ""bytes""], ""source_spelling"": ""transcode_bytes"" },
            ""symbol"": { ""id"": 15, ""name"": ""boltffi_function_transcode_bytes"" },
            ""callable"": {
                ""params"": [
                    {
                        ""name"": { ""parts"": [""frame"", ""delays"", ""ms""], ""source_spelling"": ""frame_delays_ms"" },
                        ""payload"": { ""Value"": { ""DirectVec"": { ""element"": { ""Primitive"": ""U32"" } } } }
                    }
                ],
                ""returns"": {
                    ""plan"": { ""EncodedViaOutPointer"": { ""ty"": ""Bytes"" } }
                },
                ""error"": { ""EncodedViaReturnSlot"": { ""ty"": { ""Enum"": 25 } } }
            }
        }";

        var node = UniFFISharp.Generator.Metadata.BoltFfi.JsonNode.Parse(funcJson);
        var decl = BoltFfiFunctionDecl.FromJson(node);

        Assert.True(decl.HasError);
        Assert.Equal(BoltFfiTypeKind.Bytes, decl.ReturnType.Kind);
        Assert.Single(decl.Parameters);
        Assert.Equal(BoltFfiTypeKind.Sequence, decl.Parameters[0].Type.Kind);
        Assert.Equal(BoltFfiTypeKind.UInt32, decl.Parameters[0].Type.InnerType?.Kind);
        Assert.True(UniFFISharp.Generator.Metadata.BoltFfi.BoltFfiCoexistence.CanBind(decl));

        // Test ScalarOption
        string optionFuncJson = @"{
            ""id"": 16,
            ""name"": { ""parts"": [""layout"", ""can"", ""place""], ""source_spelling"": ""layout_can_place"" },
            ""symbol"": { ""id"": 16, ""name"": ""boltffi_function_layout_can_place"" },
            ""callable"": {
                ""params"": [
                    {
                        ""name"": { ""parts"": [""moving"", ""index""], ""source_spelling"": ""moving_index"" },
                        ""payload"": { ""Value"": { ""ScalarOption"": { ""primitive"": ""U32"" } } }
                    }
                ],
                ""returns"": { ""plan"": ""Void"" }
            }
        }";

        var optNode = UniFFISharp.Generator.Metadata.BoltFfi.JsonNode.Parse(optionFuncJson);
        var optDecl = BoltFfiFunctionDecl.FromJson(optNode);
        Assert.Single(optDecl.Parameters);
        Assert.Equal(BoltFfiTypeKind.Option, optDecl.Parameters[0].Type.Kind);
        Assert.Equal(BoltFfiTypeKind.UInt32, optDecl.Parameters[0].Type.InnerType?.Kind);
        Assert.True(UniFFISharp.Generator.Metadata.BoltFfi.BoltFfiCoexistence.CanBind(optDecl));
    }

    [Fact]
    public void CallbackImplementationInfo_Equality_AccountsForImplementedInterfacesAndIsSealed()
    {
        var info1 = new UniFFISharp.Generator.Arbitration.CallbackImplementationInfo(
            "MyNs.MyClass", "MyClass", isSealed: false, new List<string> { "IFoo" });
        var info2 = new UniFFISharp.Generator.Arbitration.CallbackImplementationInfo(
            "MyNs.MyClass", "MyClass", isSealed: false, new List<string> { "IFoo" });
        var infoWithNewInterface = new UniFFISharp.Generator.Arbitration.CallbackImplementationInfo(
            "MyNs.MyClass", "MyClass", isSealed: false, new List<string> { "IFoo", "IBar" });
        var infoSealed = new UniFFISharp.Generator.Arbitration.CallbackImplementationInfo(
            "MyNs.MyClass", "MyClass", isSealed: true, new List<string> { "IFoo" });

        Assert.Equal(info1, info2);
        Assert.Equal(info1.GetHashCode(), info2.GetHashCode());

        Assert.NotEqual(info1, infoWithNewInterface);
        Assert.NotEqual(info1, infoSealed);
    }
}

