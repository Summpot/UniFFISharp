using System;
using System.Collections;
using System.Collections.Generic;

namespace UniFFISharp.Generator.Arbitration;

public enum InteropEngine
{
    Auto = 0,
    UniFFI = 1,
    BoltFFI = 2
}

public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? _items;

    public EquatableArray(T[]? items)
    {
        _items = items;
    }

    public int Count => _items?.Length ?? 0;

    public T this[int index] => (_items ?? Array.Empty<T>())[index];

    public bool Equals(EquatableArray<T> other)
    {
        int count = Count;
        if (count != other.Count) return false;
        if (count == 0) return true;
        if (ReferenceEquals(_items, other._items)) return true;

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < count; i++)
        {
            if (!comparer.Equals(_items![i], other._items![i])) return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        int count = Count;
        if (count == 0) return 0;
        unchecked
        {
            var comparer = EqualityComparer<T>.Default;
            int hash = count;
            for (int i = 0; i < count; i++)
            {
                T item = _items![i];
                hash = (hash * 397) ^ (item is null ? 0 : comparer.GetHashCode(item));
            }
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class MethodEmissionConfig
{
    public bool EmitMethod { get; set; } = true;
    public bool IsPartial { get; set; } = false;
    public bool SuppressGCTransition { get; set; } = false;
}

public sealed class UserPartialMethodInfo : IEquatable<UserPartialMethodInfo>
{
    public string ContainingNamespace { get; }
    public string ContainingTypeName { get; }
    public string MethodName { get; }
    public int ParameterCount { get; }
    public InteropEngine Engine { get; }
    public bool HasExplicitOverride { get; }
    public bool SuppressGCTransition { get; }

    public UserPartialMethodInfo(
        string containingNamespace,
        string containingTypeName,
        string methodName,
        int parameterCount,
        InteropEngine engine,
        bool hasExplicitOverride,
        bool suppressGCTransition = false)
    {
        ContainingNamespace = containingNamespace ?? string.Empty;
        ContainingTypeName = containingTypeName ?? string.Empty;
        MethodName = methodName ?? string.Empty;
        ParameterCount = parameterCount;
        Engine = engine;
        HasExplicitOverride = hasExplicitOverride;
        SuppressGCTransition = suppressGCTransition;
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
               HasExplicitOverride == other.HasExplicitOverride &&
               SuppressGCTransition == other.SuppressGCTransition;
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
            hash = hash * 31 + SuppressGCTransition.GetHashCode();
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

public sealed class CallbackImplementationInfo : IEquatable<CallbackImplementationInfo>
{
    public string FullyQualifiedTypeName { get; }
    public string TypeName { get; }
    public bool IsSealed { get; }
    public List<string> ImplementedInterfaces { get; }

    public CallbackImplementationInfo(
        string fullyQualifiedTypeName,
        string typeName,
        bool isSealed,
        List<string> implementedInterfaces)
    {
        FullyQualifiedTypeName = fullyQualifiedTypeName ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        IsSealed = isSealed;
        ImplementedInterfaces = implementedInterfaces ?? new List<string>();
    }

    public bool Equals(CallbackImplementationInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!string.Equals(FullyQualifiedTypeName, other.FullyQualifiedTypeName, StringComparison.Ordinal) ||
            !string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) ||
            IsSealed != other.IsSealed ||
            ImplementedInterfaces.Count != other.ImplementedInterfaces.Count)
        {
            return false;
        }

        for (int i = 0; i < ImplementedInterfaces.Count; i++)
        {
            if (!string.Equals(ImplementedInterfaces[i], other.ImplementedInterfaces[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as CallbackImplementationInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = StringComparer.Ordinal.GetHashCode(FullyQualifiedTypeName);
            hash = (hash * 397) ^ IsSealed.GetHashCode();
            hash = (hash * 397) ^ ImplementedInterfaces.Count.GetHashCode();
            foreach (var iface in ImplementedInterfaces)
            {
                hash = (hash * 397) ^ (iface != null ? StringComparer.Ordinal.GetHashCode(iface) : 0);
            }
            return hash;
        }
    }
}
