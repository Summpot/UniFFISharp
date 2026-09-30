using System;

namespace UniFFISharp.Exceptions;

public class UniffiException : Exception
{
    public UniffiException() : base() { }
    public UniffiException(string message) : base(message) { }
    public UniffiException(string message, Exception innerException) : base(message, innerException) { }
}

public class UndeclaredErrorException : UniffiException
{
    public UndeclaredErrorException(string message) : base(message) { }
}

public class PanicException : UniffiException
{
    public PanicException(string message) : base(message) { }
}

public class AllocationException : UniffiException
{
    public AllocationException(string message) : base(message) { }
}

public class InternalException : UniffiException
{
    public InternalException(string message) : base(message) { }
}

public class InvalidEnumException : InternalException
{
    public InvalidEnumException(string message) : base(message) { }
}

public class UniffiContractVersionException : UniffiException
{
    public UniffiContractVersionException(string message) : base(message) { }
}

public class UniffiContractChecksumException : UniffiException
{
    public UniffiContractChecksumException(string message) : base(message) { }
}

public class StreamUnderflowException : UniffiException
{
    public StreamUnderflowException() : base("Stream underflow occurred while reading UniFFI buffer.") { }
    public StreamUnderflowException(string message) : base(message) { }
}
