using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniFFISharp.Generator.Metadata;

namespace UniFFISharp.Generator.Codegen;

public static class TypeHelper
{
    private static readonly HashSet<string> ReservedKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
        "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
        "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
        "virtual", "void", "volatile", "while"
    };

    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    sb.Append(part.Substring(1));
                }
            }
        }
        return sb.ToString();
    }

    public static string ToCamelCase(string name)
    {
        string pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        string camel = char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        if (ReservedKeywords.Contains(camel))
        {
            return "@" + camel;
        }
        return camel;
    }

    public static string EscapeIdentifier(string name)
    {
        if (ReservedKeywords.Contains(name))
        {
            return "@" + name;
        }
        return name;
    }

    private static string GetExternalPrefix(UniFFIType type, string? currentCrate)
    {
        if (string.IsNullOrEmpty(currentCrate) || string.IsNullOrEmpty(type.ModulePath))
            return string.Empty;

        string crate = type.ModulePath.Split(new[] { "::" }, StringSplitOptions.None)[0].Replace('-', '_');
        string curr = currentCrate!.Replace('-', '_');
        if (!string.Equals(crate, curr, StringComparison.OrdinalIgnoreCase))
        {
            return ToPascalCase(crate) + ".";
        }
        return string.Empty;
    }

    public static string ToCSharpType(UniFFIType type, string? currentCrate = null)
    {
        string prefix = GetExternalPrefix(type, currentCrate);
        return type.Kind switch
        {
            TypeKind.UInt8 => "byte",
            TypeKind.Int8 => "sbyte",
            TypeKind.UInt16 => "ushort",
            TypeKind.Int16 => "short",
            TypeKind.UInt32 => "uint",
            TypeKind.Int32 => "int",
            TypeKind.UInt64 => "ulong",
            TypeKind.Int64 => "long",
            TypeKind.Float32 => "float",
            TypeKind.Float64 => "double",
            TypeKind.Boolean => "bool",
            TypeKind.String => "string",
            TypeKind.Bytes => "byte[]",
            TypeKind.Record => prefix + ToPascalCase(type.Name),
            TypeKind.Enum => prefix + ToPascalCase(type.Name),
            TypeKind.Interface => prefix + ToPascalCase(type.Name),
            TypeKind.CallbackInterface => prefix + "I" + ToPascalCase(type.Name),
            TypeKind.Option => ToCSharpType(type.InnerType!, currentCrate) + "?",
            TypeKind.Sequence => $"List<{ToCSharpType(type.InnerType!, currentCrate)}>",
            TypeKind.Map => $"Dictionary<{ToCSharpType(type.KeyType!, currentCrate)}, {ToCSharpType(type.ValueType!, currentCrate)}>",
            TypeKind.Set => $"HashSet<{ToCSharpType(type.InnerType!, currentCrate)}>",
            TypeKind.Custom => prefix + ToPascalCase(type.Name),
            TypeKind.Timestamp => "DateTimeOffset",
            TypeKind.Duration => "TimeSpan",
            _ => "object"
        };
    }

    public static string ToFfiType(UniFFIType type)
    {
        return type.Kind switch
        {
            TypeKind.UInt8 => "byte",
            TypeKind.Int8 => "sbyte",
            TypeKind.UInt16 => "ushort",
            TypeKind.Int16 => "short",
            TypeKind.UInt32 => "uint",
            TypeKind.Int32 => "int",
            TypeKind.UInt64 => "ulong",
            TypeKind.Int64 => "long",
            TypeKind.Float32 => "float",
            TypeKind.Float64 => "double",
            TypeKind.Boolean => "sbyte",
            TypeKind.Interface => "IntPtr",
            TypeKind.CallbackInterface => "ulong",
            _ => "RustBuffer"
        };
    }

    public static bool IsFfiPrimitive(UniFFIType type)
    {
        return type.Kind switch
        {
            TypeKind.UInt8 or TypeKind.Int8 or TypeKind.UInt16 or TypeKind.Int16 or
            TypeKind.UInt32 or TypeKind.Int32 or TypeKind.UInt64 or TypeKind.Int64 or
            TypeKind.Float32 or TypeKind.Float64 => true,
            _ => false
        };
    }

    public static bool IsCSharpValueType(UniFFIType type, ComponentInterface? ci = null)
    {
        return type.Kind switch
        {
            TypeKind.UInt8 or TypeKind.Int8 or TypeKind.UInt16 or TypeKind.Int16 or
            TypeKind.UInt32 or TypeKind.Int32 or TypeKind.UInt64 or TypeKind.Int64 or
            TypeKind.Float32 or TypeKind.Float64 or TypeKind.Boolean or
            TypeKind.Timestamp or TypeKind.Duration => true,
            TypeKind.Enum => ci != null && ci.Enums.Any(e => e.Name == type.Name && e.Shape == EnumShape.Enum && e.Variants.All(v => v.Fields.Count == 0)),
            _ => false
        };
    }

    public static string ToTypeIdentifier(UniFFIType type)
    {
        return type.Kind switch
        {
            TypeKind.UInt8 => "UInt8",
            TypeKind.Int8 => "Int8",
            TypeKind.UInt16 => "UInt16",
            TypeKind.Int16 => "Int16",
            TypeKind.UInt32 => "UInt32",
            TypeKind.Int32 => "Int32",
            TypeKind.UInt64 => "UInt64",
            TypeKind.Int64 => "Int64",
            TypeKind.Float32 => "Float32",
            TypeKind.Float64 => "Float64",
            TypeKind.Boolean => "Boolean",
            TypeKind.String => "String",
            TypeKind.Bytes => "ByteArray",
            TypeKind.Timestamp => "Timestamp",
            TypeKind.Duration => "Duration",
            TypeKind.Record => ToPascalCase(type.Name),
            TypeKind.Enum => ToPascalCase(type.Name),
            TypeKind.Interface => ToPascalCase(type.Name),
            TypeKind.CallbackInterface => ToPascalCase(type.Name),
            TypeKind.Option => $"Optional{ToTypeIdentifier(type.InnerType!)}",
            TypeKind.Sequence => $"Sequence{ToTypeIdentifier(type.InnerType!)}",
            TypeKind.Map => $"Map{ToTypeIdentifier(type.KeyType!)}_{ToTypeIdentifier(type.ValueType!)}",
            TypeKind.Set => $"Set{ToTypeIdentifier(type.InnerType!)}",
            TypeKind.Custom => ToPascalCase(type.Name),
            _ => "Object"
        };
    }

    public static string ConverterClassName(UniFFIType type, string? currentCrate = null)
    {
        string prefix = GetExternalPrefix(type, currentCrate);
        return type.Kind switch
        {
            TypeKind.UInt8 => "FfiConverterUInt8",
            TypeKind.Int8 => "FfiConverterInt8",
            TypeKind.UInt16 => "FfiConverterUInt16",
            TypeKind.Int16 => "FfiConverterInt16",
            TypeKind.UInt32 => "FfiConverterUInt32",
            TypeKind.Int32 => "FfiConverterInt32",
            TypeKind.UInt64 => "FfiConverterUInt64",
            TypeKind.Int64 => "FfiConverterInt64",
            TypeKind.Float32 => "FfiConverterFloat32",
            TypeKind.Float64 => "FfiConverterFloat64",
            TypeKind.Boolean => "FfiConverterBoolean",
            TypeKind.String => "FfiConverterString",
            TypeKind.Bytes => "FfiConverterByteArray",
            TypeKind.Timestamp => "FfiConverterTimestamp",
            TypeKind.Duration => "FfiConverterDuration",
            TypeKind.Record => $"{prefix}FfiConverterType{ToPascalCase(type.Name)}",
            TypeKind.Enum => $"{prefix}FfiConverterType{ToPascalCase(type.Name)}",
            TypeKind.Interface => $"{prefix}FfiConverterType{ToPascalCase(type.Name)}",
            TypeKind.CallbackInterface => $"{prefix}FfiConverterType{ToPascalCase(type.Name)}",
            TypeKind.Option => $"FfiConverterOptional{ToTypeIdentifier(type.InnerType!)}",
            TypeKind.Sequence => $"FfiConverterSequence{ToTypeIdentifier(type.InnerType!)}",
            TypeKind.Map => $"FfiConverterMap{ToTypeIdentifier(type.KeyType!)}_{ToTypeIdentifier(type.ValueType!)}",
            TypeKind.Set => $"FfiConverterSet{ToTypeIdentifier(type.InnerType!)}",
            _ => $"{prefix}FfiConverterType{ToPascalCase(type.Name)}"
        };
    }

    public static string ConverterInstance(UniFFIType type, string? currentCrate = null)
    {
        return $"{ConverterClassName(type, currentCrate)}.INSTANCE";
    }

    public static string FutureSuffix(UniFFIType? returnType)
    {
        if (returnType == null) return "void";
        return returnType.Kind switch
        {
            TypeKind.UInt8 => "u8",
            TypeKind.Int8 => "i8",
            TypeKind.UInt16 => "u16",
            TypeKind.Int16 => "i16",
            TypeKind.UInt32 => "u32",
            TypeKind.Int32 => "i32",
            TypeKind.UInt64 => "u64",
            TypeKind.Int64 => "i64",
            TypeKind.Float32 => "f32",
            TypeKind.Float64 => "f64",
            TypeKind.Interface => "u64",
            TypeKind.CallbackInterface => "u64",
            _ => "rust_buffer"
        };
    }

    public static string FutureCompleteFfiType(UniFFIType? returnType)
    {
        if (returnType == null) return "void";
        return returnType.Kind switch
        {
            TypeKind.UInt8 => "byte",
            TypeKind.Int8 => "sbyte",
            TypeKind.UInt16 => "ushort",
            TypeKind.Int16 => "short",
            TypeKind.UInt32 => "uint",
            TypeKind.Int32 => "int",
            TypeKind.UInt64 => "ulong",
            TypeKind.Int64 => "long",
            TypeKind.Float32 => "float",
            TypeKind.Float64 => "double",
            TypeKind.Interface => "IntPtr",
            TypeKind.CallbackInterface => "ulong",
            _ => "RustBuffer"
        };
    }
}
