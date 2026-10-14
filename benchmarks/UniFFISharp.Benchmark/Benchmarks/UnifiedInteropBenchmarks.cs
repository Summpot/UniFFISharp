using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using CsBindgenBench;
using DiplomatBench;
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
    private UniffiBench.Point3D _uPoint;
    private UniffiBench.Bolt.Point3D _bPoint;
    private DiplomatBench.Point3D _dPoint;
    private CsBindgenBench.Point3D _cPoint;
    private InteroptopusBench.Point3D _iPoint;

    private UniffiBench.UserInfo _uUser;
    private UniffiBench.Bolt.UserInfo _bUser;
    private DiplomatBench.UserInfo _dUser;
    private CsBindgenBench.UserInfo _cUser;
    private InteroptopusBench.UserInfo _iUser;

    // Objects
    private UniffiBench.BenchCounter _uCounter = null!;
    private UniffiBench.Bolt.BoltCounter _bCounter = null!;
    private DiplomatBench.BenchCounter _dCounter = null!;
    private CsBindgenBench.BenchCounter* _cCounter;
    private InteroptopusBench.BenchCounter _iCounter = null!;

    // Interoptopus Utf8Strings
    private InteroptopusBench.Utf8String _iSmallAscii = null!;
    private InteroptopusBench.Utf8String _iMediumAscii = null!;
    private InteroptopusBench.Utf8String _iLargeAscii = null!;
    private InteroptopusBench.Utf8String _iUnicodeStr = null!;
    private InteroptopusBench.Utf8String _iWorld = null!;
    private InteroptopusBench.Utf8String _iEcho = null!;

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
        _iPoint = new InteroptopusBench.Point3D { x = 12.5, y = 34.75, z = 56.125 };

        _uUser = new UniffiBench.UserInfo(987654321, 32, 95.8, true);
        _bUser = new UniffiBench.Bolt.UserInfo(987654321, 32, 95.8, true);
        _dUser = new DiplomatBench.UserInfo { Id = 987654321, Age = 32, Score = 95.8, IsActive = true };
        _cUser = new CsBindgenBench.UserInfo { id = 987654321, age = 32, score = 95.8, is_active = true };
        _iUser = new InteroptopusBench.UserInfo { id = 987654321, age = 32, score = 95.8, is_active = true };

        _uCounter = new UniffiBench.BenchCounter(0);
        _bCounter = new UniffiBench.Bolt.BoltCounter(0);
        _dCounter = DiplomatBench.BenchCounter.New(0);
        _cCounter = CsBindgenNative.cs_bench_counter_new(0);
        _iCounter = InteroptopusBench.BenchCounter.Create(0);

        _iSmallAscii = InteroptopusBench.Utf8String.From(_smallAscii);
        _iMediumAscii = InteroptopusBench.Utf8String.From(_mediumAscii);
        _iLargeAscii = InteroptopusBench.Utf8String.From(_largeAscii);
        _iUnicodeStr = InteroptopusBench.Utf8String.From(_unicodeStr);
        _iWorld = InteroptopusBench.Utf8String.From("World");
        _iEcho = InteroptopusBench.Utf8String.From("BenchmarkPayload");
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
        _iCounter.Dispose();

        _iSmallAscii.Dispose();
        _iMediumAscii.Dispose();
        _iLargeAscii.Dispose();
        _iUnicodeStr.Dispose();
        _iWorld.Dispose();
        _iEcho.Dispose();
    }

    // =========================================================================
    // 1. PRIMITIVES
    // =========================================================================

    [BenchmarkCategory("01_NoOp"), Benchmark(Baseline = true, Description = "UniFFI")]
    public void NoOp_UniFFI() => ClassicBench.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public void NoOp_BoltFFI() => BoltBench.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "Diplomat")]
    public void NoOp_Diplomat() => BenchOps.BenchNoop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "CsBindgen")]
    public void NoOp_CsBindgen() => CsBindgenNative.cs_bench_noop();

    [BenchmarkCategory("01_NoOp"), Benchmark(Description = "Interoptopus")]
    public void NoOp_Interoptopus() => InteroptopusBench.Interop.interop_bench_noop();

    [BenchmarkCategory("02_AddInt64"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long AddInt64_UniFFI() => ClassicBench.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long AddInt64_BoltFFI() => BoltBench.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "Diplomat")]
    public long AddInt64_Diplomat() => BenchOps.BenchAddI64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "CsBindgen")]
    public long AddInt64_CsBindgen() => CsBindgenNative.cs_bench_add_i64(123456789, 987654321);

    [BenchmarkCategory("02_AddInt64"), Benchmark(Description = "Interoptopus")]
    public long AddInt64_Interoptopus() => InteroptopusBench.Interop.interop_bench_add_i64(123456789, 987654321);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Baseline = true, Description = "UniFFI")]
    public double ScalarMath_UniFFI() => ClassicBench.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public double ScalarMath_BoltFFI() => BoltBench.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "Diplomat")]
    public double ScalarMath_Diplomat() => BenchOps.BenchScalarMath(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "CsBindgen")]
    public double ScalarMath_CsBindgen() => CsBindgenNative.cs_bench_scalar_math(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("03_ScalarMath"), Benchmark(Description = "Interoptopus")]
    public double ScalarMath_Interoptopus() => InteroptopusBench.Interop.interop_bench_scalar_math(123, 456789, 12.5f, 98.75, true);

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long Loop1000_UniFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += ClassicBench.BenchAddI64(sum, i);
        return sum;
    }

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long Loop1000_BoltFFI()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += BoltBench.BenchAddI64(sum, i);
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

    [BenchmarkCategory("04_Loop1000Add"), Benchmark(Description = "Interoptopus")]
    public long Loop1000_Interoptopus()
    {
        long sum = 0;
        for (int i = 0; i < LoopCount; i++) sum += InteroptopusBench.Interop.interop_bench_add_i64(sum, i);
        return sum;
    }

    // =========================================================================
    // 2. STRUCTS & RECORDS
    // =========================================================================

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Baseline = true, Description = "UniFFI")]
    public double PassPoint3D_UniFFI() => ClassicBench.BenchPassPoint(_uPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public double PassPoint3D_BoltFFI() => BoltBench.BenchPassPoint(_bPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "Diplomat")]
    public double PassPoint3D_Diplomat() => BenchOps.BenchPassPoint(_dPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "CsBindgen")]
    public double PassPoint3D_CsBindgen() => CsBindgenNative.cs_bench_pass_point(_cPoint);

    [BenchmarkCategory("05_PassPoint3D"), Benchmark(Description = "Interoptopus")]
    public double PassPoint3D_Interoptopus() => InteroptopusBench.Interop.interop_bench_pass_point(_iPoint);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Baseline = true, Description = "UniFFI")]
    public UniffiBench.Point3D RoundtripPoint3D_UniFFI() => ClassicBench.BenchRoundtripPoint(_uPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public UniffiBench.Bolt.Point3D RoundtripPoint3D_BoltFFI() => BoltBench.BenchRoundtripPoint(_bPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "Diplomat")]
    public DiplomatBench.Point3D RoundtripPoint3D_Diplomat() => BenchOps.BenchRoundtripPoint(_dPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "CsBindgen")]
    public CsBindgenBench.Point3D RoundtripPoint3D_CsBindgen() => CsBindgenNative.cs_bench_roundtrip_point(_cPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("06_RoundtripPoint3D"), Benchmark(Description = "Interoptopus")]
    public InteroptopusBench.Point3D RoundtripPoint3D_Interoptopus() => InteroptopusBench.Interop.interop_bench_roundtrip_point(_iPoint, 1.0, 2.0, 3.0);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong PassUserInfo_UniFFI() => ClassicBench.BenchPassUserInfo(_uUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong PassUserInfo_BoltFFI() => BoltBench.BenchPassUserInfo(_bUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "Diplomat")]
    public ulong PassUserInfo_Diplomat() => BenchOps.BenchPassUserInfo(_dUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "CsBindgen")]
    public ulong PassUserInfo_CsBindgen() => CsBindgenNative.cs_bench_pass_user_info(_cUser);

    [BenchmarkCategory("07_PassUserInfo"), Benchmark(Description = "Interoptopus")]
    public ulong PassUserInfo_Interoptopus() => InteroptopusBench.Interop.interop_bench_pass_user_info(_iUser);

    // =========================================================================
    // 3. STRINGS
    // =========================================================================

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_16B_UniFFI() => ClassicBench.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_16B_BoltFFI() => BoltBench.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_16B_Diplomat() => BenchOps.BenchStringLength(_smallAscii);

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_16B_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_smallAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("08_StringPass16B"), Benchmark(Description = "Interoptopus")]
    public ulong StringPass_16B_Interoptopus() => InteroptopusBench.Interop.interop_bench_string_length(_iSmallAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_1KB_UniFFI() => ClassicBench.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_1KB_BoltFFI() => BoltBench.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_1KB_Diplomat() => BenchOps.BenchStringLength(_mediumAscii);

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_1KB_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_mediumAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("09_StringPass1KB"), Benchmark(Description = "Interoptopus")]
    public ulong StringPass_1KB_Interoptopus() => InteroptopusBench.Interop.interop_bench_string_length(_iMediumAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_32KB_UniFFI() => ClassicBench.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_32KB_BoltFFI() => BoltBench.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_32KB_Diplomat() => BenchOps.BenchStringLength(_largeAscii);

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_32KB_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_largeAscii);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("10_StringPass32KB"), Benchmark(Description = "Interoptopus")]
    public ulong StringPass_32KB_Interoptopus() => InteroptopusBench.Interop.interop_bench_string_length(_iLargeAscii);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong StringPass_Unicode_UniFFI() => ClassicBench.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong StringPass_Unicode_BoltFFI() => BoltBench.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "Diplomat")]
    public ulong StringPass_Unicode_Diplomat() => BenchOps.BenchStringLength(_unicodeStr);

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "CsBindgen")]
    public ulong StringPass_Unicode_CsBindgen()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(_unicodeStr);
        fixed (byte* p = bytes) return CsBindgenNative.cs_bench_string_length(p, (nuint)bytes.Length);
    }

    [BenchmarkCategory("11_StringPassUnicode"), Benchmark(Description = "Interoptopus")]
    public ulong StringPass_Unicode_Interoptopus() => InteroptopusBench.Interop.interop_bench_string_length(_iUnicodeStr);

    [BenchmarkCategory("12_StringReturn"), Benchmark(Baseline = true, Description = "UniFFI")]
    public string StringReturn_UniFFI() => ClassicBench.BenchStringGreeting("World");

    [BenchmarkCategory("12_StringReturn"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public string StringReturn_BoltFFI() => BoltBench.BenchStringGreeting("World");

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

    [BenchmarkCategory("12_StringReturn"), Benchmark(Description = "Interoptopus")]
    public string StringReturn_Interoptopus()
    {
        using var res = InteroptopusBench.Interop.interop_bench_string_greeting(_iWorld);
        return res.String;
    }

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Baseline = true, Description = "UniFFI")]
    public string StringRoundtrip_UniFFI() => ClassicBench.BenchStringRoundtrip("BenchmarkPayload");

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public string StringRoundtrip_BoltFFI() => BoltBench.BenchStringRoundtrip("BenchmarkPayload");

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

    [BenchmarkCategory("13_StringRoundtrip"), Benchmark(Description = "Interoptopus")]
    public string StringRoundtrip_Interoptopus()
    {
        using var res = InteroptopusBench.Interop.interop_bench_string_roundtrip(_iEcho);
        return res.String;
    }

    // =========================================================================
    // 4. BYTE SLICES & BUFFERS
    // =========================================================================

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_64B_UniFFI() => ClassicBench.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_64B_BoltFFI() => BoltBench.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_64B_Diplomat() => BenchOps.BenchSumBytes(_smallBytes);

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_64B_CsBindgen()
    {
        fixed (byte* p = _smallBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_smallBytes.Length);
    }

    [BenchmarkCategory("14_SlicePass64B"), Benchmark(Description = "Interoptopus")]
    public ulong SlicePass_64B_Interoptopus()
    {
        using var s = SliceByte.From(_smallBytes);
        return InteroptopusBench.Interop.interop_bench_sum_bytes(s);
    }

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_4KB_UniFFI() => ClassicBench.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_4KB_BoltFFI() => BoltBench.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_4KB_Diplomat() => BenchOps.BenchSumBytes(_mediumBytes);

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_4KB_CsBindgen()
    {
        fixed (byte* p = _mediumBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_mediumBytes.Length);
    }

    [BenchmarkCategory("15_SlicePass4KB"), Benchmark(Description = "Interoptopus")]
    public ulong SlicePass_4KB_Interoptopus()
    {
        using var s = SliceByte.From(_mediumBytes);
        return InteroptopusBench.Interop.interop_bench_sum_bytes(s);
    }

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Baseline = true, Description = "UniFFI")]
    public ulong SlicePass_64KB_UniFFI() => ClassicBench.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public ulong SlicePass_64KB_BoltFFI() => BoltBench.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "Diplomat")]
    public ulong SlicePass_64KB_Diplomat() => BenchOps.BenchSumBytes(_largeBytes);

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "CsBindgen")]
    public ulong SlicePass_64KB_CsBindgen()
    {
        fixed (byte* p = _largeBytes) return CsBindgenNative.cs_bench_sum_bytes(p, (nuint)_largeBytes.Length);
    }

    [BenchmarkCategory("16_SlicePass64KB"), Benchmark(Description = "Interoptopus")]
    public ulong SlicePass_64KB_Interoptopus()
    {
        using var s = SliceByte.From(_largeBytes);
        return InteroptopusBench.Interop.interop_bench_sum_bytes(s);
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Baseline = true, Description = "UniFFI (To managed byte[])")]
    public byte[] SliceReturn_4KB_UniFFI() => ClassicBench.BenchGenerateBytes(4096);

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public byte[] SliceReturn_4KB_BoltFFI() => BoltBench.BenchGenerateBytes(4096);

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

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "Interoptopus (ZeroCopy VecByte)")]
    public nuint SliceReturn_4KB_Interoptopus_ZeroCopy()
    {
        using var vec = InteroptopusBench.Interop.interop_bench_generate_bytes(4096);
        return (nuint)vec.Count;
    }

    [BenchmarkCategory("17_SliceReturn4KB"), Benchmark(Description = "Interoptopus (To managed byte[])")]
    public byte[] SliceReturn_4KB_Interoptopus_ToArray()
    {
        using var vec = InteroptopusBench.Interop.interop_bench_generate_bytes(4096);
        byte[] arr = new byte[vec.Count];
        for (int i = 0; i < vec.Count; i++) arr[i] = vec[i];
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

    [BenchmarkCategory("18_ObjectLifecycle"), Benchmark(Description = "Interoptopus")]
    public void Object_CreateDestroy_Interoptopus()
    {
        using var counter = InteroptopusBench.BenchCounter.Create(42);
        counter.Increment(1);
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

    [BenchmarkCategory("19_ObjectMethodCalls"), Benchmark(Description = "Interoptopus")]
    public ulong Object_MethodCalls_Interoptopus()
    {
        _iCounter.Increment(5);
        return _iCounter.Get();
    }

    // =========================================================================
    // 6. ERRORS & RESULTS
    // =========================================================================

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Baseline = true, Description = "UniFFI")]
    public long Result_Success_UniFFI() => ClassicBench.BenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public long Result_Success_BoltFFI() => BoltBench.BoltBenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "Diplomat")]
    public long Result_Success_Diplomat() => BenchOps.BenchParsePositive(42);

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "CsBindgen")]
    public long Result_Success_CsBindgen()
    {
        var res = CsBindgenNative.cs_bench_parse_positive(42);
        return res.ok;
    }

    [BenchmarkCategory("20_ResultSuccess"), Benchmark(Description = "Interoptopus")]
    public long Result_Success_Interoptopus()
    {
        var res = InteroptopusBench.Interop.interop_bench_parse_positive(42);
        return res.AsOk();
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Baseline = true, Description = "UniFFI")]
    public bool Result_Failure_UniFFI()
    {
        try { ClassicBench.BenchParsePositive(-1); return false; }
        catch (UniffiBench.BenchError.InvalidValue) { return true; }
    }

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Description = "UniFFISharp (BoltFFI)")]
    public bool Result_Failure_BoltFFI()
    {
        try { BoltBench.BoltBenchParsePositive(-1); return false; }
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

    [BenchmarkCategory("21_ResultFailure"), Benchmark(Description = "Interoptopus")]
    public bool Result_Failure_Interoptopus()
    {
        var res = InteroptopusBench.Interop.interop_bench_parse_positive(-1);
        if (res.IsErr)
        {
            try { throw new InvalidOperationException($"Interoptopus error {res.AsErr()}"); }
            catch (InvalidOperationException) { return true; }
        }
        return false;
    }
}
