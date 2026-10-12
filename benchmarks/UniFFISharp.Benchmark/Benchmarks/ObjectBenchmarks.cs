using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class ObjectBenchmarks
{
    private UniffiBench.BenchCounter _uCounter = null!;
    private DiplomatBench.BenchCounter _dCounter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _uCounter = new UniffiBench.BenchCounter(0);
        _dCounter = DiplomatBench.BenchCounter.New(0);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _uCounter.Dispose();
        _dCounter.Dispose();
    }

    // --- Object Lifecycle (Create & Destroy) ---
    [Benchmark(Baseline = true, Description = "UniFFI: Object Create & Destroy")]
    public void Object_CreateDestroy_UniFFI()
    {
        using var counter = new UniffiBench.BenchCounter(42);
        counter.Increment(1);
    }

    [Benchmark(Description = "Diplomat: Object Create & Destroy")]
    public void Object_CreateDestroy_Diplomat()
    {
        using var counter = DiplomatBench.BenchCounter.New(42);
        counter.Increment(1);
    }

    // --- Object Method Calls (Stateful Dispatch) ---
    [Benchmark(Description = "UniFFI: Object Method Calls")]
    public ulong Object_MethodCalls_UniFFI()
    {
        _uCounter.Increment(5);
        return _uCounter.Get();
    }

    [Benchmark(Description = "Diplomat: Object Method Calls")]
    public ulong Object_MethodCalls_Diplomat()
    {
        _dCounter.Increment(5);
        return _dCounter.Get();
    }
}
