using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using UniFFISharp.BoltFfi;
using UniFFISharp.Collections;
using UniFFISharp.Generator;
using UniFFISharp.Generator.Arbitration;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;
using UniFFISharp.Generator.PeParser;
using Xunit;
using TypeKind = UniFFISharp.Generator.Metadata.TypeKind;

namespace UniFFISharp.Tests;

public class PerformanceOptimizationTests
{
    [Fact]
    public void ValueWireWriter_StackFastPath_ZeroAllocatesAndRoundTrips()
    {
        static byte[] EncodeSmallWithValueWriter()
        {
            Span<byte> stack = stackalloc byte[256];
            using var writer = new ValueWireWriter(stack);
            writer.WriteBool(true);
            writer.WriteI32(123456);
            writer.WriteF64(3.14159);
            writer.WriteString("hello-fast-wire");
            writer.WriteBytes(new byte[] { 1, 2, 3, 4 });
            return writer.WrittenSpan.ToArray();
        }

        // Warmup JIT
        _ = EncodeSmallWithValueWriter();

        byte[] emptyPayload = [];
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        Span<byte> stack = stackalloc byte[256];
        using (var writer = new ValueWireWriter(stack))
        {
            writer.WriteBool(true);
            writer.WriteI32(123456);
            writer.WriteF64(3.14159);
            writer.WriteString("hello-fast-wire");
            writer.WriteBytes(emptyPayload);
            Assert.True(writer.Length > 0);
            Assert.True(writer.RawBufferSpan.Length >= 256);
        }
        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, allocAfter - allocBefore);

        // Verify parity with WireWriter across >256B payload triggering ArrayPool growth
        string largeStr = new string('x', 900);
        var smallList = new List<string> { "a", "bb", "ccc" };
        var set = new HashSet<string> { "k1", "k2" };

        var legacy = new WireWriter(64);
        legacy.WriteBool(true);
        legacy.WriteI32(-42);
        legacy.WriteString(largeStr);
        legacy.WriteList(smallList, (w, s) => w.WriteString(s));
        legacy.WriteSet(set, (w, s) => w.WriteString(s));

        Span<byte> growStack = stackalloc byte[64];
        using var vwriter = new ValueWireWriter(growStack);
        vwriter.WriteBool(true);
        vwriter.WriteI32(-42);
        vwriter.WriteString(largeStr);
        vwriter.WriteList(smallList, static (ref ValueWireWriter w, string s) => w.WriteString(s));
        vwriter.WriteSet(set, static (ref ValueWireWriter w, string s) => w.WriteString(s));

        Assert.Equal(legacy.ToArray(), vwriter.ToArray());

        var reader = new WireReader(vwriter.WrittenSpan);
        Assert.True(reader.ReadBool());
        Assert.Equal(-42, reader.ReadI32());
        Assert.Equal(largeStr, reader.ReadString());
        Assert.Equal(smallList, reader.ReadStringList());
        var readSet = reader.ReadSet(static (ref WireReader r) => r.ReadString());
        Assert.Equal(set, readSet);
    }

    [Fact]
    public void WireReader_SpecializedListAndArrayDecoders_WorkAndValidateCorruption()
    {
        var writer = new WireWriter();
        var strings = new List<string> { "alpha", "", "gamma-🚀" };
        var nums = new List<long> { 10L, -20L, 3000000000L };
        writer.WriteList(strings, (w, s) => w.WriteString(s));
        writer.WriteList(strings, (w, s) => w.WriteString(s));
        writer.WriteUnmanagedList(nums);

        var reader = new WireReader(writer.WrittenSpan);
        Assert.Equal(strings, reader.ReadStringList());
        Assert.Equal(strings.ToArray(), reader.ReadStringArray());
        Assert.Equal(nums.ToArray(), reader.ReadUnmanagedArray<long>());
        Assert.Equal(writer.Length, reader.Position);

        byte[] corruptCount = [0xFF, 0x00, 0x00, 0x00, 0x01];
        Assert.Throws<EndOfStreamException>(() =>
        {
            var corruptReader = new WireReader(corruptCount);
            _ = corruptReader.ReadStringList();
        });
        Assert.Throws<EndOfStreamException>(() =>
        {
            var corruptReader = new WireReader(corruptCount);
            _ = corruptReader.ReadStringArray();
        });
        Assert.Throws<EndOfStreamException>(() =>
        {
            var corruptReader = new WireReader(corruptCount);
            _ = corruptReader.ReadUnmanagedArray<long>();
        });
    }

    [Fact]
    public void ConcurrentHandleMap_TlsSlotCache_IsolatesMapsAndZeroAllocates()
    {
        var mapA = new ConcurrentHandleMap<string>();
        var mapB = new ConcurrentHandleMap<string>();

        // Warmup thread-local cache
        ulong warm = mapA.Insert("warm");
        Assert.True(mapA.Remove(warm, out _));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            ulong h = mapA.Insert("item");
            Assert.True(mapA.TryGet(h, out var val));
            Assert.Same("item", val);
            Assert.True(mapA.Remove(h));
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);

        // Fill 4 TLS cache slots with mapA, then interleave mapB without evicting mapA slots
        var handlesA = new List<ulong>();
        for (int i = 0; i < 5; i++) handlesA.Add(mapA.Insert($"a{i}"));
        foreach (var h in handlesA) Assert.True(mapA.Remove(h));

        var handlesB = new List<ulong>();
        for (int i = 0; i < 6; i++) handlesB.Add(mapB.Insert($"b{i}"));
        foreach (var h in handlesB) Assert.True(mapB.Remove(h));

        // Reinsert in mapA and mapB and ensure unique valid handles and no slot collisions
        var liveA = new HashSet<ulong>();
        var liveB = new HashSet<ulong>();
        for (int i = 0; i < 16; i++)
        {
            ulong ha = mapA.Insert($"A_{i}");
            ulong hb = mapB.Insert($"B_{i}");
            Assert.True(liveA.Add(ha));
            Assert.True(liveB.Add(hb));
            Assert.Equal($"A_{i}", mapA.Get(ha));
            Assert.Equal($"B_{i}", mapB.Get(hb));
        }
        foreach (var h in liveA) Assert.True(mapA.Remove(h));
        foreach (var h in liveB) Assert.True(mapB.Remove(h));
    }

    [Fact]
    public void MultiCrateGeneratedCode_UsesMethodLevelSkipLocalsInit_WithoutCS0579()
    {
        var ci1 = new ComponentInterface
        {
            CrateName = "alpha_crate",
            Namespace = "MultiCrate.Alpha",
            Functions =
            {
                new FnMetadata
                {
                    Name = "ping_alpha",
                    Inputs = { new FnParamMetadata { Name = "s", Type = UniFFIType.Primitive(TypeKind.String) } },
                    ReturnType = UniFFIType.Primitive(TypeKind.String)
                }
            }
        };
        var ci2 = new ComponentInterface
        {
            CrateName = "beta_crate",
            Namespace = "MultiCrate.Beta",
            Functions =
            {
                new FnMetadata
                {
                    Name = "ping_beta",
                    Inputs = { new FnParamMetadata { Name = "x", Type = UniFFIType.Primitive(TypeKind.Int64) } },
                    ReturnType = UniFFIType.Primitive(TypeKind.Int64)
                }
            }
        };

        var boltEnv1 = BoltFfiMetadataEnvelope.Parse("""
            {"magic":"boltffi.bindings","format":{"major":0,"minor":1},"surface":"Native",
             "package":{"name":{"parts":["alpha_bolt"]}},
             "bindings":{"Native":{"decls":[{"Function":{"name":{"parts":["fast_a"]},"symbol":{"name":"boltffi_a"},"callable":{"receiver":null,"params":[],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}}},"error":"None","execution":"Synchronous"}}]}}}
            """)!;
        var boltEnv2 = BoltFfiMetadataEnvelope.Parse("""
            {"magic":"boltffi.bindings","format":{"major":0,"minor":1},"surface":"Native",
             "package":{"name":{"parts":["beta_bolt"]}},
             "bindings":{"Native":{"decls":[{"Function":{"name":{"parts":["fast_b"]},"symbol":{"name":"boltffi_b"},"callable":{"receiver":null,"params":[],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}}},"error":"None","execution":"Synchronous"}}]}}}
            """)!;

        string u1 = CodeGenerator.Generate(ci1, "alpha.dll");
        string u2 = CodeGenerator.Generate(ci2, "beta.dll");
        string b1 = BoltFfiCodeGenerator.Generate(boltEnv1, "alpha.dll", "MultiCrate.AlphaBolt");
        string b2 = BoltFfiCodeGenerator.Generate(boltEnv2, "beta.dll", "MultiCrate.BetaBolt");

        foreach (string src in new[] { u1, u2, b1, b2 })
        {
            Assert.DoesNotContain("[module: SkipLocalsInit]", src);
            Assert.DoesNotContain("[module: global::System.Runtime.CompilerServices.SkipLocalsInit]", src);
            Assert.Contains("SkipLocalsInit", src);
        }

        var compilation = CreateCompilation(new[] { u1, u2, b1, b2 });
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);
    }

    [Fact]
    public void UniFFI_CodeGenerator_EmitsOptimizedAllocationSize_StaticPrimitives_StateLayout_AndCallbacks()
    {
        var pointType = new UniFFIType { Kind = TypeKind.Record, Name = "point3d" };
        var ci = new ComponentInterface
        {
            CrateName = "perf_crate",
            Namespace = "Perf.Test",
            Records =
            {
                new RecordMetadata
                {
                    Name = "point3d",
                    Fields =
                    {
                        new FieldMetadata { Name = "x", Type = UniFFIType.Primitive(TypeKind.Float64) },
                        new FieldMetadata { Name = "y", Type = UniFFIType.Primitive(TypeKind.Float64) },
                        new FieldMetadata { Name = "z", Type = UniFFIType.Primitive(TypeKind.Float64) }
                    }
                }
            },
            Objects =
            {
                new ObjectMetadata
                {
                    Name = "item_stream",
                    Methods =
                    {
                        new MethodMetadata
                        {
                            Name = "next",
                            IsAsync = true,
                            ReturnType = new UniFFIType { Kind = TypeKind.Option, InnerType = UniFFIType.Primitive(TypeKind.Int32) }
                        }
                    }
                }
            },
            CallbackInterfaces =
            {
                new CallbackInterfaceMetadata
                {
                    Name = "progress_cb",
                    Methods =
                    {
                        new TraitMethodMetadata
                        {
                            Name = "on_step",
                            Inputs = { new FnParamMetadata { Name = "pct", Type = UniFFIType.Primitive(TypeKind.Int32) } }
                        },
                        new TraitMethodMetadata
                        {
                            Name = "compute_async",
                            IsAsync = true,
                            ReturnType = UniFFIType.Primitive(TypeKind.Int32)
                        }
                    }
                }
            },
            Functions =
            {
                new FnMetadata
                {
                    Name = "process",
                    Checksum = 42,
                    Inputs =
                    {
                        new FnParamMetadata { Name = "pts", Type = new UniFFIType { Kind = TypeKind.Sequence, InnerType = pointType } },
                        new FnParamMetadata { Name = "map", Type = new UniFFIType { Kind = TypeKind.Map, KeyType = UniFFIType.Primitive(TypeKind.Int32), ValueType = pointType } },
                        new FnParamMetadata { Name = "opt", Type = new UniFFIType { Kind = TypeKind.Option, InnerType = pointType } }
                    }
                }
            }
        };

        var impls = new[]
        {
            new CallbackImplementationInfo("global::Perf.Test.FastProgress", "FastProgress", true, new List<string> { "IProgressCb" })
        };

        string source = CodeGenerator.Generate(
            ci,
            "perf_crate.dll",
            null,
            new CodeGeneratorOptions { SkipChecksums = false },
            impls);

        // O(1) composite AllocationSize
        Assert.Contains("return value == null ? 4 : checked(4 + value.Count * 24);", source);
        Assert.Contains("return value == null ? 4 : checked(4 + value.Count * 28);", source);
        Assert.Contains("return value == null ? 1 : 25;", source);

        // Static primitive read/write calls and Span sequence loop
        Assert.Contains("FfiConverterFloat64.WriteStatic", source);
        Assert.Contains("FfiConverterFloat64.ReadStatic", source);
        Assert.Contains("CollectionsMarshal.AsSpan(value)", source);
        Assert.Contains("CollectionsMarshal.SetCount(list, count)", source);

        // Compact _state object layout + ValueTask stream helper
        Assert.Contains("private long _state = 4L;", source);
        Assert.DoesNotContain("_wasDestroyed", source);
        Assert.Contains("Interlocked.Or(ref _state, 2L)", source);
        Assert.Contains("private ValueTask<int?> NextAsyncValueTask", source);
        Assert.Contains("UniffiRustCallValueTaskAsync", source);

        // Shared library resolver + checksum early exit
        Assert.Contains("UniffiLibraryResolver.Register(", source);
        Assert.Contains("return;", source);

        // Callback GDV & async without Task.Run or .Cts
        Assert.Contains("if (obj is global::Perf.Test.FastProgress direct0)", source);
        Assert.Contains("if (callbackTask.IsCompleted)", source);
        Assert.Contains("CompleteAsyncCallback_ProgressCb_1", source);
        Assert.DoesNotContain("Task.Run(", source);
        Assert.DoesNotContain("futureHandle.Cts", source);

        // SkipChecksums omits checksum invocation and declaration
        string skipSource = CodeGenerator.Generate(ci, "perf_crate.dll", null, new CodeGeneratorOptions { SkipChecksums = true });
        Assert.DoesNotContain("uniffiCheckApiChecksums();", skipSource);
        Assert.DoesNotContain("uniffi_perf_crate_checksum_func_process", skipSource);
    }

    [Fact]
    public void BoltFFI_CodeGenerator_EmitsValueWireWriter_Set_DecodeList_StaticAsyncLambdas_And_SuppressGCTransition()
    {
        string json = """
            {"magic":"boltffi.bindings","format":{"major":0,"minor":1},"surface":"Native",
             "package":{"name":{"parts":["opt_crate"]}},
             "bindings":{"Native":{"decls":[
                {"Record":{"Encoded":{"name":{"parts":["item"]},"fields":[
                    {"key":{"Named":{"parts":["name"]}},"ty":"String","default":null,"doc":null}
                ],"doc":null}}},
                {"Class":{"name":{"parts":["engine"]},"initializers":[],"methods":[
                    {"name":{"parts":["tick"]},"symbol":{"name":"boltffi_engine_tick"},"callable":{"receiver":"Ref","params":[],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}},"error":"None","execution":"Synchronous"}}
                ],"release_symbol":{"name":"boltffi_engine_free"},"doc":null}},
                {"Function":{"name":{"parts":["fast_add"]},"symbol":{"name":"boltffi_fast_add"},"callable":{"receiver":null,"params":[{"name":{"parts":["a"]},"payload":{"Value":{"Direct":{"ty":"I64"}}}},{"name":{"parts":["b"]},"payload":{"Value":{"Direct":{"ty":"I64"}}}}],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}},"error":"None","execution":"Synchronous"}}},
                {"Function":{"name":{"parts":["normal_add"]},"symbol":{"name":"boltffi_normal_add"},"callable":{"receiver":null,"params":[],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}},"error":"None","execution":"Synchronous"}}},
                {"Function":{"name":{"parts":["send_collections"]},"symbol":{"name":"boltffi_send_collections"},"callable":{"receiver":null,"params":[
                    {"name":{"parts":["tags"]},"payload":{"Value":{"Encoded":{"ty":{"Set":"String"}}}}},
                    {"name":{"parts":["items"]},"payload":{"Value":{"Encoded":{"ty":{"Sequence":{"Record":"Item"}}}}}}
                ],"returns":{"plan":{"EncodedViaReturnSlot":{"ty":{"Sequence":{"Record":"Item"}}}}},"error":"None","execution":"Synchronous"}}},
                {"Function":{"name":{"parts":["fetch_str_async"]},"symbol":{"name":"boltffi_fetch_str_async"},"callable":{"receiver":null,"params":[{"name":{"parts":["input"]},"payload":{"Value":{"Encoded":{"ty":"String"}}}}],"returns":{"plan":{"EncodedViaReturnSlot":{"ty":"String"}}},"error":"None","execution":"Async"}}},
                {"Stream":{"name":{"parts":["events"]},"item":{"ty":"I32"},"protocol":{"subscribe":{"name":"boltffi_sub"},"pop_batch":{"name":"boltffi_pop"},"wait":{"name":"boltffi_wait"},"poll":{"name":"boltffi_poll"},"unsubscribe":{"name":"boltffi_unsub"},"free":{"name":"boltffi_sfree"}},"doc":null}}
             ]}}}
            """;
        var env = BoltFfiMetadataEnvelope.Parse(json)!;
        var methodConfigs = new Dictionary<string, MethodEmissionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["FastAdd"] = new MethodEmissionConfig { EmitMethod = true, IsPartial = true, SuppressGCTransition = true },
            ["Engine.Tick"] = new MethodEmissionConfig { EmitMethod = true, IsPartial = false, SuppressGCTransition = true }
        };

        string source = BoltFfiCodeGenerator.Generate(env, "opt_crate.dll", "Opt.Ns", methodConfigs);

        Assert.Contains("internal void Encode(ref ValueWireWriter writer)", source);
        Assert.Contains("internal void Encode(WireWriter writer)", source);
        Assert.Contains("WriteSet(", source);
        Assert.Contains("DecodeList(ref WireReader reader)", source);
        Assert.Contains("new ValueWireWriter(tags_stack)", source);
        Assert.Contains("RawBufferSpan", source);
        Assert.Contains("static (future, cbData, cb) =>", source);
        Assert.Contains("static (future, cancellationToken) =>", source);
        Assert.Contains("if (retBuf.ptr != 0) NativeMethods.boltffi_free_buf(retBuf);", source);
        Assert.Contains("reader.ReadUnmanagedArray<int>()", source);
        Assert.Contains("batchSize: 64", source);

        Assert.Contains("[SuppressGCTransition]\r\n        [DllImport(NativeLib, EntryPoint = \"boltffi_fast_add\"", source.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.Contains("[SuppressGCTransition]\r\n        [DllImport(NativeLib, EntryPoint = \"boltffi_engine_tick\"", source.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.DoesNotContain("[SuppressGCTransition]\r\n        [DllImport(NativeLib, EntryPoint = \"boltffi_normal_add\"", source.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
        Assert.DoesNotContain("[SuppressGCTransition]\r\n        [DllImport(NativeLib, EntryPoint = \"boltffi_engine_free\"", source.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));
    }

    [Fact]
    public void UniFFIGenerator_IncrementalCaching_And_NonMutatingMetadata_WorkEndToEnd()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "uniffisharp_perf_inc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string mockLibPath = Path.Combine(tempDir, "inc_native.dll");

        try
        {
            string boltJson = """
                {"magic":"boltffi.bindings","format":{"major":0,"minor":1},"surface":"Native",
                 "package":{"name":{"parts":["inc_pkg"]}},
                 "bindings":{"Native":{"decls":[
                    {"Function":{"name":{"parts":["fast_mul"]},"symbol":{"name":"boltffi_fast_mul"},"callable":{"receiver":null,"params":[{"name":{"parts":["a"]},"payload":{"Value":{"Direct":{"ty":"I64"}}}},{"name":{"parts":["b"]},"payload":{"Value":{"Direct":{"ty":"I64"}}}}],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}}},"error":"None","execution":"Synchronous"}},
                    {"Function":{"name":{"parts":["slow_div"]},"symbol":{"name":"boltffi_slow_div"},"callable":{"receiver":null,"params":[],"returns":{"plan":{"DirectViaReturnSlot":{"ty":"I64"}}}},"error":"None","execution":"Synchronous"}}
                 ]}}}
                """;
            byte[] jsonBytes = Encoding.UTF8.GetBytes(boltJson);
            byte[] framed = new byte[16 + jsonBytes.Length];
            Encoding.ASCII.GetBytes("BFFIMD01").CopyTo(framed, 0);
            BinaryPrimitives.WriteUInt64LittleEndian(framed.AsSpan(8, 8), (ulong)jsonBytes.Length);
            jsonBytes.CopyTo(framed, 16);
            File.WriteAllBytes(mockLibPath, framed);

            string userPartial = """
                using System.Runtime.InteropServices;

                namespace IncPkg;

                public static partial class IncPkgMethods
                {
                    [SuppressGCTransition]
                    public static partial long FastMul(long a, long b);
                }
                """;
            string unrelatedSourceV1 = """
                namespace App.Helpers;

                public class MathHelper
                {
                    public int Compute() => 1;
                }
                """;
            string unrelatedSourceV2 = """
                namespace App.Helpers;

                public class MathHelper
                {
                    public int Compute() => 999;
                }
                """;

            var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
            var treePartial = CSharpSyntaxTree.ParseText(userPartial, parseOptions, path: "IncPkgMethods.cs");
            var treeUnrelatedV1 = CSharpSyntaxTree.ParseText(unrelatedSourceV1, parseOptions, path: "MathHelper.cs");

            var compilationV1 = CreateCompilation(Array.Empty<string>(), new[] { treePartial, treeUnrelatedV1 });

            var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(
                generators: new[] { new UniFFIGenerator().AsSourceGenerator() },
                additionalTexts: new AdditionalText[] { new TestAdditionalText(Path.Combine(tempDir, "target.uniffi.stamp"), mockLibPath) },
                optionsProvider: new TestOptionsProvider(new Dictionary<string, string>
                {
                    ["build_metadata.AdditionalFiles.UniFFI"] = "true",
                    ["build_property.UniFFIEngine"] = "Dual",
                    ["build_property.UniFFISkipChecksums"] = "true"
                }),
                parseOptions: parseOptions,
                driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

            driver = driver.RunGenerators(compilationV1);
            var res1 = driver.GetRunResult().Results[0];
            string genText = res1.GeneratedSources.Single(s => s.HintName.StartsWith("BoltFFIBindings.")).SourceText.ToString();
            Assert.Contains("[SuppressGCTransition]", genText);
            string normalizedGen = genText.Replace("\r\n", "\n");
            Assert.Contains("[SuppressGCTransition]\n        [DllImport(NativeLib, EntryPoint = \"boltffi_fast_mul\"", normalizedGen);
            Assert.DoesNotContain("[SuppressGCTransition]\n        [DllImport(NativeLib, EntryPoint = \"boltffi_slow_div\"", normalizedGen);

            // Modify unrelated C# class body and re-run generator: final output step must be Cached
            var treeUnrelatedV2 = CSharpSyntaxTree.ParseText(unrelatedSourceV2, parseOptions, path: "MathHelper.cs");
            var compilationV2 = compilationV1.ReplaceSyntaxTree(treeUnrelatedV1, treeUnrelatedV2);
            driver = driver.RunGenerators(compilationV2);
            var res2 = driver.GetRunResult().Results[0];
            Assert.NotEmpty(res2.TrackedOutputSteps);
            foreach (var step in res2.TrackedOutputSteps.SelectMany(kv => kv.Value))
            {
                Assert.True(step.Outputs.All(o => o.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged));
            }

            // Metadata immutability test with ResolvedNamespace & SuppressTypeDefinitions & declsOverride
            var cachedEnv = BoltFfiMetadataExtractor.ExtractAll(mockLibPath).Single();
            var filtered = new List<BoltFfiDecl>(cachedEnv.Bindings.Native!.Decls);
            filtered.RemoveAll(d => d.DeclName == "SlowDiv");
            _ = BoltFfiCodeGenerator.Generate(cachedEnv, "inc_native.dll", "IncPkg", declsOverride: filtered);
            Assert.Equal(2, cachedEnv.Bindings.Native.Decls.Count);

            var cachedCi = new ComponentInterface
            {
                CrateName = "orig_crate",
                Namespace = "orig_ns",
                Objects = { new ObjectMetadata { Name = "ObjA" } },
                CallbackInterfaces = { new CallbackInterfaceMetadata { Name = "CbA" } }
            };
            _ = CodeGenerator.Generate(cachedCi, "inc_native.dll", null, new CodeGeneratorOptions
            {
                ResolvedNamespace = "Custom.Overridden",
                SuppressTypeDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ObjA", "CbA" }
            });
            Assert.Equal("orig_ns", cachedCi.Namespace);
            Assert.Single(cachedCi.Objects);
            Assert.Single(cachedCi.CallbackInterfaces);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static CSharpCompilation CreateCompilation(IEnumerable<string> generatedSources, IEnumerable<SyntaxTree>? existingTrees = null)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var trees = new List<SyntaxTree>();
        if (existingTrees != null)
        {
            trees.AddRange(existingTrees);
        }
        foreach (string src in generatedSources)
        {
            trees.Add(CSharpSyntaxTree.ParseText(src, parseOptions));
        }

        var refs = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (string asm in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(asm))
                {
                    refs.Add(MetadataReference.CreateFromFile(asm));
                }
            }
        }
        refs.Add(MetadataReference.CreateFromFile(typeof(ValueWireWriter).Assembly.Location));

        return CSharpCompilation.Create(
            "PerfTestAssembly",
            syntaxTrees: trees,
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private sealed class TestAdditionalText : AdditionalText
    {
        private readonly string _path;
        private readonly SourceText _text;

        public TestAdditionalText(string path, string content)
        {
            _path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }

        public override string Path => _path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _global;
        public TestOptionsProvider(Dictionary<string, string> props) => _global = new DictOptions(props);
        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;

        private sealed class DictOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _props;
            public DictOptions(Dictionary<string, string> props) => _props = props;
            public override bool TryGetValue(string key, out string value) => _props.TryGetValue(key, out value!);
        }
    }
}
