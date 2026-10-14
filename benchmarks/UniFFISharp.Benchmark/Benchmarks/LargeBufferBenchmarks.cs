using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using InteroptopusBench;
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
public unsafe class LargeBufferBenchmarks
{
    // Sizes
    private const int Size100KB = 100 * 1024;       // Just above LOH (85KB)
    private const int Size1MB = 1024 * 1024;        // Medium large buffer
    private const int Size5MB = 5 * 1024 * 1024;    // Substantial large buffer

    private byte[] _buf100KB = null!;
    private byte[] _buf1MB = null!;
    private byte[] _buf5MB = null!;

    [GlobalSetup]
    public void Setup()
    {
        _buf100KB = new byte[Size100KB];
        _buf1MB = new byte[Size1MB];
        _buf5MB = new byte[Size5MB];

        new Random(42).NextBytes(_buf100KB);
        new Random(42).NextBytes(_buf1MB);
        new Random(42).NextBytes(_buf5MB);
    }

    // =========================================================================
    // 1. 100 KB Buffer Processing (Crosses LOH Threshold)
    // =========================================================================

    [BenchmarkCategory("01_LargeBuffer100KB"), Benchmark(Baseline = true, Description = "UniFFI (Classic RustBuffer)")]
    public int LargeBuffer100KB_UniFFI()
    {
        var res = ClassicBench.BenchProcessLargeBuffer(_buf100KB);
        return res.Length;
    }

    [BenchmarkCategory("01_LargeBuffer100KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public int LargeBuffer100KB_BoltFFI()
    {
        var res = BoltBench.BenchProcessLargeBuffer(_buf100KB);
        return res.Length;
    }

    [BenchmarkCategory("01_LargeBuffer100KB"), Benchmark(Description = "CsBindgen (In-Place Zero-Copy)")]
    public int LargeBuffer100KB_CsBindgen()
    {
        fixed (byte* p = _buf100KB)
        {
            CsBindgenNative.cs_bench_process_large_buffer(p, (nuint)_buf100KB.Length);
        }
        return _buf100KB.Length;
    }

    [BenchmarkCategory("01_LargeBuffer100KB"), Benchmark(Description = "Interoptopus (VecByte)")]
    public int LargeBuffer100KB_Interoptopus()
    {
        using var vec = InteroptopusBench.VecByte.From(_buf100KB);
        using var res = InteroptopusBench.Interop.interop_bench_process_large_buffer(vec);
        return res.Count;
    }

    // =========================================================================
    // 2. 1 MB Buffer Processing
    // =========================================================================

    [BenchmarkCategory("02_LargeBuffer1MB"), Benchmark(Baseline = true, Description = "UniFFI (Classic RustBuffer)")]
    public int LargeBuffer1MB_UniFFI()
    {
        var res = ClassicBench.BenchProcessLargeBuffer(_buf1MB);
        return res.Length;
    }

    [BenchmarkCategory("02_LargeBuffer1MB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public int LargeBuffer1MB_BoltFFI()
    {
        var res = BoltBench.BenchProcessLargeBuffer(_buf1MB);
        return res.Length;
    }

    [BenchmarkCategory("02_LargeBuffer1MB"), Benchmark(Description = "CsBindgen (In-Place Zero-Copy)")]
    public int LargeBuffer1MB_CsBindgen()
    {
        fixed (byte* p = _buf1MB)
        {
            CsBindgenNative.cs_bench_process_large_buffer(p, (nuint)_buf1MB.Length);
        }
        return _buf1MB.Length;
    }

    [BenchmarkCategory("02_LargeBuffer1MB"), Benchmark(Description = "Interoptopus (VecByte)")]
    public int LargeBuffer1MB_Interoptopus()
    {
        using var vec = InteroptopusBench.VecByte.From(_buf1MB);
        using var res = InteroptopusBench.Interop.interop_bench_process_large_buffer(vec);
        return res.Count;
    }

    // =========================================================================
    // 3. 5 MB Buffer Processing
    // =========================================================================

    [BenchmarkCategory("03_LargeBuffer5MB"), Benchmark(Baseline = true, Description = "UniFFI (Classic RustBuffer)")]
    public int LargeBuffer5MB_UniFFI()
    {
        var res = ClassicBench.BenchProcessLargeBuffer(_buf5MB);
        return res.Length;
    }

    [BenchmarkCategory("03_LargeBuffer5MB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public int LargeBuffer5MB_BoltFFI()
    {
        var res = BoltBench.BenchProcessLargeBuffer(_buf5MB);
        return res.Length;
    }

    [BenchmarkCategory("03_LargeBuffer5MB"), Benchmark(Description = "CsBindgen (In-Place Zero-Copy)")]
    public int LargeBuffer5MB_CsBindgen()
    {
        fixed (byte* p = _buf5MB)
        {
            CsBindgenNative.cs_bench_process_large_buffer(p, (nuint)_buf5MB.Length);
        }
        return _buf5MB.Length;
    }

    [BenchmarkCategory("03_LargeBuffer5MB"), Benchmark(Description = "Interoptopus (VecByte)")]
    public int LargeBuffer5MB_Interoptopus()
    {
        using var vec = InteroptopusBench.VecByte.From(_buf5MB);
        using var res = InteroptopusBench.Interop.interop_bench_process_large_buffer(vec);
        return res.Count;
    }
}
