using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using UniffiBench;
using UniffiBench.Bolt;

using UniFfiBench = UniffiBench.UniffiBenchMethods;
using BoltBench = UniffiBench.Bolt.UniffiBenchMethods;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public class TaggedUnionAstBenchmarks
{
    private UniffiBench.BenchTaggedUnion _uAst = null!;
    private UniffiBench.Bolt.BenchTaggedUnion _bAst = null!;

    // Pure C# baseline for comparison
    public abstract record CsAstNode
    {
        public sealed record Empty : CsAstNode;
        public sealed record Integer(long Value) : CsAstNode;
        public sealed record Text(string Content) : CsAstNode;
        public sealed record Pair(string Key, string Value) : CsAstNode;
        public sealed record Nested(List<CsAstNode> Children) : CsAstNode;
    }

    private CsAstNode _csAst = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Construct a deep AST tree of height 3 with various variants
        _uAst = new UniffiBench.BenchTaggedUnion.Nested(new List<UniffiBench.BenchTaggedUnion>
        {
            new UniffiBench.BenchTaggedUnion.Integer(42),
            new UniffiBench.BenchTaggedUnion.Text("ast-leaf-text-node"),
            new UniffiBench.BenchTaggedUnion.Pair("metadata_key", "metadata_val"),
            new UniffiBench.BenchTaggedUnion.Nested(new List<UniffiBench.BenchTaggedUnion>
            {
                new UniffiBench.BenchTaggedUnion.Integer(100),
                new UniffiBench.BenchTaggedUnion.Text("inner-child-node"),
                new UniffiBench.BenchTaggedUnion.Empty()
            })
        });

        _bAst = new UniffiBench.Bolt.BenchTaggedUnion.Nested(new List<UniffiBench.Bolt.BenchTaggedUnion>
        {
            new UniffiBench.Bolt.BenchTaggedUnion.Integer(42),
            new UniffiBench.Bolt.BenchTaggedUnion.Text("ast-leaf-text-node"),
            new UniffiBench.Bolt.BenchTaggedUnion.Pair("metadata_key", "metadata_val"),
            new UniffiBench.Bolt.BenchTaggedUnion.Nested(new List<UniffiBench.Bolt.BenchTaggedUnion>
            {
                new UniffiBench.Bolt.BenchTaggedUnion.Integer(100),
                new UniffiBench.Bolt.BenchTaggedUnion.Text("inner-child-node"),
                new UniffiBench.Bolt.BenchTaggedUnion.Empty()
            })
        });

        _csAst = new CsAstNode.Nested(new List<CsAstNode>
        {
            new CsAstNode.Integer(42),
            new CsAstNode.Text("ast-leaf-text-node"),
            new CsAstNode.Pair("metadata_key", "metadata_val"),
            new CsAstNode.Nested(new List<CsAstNode>
            {
                new CsAstNode.Integer(100),
                new CsAstNode.Text("inner-child-node"),
                new CsAstNode.Empty()
            })
        });
    }

    private static ulong EvalCsAst(CsAstNode node) => node switch
    {
        CsAstNode.Empty => 0,
        CsAstNode.Integer i => (ulong)Math.Abs(i.Value),
        CsAstNode.Text t => (ulong)t.Content.Length,
        CsAstNode.Pair p => (ulong)(p.Key.Length + p.Value.Length),
        CsAstNode.Nested n => (ulong)n.Children.Sum(c => (long)EvalCsAst(c)),
        _ => 0
    };

    [BenchmarkCategory("01_EvalTaggedUnionAst"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): RustBuffer Wire")]
    public ulong EvalAst_UniFFI()
    {
        return UniFfiBench.BenchEvalTaggedUnion(_uAst);
    }

    [BenchmarkCategory("01_EvalTaggedUnionAst"), Benchmark(Description = "UniFFISharp (BoltFFI): Direct Wire")]
    public ulong EvalAst_BoltFFI()
    {
        return BoltBench.BenchEvalTaggedUnion(_bAst);
    }

    [BenchmarkCategory("01_EvalTaggedUnionAst"), Benchmark(Description = "Pure C# Managed Evaluation")]
    public ulong EvalAst_PureCSharp()
    {
        return EvalCsAst(_csAst);
    }
}
