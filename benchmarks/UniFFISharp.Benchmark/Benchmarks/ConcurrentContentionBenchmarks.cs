using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using InteroptopusBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public unsafe class ConcurrentContentionBenchmarks
{
    private const int ThreadCount = 16;
    private const int OpsPerThread = 200;

    private BenchSharedService _uShared = null!;
    private InteroptopusBench.BenchCounter _iShared = null!;
    private CsBindgenBench.BenchCounter* _cShared;
    private UniffiBench.Bolt.BoltCounter _bShared = null!;

    [GlobalSetup]
    public void Setup()
    {
        _uShared = new BenchSharedService();
        _iShared = InteroptopusBench.BenchCounter.Create(0);
        _cShared = CsBindgenNative.cs_bench_counter_new(0);
        _bShared = new UniffiBench.Bolt.BoltCounter(0);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _uShared.Dispose();
        _iShared.Dispose();
        _bShared.Dispose();
        if (_cShared != null)
        {
            CsBindgenNative.cs_bench_counter_destroy(_cShared);
            _cShared = null;
        }
    }

    // =========================================================================
    // 1. Concurrent Read Contention (16 parallel threads reading shared state)
    // =========================================================================

    [BenchmarkCategory("01_ConcurrentReads_16Threads"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): Arc<T> Shared Read")]
    public long ConcurrentReads_UniFFI()
    {
        long total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            long sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                sum += _uShared.ReadState();
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("01_ConcurrentReads_16Threads"), Benchmark(Description = "UniFFISharp (BoltFFI): Atomic Read")]
    public ulong ConcurrentReads_BoltFFI()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                sum += _bShared.Get();
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("01_ConcurrentReads_16Threads"), Benchmark(Description = "Interoptopus (Atomic Read)")]
    public ulong ConcurrentReads_Interoptopus()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                sum += _iShared.Get();
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("01_ConcurrentReads_16Threads"), Benchmark(Description = "CsBindgen (Atomic Read)")]
    public ulong ConcurrentReads_CsBindgen()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                sum += CsBindgenNative.cs_bench_counter_get(_cShared);
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    // =========================================================================
    // 2. Concurrent Read/Write Contention (16 threads reading + mutating state)
    // =========================================================================

    [BenchmarkCategory("02_ConcurrentReadWrite_16Threads"), Benchmark(Baseline = true, Description = "UniFFISharp (UniFFI): Arc<T> Shared Read/Write")]
    public long ConcurrentReadWrite_UniFFI()
    {
        long total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            long sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                sum += _uShared.WriteState(1);
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("02_ConcurrentReadWrite_16Threads"), Benchmark(Description = "UniFFISharp (BoltFFI): Atomic Read/Write")]
    public ulong ConcurrentReadWrite_BoltFFI()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                _bShared.Increment(1);
                sum += _bShared.Get();
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("02_ConcurrentReadWrite_16Threads"), Benchmark(Description = "Interoptopus (Atomic Read/Write)")]
    public ulong ConcurrentReadWrite_Interoptopus()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                _iShared.Increment(1);
                sum += _iShared.Get();
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }

    [BenchmarkCategory("02_ConcurrentReadWrite_16Threads"), Benchmark(Description = "CsBindgen (Atomic Read/Write)")]
    public ulong ConcurrentReadWrite_CsBindgen()
    {
        ulong total = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            ulong sum = 0;
            for (int i = 0; i < OpsPerThread; i++)
            {
                CsBindgenNative.cs_bench_counter_increment(_cShared, 1);
                sum += CsBindgenNative.cs_bench_counter_get(_cShared);
            }
            System.Threading.Interlocked.Add(ref total, sum);
        });
        return total;
    }
}
