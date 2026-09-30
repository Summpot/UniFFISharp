using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UniFFISharp.Exceptions;

namespace UniFFISharp.Collections;

public class ConcurrentHandleMap<T> where T : notnull
{
    private readonly ConcurrentDictionary<ulong, T> _map = new();

    // Handles are odd numbers (1, 3, 5, ...) - the lowest bit must always be set.
    // Rust uses (handle & 1) to distinguish foreign-language handles from Rust Arc
    // pointers, which are always even due to memory alignment.
    private const long HandleInitial = 1;
    private const long HandleDelta = 2;
    private long _currentHandle = HandleInitial - HandleDelta;

    public ulong Insert(T obj)
    {
        ulong handle = (ulong)Interlocked.Add(ref _currentHandle, HandleDelta);
        if (!_map.TryAdd(handle, obj))
        {
            throw new InternalException("ConcurrentHandleMap: Duplicate handle");
        }
        return handle;
    }

    public bool TryGet(ulong handle, [NotNullWhen(true)] out T? result)
    {
        return _map.TryGetValue(handle, out result);
    }

    public T Get(ulong handle)
    {
        if (_map.TryGetValue(handle, out var result))
        {
            return result;
        }
        throw new InternalException($"ConcurrentHandleMap: Invalid handle {handle}");
    }

    public bool Remove(ulong handle)
    {
        return _map.TryRemove(handle, out _);
    }

    public bool Remove(ulong handle, [NotNullWhen(true)] out T? result)
    {
        return _map.TryRemove(handle, out result);
    }
}
