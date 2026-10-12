using System;
using System.Runtime.InteropServices;
using DiplomatBench;
using DiplomatBench.Diplomat;

namespace DiplomatBench.Raw;

[StructLayout(LayoutKind.Sequential)]
internal partial struct BenchOps
{

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_noop", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void BenchNoop();

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_add_i64", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern long BenchAddI64(long a, long b);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_scalar_math", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern double BenchScalarMath(int a, long b, float c, double d, [MarshalAs(UnmanagedType.U1)] bool flag);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_string_length", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern ulong BenchStringLength(DiplomatSliceU8 s);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_string_greeting", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void BenchStringGreeting(DiplomatSliceU8 name, DiplomatWrite* writeable);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_string_roundtrip", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void BenchStringRoundtrip(DiplomatSliceU8 s, DiplomatWrite* writeable);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_sum_bytes", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern ulong BenchSumBytes(DiplomatSliceU8 data);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_generate_bytes", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern DiplomatOwnedSliceU8 BenchGenerateBytes(nuint size);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_pass_point", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern double BenchPassPoint(Point3D p);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_roundtrip_point", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern Point3D BenchRoundtripPoint(Point3D p, double dx, double dy, double dz);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_pass_user_info", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern ulong BenchPassUserInfo(UserInfo u);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_bench_parse_positive", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern DiplomatResultLongBenchError BenchParsePositive(long val);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchOps_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void Destroy(BenchOps* handle);
}