using System;
using System.Runtime.InteropServices;
using UniFFISharp.Types;

namespace UniFFISharp.Callbacks;

public static class UniffiCallbackResponseStatus
{
    public const sbyte SUCCESS = 0;
    public const sbyte ERROR = 1;
    public const sbyte UNEXPECTED_ERROR = 2;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void UniffiCallbackInterfaceFree(ulong handle);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate ulong UniffiCallbackInterfaceClone(ulong handle);

[StructLayout(LayoutKind.Sequential)]
public struct ForeignFutureDroppedCallbackStruct
{
    public ulong handle;
    public IntPtr free;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void UniffiForeignFutureDroppedCallback(ulong handle);

[StructLayout(LayoutKind.Sequential)]
public struct ForeignFutureResult<T> where T : struct
{
    public T returnValue;
    public UniffiRustCallStatus callStatus;
}

[StructLayout(LayoutKind.Sequential)]
public struct ForeignFutureResultVoid
{
    public UniffiRustCallStatus callStatus;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ForeignFutureCallbackDelegate<T>(ulong callbackData, ForeignFutureResult<T> result) where T : struct;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ForeignFutureCallbackVoidDelegate(ulong callbackData, ForeignFutureResultVoid result);
