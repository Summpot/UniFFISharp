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
using GeneratorEngine = UniFFISharp.Generator.Arbitration.InteropEngine;
using TypeKind = UniFFISharp.Generator.Metadata.TypeKind;

namespace UniFFISharp.Tests;

public class SignatureArbitrationAndPartialMethodTests
{
    [Fact]
    public void SelectBestEngine_InfalliblePureScalar_SelectsBoltFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "add",
            ReturnType = UniFFIType.Primitive(TypeKind.Int64),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "a", Type = UniFFIType.Primitive(TypeKind.Int64) },
                new() { Name = "b", Type = UniFFIType.Primitive(TypeKind.Int64) }
            },
            Throws = null
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "add" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "a", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 } },
                new() { Name = "b", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 } }
            },
            HasError = false
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_FalliblePureScalar_SelectsUniFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "parse_positive",
            ReturnType = UniFFIType.Primitive(TypeKind.Int64),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "value", Type = UniFFIType.Primitive(TypeKind.Int64) }
            },
            Throws = new UniFFIType { Kind = TypeKind.Enum, Name = "BenchError" }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "parse", "positive" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "value", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 } }
            },
            HasError = true
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.UniFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_DirectBlittableStruct_SelectsBoltFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "point3d_roundtrip",
            ReturnType = new UniFFIType { Kind = TypeKind.Record, Name = "Point3D" },
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "p", Type = new UniFFIType { Kind = TypeKind.Record, Name = "Point3D" } }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "point3d", "roundtrip" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Record, IsDirectRecord = true, CustomTypeName = "Point3D" },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "p", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Record, IsDirectRecord = true, CustomTypeName = "Point3D" } }
            }
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_ClassHandle_SelectsBoltFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "use_counter",
            ReturnType = UniFFIType.Primitive(TypeKind.Int32),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "counter", Type = new UniFFIType { Kind = TypeKind.Interface, Name = "BenchCounter" } }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "use", "counter" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Int32 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "counter", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Class, CustomTypeName = "BenchCounter" } }
            }
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_ByteSlice_SelectsBoltFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "vec_u8_sum",
            ReturnType = UniFFIType.Primitive(TypeKind.UInt64),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "data", Type = UniFFIType.Primitive(TypeKind.Bytes) }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "vec", "u8", "sum" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.UInt64 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "data", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Bytes } }
            }
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_StringInput_SelectsBoltFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "string_len",
            ReturnType = UniFFIType.Primitive(TypeKind.Int32),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "s", Type = UniFFIType.Primitive(TypeKind.String) }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "string", "len" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Int32 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "s", Type = new BoltFfiType { Kind = BoltFfiTypeKind.String } }
            }
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_StringEcho_SelectsUniFFI()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "echo_string",
            ReturnType = UniFFIType.Primitive(TypeKind.String),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "s", Type = UniFFIType.Primitive(TypeKind.String) }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "echo", "string" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.String },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "s", Type = new BoltFfiType { Kind = BoltFfiTypeKind.String } }
            }
        };

        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn);
        Assert.Equal(GeneratorEngine.UniFFI, winner);
    }

    [Fact]
    public void SelectBestEngine_UserOverride_OverridesDefault()
    {
        var uniffiFn = new FnMetadata
        {
            Name = "add",
            ReturnType = UniFFIType.Primitive(TypeKind.Int64),
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "a", Type = UniFFIType.Primitive(TypeKind.Int64) }
            }
        };

        var boltFn = new BoltFfiFunctionDecl
        {
            Name = new BoltFfiCanonicalName { Parts = new List<string> { "add" } },
            ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 },
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "a", Type = new BoltFfiType { Kind = BoltFfiTypeKind.Int64 } }
            }
        };

        // Default would be BoltFFI, but user override forces UniFFI
        var winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn, GeneratorEngine.UniFFI);
        Assert.Equal(GeneratorEngine.UniFFI, winner);

        // Fallible scalar default would be UniFFI, but user override forces BoltFFI
        uniffiFn.Throws = new UniFFIType { Kind = TypeKind.Enum };
        boltFn.HasError = true;
        winner = InteropArbitrator.SelectBestEngine(uniffiFn, boltFn, GeneratorEngine.BoltFFI);
        Assert.Equal(GeneratorEngine.BoltFFI, winner);
    }

    [Fact]
    public void IncrementalGenerator_ArbitratesAndSelectsFastestEnginePerSignature()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "uniffisharp_arb_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string mockLibPath = Path.Combine(tempDir, "mock_native.dll");

        try
        {
            // Create Bolt envelope with 2 functions: 'add' (infallible scalar) and 'parse_positive' (fallible scalar)
            string boltJson = @"
            {
                ""magic"": ""boltffi.bindings"",
                ""format"": { ""major"": 0, ""minor"": 1 },
                ""surface"": ""Native"",
                ""package"": {
                    ""name"": { ""parts"": [""sample"", ""crate""] }
                },
                ""bindings"": {
                    ""Native"": {
                        ""package"": {
                            ""name"": { ""parts"": [""sample"", ""crate""] }
                        },
                        ""decls"": [
                            {
                                ""Function"": {
                                    ""name"": { ""parts"": [""add""] },
                                    ""symbol"": ""boltffi_sample_crate_add"",
                                    ""callable"": {
                                        ""execution"": ""Sync"",
                                        ""returns"": { ""plan"": { ""DirectViaReturnSlot"": { ""Primitive"": ""I64"" } } },
                                        ""params"": [
                                            { ""name"": { ""parts"": [""a""] }, ""plan"": { ""Direct"": { ""Primitive"": ""I64"" } } },
                                            { ""name"": { ""parts"": [""b""] }, ""plan"": { ""Direct"": { ""Primitive"": ""I64"" } } }
                                        ]
                                    }
                                }
                            },
                            {
                                ""Function"": {
                                    ""name"": { ""parts"": [""parse"", ""positive""] },
                                    ""symbol"": ""boltffi_sample_crate_parse_positive"",
                                    ""callable"": {
                                        ""execution"": ""Sync"",
                                        ""returns"": { ""plan"": { ""DirectViaReturnSlot"": { ""Primitive"": ""I64"" } } },
                                        ""params"": [
                                            { ""name"": { ""parts"": [""val""] }, ""plan"": { ""Direct"": { ""Primitive"": ""I64"" } } }
                                        ],
                                        ""error"": { ""kind"": ""BoltError"" }
                                    }
                                }
                            }
                        ]
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
                ["build_property.UniFFIEngine"] = "Dual",
                ["build_property.UniFFIEnginePrefer"] = "Auto"
            };
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(options);

            var compilation = CSharpCompilation.Create("TestAssembly",
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(UniFFIPreferenceAttribute).Assembly.Location)
                });

            driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            driver = driver.RunGenerators(compilation);
            var runResult = driver.GetRunResult();

            Assert.Contains(runResult.GeneratedTrees, t => t.FilePath.Contains("BoltFFIBindings.sample_crate.g.cs"));
            var boltTree = runResult.GeneratedTrees.First(t => t.FilePath.Contains("BoltFFIBindings.sample_crate.g.cs"));
            string boltText = boltTree.GetText().ToString();

            // Add is infallible scalar -> BoltFFI emitted it
            Assert.Contains("public static long Add(long a, long b)", boltText);
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
    public void IncrementalGenerator_UserPartialMethodOverride_EmitsPartialImplementation()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "uniffisharp_partial_test_" + Guid.NewGuid().ToString("N"));
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
                    ""name"": { ""parts"": [""sample"", ""math""] }
                },
                ""bindings"": {
                    ""Native"": {
                        ""package"": {
                            ""name"": { ""parts"": [""sample"", ""math""] }
                        },
                        ""decls"": [
                            {
                                ""Function"": {
                                    ""name"": { ""parts"": [""compute""] },
                                    ""symbol"": ""boltffi_sample_math_compute"",
                                    ""callable"": {
                                        ""execution"": ""Sync"",
                                        ""returns"": { ""plan"": { ""DirectViaReturnSlot"": { ""Primitive"": ""I32"" } } },
                                        ""params"": [
                                            { ""name"": { ""parts"": [""x""] }, ""plan"": { ""Direct"": { ""Primitive"": ""I32"" } } }
                                        ]
                                    }
                                }
                            }
                        ]
                    }
                }
            }";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(boltJson);
            byte[] framed = new byte[8 + 8 + jsonBytes.Length];
            Encoding.ASCII.GetBytes("BFFIMD01").CopyTo(framed, 0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(framed.AsSpan(8, 8), (ulong)jsonBytes.Length);
            jsonBytes.CopyTo(framed, 16);
            File.WriteAllBytes(mockLibPath, framed);

            // User declared partial method with [PreferBoltFFI]
            string userSource = @"
            using UniFFISharp;

            namespace SampleMath;

            public static partial class SampleMathMethods
            {
                [PreferBoltFFI]
                public static partial int Compute(int x);
            }
            ";
            var userTree = CSharpSyntaxTree.ParseText(userSource);

            var compilation = CSharpCompilation.Create("TestAssembly",
                syntaxTrees: new[] { userTree },
                references: new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(UniFFIPreferenceAttribute).Assembly.Location)
                });

            var generator = new UniFFIGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

            string stampPath = Path.Combine(tempDir, "target.uniffi.stamp");
            var additionalText = new TestAdditionalText(stampPath, mockLibPath);

            var options = new Dictionary<string, string>
            {
                ["build_metadata.AdditionalFiles.UniFFI"] = "true",
                ["build_property.UniFFIEngine"] = "Dual",
                ["build_property.UniFFIEnginePrefer"] = "Auto"
            };
            var optionsProvider = new TestAnalyzerConfigOptionsProvider(options);

            driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(additionalText));
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            driver = driver.RunGenerators(compilation);
            var runResult = driver.GetRunResult();

            Assert.Contains(runResult.GeneratedTrees, t => t.FilePath.Contains("BoltFFIBindings.sample_math.g.cs"));
            var boltTree = runResult.GeneratedTrees.First(t => t.FilePath.Contains("BoltFFIBindings.sample_math.g.cs"));
            string boltText = boltTree.GetText().ToString();

            // BoltFFI detected the partial method declaration and emitted 'public static partial int Compute(int x)'
            Assert.Contains("public static partial int Compute(int x)", boltText);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
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
}
