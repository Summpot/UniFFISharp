using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using DiplomatBench;
using UniffiBench;
using UniffiBench.Bolt;

namespace UniFFISharp.Diplomat.Benchmark.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[MarkdownExporterAttribute.GitHub]
public unsafe class UnifiedInteropBenchmarks
{
    private const int LoopCount = 1000;

    // String payloads
    private string _smallAscii = null!;
    private string _mediumAscii = null!;
    private string _largeAscii = null!;
    private string _unicodeStr = null!;
    private byte[] _worldBytes = null!;
    private byte[] _echoBytes = null!;

    // Slice payloads
    private byte[] _smallBytes = null!;
    private byte[] _mediumBytes = null!;
    private byte[] _largeBytes = null!;

    // Struct payloads
    private UniffiBench.Point3D _uPoint = null!;
    private UniffiBench.Bolt.Point3D _bPoint;
    private DiplomatBench.Point3D _dPoint;
    private CsBindgenBench.Point3D _cPoint;

    private UniffiBench.UserInfo _uUser = null!;
    private UniffiBench.Bolt.UserInfo _bUser;
    private DiplomatBench.UserInfo _dUser;
    private CsBindgenBench.UserInfo _cUser;

    // Objects
    private UniffiBench.BenchCounter _uCounter = null!;
    private UniffiBench.Bolt.BoltCounter _bCounter = null!;
    private DiplomatBench.BenchCounter _dCounter = null!;
    private CsBindgenBench.BenchCounter* _cCounter;

    [GlobalSetup]
    public void Setup()
    {
        _smallAscii = "hello_bench_str!";
        _mediumAscii = new string('A', 1024);
        _largeAscii = new string('B', 32768);
        _unicodeStr = "Rust与.NET高性能跨语言FFI互操作对比基准测试🚀🦀⚡";
        _worldBytes = Encoding.UTF8.GetBytes("World");
        _echoBytes = Encoding.UTF8.GetBytes("BenchmarkPayload");

        _smallBytes = new byte[64];
        _mediumBytes = new byte[4096];
        _largeBytes = new byte[65536];
        new Random(42).NextBytes(_smallBytes);
        new Random(42).NextBytes(_mediumBytes);
        new Random(42).NextBytes(_largeBytes);

        _uPoint = new UniffiBench.Point3D(12.5, 34.75, 56.125);
        _bPoint = new UniffiBench.Bolt.Point3D(12.5, 34.75, 56.125);
        _dPoint = new DiplomatBench.Point3D { X = 12.5, Y = 34.75, Z = 56.125 };
        _cPoint = new CsBindgenBench.Point3D { x = 12.5, y = 34.75, z = 56.125 };

        _uUser = new UniffiBench.UserInfo(987654321, 32, 95.8, true);
        _bUser = new UniffiBench.Bolt.UserInfo(987654321, 32, 95.8, true);
        _dUser = new DiplomatBench.UserInfo { Id = 987654321, Age = 32, Score = 95.8, IsActive = true };
        _cUser = new CsBindgenBench.UserInfo { id = 987654321, age = 32, score = 95.8, is_active = true };

        _uCounter = new UniffiBench.BenchCounter(0);
        _bCounter = new UniffiBench.Bolt.BoltCounter(0);
        _dCounter = DiplomatBench.BenchCounter.New(0);
        _cCounter = CsBindgenNative.cs_bench_counter_new(0);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _uCounter.Dispose();
        _bCounter.Dispose();
        _dCounter.Dispose();
        if (_cCounter != null)
        {
            CsBindgenNative.cs_bench_counter_destroy(_cCounter);
            _cCounter = null;
        }
    }

    // =========================================================================
    // 1. PRIMITIVES
    // =========================================================================

    [BenchmarkCategory("01_NoOp"), Benchmark(Baseline = true, Description = "UniFFI")]
    public void NoOp_UniFFI() => UniffiBenchMethods.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public void NoOp_BoltFFI() => Uniffi_benchMethods.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "Diplomat")]
    public void NoOp_Diplomat() => BenchOps.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "CsBindgen")]
    public void NoOp_CsBindgen() => CsBindgenNative.cs_bench_noop();

    [BenchmarkCategory("02_AddInt64"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long AddInt64_UniFFI() => UniffiBenchMethods.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long AddInt64_BoltFFI() => Uniffi_benchMethods.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "Diplomat")]
    public long AddInt64_Diplomat() => BenchOps.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "CsBindgen")]
    public long AddInt64_CsBindgen() => CsBindgenNative.cs_bench_add_i64(123456789, 987654321);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Baseline = true, Description = "UniFFI")]
    public double ScalarMath_UniFFI() => UniffiBenchMethods.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public double ScalarMath_BoltFFI() => Uniffi_benchMethods.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "Diplomat")]
    public double ScalarMath_Diplomat() => BenchOps.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "CsBindgen")]
    public double ScalarMath_CsBindgen() => CsBindgenNative.cs_bench_scalar_math(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long Loop1000_UniFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += UniffiBenchMethods.BenchAddI64(sum, i);
        return sum;
    }

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long Loop1000_BoltFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += Uniffi_benchMethods.BenchAddI64(sum, i);
        return sum;
    }

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Description = "Diplomat")]
    public long Loop1000_Diplomat()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += BenchOps.BenchAddI64(sum, i);
        return sum;
    }

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Description = "CsBindgen")]
    public long Loop1000_CsBindgen()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += CsBindgenNative.cs_bench_add_i64(sum, i);
        return sum;
    }

    // =========================================================================
    // 2. STRUCTS & RECORDS
    // =========================================================================

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Baseline = true, Description = "UniFFI")]
    public double PassPoint3D_UniFFI() => UniffiBenchMethods.BenchPassPoint(_uPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public double PassPoint3D_BoltFFI() => Uniffi_benchMethods.BenchPassPoint(_bPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "Diplomat")]
    public double PassPoint3D_Diplomat() => BenchOps.BenchPassPoint(_dPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "CsBindgen")]
    public double PassPoint3D_CsBindgen() => CsBindgenNative.cs_bench_pass_point(_cPoint);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Baseline = true, Description = "UniFFI")]
    public UniffiBench.Point3D RoundtripPoint3D_UniFFI() => UniffiBenchMethods.BenchRoundtripPoint(_uPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public UniffiBench.Bolt.Point3D RoundtripPoint3D_BoltFFI() => Uniffi_benchMethods.BenchRoundtripPoint(_bPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "Diplomat")]
    public DiplomatBench.Point3D RoundtripPoint3D_Diplomat() => BenchOps.BenchRoundtripPoint(_dPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "CsBindgen")]
    public CsBindgenBench.Point3D RoundtripPoint3D_CsBindgen() => CsBindgenNative.cs_bench_roundtrip_point(_cPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong PassUserInfo_UniFFI() => UniffiBenchMethods.BenchPassUserInfo(_uUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong PassUserInfo_BoltFFI() => Uniffi_benchMethods.BenchPassUserInfo(_bUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "Diplomat")]
    public ulong PassUserInfo_Diplomat() => BenchOps.BenchPassUserInfo(_dUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "CsBindgen")]
    public ulong PassUserInfo_CsBindgen() => CsBindgenNative.cs_bench_pass_user_info(_cUser);

    // =========================================================================
    // 3. STRINGS
    // =========================================================================

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_16B_UniFFI() => UniffiBenchMethods.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_16B_BoltFFI() => Uniffi_benchMethods.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_16B_Diplomat() => BenchOps.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_16B_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_smallAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_1KB_UniFFI() => UniffiBenchMethods.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_1KB_BoltFFI() => Uniffi_benchMethods.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_1KB_Diplomat() => BenchOps.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_1KB_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_mediumAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_32KB_UniFFI() => UniffiBenchMethods.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_32KB_BoltFFI() => Uniffi_benchMethods.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_32KB_Diplomat() => BenchOps.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_32KB_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_largeAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_Unicode_UniFFI() => UniffiBenchMethods.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_Unicode_BoltFFI() => Uniffi_benchMethods.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_Unicode_Diplomat() => BenchOps.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_Unicode_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_unicodeStr);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("12_StringReturn"), Benchmark(Baseline = true, Description = "UniFFI")]
    public string StringReturn_UniFFI() => UniffiBenchMethods.BenchStringGreeting("World");

    [BenchmarkCategory("12_StringReturn"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public string StringReturn_BoltFFI() => Uniffi_benchMethods.BenchStringGreeting("World");

    [BenchmarkCategory("12_StringReturn"), Benchmark(Description = "Diplomat")]
    public string StringReturn_Diplomat() => BenchOps.BenchStringGreeting("World");

    [BenchmarkCategory("12_StringReturn"), Benchmark(Description = "CsBindgen")]
    public string StringReturn_CsBindgen()
    {
        fixed (byte* p = _worldBytes)
        {
            var buf = CsBindgenNative.cs_bench_string_greeting(p, (nuint)_worldBytes.Length);
            string res = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgenNative.cs_bench_free_byte_buffer(buf);
            return res;
        }
    }

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Baseline = true, Description = "UniFFI")]
    public string StringRoundtrip_UniFFI() => UniffiBenchMethods.BenchStringRoundtrip("BenchmarkPayload");

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public string StringRoundtrip_BoltFFI() => Uniffi_benchMethods.BenchStringRoundtrip("BenchmarkPayload");

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Description = "Diplomat")]
    public string StringRoundtrip_Diplomat() => BenchOps.BenchStringRoundtrip("BenchmarkPayload");

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Description = "CsBindgen")]
    public string StringRoundtrip_CsBindgen()
    {
        fixed (byte* p = _echoBytes)
        {
            var buf = CsBindgenNative.cs_bench_string_roundtrip(p, (nuint)_echoBytes.Length);
            string res = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgenNative.cs_bench_free_byte_buffer(buf);
            return res;
        }
    }

    // =========================================================================
    // 4. BYTE SLICES & BUFFERS
    // =========================================================================

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_64B_UniFFI() => UniffiBenchMethods.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_64B_BoltFFI() => Uniffi_benchMethods.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_64B_Diplomat() => BenchOps.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_64B_CsBindgen()
    {
        fixed (byte* p = _smallBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_smallBytes.Length);
    }

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_4KB_UniFFI() => UniffiBenchMethods.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_4KB_BoltFFI() => Uniffi_benchMethods.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_4KB_Diplomat() => BenchOps.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_4KB_CsBindgen()
    {
        fixed (byte* p = _mediumBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_mediumBytes.Length);
    }

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_64KB_UniFFI() => UniffiBenchMethods.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_64KB_BoltFFI() => Uniffi_benchMethods.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_64KB_Diplomat() => BenchOps.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_64KB_CsBindgen()
    {
        fixed (byte* p = _largeBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_largeBytes.Length);
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Baseline = true, Description = "UniFFI (To managed byte[])")]
    public byte[] SliceReturn_4KB_UniFFI() => UniffiBenchMethods.BenchGenerateBytes(4096);

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public byte[] SliceReturn_4KB_BoltFFI() => Uniffi_benchMethods.BenchGenerateBytes(4096);

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "Diplomat (ZeroCopy RustVec)")]
    public nuint SliceReturn_4KB_Diplomat_ZeroCopy()
    {
        using var vec = BenchOps.BenchGenerateBytes(4096);
        return (nuint)vec.Length;
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "Diplomat (To managed byte[])")]
    public byte[] SliceReturn_4KB_Diplomat_ToArray()
    {
        using var vec = BenchOps.BenchGenerateBytes(4096);
        return vec.Clone();
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "CsBindgen (ZeroCopy ByteBuffer)")]
    public nuint SliceReturn_4KB_CsBindgen_ZeroCopy()
    {
        var buf = CsBindgenNative.cs_bench_generate_bytes(4096);
        nuint len = buf.len;
        CsBindgenNative.cs_bench_free_byte_buffer(buf);
        return len;
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "CsBindgen (To managed byte[])")]
    public byte[] SliceReturn_4KB_CsBindgen_ToArray()
    {
        var buf = CsBindgenNative.cs_bench_generate_bytes(4096);
        byte[] arr = new ReadOnlySpan<byte>(buf.ptr, (int)buf.len).ToArray();
        CsBindgenNative.cs_bench_free_byte_buffer(buf);
        return arr;
    }

    // =========================================================================
    // 5. OBJECTS & LIFECYCLES
    // =========================================================================

    [BenchmarkCategory("18_ObjectLifecycle"), Benchmark(Baseline = true, Description = "UniFFI")]
    public void Object_CreateDestroy_UniFFI()
    {
        using var counter = new UniffiBench.BenchCounter(42);
        counter.Increment(1);
    }

    [BenchmarkCategory("18_ObjectLifecycle"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public void Object_CreateDestroy_BoltFFI()
    {
        using var counter = new UniffiBench.Bolt.BoltCounter(42);
        counter.Increment(1);
    }

    [BenchmarkCategory("18_ObjectLifecycle"), Benchmark(Description = "Diplomat")]
    public void Object_CreateDestroy_Diplomat()
    {
        using var counter = DiplomatBench.BenchCounter.New(42);
        counter.Increment(1);
    }

    [BenchmarkCategory("18_ObjectLifecycle"), Benchmark(Description = "CsBindgen")]
    public void Object_CreateDestroy_CsBindgen()
    {
        var counter = CsBindgenNative.cs_bench_counter_new(42);
        CsBindgenNative.cs_bench_counter_increment(counter, 1);
        CsBindgenNative.cs_bench_counter_destroy(counter);
    }

    [BenchmarkCategory("19_ObjectMethodCalls"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong Object_MethodCalls_UniFFI()
    {
        _uCounter.Increment(5);
        return _uCounter.Get();
    }

    [BenchmarkCategory("19_ObjectMethodCalls"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong Object_MethodCalls_BoltFFI()
    {
        _bCounter.Increment(5);
        return _bCounter.Get();
    }

    [BenchmarkCategory("19_ObjectMethodCalls"), Benchmark(Description = "Diplomat")]
    public ulong Object_MethodCalls_Diplomat()
    {
        _dCounter.Increment(5);
        return _dCounter.Get();
    }

    [BenchmarkCategory("19_ObjectMethodCalls"), Benchmark(Description = "CsBindgen")]
    public ulong Object_MethodCalls_CsBindgen()
    {
        CsBindgenNative.cs_bench_counter_increment(_cCounter, 5);
        return CsBindgenNative.cs_bench_counter_get(_cCounter);
    }

    // =========================================================================
    // 6. ERRORS & RESULTS
    // =========================================================================

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long Result_Success_UniFFI() => UniffiBenchMethods.BenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long Result_Success_BoltFFI() => Uniffi_benchMethods.BoltBenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "Diplomat")]
    public long Result_Success_Diplomat() => BenchOps.BenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "CsBindgen")]
    public long Result_Success_CsBindgen()
    {
        var res = CsBindgenNative.cs_bench_parse_positive(42);
        return res.ok;
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Baseline = true, Description = "UniFFI")]
    public bool Result_Failure_UniFFI()
    {
        try { UniffiBenchMethods.BenchParsePositive(-1); return false; }
        catch (UniffiBench.BenchError.InvalidValue) { return true; }
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public bool Result_Failure_BoltFFI()
    {
        try { Uniffi_benchMethods.BoltBenchParsePositive(-1); return false; }
        catch { return true; }
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Description = "Diplomat")]
    public bool Result_Failure_Diplomat()
    {
        try { BenchOps.BenchParsePositive(-1); return false; }
        catch (DiplomatBench.BenchErrorException) { return true; }
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Description = "CsBindgen")]
    public bool Result_Failure_CsBindgen()
    {
        var res = CsBindgenNative.cs_bench_parse_positive(-1);
        if (!res.is_ok)
        {
            try { throw new InvalidOperationException($"CsBindgen error {res.err_code}"); }
            catch (InvalidOperationException) { return true; }
        }
        return false;
    }
}
