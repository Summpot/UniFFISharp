using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class ErrorBenchmarks
{
    // --- Result Success Path ---
    [Benchmark(Baseline = true, Description = "UniFFI: Result Success")]
    public long Result_Success_UniFFI()
    {
        return UniffiBenchMethods.BenchParsePositive(42);
    }

    [Benchmark(Description = "Diplomat: Result Success")]
    public long Result_Success_Diplomat()
    {
        return BenchOps.BenchParsePositive(42);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): Result Success")]
    public long Result_Success_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BoltBenchParsePositive(42);
    }

    // --- Result Failure Path (Throws Exception) ---
    [Benchmark(Description = "UniFFI: Result Failure (Catch)")]
    public bool Result_Failure_UniFFI()
    {
        try
        {
            UniffiBenchMethods.BenchParsePositive(-1);
            return false;
        }
        catch (UniffiBench.BenchError.InvalidValue)
        {
            return true;
        }
    }

    [Benchmark(Description = "Diplomat: Result Failure (Catch)")]
    public bool Result_Failure_Diplomat()
    {
        try
        {
            BenchOps.BenchParsePositive(-1);
            return false;
        }
        catch (DiplomatBench.BenchErrorException)
        {
            return true;
        }
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): Result Failure (Catch)")]
    public bool Result_Failure_BoltFFI()
    {
        try
        {
            UniffiBench.Bolt.UniffiBenchMethods.BoltBenchParsePositive(-1);
            return false;
        }
        catch (UniFFISharp.Exceptions.UniffiException)
        {
            return true;
        }
    }
}
