using System;
using System.Diagnostics;
using System.Text;
using BenchmarkDotNet.Running;
using CsBindgenBench;
using DiplomatBench;
using InteroptopusBench;
using UniffiBench;
using UniffiBench.Bolt;

using ClassicBench = UniffiBench.UniffiBenchMethods;
using BoltBench = UniffiBench.Bolt.UniffiBenchMethods;
using DiplomatOps = DiplomatBench.BenchOps;
using CsBindgen = CsBindgenBench.CsBindgenNative;
using Interoptopus = InteroptopusBench.Interop;

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

        Console.WriteLine("=================================================================================================");
        Console.WriteLine(" UniFFISharp vs BoltFFI vs Diplomat vs CsBindgen vs Interoptopus .NET Interop Benchmark Suite");
        Console.WriteLine("=================================================================================================");
        Console.WriteLine("Frameworks compared:");
        Console.WriteLine("  1. UniFFI (Classic)       : Roslyn source-generated, RustBuffer wire protocol, auto-cargo");
        Console.WriteLine("  2. UniFFISharp (BoltFFI)  : High performance BoltFFI engine, direct primitives, SuppressGCTransition");
        Console.WriteLine("  3. Diplomat               : C-ABI bindings, repr(C) value types, RustHandle, Writeable");
        Console.WriteLine("  4. CsBindgen              : Raw C-ABI P/Invoke generator (Cysharp), direct pointer/struct calls");
        Console.WriteLine("  5. Interoptopus           : Zero-copy C-ABI generator, native Tokio async tasks, typed services");
        Console.WriteLine();
        Console.WriteLine("Hint: Run with '--job short' for quick benchmark runs.");
        Console.WriteLine("      Run with '--verify' to run functional equivalence tests.");
        Console.WriteLine("=================================================================================================");
        Console.WriteLine();

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }

    public static unsafe void RunVerification()
    {
        Console.WriteLine("=== Verifying UniFFI (Classic), UniFFISharp (BoltFFI), Diplomat, CsBindgen & Interoptopus ===");

        // 1. NoOp
        ClassicBench.BenchNoop();
        BoltBench.BenchNoop();
        DiplomatOps.BenchNoop();
        CsBindgen.cs_bench_noop();
        Interoptopus.interop_bench_noop();
        Console.WriteLine("[✓] NoOp passed");

        // 2. Add Int64
        long uAdd = ClassicBench.BenchAddI64(100, 200);
        long bAdd = BoltBench.BenchAddI64(100, 200);
        long dAdd = DiplomatOps.BenchAddI64(100, 200);
        long cAdd = CsBindgen.cs_bench_add_i64(100, 200);
        long iAdd = Interoptopus.interop_bench_add_i64(100, 200);
        Debug.Assert(uAdd == 300 && bAdd == 300 && dAdd == 300 && cAdd == 300 && iAdd == 300, $"Add mismatch");
        Console.WriteLine($"[✓] AddInt64: UniFFI={uAdd}, BoltFFI={bAdd}, Diplomat={dAdd}, CsBindgen={cAdd}, Interoptopus={iAdd}");

        // 3. Scalar Math
        double uMath = ClassicBench.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double bMath = BoltBench.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double dMath = DiplomatOps.BenchScalarMath(10, 20, 30.5f, 40.5, true);
        double cMath = CsBindgen.cs_bench_scalar_math(10, 20, 30.5f, 40.5, true);
        double iMath = Interoptopus.interop_bench_scalar_math(10, 20, 30.5f, 40.5, true);
        Debug.Assert(Math.Abs(uMath - dMath) < 1e-6 && Math.Abs(uMath - bMath) < 1e-6 && Math.Abs(uMath - cMath) < 1e-6 && Math.Abs(uMath - iMath) < 1e-6, $"ScalarMath mismatch");
        Console.WriteLine($"[✓] ScalarMath: UniFFI={uMath}, BoltFFI={bMath}, Diplomat={dMath}, CsBindgen={cMath}, Interoptopus={iMath}");

        // 4. String Length
        string testStr = "BenchmarkString-1234567890-你好世界🦀";
        byte[] testStrBytes = Encoding.UTF8.GetBytes(testStr);
        ulong uLen = ClassicBench.BenchStringLength(testStr);
        ulong bLen = BoltBench.BenchStringLength(testStr);
        ulong dLen = DiplomatOps.BenchStringLength(testStr);
        ulong cLen;
        fixed (byte* p = testStrBytes)
        {
            cLen = CsBindgen.cs_bench_string_length(p, (nuint)testStrBytes.Length);
        }
        using var iUtf8 = InteroptopusBench.Utf8String.From(testStr);
        ulong iLen = Interoptopus.interop_bench_string_length(iUtf8);
        Debug.Assert(uLen == dLen && uLen == bLen && uLen == cLen && uLen == iLen, $"StringLength mismatch: {uLen} vs {bLen} vs {dLen} vs {cLen} vs {iLen}");
        Console.WriteLine($"[✓] StringLength: UniFFI={uLen}, BoltFFI={bLen}, Diplomat={dLen}, CsBindgen={cLen}, Interoptopus={iLen}");

        // 5. String Greeting
        string uGreeting = ClassicBench.BenchStringGreeting("World");
        string bGreeting = BoltBench.BenchStringGreeting("World");
        string dGreeting = DiplomatOps.BenchStringGreeting("World");
        byte[] worldBytes = Encoding.UTF8.GetBytes("World");
        string cGreeting;
        fixed (byte* p = worldBytes)
        {
            var buf = CsBindgen.cs_bench_string_greeting(p, (nuint)worldBytes.Length);
            cGreeting = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgen.cs_bench_free_byte_buffer(buf);
        }
        using var iWorld = InteroptopusBench.Utf8String.From("World");
        using var iGreetingUtf8 = Interoptopus.interop_bench_string_greeting(iWorld);
        string iGreeting = iGreetingUtf8.String;
        Debug.Assert(uGreeting == dGreeting && uGreeting == bGreeting && uGreeting == cGreeting && uGreeting == iGreeting, $"Greeting mismatch: {uGreeting} vs {bGreeting} vs {cGreeting} vs {iGreeting}");
        Console.WriteLine($"[✓] StringGreeting: UniFFI={uGreeting}, BoltFFI={bGreeting}, Diplomat={dGreeting}, CsBindgen={cGreeting}, Interoptopus={iGreeting}");

        // 6. String Roundtrip
        string uEcho = ClassicBench.BenchStringRoundtrip("UniFFI-Diplomat");
        string bEcho = BoltBench.BenchStringRoundtrip("UniFFI-Diplomat");
        string dEcho = DiplomatOps.BenchStringRoundtrip("UniFFI-Diplomat");
        byte[] echoBytes = Encoding.UTF8.GetBytes("UniFFI-Diplomat");
        string cEcho;
        fixed (byte* p = echoBytes)
        {
            var buf = CsBindgen.cs_bench_string_roundtrip(p, (nuint)echoBytes.Length);
            cEcho = Encoding.UTF8.GetString(buf.ptr, (int)buf.len);
            CsBindgen.cs_bench_free_byte_buffer(buf);
        }
        using var iEchoUtf8 = InteroptopusBench.Utf8String.From("UniFFI-Diplomat");
        using var iEchoRes = Interoptopus.interop_bench_string_roundtrip(iEchoUtf8);
        string iEcho = iEchoRes.String;
        Debug.Assert(uEcho == dEcho && uEcho == bEcho && uEcho == cEcho && iEcho.Contains("UniFFI-Diplomat"), $"Echo mismatch");
        Console.WriteLine($"[✓] StringRoundtrip: UniFFI={uEcho}, BoltFFI={bEcho}, Diplomat={dEcho}, CsBindgen={cEcho}, Interoptopus={iEcho}");

        // 7. Sum Bytes
        byte[] bytes = new byte[] { 1, 2, 3, 4, 5, 10, 20, 30 };
        ulong uSum = ClassicBench.BenchSumBytes(bytes);
        ulong bSum = BoltBench.BenchSumBytes(bytes);
        ulong dSum = DiplomatOps.BenchSumBytes(bytes);
        ulong cSum;
        fixed (byte* p = bytes)
        {
            cSum = CsBindgen.cs_bench_sum_bytes(p, (nuint)bytes.Length);
        }
        using var iSlice = InteroptopusBench.SliceByte.From(bytes);
        ulong iSum = Interoptopus.interop_bench_sum_bytes(iSlice);
        Debug.Assert(uSum == dSum && uSum == bSum && uSum == cSum && uSum == iSum, $"SumBytes mismatch: {uSum} vs {bSum} vs {cSum} vs {iSum}");
        Console.WriteLine($"[✓] SumBytes: UniFFI={uSum}, BoltFFI={bSum}, Diplomat={dSum}, CsBindgen={cSum}, Interoptopus={iSum}");

        // 8. Generate Bytes
        byte[] uGen = ClassicBench.BenchGenerateBytes(32);
        byte[] bGen = BoltBench.BenchGenerateBytes(32);
        using var dGen = DiplomatOps.BenchGenerateBytes(32);
        var cBuf = CsBindgen.cs_bench_generate_bytes(32);
        using var iVec = Interoptopus.interop_bench_generate_bytes(32);
        Debug.Assert(uGen.Length == 32 && bGen.Length == 32 && dGen.Length == 32 && cBuf.len == 32 && iVec.Count == 32, "GenerateBytes length mismatch");
        CsBindgen.cs_bench_free_byte_buffer(cBuf);
        Console.WriteLine($"[✓] GenerateBytes: UniFFI={uGen.Length}, BoltFFI={bGen.Length}, Diplomat={dGen.Length}, CsBindgen={cBuf.len}, Interoptopus={iVec.Count}");

        // 9. Pass Point3D
        var uPoint = new UniffiBench.Point3D(1.0, 2.0, 3.0);
        var bPoint = new UniffiBench.Bolt.Point3D(1.0, 2.0, 3.0);
        var dPoint = new DiplomatBench.Point3D { X = 1.0, Y = 2.0, Z = 3.0 };
        var cPoint = new CsBindgenBench.Point3D { x = 1.0, y = 2.0, z = 3.0 };
        var iPoint = new InteroptopusBench.Point3D { x = 1.0, y = 2.0, z = 3.0 };
        double uDist = ClassicBench.BenchPassPoint(uPoint);
        double bDist = BoltBench.BenchPassPoint(bPoint);
        double dDist = DiplomatOps.BenchPassPoint(dPoint);
        double cDist = CsBindgen.cs_bench_pass_point(cPoint);
        double iDist = Interoptopus.interop_bench_pass_point(iPoint);
        Debug.Assert(Math.Abs(uDist - dDist) < 1e-6 && Math.Abs(uDist - bDist) < 1e-6 && Math.Abs(uDist - cDist) < 1e-6 && Math.Abs(uDist - iDist) < 1e-6, $"PassPoint mismatch");
        Console.WriteLine($"[✓] PassPoint: UniFFI={uDist}, BoltFFI={bDist}, Diplomat={dDist}, CsBindgen={cDist}, Interoptopus={iDist}");

        // 10. Roundtrip Point3D
        var uRtPoint = ClassicBench.BenchRoundtripPoint(uPoint, 10.0, 20.0, 30.0);
        var bRtPoint = BoltBench.BenchRoundtripPoint(bPoint, 10.0, 20.0, 30.0);
        var dRtPoint = DiplomatOps.BenchRoundtripPoint(dPoint, 10.0, 20.0, 30.0);
        var cRtPoint = CsBindgen.cs_bench_roundtrip_point(cPoint, 10.0, 20.0, 30.0);
        var iRtPoint = Interoptopus.interop_bench_roundtrip_point(iPoint, 10.0, 20.0, 30.0);
        Debug.Assert(uRtPoint.X == dRtPoint.X && uRtPoint.Y == dRtPoint.Y && uRtPoint.Z == dRtPoint.Z, "RoundtripPoint mismatch");
        Debug.Assert(uRtPoint.X == bRtPoint.X && uRtPoint.Y == bRtPoint.Y && uRtPoint.Z == bRtPoint.Z, "RoundtripPoint BoltFFI mismatch");
        Debug.Assert(uRtPoint.X == cRtPoint.x && uRtPoint.Y == cRtPoint.y && uRtPoint.Z == cRtPoint.z, "RoundtripPoint csbindgen mismatch");
        Debug.Assert(uRtPoint.X == iRtPoint.x && uRtPoint.Y == iRtPoint.y && uRtPoint.Z == iRtPoint.z, "RoundtripPoint interoptopus mismatch");
        Console.WriteLine($"[✓] RoundtripPoint: UniFFI=({uRtPoint.X},{uRtPoint.Y},{uRtPoint.Z}), BoltFFI=({bRtPoint.X},{bRtPoint.Y},{bRtPoint.Z}), Diplomat=({dRtPoint.X},{dRtPoint.Y},{dRtPoint.Z}), CsBindgen=({cRtPoint.x},{cRtPoint.y},{cRtPoint.z}), Interoptopus=({iRtPoint.x},{iRtPoint.y},{iRtPoint.z})");

        // 11. Pass UserInfo
        var uUser = new UniffiBench.UserInfo(12345, 25, 98.5, true);
        var bUser = new UniffiBench.Bolt.UserInfo(12345, 25, 98.5, true);
        var dUser = new DiplomatBench.UserInfo { Id = 12345, Age = 25, Score = 98.5, IsActive = true };
        var cUser = new CsBindgenBench.UserInfo { id = 12345, age = 25, score = 98.5, is_active = true };
        var iUser = new InteroptopusBench.UserInfo { id = 12345, age = 25, score = 98.5, is_active = true };
        ulong uUserRes = ClassicBench.BenchPassUserInfo(uUser);
        ulong bUserRes = BoltBench.BenchPassUserInfo(bUser);
        ulong dUserRes = DiplomatOps.BenchPassUserInfo(dUser);
        ulong cUserRes = CsBindgen.cs_bench_pass_user_info(cUser);
        ulong iUserRes = Interoptopus.interop_bench_pass_user_info(iUser);
        Debug.Assert(uUserRes == dUserRes && uUserRes == bUserRes && uUserRes == cUserRes && uUserRes == iUserRes, $"PassUserInfo mismatch");
        Console.WriteLine($"[✓] PassUserInfo: UniFFI={uUserRes}, BoltFFI={bUserRes}, Diplomat={dUserRes}, CsBindgen={cUserRes}, Interoptopus={iUserRes}");

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

        var cCounter = CsBindgen.cs_bench_counter_new(100);
        CsBindgen.cs_bench_counter_increment(cCounter, 50);
        ulong cCounterVal = CsBindgen.cs_bench_counter_get(cCounter);
        CsBindgen.cs_bench_counter_destroy(cCounter);

        using var iCounter = InteroptopusBench.BenchCounter.Create(100);
        iCounter.Increment(50);
        ulong iCounterVal = iCounter.Get();

        Debug.Assert(uCounterVal == 150 && bCounterVal == 150 && dCounterVal == 150 && cCounterVal == 150 && iCounterVal == 150, $"Counter mismatch");
        Console.WriteLine($"[✓] Counter: UniFFI={uCounterVal}, BoltFFI={bCounterVal}, Diplomat={dCounterVal}, CsBindgen={cCounterVal}, Interoptopus={iCounterVal}");

        // 13. Result Success
        long uSucc = ClassicBench.BenchParsePositive(42);
        long bSucc = BoltBench.BoltBenchParsePositive(42);
        long dSucc = DiplomatOps.BenchParsePositive(42);
        var cSucc = CsBindgen.cs_bench_parse_positive(42);
        var iSucc = Interoptopus.interop_bench_parse_positive(42);
        Debug.Assert(uSucc == 84 && bSucc == 84 && dSucc == 84 && cSucc.is_ok && cSucc.ok == 84 && iSucc.IsOk && iSucc.AsOk() == 84, $"Result success mismatch");
        Console.WriteLine($"[✓] ResultSuccess: UniFFI={uSucc}, BoltFFI={bSucc}, Diplomat={dSucc}, CsBindgen={cSucc.ok}, Interoptopus={iSucc.AsOk()}");

        // 14. Result Failure
        bool uThrew = false;
        try { ClassicBench.BenchParsePositive(-5); }
        catch (UniffiBench.BenchError.InvalidValue) { uThrew = true; }

        bool bThrew = false;
        try { BoltBench.BoltBenchParsePositive(-5); }
        catch { bThrew = true; }

        bool dThrew = false;
        try { DiplomatOps.BenchParsePositive(-5); }
        catch (DiplomatBench.BenchErrorException) { dThrew = true; }

        var cFail = CsBindgen.cs_bench_parse_positive(-5);
        bool cThrew = !cFail.is_ok && cFail.err_code == 1;

        var iFail = Interoptopus.interop_bench_parse_positive(-5);
        bool iThrew = iFail.IsErr;

        Debug.Assert(uThrew && dThrew && cThrew && iThrew, $"Result error mismatch");
        Console.WriteLine($"[✓] ResultFailure: UniFFI={uThrew}, BoltFFI={bThrew}, Diplomat={dThrew}, CsBindgen={cThrew}, Interoptopus={iThrew}");

        // 15. Callback
        long iCbVal = Interoptopus.interop_bench_invoke_callback(val => val * 3, 10);
        Debug.Assert(iCbVal == 30, $"Callback mismatch: {iCbVal}");
        Console.WriteLine($"[✓] Callback: Interoptopus={iCbVal}");

        // 16. Async Service (Tokio native async)
        using var iAsync = InteroptopusBench.BenchAsyncService.Create();
        ulong iAsyncRes = iAsync.Compute(21, 10).GetAwaiter().GetResult();
        Debug.Assert(iAsyncRes == 42, $"Async mismatch: {iAsyncRes}");
        Console.WriteLine($"[✓] AsyncService (Tokio): Interoptopus={iAsyncRes}");

        Console.WriteLine("\n ALL FIVE FRAMEWORKS (UniFFI, UniFFISharp-BoltFFI, Diplomat, CsBindgen, Interoptopus) PASSED FUNCTIONAL VERIFICATION!");
    }
}
