using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using InteroptopusBench;
using UniffiBench;

using UniFfiBench = UniffiBench.UniffiBenchMethods;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public class AsyncMethodBenchmarks
{
    private BenchAsyncService _iAsyncService = null!;

    [GlobalSetup]
    public void Setup()
    {
        _iAsyncService = BenchAsyncService.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _iAsyncService.Dispose();
    }

    // CsBindgen semantic equivalent using TaskCompletionSource & GCHandle
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void OnCsAsyncComplete(void* context, ulong result)
    {
        var handle = GCHandle.FromIntPtr((nint)context);
        var tcs = (TaskCompletionSource<ulong>)handle.Target!;
        handle.Free();
        tcs.TrySetResult(result);
    }

    private static unsafe Task<ulong> CsAsyncCompute(ulong input, ulong delayMicros)
    {
        var tcs = new TaskCompletionSource<ulong>(TaskCreationOptions.RunContinuationsAsynchronously);
        var handle = GCHandle.Alloc(tcs);
        delegate* unmanaged[Cdecl]<void*, ulong, void> cb = &OnCsAsyncComplete;
        CsBindgenNative.cs_bench_async_compute(input, delayMicros, (void*)GCHandle.ToIntPtr(handle), cb);
        return tcs.Task;
    }

    // =========================================================================
    // 1. Single Async Invocation (Zero Delay / Immediate Yield)
    // =========================================================================

    [BenchmarkCategory("01_SingleAsyncImmediate"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): Rust Task Poll")]
    public async Task<ulong> SingleAsyncImmediate_UniFFI()
    {
        return await UniFfiBench.BenchAsyncComputeAsync(42, 0);
    }

    [BenchmarkCategory("01_SingleAsyncImmediate"), Benchmark(Description = "Interoptopus (Tokio Native Task)")]
    public async Task<ulong> SingleAsyncImmediate_Interoptopus()
    {
        return await _iAsyncService.Compute(42, 0);
    }

    [BenchmarkCategory("01_SingleAsyncImmediate"), Benchmark(Description = "CsBindgen (Equiv TaskCompletionSource)")]
    public async Task<ulong> SingleAsyncImmediate_CsBindgen()
    {
        return await CsAsyncCompute(42, 0);
    }

    // =========================================================================
    // 2. Concurrent Async Fanout (20 Tasks in Parallel via Task.WhenAll)
    // =========================================================================

    [BenchmarkCategory("02_ConcurrentFanout20"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): Rust Task Poll")]
    public async Task<ulong> ConcurrentFanout20_UniFFI()
    {
        var tasks = new Task<ulong>[20];
        for (ulong i = 0; i < 20; i++)
        {
            tasks[i] = UniFfiBench.BenchAsyncComputeAsync(i, 0);
        }
        var results = await Task.WhenAll(tasks);
        ulong sum = 0;
        for (int i = 0; i < results.Length; i++) sum += results[i];
        return sum;
    }

    [BenchmarkCategory("02_ConcurrentFanout20"), Benchmark(Description = "Interoptopus (Tokio Native Task)")]
    public async Task<ulong> ConcurrentFanout20_Interoptopus()
    {
        var tasks = new Task<ulong>[20];
        for (ulong i = 0; i < 20; i++)
        {
            tasks[i] = _iAsyncService.Compute(i, 0);
        }
        var results = await Task.WhenAll(tasks);
        ulong sum = 0;
        for (int i = 0; i < results.Length; i++) sum += results[i];
        return sum;
    }

    [BenchmarkCategory("02_ConcurrentFanout20"), Benchmark(Description = "CsBindgen (Equiv TaskCompletionSource)")]
    public async Task<ulong> ConcurrentFanout20_CsBindgen()
    {
        var tasks = new Task<ulong>[20];
        for (ulong i = 0; i < 20; i++)
        {
            tasks[i] = CsAsyncCompute(i, 0);
        }
        var results = await Task.WhenAll(tasks);
        ulong sum = 0;
        for (int i = 0; i < results.Length; i++) sum += results[i];
        return sum;
    }
}
