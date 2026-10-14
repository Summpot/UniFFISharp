using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class PrimitiveBenchmarks
{
    private const int LoopCount = 1000;

    [Benchmark(Baseline = true, Description = "UniFFI: NoOp")]
    public void NoOp_UniFFI()
    {
        UniffiBenchMethods.BenchNoop();
    }

    [Benchmark(Description = "Diplomat: NoOp")]
    public void NoOp_Diplomat()
    {
        BenchOps.BenchNoop();
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): NoOp")]
    public void NoOp_BoltFFI()
    {
        UniffiBench.Bolt.UniffiBenchMethods.BenchNoop();
    }

    [Benchmark(Description = "UniFFI: AddInt64")]
    public long AddInt64_UniFFI()
    {
        return UniffiBenchMethods.BenchAddI64(123456789, 987654321);
    }

    [Benchmark(Description = "Diplomat: AddInt64")]
    public long AddInt64_Diplomat()
    {
        return BenchOps.BenchAddI64(123456789, 987654321);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): AddInt64")]
    public long AddInt64_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchAddI64(123456789, 987654321);
    }

    [Benchmark(Description = "UniFFI: ScalarMath (5 args)")]
    public double ScalarMath_UniFFI()
    {
        return UniffiBenchMethods.BenchScalarMath(123, 456789, 12.5f, 98.75, true);
    }

    [Benchmark(Description = "Diplomat: ScalarMath (5 args)")]
    public double ScalarMath_Diplomat()
    {
        return BenchOps.BenchScalarMath(123, 456789, 12.5f, 98.75, true);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): ScalarMath (5 args)")]
    public double ScalarMath_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchScalarMath(123, 456789, 12.5f, 98.75, true);
    }

    [Benchmark(Description = "UniFFI: Loop 1000 AddInt64")]
    public long Loop1000_Add_UniFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++)
        {
            sum += UniffiBenchMethods.BenchAddI64(sum, i);
        }
        return sum;
    }

    [Benchmark(Description = "Diplomat: Loop 1000 AddInt64")]
    public long Loop1000_Add_Diplomat()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++)
        {
            sum += BenchOps.BenchAddI64(sum, i);
        }
        return sum;
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): Loop 1000 AddInt64")]
    public long Loop1000_Add_BoltFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++)
        {
            sum += UniffiBench.Bolt.UniffiBenchMethods.BenchAddI64(sum, i);
        }
        return sum;
    }
}
