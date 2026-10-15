using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public class AsyncStreamBenchmarks
{
    private const uint StreamItemCount = 50;

    // Pure C# baseline stream generator
    private static async IAsyncEnumerable<int> GenerateCSharpStream(int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }

    // Semantic equivalent Channel-based Async Stream (representing callback / push-based FFI streamed to IAsyncEnumerable)
    private static async IAsyncEnumerable<int> GenerateChannelStream(int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(16)
        {
            SingleWriter = true,
            SingleReader = true
        });

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < count; i++)
            {
                await channel.Writer.WriteAsync(i, ct);
            }
            channel.Writer.Complete();
        }, ct);

        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }

    // =========================================================================
    // 1. Consume 50 Items via `await foreach (var item in stream)`
    // =========================================================================

    [BenchmarkCategory("01_AsyncStream50Items"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): Inferred IAsyncEnumerable")]
    public async Task<int> AsyncStream_UniFFISharp()
    {
        using var engine = new BenchAsyncStreamEngine(StreamItemCount);
        int receivedCount = 0;
        await foreach (var entity in engine)
        {
            receivedCount++;
        }
        return receivedCount;
    }

    [BenchmarkCategory("01_AsyncStream50Items"), Benchmark(Description = "Channel-based Push Stream (Semantic Equiv)")]
    public async Task<int> AsyncStream_ChannelPush()
    {
        int receivedCount = 0;
        await foreach (var item in GenerateChannelStream((int)StreamItemCount))
        {
            receivedCount++;
        }
        return receivedCount;
    }

    [BenchmarkCategory("01_AsyncStream50Items"), Benchmark(Description = "Pure C# Async Generator (Baseline)")]
    public async Task<int> AsyncStream_PureCSharp()
    {
        int receivedCount = 0;
        await foreach (var item in GenerateCSharpStream((int)StreamItemCount))
        {
            receivedCount++;
        }
        return receivedCount;
    }
}
