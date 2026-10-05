using Microsoft.CodeAnalysis;
using Xunit;
using UniFFISharp.Generator;

namespace UniFFISharp.Tests;

public class NamespaceConfigTests
{
    [Fact]
    public void TestEditorConfigCommentBehavior()
    {
        string editorConfigWithSemicolon = "is_global = true\r\nbuild_property.UniFFINamespace = MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor\r\n";
        string editorConfigWithComma = "is_global = true\r\nbuild_property.UniFFINamespace = MyCompany.Sdk,sub_alpha=Security,sub_legacy=global::LegacyVendor\r\n";
        string editorConfigWithPipe = "is_global = true\r\nbuild_property.UniFFINamespace = MyCompany.Sdk|sub_alpha=Security|sub_legacy=global::LegacyVendor\r\n";
        
        var match = typeof(Microsoft.CodeAnalysis.Compilation).Assembly.GetType("Microsoft.CodeAnalysis.AnalyzerConfig")!;
        var parseMethod = match.GetMethod("Parse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null)!;
        
        // 1. Semicolon gets truncated because ';' is an EditorConfig comment!
        var configSemi = parseMethod.Invoke(null, new object[] { editorConfigWithSemicolon, "C:\\dummy1.editorconfig" })!;
        var valSemi = GetProperty(configSemi, "build_property.UniFFINamespace");
        Assert.Equal("MyCompany.Sdk", valSemi); // Truncated!

        // 2. Comma is NOT a comment! Preserves entire string!
        var configComma = parseMethod.Invoke(null, new object[] { editorConfigWithComma, "C:\\dummy2.editorconfig" })!;
        var valComma = GetProperty(configComma, "build_property.UniFFINamespace");
        Assert.Equal("MyCompany.Sdk,sub_alpha=Security,sub_legacy=global::LegacyVendor", valComma);

        // 3. Pipe is also NOT a comment!
        var configPipe = parseMethod.Invoke(null, new object[] { editorConfigWithPipe, "C:\\dummy3.editorconfig" })!;
        var valPipe = GetProperty(configPipe, "build_property.UniFFINamespace");
        Assert.Equal("MyCompany.Sdk|sub_alpha=Security|sub_legacy=global::LegacyVendor", valPipe);

        // 4. _UniFFINamespaceEncoded (URL encoded by MSBuild) survives EditorConfig with zero truncation!
        string rawWithSemi = "MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor";
        string escapedUri = System.Uri.EscapeDataString(rawWithSemi);
        string editorConfigWithEncoded = $"is_global = true\r\nbuild_property._UniFFINamespaceEncoded = {escapedUri}\r\n";
        var configEncoded = parseMethod.Invoke(null, new object[] { editorConfigWithEncoded, "C:\\dummy4.editorconfig" })!;
        var valEncoded = GetProperty(configEncoded, "build_property._UniFFINamespaceEncoded");
        Assert.NotNull(valEncoded);
        var nsConfigFromEncoded = new NamespaceConfig(valEncoded!);
        Assert.Equal("MyCompany.Sdk", nsConfigFromEncoded.RootNamespace);
        Assert.Equal("MyCompany.Sdk.Security", nsConfigFromEncoded.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", nsConfigFromEncoded.ResolveNamespace("sub_legacy", false));

        // 5. Base64 in EditorConfig also survives with zero truncation!
        string b64 = "base64:" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rawWithSemi));
        string editorConfigWithB64 = $"is_global = true\r\nbuild_property.UniFFINamespace = {b64}\r\n";
        var configB64 = parseMethod.Invoke(null, new object[] { editorConfigWithB64, "C:\\dummy5.editorconfig" })!;
        var valB64 = GetProperty(configB64, "build_property.UniFFINamespace");
        Assert.NotNull(valB64);
        var nsConfigFromB64 = new NamespaceConfig(valB64!);
        Assert.Equal("MyCompany.Sdk", nsConfigFromB64.RootNamespace);
        Assert.Equal("MyCompany.Sdk.Security", nsConfigFromB64.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", nsConfigFromB64.ResolveNamespace("sub_legacy", false));
    }

    private static string? GetProperty(object config, string key)
    {
        var globalSection = config.GetType().GetProperty("GlobalSection", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(config);
        var propsDict = globalSection!.GetType().GetProperty("Properties", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(globalSection);
        var itemProp = propsDict!.GetType().GetProperty("Item", new[] { typeof(string) });
        return itemProp?.GetValue(propsDict, new object[] { key })?.ToString();
    }

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

    [Fact]
    public void TestMultiCrateWithCommas()
    {
        var config = new NamespaceConfig("MyCompany.Sdk, sub_alpha=Security, sub_legacy=global::LegacyVendor, sub_dot=.Deep.Feature, sub_same=");
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("umbrella", true));
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
        Assert.Equal("MyCompany.Sdk.Deep.Feature", config.ResolveNamespace("sub_dot", false));
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("sub_same", false));
        Assert.Equal("MyCompany.Sdk.SubOther", config.ResolveNamespace("sub_other", false));
    }

    [Fact]
    public void TestMultiCrateWithPipes()
    {
        var config = new NamespaceConfig("MyCompany.Sdk|sub_alpha=Security|sub_legacy=global::LegacyVendor|sub_dot=.Deep.Feature|sub_same=");
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk", config.ResolveNamespace("umbrella", true));
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
    }

    [Fact]
    public void TestBase64Config()
    {
        string raw = "MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor";
        string b64 = "base64:" + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
        var config = new NamespaceConfig(b64);
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
    }

    [Fact]
    public void TestHexConfig()
    {
        string raw = "MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        string hex = "hex:" + System.BitConverter.ToString(bytes).Replace("-", "");
        var config = new NamespaceConfig(hex);
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
    }

    [Fact]
    public void TestPercentEncodedConfig()
    {
        string raw = "MyCompany.Sdk;sub_alpha=Security;sub_legacy=global::LegacyVendor";
        string escaped = System.Uri.EscapeDataString(raw);
        var config = new NamespaceConfig(escaped);
        Assert.Equal("MyCompany.Sdk", config.RootNamespace);
        Assert.Equal("MyCompany.Sdk.Security", config.ResolveNamespace("sub_alpha", false));
        Assert.Equal("LegacyVendor", config.ResolveNamespace("sub_legacy", false));
    }
}
