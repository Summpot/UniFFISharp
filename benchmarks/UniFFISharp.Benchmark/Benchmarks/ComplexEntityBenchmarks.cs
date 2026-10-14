using System;
using System.Collections.Generic;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using UniffiBench;
using UniffiBench.Bolt;

using ClassicBench = UniffiBench.UniffiBenchMethods;
using BoltBench = UniffiBench.Bolt.UniffiBenchMethods;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public unsafe class ComplexEntityBenchmarks
{
    private List<UniffiBench.BenchComplexEntity> _uList = null!;
    private List<UniffiBench.Bolt.BenchComplexEntity> _bList = null!;

    // Semantic equivalent for CsBindgen / raw C-ABI
    public sealed class EquivalentEntity
    {
        public long Id { get; set; }
        public string Title { get; set; } = "";
        public int Status { get; set; }
        public List<EquivalentTag> Tags { get; set; } = new();
        public List<string> Attributes { get; set; } = new();
        public string? MetadataJson { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class EquivalentTag
    {
        public long TagId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    private List<EquivalentEntity> _eqList = null!;
    private byte[] _jsonBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        const int count = 50;
        _uList = new List<UniffiBench.BenchComplexEntity>(count);
        _bList = new List<UniffiBench.Bolt.BenchComplexEntity>(count);
        _eqList = new List<EquivalentEntity>(count);

        for (int i = 0; i < count; i++)
        {
            var uTags = new List<UniffiBench.BenchNestedTag>
            {
                new(1, "tag_alpha", "first tag description"),
                new(2, "tag_beta", null)
            };
            var bTags = new List<UniffiBench.Bolt.BenchNestedTag>
            {
                new(1, "tag_alpha", "first tag description"),
                new(2, "tag_beta", null)
            };
            var eqTags = new List<EquivalentTag>
            {
                new() { TagId = 1, Name = "tag_alpha", Description = "first tag description" },
                new() { TagId = 2, Name = "tag_beta", Description = null }
            };

            var attrs = new List<string> { "attr_1", "attr_2", "attr_3" };

            _uList.Add(new UniffiBench.BenchComplexEntity(
                i, $"Entity Title #{i}", 1, uTags, attrs, "{\"key\":\"value\"}", true));

            _bList.Add(new UniffiBench.Bolt.BenchComplexEntity(
                i, $"Entity Title #{i}", 1, bTags, attrs, "{\"key\":\"value\"}", true));

            _eqList.Add(new EquivalentEntity
            {
                Id = i,
                Title = $"Entity Title #{i}",
                Status = 1,
                Tags = eqTags,
                Attributes = attrs,
                MetadataJson = "{\"key\":\"value\"}",
                IsActive = true
            });
        }

        _jsonBytes = JsonSerializer.SerializeToUtf8Bytes(_eqList);
    }

    // =========================================================================
    // 1. Pass 50 Complex Nested Entities (Host -> Rust)
    // =========================================================================

    [BenchmarkCategory("01_PassComplexEntities50"), Benchmark(Baseline = true, Description = "UniFFI (Classic RustBuffer)")]
    public ulong PassComplexEntities_UniFFI()
    {
        return ClassicBench.BenchPassComplexEntities(_uList);
    }

    [BenchmarkCategory("01_PassComplexEntities50"), Benchmark(Description = "UniFFISharp (BoltFFI Direct Wire)")]
    public ulong PassComplexEntities_BoltFFI()
    {
        return BoltBench.BenchPassComplexEntities(_bList);
    }

    [BenchmarkCategory("01_PassComplexEntities50"), Benchmark(Description = "CsBindgen (Equiv Json Wire)")]
    public ulong PassComplexEntities_CsBindgen()
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(_eqList);
        fixed (byte* p = bytes)
        {
            return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)bytes.Length);
        }
    }

    // =========================================================================
    // 2. Query 50 Complex Nested Entities (Rust -> Host)
    // =========================================================================

    [BenchmarkCategory("02_QueryComplexEntities50"), Benchmark(Baseline = true, Description = "UniFFI (Classic RustBuffer)")]
    public int QueryComplexEntities_UniFFI()
    {
        var list = ClassicBench.BenchQueryComplexEntities(50);
        return list.Count;
    }

    [BenchmarkCategory("02_QueryComplexEntities50"), Benchmark(Description = "UniFFISharp (BoltFFI Direct Wire)")]
    public int QueryComplexEntities_BoltFFI()
    {
        var list = BoltBench.BenchQueryComplexEntities(50);
        return list.Count;
    }

    [BenchmarkCategory("02_QueryComplexEntities50"), Benchmark(Description = "CsBindgen (Equiv Json Wire)")]
    public int QueryComplexEntities_CsBindgen()
    {
        var buf = CsBindgenNative.cs_bench_generate_bytes((nuint)_jsonBytes.Length);
        try
        {
            var span = new ReadOnlySpan<byte>(buf.ptr, (int)buf.len);
            var deserialized = JsonSerializer.Deserialize<List<EquivalentEntity>>(span);
            return deserialized?.Count ?? 0;
        }
        finally
        {
            CsBindgenNative.cs_bench_free_byte_buffer(buf);
        }
    }
}
