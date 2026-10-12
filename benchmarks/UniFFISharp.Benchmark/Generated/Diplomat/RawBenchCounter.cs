using System;
using System.Runtime.InteropServices;
using DiplomatBench;
using DiplomatBench.Diplomat;

namespace DiplomatBench.Raw;

[StructLayout(LayoutKind.Sequential)]
internal partial struct BenchCounter
{

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchCounter_new", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern BenchCounter* New(ulong val);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchCounter_increment", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void Increment(BenchCounter* handle, ulong amount);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchCounter_get", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern ulong Get(BenchCounter* handle);

    [DllImport(DiplomatNativeLib.Name, EntryPoint = "BenchCounter_destroy", CallingConvention = CallingConvention.Cdecl)]
    internal static unsafe extern void Destroy(BenchCounter* handle);
}