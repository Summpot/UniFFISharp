using System;

namespace DiplomatBench;

public partial class BenchCounter : IDisposable
{
    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
