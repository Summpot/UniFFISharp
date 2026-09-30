using System.Runtime.InteropServices;

namespace UniFFISharp.Types;

[StructLayout(LayoutKind.Sequential)]
public struct UniffiRustCallStatus
{
    public sbyte code;
    public RustBuffer error_buf;

    public bool IsSuccess() => code == 0;
    public bool IsError() => code == 1;
    public bool IsPanic() => code == 2;
}
