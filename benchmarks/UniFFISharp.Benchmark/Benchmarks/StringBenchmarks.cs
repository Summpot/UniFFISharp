using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class StringBenchmarks
{
    private string _smallAscii = null!;
    private string _mediumAscii = null!;
    private string _largeAscii = null!;
    private string _unicodeStr = null!;

    [GlobalSetup]
    public void Setup()
    {
        _smallAscii = "hello_bench_str!"; // 16 bytes
        _mediumAscii = new string('A', 1024); // 1 KB
        _largeAscii = new string('B', 32768); // 32 KB
        _unicodeStr = "Rust与.NET高性能跨语言FFI互操作对比基准测试🚀🦀⚡"; // UTF-8 multi-byte
    }

    // --- Small String Passing ---
    [Benchmark(Baseline = true, Description = "UniFFI: StringPass (16B ASCII)")]
    public ulong StringPass_Small_UniFFI()
    {
        return UniffiBenchMethods.BenchStringLength(_smallAscii);
    }

    [Benchmark(Description = "Diplomat: StringPass (16B ASCII)")]
    public ulong StringPass_Small_Diplomat()
    {
        return BenchOps.BenchStringLength(_smallAscii);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringPass (16B ASCII)")]
    public ulong StringPass_Small_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringLength(_smallAscii);
    }

    // --- Medium String Passing (1 KB) ---
    [Benchmark(Description = "UniFFI: StringPass (1KB ASCII)")]
    public ulong StringPass_Medium_UniFFI()
    {
        return UniffiBenchMethods.BenchStringLength(_mediumAscii);
    }

    [Benchmark(Description = "Diplomat: StringPass (1KB ASCII)")]
    public ulong StringPass_Medium_Diplomat()
    {
        return BenchOps.BenchStringLength(_mediumAscii);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringPass (1KB ASCII)")]
    public ulong StringPass_Medium_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringLength(_mediumAscii);
    }

    // --- Large String Passing (32 KB) ---
    [Benchmark(Description = "UniFFI: StringPass (32KB ASCII)")]
    public ulong StringPass_Large_UniFFI()
    {
        return UniffiBenchMethods.BenchStringLength(_largeAscii);
    }

    [Benchmark(Description = "Diplomat: StringPass (32KB ASCII)")]
    public ulong StringPass_Large_Diplomat()
    {
        return BenchOps.BenchStringLength(_largeAscii);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringPass (32KB ASCII)")]
    public ulong StringPass_Large_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringLength(_largeAscii);
    }

    // --- Unicode String Passing ---
    [Benchmark(Description = "UniFFI: StringPass (Unicode/Emoji)")]
    public ulong StringPass_Unicode_UniFFI()
    {
        return UniffiBenchMethods.BenchStringLength(_unicodeStr);
    }

    [Benchmark(Description = "Diplomat: StringPass (Unicode/Emoji)")]
    public ulong StringPass_Unicode_Diplomat()
    {
        return BenchOps.BenchStringLength(_unicodeStr);
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringPass (Unicode/Emoji)")]
    public ulong StringPass_Unicode_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringLength(_unicodeStr);
    }

    // --- String Returning (Rust -> C#) ---
    [Benchmark(Description = "UniFFI: StringReturn")]
    public string StringReturn_UniFFI()
    {
        return UniffiBenchMethods.BenchStringGreeting("World");
    }

    [Benchmark(Description = "Diplomat: StringReturn")]
    public string StringReturn_Diplomat()
    {
        return BenchOps.BenchStringGreeting("World");
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringReturn")]
    public string StringReturn_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringGreeting("World");
    }

    // --- String Roundtrip ---
    [Benchmark(Description = "UniFFI: StringRoundtrip")]
    public string StringRoundtrip_UniFFI()
    {
        return UniffiBenchMethods.BenchStringRoundtrip("BenchmarkPayload");
    }

    [Benchmark(Description = "Diplomat: StringRoundtrip")]
    public string StringRoundtrip_Diplomat()
    {
        return BenchOps.BenchStringRoundtrip("BenchmarkPayload");
    }

    [Benchmark(Description = "UniFFISharp (BoltFFI): StringRoundtrip")]
    public string StringRoundtrip_BoltFFI()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BenchStringRoundtrip("BenchmarkPayload");
    }
}
