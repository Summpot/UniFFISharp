using System;

namespace UniFFISharp.BoltFfi;

public class BoltFfiException : Exception
{
    public int Code { get; }
    public int StatusCode => Code;

    public BoltFfiException(int code, string message) : base(message)
    {
        Code = code;
    }

    public BoltFfiException(int code, string message, Exception? innerException) : base(message, innerException)
    {
        Code = code;
    }
}
