using System;
using System.Text;
using UniFFISharp.BoltFfi;
using Xunit;

namespace UniFFISharp.Tests;

public class BoltFfiMultiParamWireWriterTests
{
    [Fact]
    public void Rent_ReturnsDistinctInstances_WhenMultipleRentsAreActive()
    {
        var w1 = WireWriter.Rent();
        var w2 = WireWriter.Rent();
        var w3 = WireWriter.Rent();

        try
        {
            Assert.NotSame(w1, w2);
            Assert.NotSame(w2, w3);
            Assert.NotSame(w1, w3);

            // Write distinct content to each
            w1.WriteString("FirstParameterPayload");
            w2.WriteString("SecondParameterPayload");
            w3.WriteString("ThirdParameterPayload");

            // Verify content does not collide or overwrite
            Assert.Equal("FirstParameterPayload", Encoding.UTF8.GetString(w1.WrittenSpan.Slice(4)));
            Assert.Equal("SecondParameterPayload", Encoding.UTF8.GetString(w2.WrittenSpan.Slice(4)));
            Assert.Equal("ThirdParameterPayload", Encoding.UTF8.GetString(w3.WrittenSpan.Slice(4)));
        }
        finally
        {
            WireWriter.Return(w3);
            WireWriter.Return(w2);
            WireWriter.Return(w1);
        }
    }

    [Fact]
    public void Return_AllowsInstanceReuse_WithResetPosition()
    {
        var w1 = WireWriter.Rent();
        w1.WriteString("TemporaryData");
        Assert.True(w1.Length > 0);
        WireWriter.Return(w1);

        // Next rent should reuse the returned instance with position reset to 0
        var reused = WireWriter.Rent();
        try
        {
            Assert.Same(w1, reused);
            Assert.Equal(0, reused.Length);
        }
        finally
        {
            WireWriter.Return(reused);
        }
    }

    [Fact]
    public void Return_IgnoresOversizedBuffers_ToPreventMemoryLeak()
    {
        var w = WireWriter.Rent();
        // Artificially grow buffer beyond 64KB
        byte[] largeData = new byte[70 * 1024];
        w.WriteBytes(largeData);
        Assert.True(w.Buffer.Length > 64 * 1024);

        WireWriter.Return(w);

        // Renting now should give a fresh instance rather than the oversized one
        var fresh = WireWriter.Rent();
        try
        {
            Assert.NotSame(w, fresh);
        }
        finally
        {
            WireWriter.Return(fresh);
        }
    }
}
