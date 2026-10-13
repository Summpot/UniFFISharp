using System.Runtime.InteropServices;

namespace UniFFISharp.BoltFfi;

[StructLayout(LayoutKind.Sequential)]
public struct FfiStatus
{
    public int code;
    public int Code { get => code; set => code = value; }
}

[StructLayout(LayoutKind.Sequential)]
public struct BoltFfiStatus
{
    public int code;
    public FfiBuf error;
    public int Code { get => code; set => code = value; }
    public FfiBuf Error { get => error; set => error = value; }
}
