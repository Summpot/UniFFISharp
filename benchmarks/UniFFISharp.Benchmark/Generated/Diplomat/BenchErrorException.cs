using System;

namespace DiplomatBench;

/// <summary>
/// If <c>BenchError</c> is an opaque error that borrows from an opaque
/// parameter or the receiver, that dependency is retained by <c>Inner</c>'s
/// own native resource state (see <c>RustHandle.cs</c>) rather than by
/// this exception class — so no separate keep-alive plumbing is needed here.
/// </summary>
public class BenchErrorException : Exception
{
    public BenchError Inner { get; }

    public BenchErrorException(BenchError inner) : base(
        $"BenchError: {inner}"
    )
    {
        Inner = inner;
    }
}