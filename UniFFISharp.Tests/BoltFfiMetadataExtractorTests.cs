using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using UniFFISharp.Generator.Metadata.BoltFfi;
using UniFFISharp.Generator.PeParser;
using Xunit;

namespace UniFFISharp.Tests;

public class BoltFfiMetadataExtractorTests
{
    [Fact]
    public void MiniJson_ParsesBasicJson()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""contract_hash"": 123456789,
            ""package"": {
                ""name"": { ""parts"": [""demo"", ""crate""] },
                ""version"": ""0.1.0""
            }
        }";

        var node = JsonNode.Parse(json);
        Assert.NotNull(node);
        Assert.Equal("boltffi.bindings", node["magic"]?.AsString());
        Assert.Equal(0, node["format"]?["major"]?.AsInt64());
        Assert.Equal(1, node["format"]?["minor"]?.AsInt64());
        Assert.Equal("Native", node["surface"]?.AsString());
        Assert.Equal("0.1.0", node["package"]?["version"]?.AsString());

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);
        Assert.True(env!.IsValid);
        Assert.Equal("DemoCrate", env.Package.Name.ToPascalCase());
        Assert.Equal("demo_crate", env.Package.PackageName);
    }

    [Fact]
    public void BoltFfiMetadataEnvelope_ParsesRecordsAndFunctions()
    {
        string json = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""format"": { ""major"": 0, ""minor"": 1 },
            ""surface"": ""Native"",
            ""package"": {
                ""name"": { ""parts"": [""math""] }
            },
            ""bindings"": {
                ""Native"": {
                    ""package"": {
                        ""name"": { ""parts"": [""math""] }
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
                            ""Function"": {
                                ""name"": { ""parts"": [""add"", ""points""] },
                                ""symbol"": ""boltffi_function_math_add_points"",
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
                                            ""name"": { ""parts"": [""a""] },
                                            ""plan"": {
                                                ""Direct"": { ""Record"": ""Point"" }
                                            }
                                        },
                                        {
                                            ""name"": { ""parts"": [""b""] },
                                            ""plan"": {
                                                ""Direct"": { ""Record"": ""Point"" }
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

        var env = BoltFfiMetadataEnvelope.Parse(json);
        Assert.NotNull(env);
        Assert.True(env!.IsValid);
        Assert.Equal("math", env.Package.PackageName);

        var native = env.Bindings.Native;
        Assert.NotNull(native);
        Assert.Equal(2, native!.Decls.Count);

        var recordDecl = native.Decls[0];
        Assert.Equal(BoltFfiDeclKind.Record, recordDecl.Kind);
        Assert.NotNull(recordDecl.Record);
        Assert.True(recordDecl.Record!.IsDirect);
        Assert.Equal("Point", recordDecl.Record.Name.ToPascalCase());
        Assert.Equal(2, recordDecl.Record.Fields.Count);
        Assert.Equal("x", recordDecl.Record.Fields[0].Name);
        Assert.Equal(BoltFfiTypeKind.Float64, recordDecl.Record.Fields[0].Type.Kind);

        var funcDecl = native.Decls[1];
        Assert.Equal(BoltFfiDeclKind.Function, funcDecl.Kind);
        Assert.NotNull(funcDecl.Function);
        Assert.Equal("AddPoints", funcDecl.Function!.Name.ToPascalCase());
        Assert.Equal("boltffi_function_math_add_points", funcDecl.Function.Symbol);
        Assert.False(funcDecl.Function.IsAsync);
        Assert.Equal(2, funcDecl.Function.Parameters.Count);
        Assert.Equal("a", funcDecl.Function.Parameters[0].Name);
        Assert.Equal("b", funcDecl.Function.Parameters[1].Name);
    }

    [Fact]
    public void BoltFfiMetadataExtractor_DecodesFramedSectionBytes()
    {
        string sampleJson = @"
        {
            ""magic"": ""boltffi.bindings"",
            ""surface"": ""Native"",
            ""package"": { ""name"": { ""parts"": [""geo""] } },
            ""bindings"": {
                ""Native"": {
                    ""package"": { ""name"": { ""parts"": [""geo""] } },
                    ""decls"": []
                }
            }
        }";

        byte[] jsonBytes = Encoding.UTF8.GetBytes(sampleJson);
        byte[] sectionBytes = new byte[16 + jsonBytes.Length];

        // Magic "BFFIMD01"
        Encoding.ASCII.GetBytes("BFFIMD01").CopyTo(sectionBytes, 0);
        // Length
        BinaryPrimitives.WriteUInt64LittleEndian(sectionBytes.AsSpan(8, 8), (ulong)jsonBytes.Length);
        // Payload
        jsonBytes.CopyTo(sectionBytes, 16);

        var envelopes = BoltFfiMetadataExtractor.DecodeSectionBytes(sectionBytes);
        Assert.Single(envelopes);
        Assert.Equal("boltffi.bindings", envelopes[0].Magic);
        Assert.Equal("geo", envelopes[0].Package.PackageName);
    }

    [Fact]
    public void BoltFfiMetadataExtractor_ExtractsFromRealDll()
    {
        string dllPath = @"C:\Users\Summp\.gemini\antigravity\brain\9b5fc636-03e2-416c-abef-16eb7bb978cc\scratch\..\UniFFISharp-Diplomat-Benchmark\target\release\uniffi_bench.dll";
        if (!System.IO.File.Exists(dllPath)) return;
        var envs = BoltFfiMetadataExtractor.ExtractAll(dllPath);
        string code = UniFFISharp.Generator.Codegen.BoltFfiCodeGenerator.Generate(envs[0], "test_bolt.dll", "TestBolt");
        System.IO.File.WriteAllText(@"C:\Users\Summp\.gemini\antigravity\brain\9b5fc636-03e2-416c-abef-16eb7bb978cc\scratch\generated_bolt.cs", code);
    }
}
