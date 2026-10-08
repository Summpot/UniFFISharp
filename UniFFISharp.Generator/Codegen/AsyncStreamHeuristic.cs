using System;
using System.Linq;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.Codegen;

public class AsyncStreamDescriptor
{
    public MethodMetadata NextMethod { get; }
    public UniFFIType ItemType { get; }
    public string CSharpItemType { get; }
    public bool IsValueType { get; }

    public AsyncStreamDescriptor(MethodMetadata nextMethod, UniFFIType itemType, string csharpItemType, bool isValueType)
    {
        NextMethod = nextMethod;
        ItemType = itemType;
        CSharpItemType = csharpItemType;
        IsValueType = isValueType;
    }
}

public static class AsyncStreamHeuristic
{
    public static AsyncStreamDescriptor? Detect(ObjectMetadata obj, ComponentInterface ci, bool useNativeUnions = false)
    {
        // 1. Must be an Object (guaranteed by ObjectMetadata)
        // 2. Contains a method whose name matches "next" (case-insensitive or OrigName)
        // 3. Inputs.Count == 0 (no parameters other than self)
        // 4. IsAsync == true
        // 5. ReturnType != null && ReturnType.Kind == TypeKind.Option && ReturnType.InnerType != null
        var nextMethod = obj.Methods.FirstOrDefault(m =>
            (string.Equals(m.Name, "next", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(m.OrigName, "next", StringComparison.OrdinalIgnoreCase)) &&
            m.Inputs.Count == 0 &&
            m.IsAsync &&
            m.ReturnType != null &&
            m.ReturnType.Kind == TypeKind.Option &&
            m.ReturnType.InnerType != null
        );

        if (nextMethod == null) return null;

        var itemType = nextMethod.ReturnType!.InnerType!;
        string csItemType = TypeHelper.ToCSharpType(itemType);
        bool isValueType = TypeHelper.IsCSharpValueType(itemType, ci, useNativeUnions);

        return new AsyncStreamDescriptor(nextMethod, itemType, csItemType, isValueType);
    }
}
