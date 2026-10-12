using System;

namespace UniFFISharp.Generator.Arbitration;

public enum InteropEngine
{
    Auto = 0,
    UniFFI = 1,
    BoltFFI = 2
}

public sealed class MethodEmissionConfig
{
    public bool EmitMethod { get; set; } = true;
    public bool IsPartial { get; set; } = false;
}

public sealed class UserPartialMethodInfo : IEquatable<UserPartialMethodInfo>
{
    public string ContainingNamespace { get; }
    public string ContainingTypeName { get; }
    public string MethodName { get; }
    public int ParameterCount { get; }
    public InteropEngine Engine { get; }
    public bool HasExplicitOverride { get; }

    public UserPartialMethodInfo(
        string containingNamespace,
        string containingTypeName,
        string methodName,
        int parameterCount,
        InteropEngine engine,
        bool hasExplicitOverride)
    {
        ContainingNamespace = containingNamespace ?? string.Empty;
        ContainingTypeName = containingTypeName ?? string.Empty;
        MethodName = methodName ?? string.Empty;
        ParameterCount = parameterCount;
        Engine = engine;
        HasExplicitOverride = hasExplicitOverride;
    }

    public bool Equals(UserPartialMethodInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(ContainingNamespace, other.ContainingNamespace, StringComparison.Ordinal) &&
               string.Equals(ContainingTypeName, other.ContainingTypeName, StringComparison.Ordinal) &&
               string.Equals(MethodName, other.MethodName, StringComparison.Ordinal) &&
               ParameterCount == other.ParameterCount &&
               Engine == other.Engine &&
               HasExplicitOverride == other.HasExplicitOverride;
    }

    public override bool Equals(object? obj) => Equals(obj as UserPartialMethodInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(ContainingNamespace);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(ContainingTypeName);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(MethodName);
            hash = hash * 31 + ParameterCount.GetHashCode();
            hash = hash * 31 + Engine.GetHashCode();
            hash = hash * 31 + HasExplicitOverride.GetHashCode();
            return hash;
        }
    }
}

public sealed class UserTypePreferenceInfo : IEquatable<UserTypePreferenceInfo>
{
    public string ContainingNamespace { get; }
    public string TypeName { get; }
    public InteropEngine Engine { get; }

    public UserTypePreferenceInfo(string containingNamespace, string typeName, InteropEngine engine)
    {
        ContainingNamespace = containingNamespace ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        Engine = engine;
    }

    public bool Equals(UserTypePreferenceInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(ContainingNamespace, other.ContainingNamespace, StringComparison.Ordinal) &&
               string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
               Engine == other.Engine;
    }

    public override bool Equals(object? obj) => Equals(obj as UserTypePreferenceInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(ContainingNamespace);
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(TypeName);
            hash = hash * 31 + Engine.GetHashCode();
            return hash;
        }
    }
}
