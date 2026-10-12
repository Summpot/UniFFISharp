using System;
using System.Runtime.InteropServices;

namespace DiplomatBench.Raw;

using DiplomatBench;
using DiplomatBench.Diplomat;

[StructLayout(LayoutKind.Sequential)]
internal partial struct DiplomatResultLongBenchError
{
    [StructLayout(LayoutKind.Explicit)]
    private unsafe struct InnerUnion
    {
        [FieldOffset(0)] internal long ok;
        [FieldOffset(0)] internal BenchError err;
    }

    private InnerUnion _inner;

    public DiplomatBool IsOk;
    public long Ok => IsOk ? _inner.ok : throw new InvalidOperationException("Result does not contain Ok value");
    public BenchError Err => !IsOk ? _inner.err : throw new InvalidOperationException("Result does not contain Err value");
}