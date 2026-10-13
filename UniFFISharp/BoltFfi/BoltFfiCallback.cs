using System.Runtime.InteropServices;

namespace UniFFISharp.BoltFfi;

[StructLayout(LayoutKind.Sequential)]
public struct BoltFFICallbackHandle
{
    public ulong handle;
    public nint vtable;

    public bool IsNull => handle == 0 || vtable == 0;
    public static BoltFFICallbackHandle Null => default;
}
