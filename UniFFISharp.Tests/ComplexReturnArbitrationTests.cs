using System.Collections.Generic;
using UniFFISharp.Generator.Arbitration;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;
using Xunit;

namespace UniFFISharp.Tests;

public class ComplexReturnArbitrationTests
{
    [Fact]
    public void ParsedEncodedRecordCollection_PrefersUniFFI()
    {
        var envelope = BoltFfiMetadataEnvelope.Parse("""
            {
              "magic": "boltffi.bindings",
              "bindings": { "Native": { "decls": [
                { "Record": { "Encoded": {
                  "id": 0, "name": { "parts": ["entity"] },
                  "fields": [{ "key": "title", "ty": "String" }]
                } } },
                { "Function": {
                  "name": { "parts": ["query", "entities"] },
                  "symbol": "boltffi_function_bench_query_entities",
                  "callable": {
                    "execution": "Synchronous", "params": [], "error": "None",
                    "returns": { "plan": { "EncodedViaReturnSlot": {
                      "ty": { "Sequence": { "Record": 0 } }
                    } } }
                  }
                } }
              ] } }
            }
            """);
        Assert.NotNull(envelope);
        var bolt = envelope.Bindings.Native!.Decls[1].Function!;
        Assert.False(bolt.ReturnType.InnerType!.IsDirectRecord);
        Assert.Equal("Entity", bolt.ReturnType.InnerType.CustomTypeName);
        var (uniffiType, _) = RecordShape("sequence", direct: false);

        Assert.Equal(InteropEngine.UniFFI,
            InteropArbitrator.SelectBestEngine(new FnMetadata { ReturnType = uniffiType }, bolt));
    }

    [Theory]
    [InlineData("record", false)]
    [InlineData("option", false)]
    [InlineData("sequence", false)]
    [InlineData("set", false)]
    [InlineData("map-key", false)]
    [InlineData("map-value", false)]
    [InlineData("nested", false)]
    [InlineData("record", true)]
    [InlineData("option", true)]
    [InlineData("sequence", true)]
    [InlineData("set", true)]
    [InlineData("map-key", true)]
    [InlineData("map-value", true)]
    [InlineData("nested", true)]
    public void RecordReturn_UsesEncodingAndDirection(string shape, bool direct)
    {
        var (uniffiType, boltType) = RecordShape(shape, direct);
        var uniffi = new FnMetadata
        {
            Name = "query_entities",
            ReturnType = uniffiType,
            Inputs = new List<FnParamMetadata>
            {
                new() { Name = "count", Type = UniFFIType.Primitive(TypeKind.UInt32) }
            }
        };
        var bolt = new BoltFfiFunctionDecl
        {
            ReturnType = boltType,
            Parameters = new List<BoltFfiParamDecl>
            {
                new() { Name = "count", Type = BoltFfiType.Primitive(BoltFfiTypeKind.UInt32) }
            }
        };

        Assert.Equal(direct ? InteropEngine.BoltFFI : InteropEngine.UniFFI,
            InteropArbitrator.SelectBestEngine(uniffi, bolt));
        Assert.Equal(InteropEngine.BoltFFI,
            InteropArbitrator.SelectBestEngine(uniffi, bolt, InteropEngine.BoltFFI));

        // The same shape used as an input must not trigger the return-decoding preference.
        uniffi.Inputs = new List<FnParamMetadata> { new() { Name = "entities", Type = uniffiType } };
        uniffi.ReturnType = UniFFIType.Primitive(TypeKind.UInt64);
        bolt.Parameters = new List<BoltFfiParamDecl> { new() { Name = "entities", Type = boltType } };
        bolt.ReturnType = BoltFfiType.Primitive(BoltFfiTypeKind.UInt64);

        Assert.Equal(InteropEngine.BoltFFI, InteropArbitrator.SelectBestEngine(uniffi, bolt));
    }

    [Fact]
    public void EncodedRecordRoundtrip_PrefersReturnDecoder()
    {
        var (uniffiType, boltType) = RecordShape("sequence", direct: false);
        var uniffi = new FnMetadata
        {
            ReturnType = uniffiType,
            Inputs = new List<FnParamMetadata> { new() { Name = "entities", Type = uniffiType } }
        };
        var bolt = new BoltFfiFunctionDecl
        {
            ReturnType = boltType,
            Parameters = new List<BoltFfiParamDecl> { new() { Name = "entities", Type = boltType } }
        };

        Assert.Equal(InteropEngine.UniFFI, InteropArbitrator.SelectBestEngine(uniffi, bolt));
    }

    [Theory]
    [InlineData(TypeKind.Bytes, BoltFfiTypeKind.Bytes)]
    [InlineData(TypeKind.Sequence, BoltFfiTypeKind.Sequence)]
    [InlineData(TypeKind.Map, BoltFfiTypeKind.Map)]
    [InlineData(TypeKind.Set, BoltFfiTypeKind.Set)]
    [InlineData(TypeKind.Enum, BoltFfiTypeKind.Enum)]
    public void ReturnsWithoutEncodedRecords_RetainBoltFFI(TypeKind uniffiKind, BoltFfiTypeKind boltKind)
    {
        var uniffi = new FnMetadata
        {
            ReturnType = new UniFFIType
            {
                Kind = uniffiKind,
                InnerType = UniFFIType.Primitive(TypeKind.Int32),
                KeyType = UniFFIType.Primitive(TypeKind.String),
                ValueType = UniFFIType.Primitive(TypeKind.Int32)
            }
        };
        var bolt = new BoltFfiFunctionDecl
        {
            ReturnType = new BoltFfiType
            {
                Kind = boltKind,
                IsCStyleEnum = false,
                InnerType = BoltFfiType.Primitive(BoltFfiTypeKind.Int32),
                KeyType = BoltFfiType.Primitive(BoltFfiTypeKind.String),
                ValueType = BoltFfiType.Primitive(BoltFfiTypeKind.Int32)
            }
        };

        Assert.Equal(InteropEngine.BoltFFI, InteropArbitrator.SelectBestEngine(uniffi, bolt));
    }

    private static (UniFFIType, BoltFfiType) RecordShape(string shape, bool direct)
    {
        var uniffi = new UniFFIType { Kind = TypeKind.Record, Name = "Entity" };
        var bolt = new BoltFfiType
        {
            Kind = BoltFfiTypeKind.Record, CustomTypeName = "Entity", IsDirectRecord = direct
        };

        return shape switch
        {
            "option" => (new() { Kind = TypeKind.Option, InnerType = uniffi },
                new() { Kind = BoltFfiTypeKind.Option, InnerType = bolt }),
            "sequence" => (new() { Kind = TypeKind.Sequence, InnerType = uniffi },
                new() { Kind = BoltFfiTypeKind.Sequence, InnerType = bolt }),
            "set" => (new() { Kind = TypeKind.Set, InnerType = uniffi },
                new() { Kind = BoltFfiTypeKind.Set, InnerType = bolt }),
            "map-key" => (new() { Kind = TypeKind.Map, KeyType = uniffi, ValueType = UniFFIType.Primitive(TypeKind.Int32) },
                new() { Kind = BoltFfiTypeKind.Map, KeyType = bolt, ValueType = BoltFfiType.Primitive(BoltFfiTypeKind.Int32) }),
            "map-value" => (new() { Kind = TypeKind.Map, KeyType = UniFFIType.Primitive(TypeKind.String), ValueType = uniffi },
                new() { Kind = BoltFfiTypeKind.Map, KeyType = BoltFfiType.Primitive(BoltFfiTypeKind.String), ValueType = bolt }),
            "nested" => (new() { Kind = TypeKind.Option, InnerType = new() { Kind = TypeKind.Sequence, InnerType = uniffi } },
                new() { Kind = BoltFfiTypeKind.Option, InnerType = new() { Kind = BoltFfiTypeKind.Sequence, InnerType = bolt } }),
            _ => (uniffi, bolt)
        };
    }
}
