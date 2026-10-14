using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniFFISharp.Collections;
using UniFFISharp.Exceptions;
using Xunit;

namespace UniFFISharp.Tests;

public class ConcurrentHandleMapTests
{
    private sealed class TestItem
    {
        public string Name { get; set; }
        public TestItem(string name) => Name = name;
    }

    [Fact]
    public void BasicInsertGetRemove_WorksCorrectly()
    {
        var map = new ConcurrentHandleMap<TestItem>();
        var item1 = new TestItem("Alpha");
        var item2 = new TestItem("Beta");

        ulong h1 = map.Insert(item1);
        ulong h2 = map.Insert(item2);

        // Handles must be odd (lowest bit is 1)
        Assert.True((h1 & 1) == 1);
        Assert.True((h2 & 1) == 1);
        Assert.NotEqual(h1, h2);

        Assert.True(map.TryGet(h1, out var res1));
        Assert.Same(item1, res1);
        Assert.Same(item1, map.Get(h1));

        Assert.True(map.TryGet(h2, out var res2));
        Assert.Same(item2, res2);

        // Remove h1
        Assert.True(map.Remove(h1, out var removed1));
        Assert.Same(item1, removed1);

        // h1 is no longer valid
        Assert.False(map.TryGet(h1, out _));
        Assert.Throws<InternalException>(() => map.Get(h1));

        // h2 is still valid
        Assert.True(map.TryGet(h2, out var res2After));
        Assert.Same(item2, res2After);
    }

    [Fact]
    public void SlotReuse_IncrementsGeneration_InvalidatingOldHandle()
    {
        var map = new ConcurrentHandleMap<TestItem>();
        var item1 = new TestItem("First");

        ulong h1 = map.Insert(item1);
        Assert.True(map.Remove(h1));

        // Insert new item: it will reuse the slot vacated by h1
        var item2 = new TestItem("Second");
        ulong h2 = map.Insert(item2);

        // Slot indices match, but versions differ!
        int slot1 = (int)((h1 & 0xFFFFFFFFUL) >> 1);
        int slot2 = (int)((h2 & 0xFFFFFFFFUL) >> 1);
        Assert.Equal(slot1, slot2);
        Assert.NotEqual(h1, h2);

        // Old handle h1 must NOT be able to access the new item in the same slot!
        Assert.False(map.TryGet(h1, out _));

        // New handle h2 retrieves item2
        Assert.True(map.TryGet(h2, out var res2));
        Assert.Same(item2, res2);
    }

    [Fact]
    public void Multithreaded_HighConcurrency_MaintainsIntegrity()
    {
        var map = new ConcurrentHandleMap<TestItem>();
        const int threadCount = 8;
        const int opsPerThread = 5000;

        Parallel.For(0, threadCount, t =>
        {
            var localHandles = new List<ulong>();
            for (int i = 0; i < opsPerThread; i++)
            {
                var item = new TestItem($"T{t}_{i}");
                ulong h = map.Insert(item);
                localHandles.Add(h);

                Assert.True(map.TryGet(h, out var retrieved));
                Assert.Same(item, retrieved);

                // Occasionally remove half to exercise free-slot reuse
                if (localHandles.Count >= 10)
                {
                    ulong toRemove = localHandles[0];
                    localHandles.RemoveAt(0);
                    Assert.True(map.Remove(toRemove));
                    Assert.False(map.TryGet(toRemove, out _));
                }
            }

            // Cleanup remaining
            foreach (var h in localHandles)
            {
                Assert.True(map.Remove(h));
            }
        });
    }
}
