using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using InteroptopusBench;
using UniffiBench;
using UniffiBench.Bolt;

using UniFfiBench = UniffiBench.UniffiBenchMethods;

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

    public sealed class BoltSimpleCb : IBoltSimpleCallback
    {
        public long OnInvoke(long val) => val * 2;
    }

    private sealed class BoltSimpleFallbackCb : IBoltSimpleCallback
    {
        public long OnInvoke(long val) => val * 2;
    }

    public sealed class BoltProgressCb : IBoltProgressCallback
    {
        public ulong LastCurrent;
        public void OnProgress(ulong current, ulong total, double percentage)
        {
            LastCurrent = current;
        }
    }

    private sealed class BoltProgressFallbackCb : IBoltProgressCallback
    {
        public ulong LastCurrent;
        public void OnProgress(ulong current, ulong total, double percentage)
        {
            LastCurrent = current;
        }
    }

    private sealed class CsSimpleCb
    {
        public long OnInvoke(long val) => val * 2;
    }

    private sealed class CsProgressCb
    {
        public ulong LastCurrent;
        public void OnProgress(ulong current, ulong total, double percentage)
        {
            LastCurrent = current;
        }
    }

    private UniFfiSimpleCb _uSimple = null!;
    private UniFfiProgressCb _uProgress = null!;
    private BoltSimpleCb _bSimple = null!;
    private BoltSimpleFallbackCb _bSimpleFallback = null!;
    private BoltProgressCb _bProgress = null!;
    private BoltProgressFallbackCb _bProgressFallback = null!;
    private CsSimpleCb _cSimple = null!;
    private CsProgressCb _cProgress = null!;
    private GCHandle _cSimpleHandle;
    private GCHandle _cProgressHandle;
    private void* _cSimpleContext;
    private void* _cProgressContext;

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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static long CsSimpleCallbackContext(long val, void* context)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)context);
        var target = (CsSimpleCb)handle.Target!;
        return target.OnInvoke(val);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CsProgressCallbackContext(ulong current, ulong total, double percentage, void* context)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)context);
        var target = (CsProgressCb)handle.Target!;
        target.OnProgress(current, total, percentage);
    }

    [GlobalSetup]
    public void Setup()
    {
        _uSimple = new UniFfiSimpleCb();
        _uProgress = new UniFfiProgressCb();
        _bSimple = new BoltSimpleCb();
        _bSimpleFallback = new BoltSimpleFallbackCb();
        _bProgress = new BoltProgressCb();
        _bProgressFallback = new BoltProgressFallbackCb();
        _cSimple = new CsSimpleCb();
        _cProgress = new CsProgressCb();

        _cSimpleHandle = GCHandle.Alloc(_cSimple);
        _cProgressHandle = GCHandle.Alloc(_cProgress);
        _cSimpleContext = (void*)GCHandle.ToIntPtr(_cSimpleHandle);
        _cProgressContext = (void*)GCHandle.ToIntPtr(_cProgressHandle);

        _iSimple = val => val * 2;
        _iProgress = (cur, tot, pct) => { _iLastCurrent = cur; };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_cSimpleHandle.IsAllocated) _cSimpleHandle.Free();
        if (_cProgressHandle.IsAllocated) _cProgressHandle.Free();
    }

    // =========================================================================
    // 1. Single Callback Invocation
    // =========================================================================

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI)")]
    public long SimpleCallback_UniFFI()
    {
        return UniFfiBench.BenchInvokeCallback(_uSimple, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "UniFFISharp (BoltFFI): Guarded Devirtualized")]
    public long SimpleCallback_BoltFFI_GuardedDevirtualized()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BoltBenchInvokeCallback(_bSimple, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "UniFFISharp (BoltFFI): Interface Fallback")]
    public long SimpleCallback_BoltFFI_InterfaceFallback()
    {
        return UniffiBench.Bolt.UniffiBenchMethods.BoltBenchInvokeCallback(_bSimpleFallback, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "CsBindgen (Instance Context Cached)")]
    public long SimpleCallback_CsBindgen_ContextCached()
    {
        delegate* unmanaged[Cdecl]<long, void*, long> fn = &CsSimpleCallbackContext;
        return CsBindgenNative.cs_bench_invoke_callback_context(fn, _cSimpleContext, 42);
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "CsBindgen (Instance Context PerCall Alloc)")]
    public long SimpleCallback_CsBindgen_ContextAlloc()
    {
        var handle = GCHandle.Alloc(_cSimple);
        try
        {
            delegate* unmanaged[Cdecl]<long, void*, long> fn = &CsSimpleCallbackContext;
            return CsBindgenNative.cs_bench_invoke_callback_context(fn, (void*)GCHandle.ToIntPtr(handle), 42);
        }
        finally
        {
            handle.Free();
        }
    }

    [BenchmarkCategory("01_SimpleCallback"), Benchmark(Description = "CsBindgen (Raw FnPtr Static)")]
    public long SimpleCallback_CsBindgen_Raw()
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

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI)")]
    public void ProgressCallback_100x_UniFFI()
    {
        UniFfiBench.BenchTriggerProgress(_uProgress, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "UniFFISharp (BoltFFI): Guarded Devirtualized")]
    public void ProgressCallback_100x_BoltFFI_GuardedDevirtualized()
    {
        UniffiBench.Bolt.UniffiBenchMethods.BoltBenchTriggerProgress(_bProgress, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "UniFFISharp (BoltFFI): Interface Fallback")]
    public void ProgressCallback_100x_BoltFFI_InterfaceFallback()
    {
        UniffiBench.Bolt.UniffiBenchMethods.BoltBenchTriggerProgress(_bProgressFallback, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "CsBindgen (Instance Context Cached)")]
    public void ProgressCallback_100x_CsBindgen_ContextCached()
    {
        delegate* unmanaged[Cdecl]<ulong, ulong, double, void*, void> fn = &CsProgressCallbackContext;
        CsBindgenNative.cs_bench_trigger_progress_context(fn, _cProgressContext, 100);
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "CsBindgen (Instance Context PerCall Alloc)")]
    public void ProgressCallback_100x_CsBindgen_ContextAlloc()
    {
        var handle = GCHandle.Alloc(_cProgress);
        try
        {
            delegate* unmanaged[Cdecl]<ulong, ulong, double, void*, void> fn = &CsProgressCallbackContext;
            CsBindgenNative.cs_bench_trigger_progress_context(fn, (void*)GCHandle.ToIntPtr(handle), 100);
        }
        finally
        {
            handle.Free();
        }
    }

    [BenchmarkCategory("02_ProgressCallback_100x"), Benchmark(Description = "CsBindgen (Raw FnPtr Static)")]
    public void ProgressCallback_100x_CsBindgen_Raw()
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
