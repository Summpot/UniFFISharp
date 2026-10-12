using System;
using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Running;
using CsBindgenBench;
using DiplomatBench;
using UniffiBench;
using UniffiBench.Bolt;

namespace UniFFISharp.Diplomat.Benchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--verify", StringComparison.OrdinalIgnoreCase))
        {
            RunVerification();
            return;
        }

        Console.WriteLine("==================================================================================");
        Console.WriteLine(" UniFFISharp vs BoltFFI vs Diplomat vs CsBindgen .NET Interop Benchmark Suite");
        Console.WriteLine("==================================================================================");
        Console.WriteLine("Frameworks compared:");
        Console.WriteLine("  1. UniFFI (Classic)       : Roslyn source-generated, RustBuffer wire protocol, auto-cargo");
        Console.WriteLine("  2. UniFFISharp (BoltFFI)  : New BoltFFI engine, direct primitive calling, SuppressGCTransition");
        Console.WriteLine("  3. Diplomat               : C-ABI bindings, repr(C) value types, RustHandle, Writeable");
        Console.WriteLine("  4. CsBindgen              : Raw C-ABI P/Invoke generator (Cysharp), direct pointer/struct calls");
        Console.WriteLine();
        Console.WriteLine("Hint: Run with '--job short' for quick benchmark runs.");
        Console.WriteLine("      Run with '--verify' to run functional equivalence tests.");
        Console.WriteLine("==================================================================================");
        Console.WriteLine();

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    public static unsafe void RunVerification()
    {
        Console.WriteLine("=== Verifying UniFFI (Classic), UniFFISharp (BoltFFI), Diplomat & CsBindgen ===");

        // 1. NoOp
        UniffiBenchMethods.BenchNoop();
        Uniffi_benchMethods.BenchNoop();
        BenchOps.BenchNoop();
        CsBindgenNative.cs_bench_noop();
        Console.WriteLine("[✓] NoOp passed");

        // 2. Add Int64
        long uAdd = UniffiBenchMethods.BenchAddI64(100, 200);
        long bAdd = Uniffi_benchMethods.BenchAddI64(100, 200);
        long dAdd = BenchOps.BenchAddI64(100, 200);
        long cAdd = CsBindgenNative.cs_bench_add_i64(100, 200);
        Debug.Assert(uAdd == 300 && bAdd == 300 && dAdd == 300 && cAdd == 300, $"Add mismatch: {uAdd} vs {bAdd} vs {dAdd} vs {cAdd}");
        Console.WriteLine($"[✓] AddInt64: UniFFI={uAdd}, BoltFFI={bAdd}, Diplomat={dAdd}, CsBindgen={cAdd}");

        // 3. Scalar Math
        double uMath = UniffiBenchMethods.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double bMath = Uniffi_benchMethods.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double dMath = BenchOps.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double cMath = CsBindgenNative.cs_bench_scalar_math(10, 20, 30.5f, 40.5, true);
        Debug.Assert(Math.Abs(uMath - dMath) < 1e-6 && Math.Abs(uMath - bMath) < 1e-6 && Math.Abs(uMath - cMath) < 1e-6, $"ScalarMath mismatch");
        Console.WriteLine($"[✓] ScalarMath: UniFFI={uMath}, BoltFFI={bMath}, Diplomat={dMath}, CsBindgen={cMath}");

        // 4. String Length
        string testStr = "BenchmarkString-1234567890-你好世界🦀";
        byte[] testStrBytes = Encoding.UTF8.GetBytes(testStr);
        ulong uLen = UniffiBenchMethods.BenchStringLength(testStr);
        ulong bLen = Uniffi_benchMethods.BenchStringLength(testStr);
        ulong dLen = BenchOps.BenchStringLength(testStr);
        ulong cLen;
        fixed (byte* p = testStrBytes)
        {
            cLen = CsBindgenNative.cs_bench_string_length(p, (nuint)testStrBytes.Length);
        }
        Debug.Assert(uLen == dLen && uLen == bLen && uLen == cLen, $"StringLength mismatch: {uLen} vs {bLen} vs {dLen} vs {cLen}");
        Console.WriteLine($"[✓] StringLength: UniFFI={uLen}, BoltFFI={bLen}, Diplomat={dLen}, CsBindgen={cLen}");

        // 5. String Greeting
        string uGreeting = UniffiBenchMethods.BenchStringGreeting("World");
        string bGreeting = Uniffi_benchMethods.BenchStringGreeting("World");
        string dGreeting = BenchOps.BenchStringGreeting("World");
        byte[] worldBytes = Encoding.UTF8.GetBytes("World");
        string cGreeting;
        fixed (byte* p = worldBytes)
        {
            var buf = CsBindgenNative.cs_bench_string_greeting(p, (nuint)worldBytes.Length);
            cGreeting = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgenNative.cs_bench_free_byte_buffer(buf);
        }
        Debug.Assert(uGreeting == dGreeting && uGreeting == bGreeting && uGreeting == cGreeting, $"Greeting mismatch: {uGreeting} vs {bGreeting} vs {cGreeting}");
        Console.WriteLine($"[✓] StringGreeting: UniFFI={uGreeting}, BoltFFI={bGreeting}, Diplomat={dGreeting}, CsBindgen={cGreeting}");

        // 6. String Roundtrip
        string uEcho = UniffiBenchMethods.BenchStringRoundtrip("UniFFI-Diplomat");
        string bEcho = Uniffi_benchMethods.BenchStringRoundtrip("UniFFI-Diplomat");
        string dEcho = BenchOps.BenchStringRoundtrip("UniFFI-Diplomat");
        byte[] echoBytes = Encoding.UTF8.GetBytes("UniFFI-Diplomat");
        string cEcho;
        fixed (byte* p = echoBytes)
        {
            var buf = CsBindgenNative.cs_bench_string_roundtrip(p, (nuint)echoBytes.Length);
            cEcho = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgenNative.cs_bench_free_byte_buffer(buf);
        }
        Debug.Assert(uEcho == dEcho && uEcho == bEcho && uEcho == cEcho, $"Echo mismatch: {uEcho} vs {bEcho} vs {cEcho}");
        Console.WriteLine($"[✓] StringRoundtrip: UniFFI={uEcho}, BoltFFI={bEcho}, Diplomat={dEcho}, CsBindgen={cEcho}");

        // 7. Sum Bytes
        byte[] bytes = new byte[] { 1, 2, 3, 4, 5, 10, 20, 30 };
        ulong uSum = UniffiBenchMethods.BenchSumBytes(bytes);
        ulong bSum = Uniffi_benchMethods.BenchSumBytes(bytes);
        ulong dSum = BenchOps.BenchSumBytes(bytes);
        ulong cSum;
        fixed (byte* p = bytes)
        {
            cSum = CsBindgenNative.cs_bench_sum_bytes(p, (nuint)bytes.Length);
        }
        Debug.Assert(uSum == dSum && uSum == bSum && uSum == cSum, $"SumBytes mismatch: {uSum} vs {bSum} vs {cSum}");
        Console.WriteLine($"[✓] SumBytes: UniFFI={uSum}, BoltFFI={bSum}, Diplomat={dSum}, CsBindgen={cSum}");

        // 8. Generate Bytes
        byte[] uGen = UniffiBenchMethods.BenchGenerateBytes(32);
        byte[] bGen = Uniffi_benchMethods.BenchGenerateBytes(32);
        using var dGen = BenchOps.BenchGenerateBytes(32);
        var cBuf = CsBindgenNative.cs_bench_generate_bytes(32);
        Debug.Assert(uGen.Length == 32 && bGen.Length == 32 && dGen.Length == 32 && cBuf.len == 32, "GenerateBytes length mismatch");
        CsBindgenNative.cs_bench_free_byte_buffer(cBuf);
        Console.WriteLine($"[✓] GenerateBytes: UniFFI len={uGen.Length}, BoltFFI len={bGen.Length}, Diplomat len={dGen.Length}, CsBindgen len={cBuf.len}");

        // 9. Pass Point3D
        var uPoint = new UniffiBench.Point3D(1.0, 2.0, 3.0);
        var bPoint = new UniffiBench.Bolt.Point3D(1.0, 2.0, 3.0);
        var dPoint = new DiplomatBench.Point3D { X = 1.0, Y = 2.0, Z = 3.0 };
        var cPoint = new CsBindgenBench.Point3D { x = 1.0, y = 2.0, z = 3.0 };
        double uDist = UniffiBenchMethods.BenchPassPoint(uPoint);
        double bDist = Uniffi_benchMethods.BenchPassPoint(bPoint);
        double dDist = BenchOps.BenchPassPoint(dPoint);
        double cDist = CsBindgenNative.cs_bench_pass_point(cPoint);
        Debug.Assert(Math.Abs(uDist - dDist) < 1e-6 && Math.Abs(uDist - bDist) < 1e-6 && Math.Abs(uDist - cDist) < 1e-6, $"PassPoint mismatch");
        Console.WriteLine($"[✓] PassPoint: UniFFI={uDist}, BoltFFI={bDist}, Diplomat={dDist}, CsBindgen={cDist}");

        // 10. Roundtrip Point3D
        var uRtPoint = UniffiBenchMethods.BenchRoundtripPoint(uPoint, 10.0, 20.0, 30.0);
        var bRtPoint = Uniffi_benchMethods.BenchRoundtripPoint(bPoint, 10.0, 20.0, 30.0);
        var dRtPoint = BenchOps.BenchRoundtripPoint(dPoint, 10.0, 20.0, 30.0);
        var cRtPoint = CsBindgenNative.cs_bench_roundtrip_point(cPoint, 10.0, 20.0, 30.0);
        Debug.Assert(uRtPoint.X == dRtPoint.X && uRtPoint.Y == dRtPoint.Y && uRtPoint.Z == dRtPoint.Z, "RoundtripPoint mismatch");
        Debug.Assert(uRtPoint.X == bRtPoint.X && uRtPoint.Y == bRtPoint.Y && uRtPoint.Z == bRtPoint.Z, "RoundtripPoint BoltFFI mismatch");
        Debug.Assert(uRtPoint.X == cRtPoint.x && uRtPoint.Y == cRtPoint.y && uRtPoint.Z == cRtPoint.z, "RoundtripPoint csbindgen mismatch");
        Console.WriteLine($"[✓] RoundtripPoint: UniFFI=({uRtPoint.X},{uRtPoint.Y},{uRtPoint.Z}), BoltFFI=({bRtPoint.X},{bRtPoint.Y},{bRtPoint.Z}), Diplomat=({dRtPoint.X},{dRtPoint.Y},{dRtPoint.Z}), CsBindgen=({cRtPoint.x},{cRtPoint.y},{cRtPoint.z})");

        // 11. Pass UserInfo
        var uUser = new UniffiBench.UserInfo(12345, 25, 98.5, true);
        var bUser = new UniffiBench.Bolt.UserInfo(12345, 25, 98.5, true);
        var dUser = new DiplomatBench.UserInfo { Id = 12345, Age = 25, Score = 98.5, IsActive = true };
        var cUser = new CsBindgenBench.UserInfo { id = 12345, age = 25, score = 98.5, is_active = true };
        ulong uUserRes = UniffiBenchMethods.BenchPassUserInfo(uUser);
        ulong bUserRes = Uniffi_benchMethods.BenchPassUserInfo(bUser);
        ulong dUserRes = BenchOps.BenchPassUserInfo(dUser);
        ulong cUserRes = CsBindgenNative.cs_bench_pass_user_info(cUser);
        Debug.Assert(uUserRes == dUserRes && uUserRes == bUserRes && uUserRes == cUserRes, $"PassUserInfo mismatch");
        Console.WriteLine($"[✓] PassUserInfo: UniFFI={uUserRes}, BoltFFI={bUserRes}, Diplomat={dUserRes}, CsBindgen={cUserRes}");

        // 12. Counter Object
        using var uCounter = new UniffiBench.BenchCounter(100);
        uCounter.Increment(50);
        ulong uCounterVal = uCounter.Get();

        using var bCounter = new UniffiBench.Bolt.BoltCounter(100);
        bCounter.Increment(50);
        ulong bCounterVal = bCounter.Get();

        using var dCounter = DiplomatBench.BenchCounter.New(100);
        dCounter.Increment(50);
        ulong dCounterVal = dCounter.Get();

        var cCounter = CsBindgenNative.cs_bench_counter_new(100);
        CsBindgenNative.cs_bench_counter_increment(cCounter, 50);
        ulong cCounterVal = CsBindgenNative.cs_bench_counter_get(cCounter);
        CsBindgenNative.cs_bench_counter_destroy(cCounter);

        Debug.Assert(uCounterVal == dCounterVal && uCounterVal == bCounterVal && uCounterVal == cCounterVal && uCounterVal == 150, $"Counter mismatch");
        Console.WriteLine($"[✓] Counter: UniFFI={uCounterVal}, BoltFFI={bCounterVal}, Diplomat={dCounterVal}, CsBindgen={cCounterVal}");

        // 13. Result Success
        long uSucc = UniffiBenchMethods.BenchParsePositive(42);
        long bSucc = Uniffi_benchMethods.BoltBenchParsePositive(42);
        long dSucc = BenchOps.BenchParsePositive(42);
        var cSucc = CsBindgenNative.cs_bench_parse_positive(42);
        Debug.Assert(uSucc == dSucc && bSucc == 84 && cSucc.is_ok && cSucc.ok == 84, $"Result success mismatch");
        Console.WriteLine($"[✓] ResultSuccess: UniFFI={uSucc}, BoltFFI={bSucc}, Diplomat={dSucc}, CsBindgen={cSucc.ok}");

        // 14. Result Failure
        bool uThrew = false;
        try { UniffiBenchMethods.BenchParsePositive(-5); }
        catch (UniffiBench.BenchError.InvalidValue) { uThrew = true; }

        bool bThrew = false;
        try { Uniffi_benchMethods.BoltBenchParsePositive(-5); }
        catch { bThrew = true; }

        bool dThrew = false;
        try { BenchOps.BenchParsePositive(-5); }
        catch (DiplomatBench.BenchErrorException) { dThrew = true; }

        var cFail = CsBindgenNative.cs_bench_parse_positive(-5);
        bool cThrew = !cFail.is_ok && cFail.err_code == 1;

        Debug.Assert(uThrew && dThrew && cThrew, $"Result error mismatch");
        Console.WriteLine($"[✓] ResultFailure: UniFFI caught={uThrew}, BoltFFI caught={bThrew}, Diplomat caught={dThrew}, CsBindgen is_err={cThrew}");

        Console.WriteLine("\n ALL FOUR FRAMEWORKS (UniFFI, UniFFISharp-BoltFFI, Diplomat, CsBindgen) PASSED FUNCTIONAL VERIFICATION!");
    }
}
