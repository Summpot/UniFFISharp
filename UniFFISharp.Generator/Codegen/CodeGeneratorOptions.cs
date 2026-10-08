using Microsoft.CodeAnalysis.CSharp;

namespace UniFFISharp.Generator.Codegen;

/// <summary>
/// Configuration options for code generation in UniFFISharp.
/// </summary>
public class CodeGeneratorOptions
{
    /// <summary>
    /// The target project's C# language version, automatically inferred from Roslyn compiler options.
    /// </summary>
    public LanguageVersion LanguageVersion { get; set; } = LanguageVersion.Default;

    /// <summary>
    /// Indicates whether the target compilation includes native union metadata support
    /// (e.g. System.Runtime.CompilerServices.UnionAttribute).
    /// </summary>
    public bool HasUnionAttributeSupport { get; set; }

    /// <summary>
    /// Explicit override for generating C# 15 native union types.
    /// If null, automatically determined by whether LanguageVersion >= CSharp 15 (1500)
    /// or Preview with union metadata support.
    /// </summary>
    public bool? UseNativeUnions { get; set; }

    /// <summary>
    /// Whether C# 15 native union syntax should be emitted.
    /// </summary>
    public bool EffectiveUseNativeUnions =>
        UseNativeUnions ?? (
            ((int)LanguageVersion >= 1500 && LanguageVersion != LanguageVersion.Preview && LanguageVersion != LanguageVersion.Latest)
            || ((LanguageVersion == LanguageVersion.Preview || LanguageVersion == LanguageVersion.Latest) && HasUnionAttributeSupport)
        );
}
