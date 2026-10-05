using System.Collections.Generic;
using Xunit;
using UniFFISharp.Generator.Codegen;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Tests;

public class TypeHelperTests
{
    [Fact]
    public void TestLocalTypeHasNoPrefix()
    {
        var recType = new UniFFIType
        {
            Kind = TypeKind.Record,
            Name = "user_info",
            ModulePath = "sub_alpha::models"
        };

        var nsResolver = new System.Func<string, string>(c => c == "sub_alpha" ? "MyCompany.Sdk.Security" : "MyCompany.Sdk");

        string csType = TypeHelper.ToCSharpType(recType, "sub_alpha", nsResolver);
        Assert.Equal("UserInfo", csType);

        string convInst = TypeHelper.ConverterInstance(recType, "sub_alpha", nsResolver);
        Assert.Equal("FfiConverterTypeUserInfo.INSTANCE", convInst);
    }

    [Fact]
    public void TestExternalTypeHasNamespacePrefixWhenDifferent()
    {
        var recType = new UniFFIType
        {
            Kind = TypeKind.Record,
            Name = "user_info",
            ModulePath = "sub_alpha::models"
        };

        var nsResolver = new System.Func<string, string>(c => c == "sub_alpha" ? "MyCompany.Sdk.Security" : "MyCompany.Sdk.Core");

        // Current crate is core, referencing sub_alpha type
        string csType = TypeHelper.ToCSharpType(recType, "core", nsResolver);
        Assert.Equal("MyCompany.Sdk.Security.UserInfo", csType);

        string convInst = TypeHelper.ConverterInstance(recType, "core", nsResolver);
        Assert.Equal("MyCompany.Sdk.Security.FfiConverterTypeUserInfo.INSTANCE", convInst);
    }

    [Fact]
    public void TestExternalTypeHasNoPrefixWhenSameNamespace()
    {
        var recType = new UniFFIType
        {
            Kind = TypeKind.Record,
            Name = "user_info",
            ModulePath = "sub_alpha::models"
        };

        // Both crates share the root namespace
        var nsResolver = new System.Func<string, string>(c => "MyCompany.Sdk");

        string csType = TypeHelper.ToCSharpType(recType, "umbrella", nsResolver);
        Assert.Equal("UserInfo", csType);

        string convInst = TypeHelper.ConverterInstance(recType, "umbrella", nsResolver);
        Assert.Equal("FfiConverterTypeUserInfo.INSTANCE", convInst);
    }

    [Fact]
    public void TestCompositeTypeWithExternalInnerType()
    {
        var innerType = new UniFFIType
        {
            Kind = TypeKind.Record,
            Name = "user_info",
            ModulePath = "sub_alpha::models"
        };

        var seqType = new UniFFIType
        {
            Kind = TypeKind.Sequence,
            InnerType = innerType
        };

        var nsResolver = new System.Func<string, string>(c => c == "sub_alpha" ? "MyCompany.Sdk.Security" : "MyCompany.Sdk.Core");

        string csType = TypeHelper.ToCSharpType(seqType, "core", nsResolver);
        Assert.Equal("List<MyCompany.Sdk.Security.UserInfo>", csType);
    }
}
