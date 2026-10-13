using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace UniFFISharp.BoltFfi;

public static class BoltFfiStreamHelper
{
    public static async IAsyncEnumerable<TItem> ReadAll<TItem>(
        Func<ulong> subscribe,
        Func<ulong, nuint, TItem[]> popBatch,
        Func<ulong, int, int> wait,
        Action<ulong> unsubscribe,
        Action<ulong> free,
        nuint batchSize = 16,
        int waitTimeoutMs = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ulong subscription = subscribe();
        if (subscription == 0) yield break;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TItem[] items = popBatch(subscription, batchSize);
                foreach (var item in items)
                {
                    yield return item;
                }
                if (items.Length != 0) continue;
                int waitResult = await Task.Run(() => wait(subscription, waitTimeoutMs), cancellationToken).ConfigureAwait(false);
                if (waitResult < 0) yield break;
            }
        }
        finally
        {
            try { unsubscribe(subscription); } catch { }
            try { free(subscription); } catch { }
        }
    }
}
