using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class SliceBenchmarks
{
    private byte[] _smallBytes = null!;
    private byte[] _mediumBytes = null!;
    private byte[] _largeBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallBytes = new byte[64];
        _mediumBytes = new byte[4096];
        _largeBytes = new byte[65536];

        new Random(42).NextBytes(_smallBytes);
        new Random(42).NextBytes(_mediumBytes);
        new Random(42).NextBytes(_largeBytes);
    }

    // --- Small Slice Passing (64B) ---
    [Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): BytesPass (64B)")]
    public ulong BytesPass_Small_UniFFI()
    {
        return UniffiBenchMethods.BenchSumBytes(_smallBytes);
    }

    [Benchmark(Description = "Diplomat: BytesPass (64B)")]
    public ulong BytesPass_Small_Diplomat()
    {
        return BenchOps.BenchSumBytes(_smallBytes);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): BytesPass (64B)")]
    public ulong BytesPass_Small_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchSumBytes(_smallBytes);
    }

    // --- Medium Slice Passing (4KB) ---
    [Benchmark(Description = "UniFFISharp (UniFFI): BytesPass (4KB)")]
    public ulong BytesPass_Medium_UniFFI()
    {
        return UniffiBenchMethods.BenchSumBytes(_mediumBytes);
    }

    [Benchmark(Description = "Diplomat: BytesPass (4KB)")]
    public ulong BytesPass_Medium_Diplomat()
    {
        return BenchOps.BenchSumBytes(_mediumBytes);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): BytesPass (4KB)")]
    public ulong BytesPass_Medium_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchSumBytes(_mediumBytes);
    }

    // --- Large Slice Passing (64KB) ---
    [Benchmark(Description = "UniFFISharp (UniFFI): BytesPass (64KB)")]
    public ulong BytesPass_Large_UniFFI()
    {
        return UniffiBenchMethods.BenchSumBytes(_largeBytes);
    }

    [Benchmark(Description = "Diplomat: BytesPass (64KB)")]
    public ulong BytesPass_Large_Diplomat()
    {
        return BenchOps.BenchSumBytes(_largeBytes);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): BytesPass (64KB)")]
    public ulong BytesPass_Large_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchSumBytes(_largeBytes);
    }

    // --- Bytes Returning (4KB) ---
    [Benchmark(Description = "UniFFISharp (UniFFI): BytesReturn (4KB to byte[])")]
    public byte[] BytesReturn_UniFFI()
    {
        return UniffiBenchMethods.BenchGenerateBytes(4096);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): BytesReturn (4KB to byte[])")]
    public byte[] BytesReturn_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchGenerateBytes(4096);
    }

    [Benchmark(Description = "Diplomat: BytesReturn (4KB Zero-Copy RustVec)")]
    public nuint BytesReturn_Diplomat_ZeroCopy()
    {
        using var vec = BenchOps.BenchGenerateBytes(4096);
        return (nuint)vec.Length;
    }

    [Benchmark(Description = "Diplomat: BytesReturn (4KB to byte[])")]
    public byte[] BytesReturn_Diplomat_ToArray()
    {
        using var vec = BenchOps.BenchGenerateBytes(4096);
        return vec.Clone();
    }
}
