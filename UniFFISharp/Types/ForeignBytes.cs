using System;
using System.Runtime.InteropServices;

namespace UniFFISharp.Types;

[StructLayout(LayoutKind.Sequential)]
public struct ForeignBytes
{
    public int length;
    public IntPtr data;
}
