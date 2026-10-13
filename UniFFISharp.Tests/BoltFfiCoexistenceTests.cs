using UniFFISharp.Generator.Arbitration;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;
using Xunit;

namespace UniFFISharp.Tests;

public class BoltFfiCoexistenceTests
{
    private static BoltFfiMetadataEnvelope Envelope(string functions) => BoltFfiMetadataEnvelope.Parse($$$$"""
        {"magic":"boltffi.bindings","format":{"major":0,"minor":1},"surface":"Native",
         "package":{"name":{"parts":["pixeval_native"]}},
         "bindings":{"Native":{"decls":[{{{{functions}}}}]}}}
        """)!;

    private static string Function(string name, string crate, string returns = "\"Bool\"", string execution = "\"Synchronous\"", string parameters = "[]") => $$$$"""
        {"Function":{"name":{"parts":["{{{{name}}}}"]},
         "symbol":{"name":"boltffi_function_{{{{crate}}}}_{{{{name}}}}"},
         "callable":{"receiver":null,"params":{{{{parameters}}}},
         "returns":{"plan":{"DirectViaReturnSlot":{"ty":{{{{returns}}}}}}},
         "error":"None","execution":{{{{execution}}}}}}}
        """;

    [Fact]
    public void AggregateExportsKeepTheirComponentCratesAndAreDeduplicated()
    {
        var envelope = Envelope(Function("version", "pixeval_native", "\"String\"") + "," +
            Function("is_pixiv_host", "pixeval_maho") + "," +
            Function("locate_server_name", "pixeval_maho", "{\"Optional\":\"String\"}"));
        var routed = BoltFfiCoexistence.Prepare(new[] { envelope, envelope }, new[] { "pixeval_native", "pixeval_maho" });

        Assert.Equal(2, routed.Count);
        var maho = Assert.Single(routed, e => e.Package.PackageName == "pixeval_maho");
        Assert.Equal("PixevalMaho", maho.Package.Name.ToPascalCase());
        Assert.Equal("IsPixivHost", Assert.Single(maho.Bindings.Native!.Decls).DeclName);
        Assert.Equal(3, envelope.Bindings.Native!.Decls.Count);
    }

    [Theory]
    [InlineData("{\"Optional\":\"String\"}")]
    [InlineData("{\"Sequence\":\"Bytes\"}")]
    [InlineData("{\"Map\":{\"key\":\"String\",\"value\":\"Bytes\"}}")]
    [InlineData("{\"Record\":42}")]
    public void UnsupportedReturnShapesRetainUniFfi(string returns)
    {
        var function = Envelope(Function("test", "pixeval_native", returns)).Bindings.Native!.Decls[0].Function!;
        Assert.False(BoltFfiCoexistence.CanBind(function));
    }

    [Fact]
    public void NativeAsyncProtocolIsRecognizedAndRetainsUniFfi()
    {
        var function = Envelope(Function("ping", "pixeval_native", "\"String\"", "{\"Asynchronous\":{}}")).Bindings.Native!.Decls[0].Function!;
        Assert.True(function.IsAsync);
        Assert.False(BoltFfiCoexistence.CanBind(function));
    }

    [Fact]
    public void MissingExportCannotBeSelectedByAnOverride()
    {
        var uniffi = new FnMetadata { Name = "ping" };
        Assert.Equal(InteropEngine.UniFFI, InteropArbitrator.SelectBestEngine(uniffi, null, InteropEngine.BoltFFI));
        var bolt = new BoltFfiFunctionDecl { Name = new BoltFfiCanonicalName { Parts = { "test" } } };
        Assert.Equal(InteropEngine.BoltFFI, InteropArbitrator.SelectBestEngine(null, bolt, InteropEngine.UniFFI));
    }

    [Fact]
    public void RustBooleansUseOneByteMarshalling()
    {
        var envelope = Envelope(Function("invert", "pixeval_native", parameters:
            "[{\"name\":{\"parts\":[\"value\"]},\"payload\":{\"Value\":{\"Direct\":{\"ty\":\"Bool\"}}}}]"));
        string source = BoltFfiCodeGenerator.Generate(envelope, "pixeval_native.dll", "Pixeval.Ffi");
        Assert.Contains("public static partial class PixevalNativeMethods", source);
        Assert.Contains("[return: MarshalAs(UnmanagedType.U1)]", source);
        Assert.Contains("[MarshalAs(UnmanagedType.U1)] bool value", source);
    }
}
