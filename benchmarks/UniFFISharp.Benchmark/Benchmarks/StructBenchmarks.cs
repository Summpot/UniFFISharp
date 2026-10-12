using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using DiplomatBench;
using UniffiBench;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MarkdownExporterAttribute.GitHub]
public class StructBenchmarks
{
    private UniffiBench.Point3D _uPoint = null!;
    private DiplomatBench.Point3D _dPoint;

    private UniffiBench.UserInfo _uUser = null!;
    private DiplomatBench.UserInfo _dUser;

    [GlobalSetup]
    public void Setup()
    {
        _uPoint = new UniffiBench.Point3D(12.5, 34.75, 56.125);
        _dPoint = new DiplomatBench.Point3D { X = 12.5, Y = 34.75, Z = 56.125 };

        _uUser = new UniffiBench.UserInfo(987654321, 32, 95.8, true);
        _dUser = new DiplomatBench.UserInfo { Id = 987654321, Age = 32, Score = 95.8, IsActive = true };
    }

    // --- Pass Point3D (Value Struct) ---
    [Benchmark(Baseline = true, Description = "UniFFI: Pass Point3D")]
    public double PassPoint3D_UniFFI()
    {
        return UniffiBenchMethods.BenchPassPoint(_uPoint);
    }

    [Benchmark(Description = "Diplomat: Pass Point3D")]
    public double PassPoint3D_Diplomat()
    {
        return BenchOps.BenchPassPoint(_dPoint);
    }

    // --- Roundtrip Point3D ---
    [Benchmark(Description = "UniFFI: Roundtrip Point3D")]
    public UniffiBench.Point3D RoundtripPoint3D_UniFFI()
    {
        return UniffiBenchMethods.BenchRoundtripPoint(_uPoint, 1.0, 2.0, 3.0);
    }

    [Benchmark(Description = "Diplomat: Roundtrip Point3D")]
    public DiplomatBench.Point3D RoundtripPoint3D_Diplomat()
    {
        return BenchOps.BenchRoundtripPoint(_dPoint, 1.0, 2.0, 3.0);
    }

    // --- Pass UserInfo (Heterogeneous Struct) ---
    [Benchmark(Description = "UniFFI: Pass UserInfo")]
    public ulong PassUserInfo_UniFFI()
    {
        return UniffiBenchMethods.BenchPassUserInfo(_uUser);
    }

    [Benchmark(Description = "Diplomat: Pass UserInfo")]
    public ulong PassUserInfo_Diplomat()
    {
        return BenchOps.BenchPassUserInfo(_dUser);
    }
}
