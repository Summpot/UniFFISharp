using System.Runtime.InteropServices;

namespace DiplomatBench.Diplomat;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct DiplomatSliceMutU8
{
    public byte* Ptr;
    public nuint Len;
}