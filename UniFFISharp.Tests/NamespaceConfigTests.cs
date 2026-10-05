using Xunit;
using UniFFISharp.Generator;

namespace UniFFISharp.Tests;

public class NamespaceConfigTests
{
    [Fact]
    public void TestEmptyConfig()
    {
        var config = new NamespaceConfig("");
        Assert.Equal(string.Empty, config.RootNamespace);
        Assert.Equal("MyCrate", config.ResolveNamespace("my_crate", true));
        Assert.Equal("SubCrate", config.ResolveNamespace("sub_crate", false));
    }

    [Fact]
    public void TestSingleRootNamespace()
    {
        var config = new NamespaceConfig("MyCompany.Sdk");
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("umbrella", true));
        Assert.Equal("MyCompany.Sdk.SubAlpha", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("MyCompany.Sdk.SubBeta", config.ResolveNamespace("sub-beta", false));
    }

    [Fact]
    public void TestMultiCrateRelativeAndGlobal()
    {
        var config = new NamespaceConfig("MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor;sub_dot=.Deep.Feature;sub_same=");
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);

        // Root crate
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("umbrella", true));

        // Relative sub-crate
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));

        // Global sub-crate
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));

        // Sub-crate with leading dot
        Assert.Equal("MyCompany.Sdk.Deep.Feature", config.ResolveNamespace("sub_dot", false));

        // Sub-crate placed in root namespace
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("sub_same", false));

        // Unmapped sub-crate
        Assert.Equal("MyCompany.Sdk.SubOther", config.ResolveNamespace("sub_other", false));
    }

    [Fact]
    public void TestHyphenatedCrateInMapping()
    {
        var config = new NamespaceConfig("MyCompany.Sdk;sub-alpha=Security");
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub-alpha", false));
    }

    [Fact]
    public void TestNoRootNamespaceOnlyMappings()
    {
        var config = new NamespaceConfig("sub_alpha=Security;sub_legacy=global::LegacyVendor");
        Assert.Equal(string.Empty, config.RootNamespace);
        Assert.Equal("Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
        Assert.Equal("Umbrella", config.ResolveNamespace("umbrella", true));
        Assert.Equal("SubOther", config.ResolveNamespace("sub_other", false));
    }
}
