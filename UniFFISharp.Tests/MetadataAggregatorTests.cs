using Xunit;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Tests;

public class MetadataAggregatorTests
{
    [Fact]
    public void TestSingleCrateAggregation()
    {
        var agg = new MetadataAggregator();
        agg.AddItem(new NamespaceMetadata { CrateName = "my_crate", Name = "my_crate" });
        agg.AddItem(new FnMetadata { ModulePath = "my_crate::api", Name = "do_something" });

        var all = agg.BuildAll();
        Assert.Single(all);
        Assert.Equal("my_crate", all[0].CrateName);
        Assert.Single(all[0].Functions);
        Assert.Equal("do_something", all[0].Functions[0].Name);
    }

    [Fact]
    public void TestMultiCrateAggregation()
    {
        var agg = new MetadataAggregator();
        // Umbrella crate
        agg.AddItem(new NamespaceMetadata { CrateName = "umbrella", Name = "umbrella" });
        agg.AddItem(new FnMetadata { ModulePath = "umbrella", Name = "umbrella_init" });

        // Sub crate A
        agg.AddItem(new FnMetadata { ModulePath = "sub_alpha::service", Name = "login" });
        agg.AddItem(new RecordMetadata { ModulePath = "sub_alpha::models", Name = "UserSession" });

        // Sub crate B (with hyphen in crate name)
        agg.AddItem(new RecordMetadata { ModulePath = "sub-beta::crypto", Name = "KeyRecord" });

        var all = agg.BuildAll();
        Assert.Equal(3, all.Count);

        // First is umbrella
        Assert.Equal("umbrella", all[0].CrateName);
        Assert.Single(all[0].Functions);
        Assert.Equal("umbrella_init", all[0].Functions[0].Name);
        Assert.Empty(all[0].Records);

        // Second is sub_alpha
        Assert.Equal("sub_alpha", all[1].CrateName);
        Assert.Single(all[1].Functions);
        Assert.Equal("login", all[1].Functions[0].Name);
        Assert.Single(all[1].Records);
        Assert.Equal("UserSession", all[1].Records[0].Name);

        // Third is sub_beta
        Assert.Equal("sub_beta", all[2].CrateName);
        Assert.Empty(all[2].Functions);
        Assert.Single(all[2].Records);
        Assert.Equal("KeyRecord", all[2].Records[0].Name);
    }

    [Fact]
    public void TestGetCrateName()
    {
        Assert.Equal("crate_a", MetadataAggregator.GetCrateName(new FnMetadata { ModulePath = "crate-a::foo::bar" }));
        Assert.Equal("crate_b", MetadataAggregator.GetCrateName(new RecordMetadata { ModulePath = "crate_b" }));
        Assert.Equal("umbrella", MetadataAggregator.GetCrateName(new NamespaceMetadata { CrateName = "umbrella" }));
    }
}
