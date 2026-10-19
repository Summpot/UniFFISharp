using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniFFISharp.BoltFfi;
using Xunit;

namespace UniFFISharp.Tests;

public class BoltFfiRuntimeTests
{
    [Fact]
    public void WireWriterAndReader_RoundTripsPrimitives()
    {
        var writer = new WireWriter();
        writer.WriteBool(true);
        writer.WriteBool(false);
        writer.WriteI8(-42);
        writer.WriteU8(200);
        writer.WriteI16(-12345);
        writer.WriteU16(54321);
        writer.WriteI32(-987654321);
        writer.WriteU32(3141592653);
        writer.WriteI64(-1234567890123456L);
        writer.WriteU64(9876543210987654UL);
        writer.WriteF32(3.14159f);
        writer.WriteF64(2.718281828459);
        writer.WriteString("Hello, BoltFFI!");
        writer.WriteBytes(new byte[] { 1, 2, 3, 4, 5 });

        var bytes = writer.ToArray();
        var reader = new WireReader(bytes);

        Assert.True(reader.ReadBool());
        Assert.False(reader.ReadBool());
        Assert.Equal(-42, reader.ReadI8());
        Assert.Equal(200, reader.ReadU8());
        Assert.Equal(-12345, reader.ReadI16());
        Assert.Equal(54321, reader.ReadU16());
        Assert.Equal(-987654321, reader.ReadI32());
        Assert.Equal(3141592653U, reader.ReadU32());
        Assert.Equal(-1234567890123456L, reader.ReadI64());
        Assert.Equal(9876543210987654UL, reader.ReadU64());
        Assert.Equal(3.14159f, reader.ReadF32(), 5);
        Assert.Equal(2.718281828459, reader.ReadF64(), 10);
        Assert.Equal("Hello, BoltFFI!", reader.ReadString());
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, reader.ReadBytes());
    }

    [Fact]
    public void WireWriterAndReader_RoundTripsDatesGuidsUris()
    {
        var writer = new WireWriter();
        var duration = TimeSpan.FromSeconds(1234.567);
        var now = DateTimeOffset.UtcNow;
        var guid = Guid.NewGuid();
        var uri = new Uri("https://github.com/Summ/UniFFISharp");

        writer.WriteDuration(duration);
        writer.WriteDateTime(now);
        writer.WriteGuid(guid);
        writer.WriteUri(uri);

        var reader = new WireReader(writer.ToArray());

        Assert.Equal(duration.TotalMilliseconds, reader.ReadDuration().TotalMilliseconds, 1);
        Assert.Equal(now.ToUnixTimeMilliseconds(), reader.ReadDateTime().ToUnixTimeMilliseconds());
        Assert.Equal(guid, reader.ReadGuid());
        Assert.Equal(uri, reader.ReadUri());
    }

    [Fact]
    public void WireWriterAndReader_RoundTripsCollectionsAndOptionals()
    {
        var writer = new WireWriter();

        var list = new List<string> { "apple", "banana", "cherry" };
        var dict = new Dictionary<string, int> { ["x"] = 10, ["y"] = 20 };
        var set = new HashSet<int> { 100, 200, 300 };

        writer.WriteList(list, (w, s) => w.WriteString(s));
        writer.WriteMap(dict, (w, k) => w.WriteString(k), (w, v) => w.WriteI32(v));
        writer.WriteSet(set, (w, v) => w.WriteI32(v));

        writer.WriteOptionString("present");
        writer.WriteOptionString(null);
        writer.WriteOptionI32(42);
        writer.WriteOptionI32(null);

        var reader = new WireReader(writer.ToArray());

        var readList = reader.ReadList((ref WireReader r) => r.ReadString());
        Assert.Equal(list, readList);

        var readDict = reader.ReadMap((ref WireReader r) => r.ReadString(), (ref WireReader r) => r.ReadI32());
        Assert.Equal(dict, readDict);

        var readSet = reader.ReadSet((ref WireReader r) => r.ReadI32());
        Assert.Equal(set, readSet);

        Assert.Equal("present", reader.ReadOptionString());
        Assert.Null(reader.ReadOptionString());
        Assert.Equal(42, reader.ReadOptionI32());
        Assert.Null(reader.ReadOptionI32());
    }

    [Fact]
    public async Task BoltFfiAsync_CallAsync_PollsAndCompletesSuccessfully()
    {
        int pollCount = 0;
        nint dummyFuture = (nint)12345;

        var task = BoltFfiAsync.CallAsync<int>(
            start: () => dummyFuture,
            poll: (future, cbData, cb) =>
            {
                pollCount++;
                if (pollCount == 1)
                {
                    // Cooperative async continuation callback invocation
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        unsafe
                        {
                            ((delegate* unmanaged[Cdecl]<ulong, sbyte, void>)cb)(cbData, 0);
                        }
                    });
                }
            },
            complete: future =>
            {
                Assert.Equal(dummyFuture, future);
                return 42;
            },
            cancel: _ => { },
            free: _ => { },
            cancellationToken: CancellationToken.None);

        int result = await task;
        Assert.Equal(42, result);
        Assert.True(pollCount >= 1);
    }

    [Fact]
    public async Task BoltFfiAsync_CallAsync_ThrowsOnCancellation()
    {
        nint dummyFuture = (nint)54321;
        bool cancelledCalled = false;
        bool freedCalled = false;
        using var cts = new CancellationTokenSource();

        var task = BoltFfiAsync.CallAsync<int>(
            start: () => dummyFuture,
            poll: (future, cbData, cb) =>
            {
                // Do not complete, trigger cancellation
                cts.Cancel();
            },
            complete: _ => 0,
            cancel: future =>
            {
                cancelledCalled = true;
            },
            free: future =>
            {
                freedCalled = true;
            },
            cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.True(cancelledCalled);
        Assert.True(freedCalled);
    }

    [Fact]
    public void BoltFfiAsync_ThrowIfStatus_ThrowsBoltFfiExceptionOnFailure()
    {
        var status = new FfiStatus
        {
            Code = 2
        };

        var ex = Assert.Throws<BoltFfiException>(() => BoltFfiAsync.ThrowIfStatus(status));
        Assert.Equal(2, ex.StatusCode);
        Assert.Contains("status code 2", ex.Message);
    }

    [Fact]
    public async Task BoltFfiStreamHelper_ReadsAllItemsUntilExhausted()
    {
        ulong streamHandle = 999;
        int batchIndex = 0;
        bool unsubscribed = false;
        bool freed = false;

        var items = new List<string>();
        await foreach (var item in BoltFfiStreamHelper.ReadAll<string>(
            subscribe: () => streamHandle,
            popBatch: (handle, max) =>
            {
                batchIndex++;
                if (batchIndex == 1)
                {
                    return new[] { "item1", "item2" };
                }
                if (batchIndex == 2)
                {
                    return new[] { "item3" };
                }
                return Array.Empty<string>();
            },
            wait: (handle, max) =>
            {
                return batchIndex >= 2 ? -1 : 0;
            },
            unsubscribe: handle => unsubscribed = true,
            free: handle => freed = true))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { "item1", "item2", "item3" }, items);
        Assert.True(unsubscribed);
        Assert.True(freed);
    }

    [Fact]
    public unsafe void WireReader_StackallocAndSpan_ZeroAllocAndAdvancesPosition()
    {
        Span<byte> buffer = stackalloc byte[32];
        var writer = new WireWriter();
        writer.WriteI32(123456);
        writer.WriteString("ZeroAlloc");
        var written = writer.WrittenSpan;
        written.CopyTo(buffer);

        // Construct directly from Span - no heap allocation, completely safe ref struct
        var reader = new WireReader(buffer.Slice(0, written.Length));
        Assert.Equal(0, reader.Position);
        Assert.Equal(written.Length, reader.Length);

        Assert.Equal(123456, reader.ReadI32());
        Assert.Equal(4, reader.Position);

        Assert.Equal("ZeroAlloc", reader.ReadString());
        Assert.Equal(written.Length, reader.Position);
    }

    [Fact]
    public void WireWriter_UnmanagedList_RoundtripsCorrectly()
    {
        var list = new List<int> { 10, 20, 30, -500, int.MaxValue };
        var writer = new WireWriter();
        writer.WriteUnmanagedList(list);

        var reader = new WireReader(writer.WrittenSpan);
        var output = reader.ReadUnmanagedList<int>();
        Assert.Equal(list, output);
    }

    [Fact]
    public void WireReader_CorruptListCount_ThrowsEndOfStreamException()
    {
        var writer = new WireWriter();
        writer.WriteU32(1_000_000);

        var reader = new WireReader(writer.WrittenSpan);
        bool threw = false;
        try
        {
            reader.ReadUnmanagedList<int>();
        }
        catch (System.IO.EndOfStreamException ex)
        {
            threw = true;
            Assert.Contains("exceeds remaining buffer bytes", ex.Message);
        }
        Assert.True(threw);
    }
}

