using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using InteroptopusBench;
using UniffiBench;

using ClassicBench = UniffiBench.UniffiBenchMethods;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public unsafe class CallbackBenchmarks
{
    private sealed class UniFfiSimpleCb : IBenchSimpleCallback
    {
        public long OnInvoke(long val) => val * 2;
    }

    private sealed class UniFfiProgressCb : IBenchProgressCallback
    {
        public ulong LastCurrent;
        public void OnProgress(ulong current, ulong total, double percentage)
        {
            LastCurrent = current;
        }
    }

    private UniFfiSimpleCb _uSimple = null!;
    private UniFfiProgressCb _uProgress = null!;

    private BenchSimpleCallbackDelegate _iSimple = null!;
    private BenchProgressCallbackDelegate _iProgress = null!;
    private ulong _iLastCurrent;

    private static ulong s_csLastCurrent;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static long CsSimpleCallback(long val) => val * 2;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CsProgressCallback(ulong current, ulong total, double percentage)
    {
        s_csLastCurrent = current;
    }

    [GlobalSetup]
    public void Setup()
    {
        _uSimple = new UniFfiSimpleCb();
        _uProgress = new UniFfiProgressCb();

        _iSimple = val => val * 2;
        _iProgress = (cur, tot, pct) => { _iLastCurrent = cur; };
    }

    // =========================================================================
    // 1. Single Callback Invocation
    // =========================================================================

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Baseline = true, Description = "UniFFI (Classic)")]
    public long SimpleCallback_UniFFI()
    {
        return ClassicBench.BenchInvokeCallback(_uSimple, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "CsBindgen (Raw FnPtr)")]
    public long SimpleCallback_CsBindgen()
    {
        delegate* unmanaged[Cdecl]<long, long> fn = &CsSimpleCallback;
        return CsBindgenNative.cs_bench_invoke_callback(fn, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "Interoptopus (InteropDelegate)")]
    public long SimpleCallback_Interoptopus()
    {
        return InteroptopusBench.Interop.interop_bench_invoke_callback(_iSimple, 42);
    }

    // =========================================================================
    // 2. High Frequency Progress Callback (100 triggers per call)
    // =========================================================================

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Baseline = true, Description = "UniFFI (Classic)")]
    public void ProgressCallback_100x_UniFFI()
    {
        ClassicBench.BenchTriggerProgress(_uProgress, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "CsBindgen (Raw FnPtr)")]
    public void ProgressCallback_100x_CsBindgen()
    {
        delegate* unmanaged[Cdecl]<ulong, ulong, double, void> fn = &CsProgressCallback;
        CsBindgenNative.cs_bench_trigger_progress(fn, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "Interoptopus (InteropDelegate)")]
    public void ProgressCallback_100x_Interoptopus()
    {
        InteroptopusBench.Interop.interop_bench_trigger_progress(_iProgress, 100);
    }
}
