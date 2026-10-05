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

    public static string SanitizeIdentifier(string name, string fallback = "UniffiItem")
    {
        if (string.IsNullOrWhiteSpace(name)) return fallback;

        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
            }
        }

        string result = sb.ToString().Trim('_');
        if (string.IsNullOrEmpty(result)) return fallback;

        if (char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        return EscapeIdentifier(result);
    }

    public static string EscapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");
    }

    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var parts = name.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                var cleanPart = new StringBuilder();
                foreach (char c in part)
                {
                    cleanPart.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':' ? c : '_');
                }
                string cp = cleanPart.ToString();
                if (cp.Length > 0)
                {
                    sb.Append(char.ToUpperInvariant(cp[0]));
                    if (cp.Length > 1)
                    {
                        sb.Append(cp.Substring(1));
                    }
                }
            }
        }
        string result = sb.ToString();
        if (string.IsNullOrEmpty(result)) return "Item";
        if (char.IsDigit(result[0])) result = "_" + result;
        return EscapeIdentifier(result);
    }

    public static string ToCamelCase(string name)
    {
        string pascal = ToPascalCase(name);
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        bool hasAt = pascal.StartsWith("@");
        string clean = hasAt ? pascal.Substring(1) : pascal;
        string camel = char.ToLowerInvariant(clean[0]) + clean.Substring(1);
        return EscapeIdentifier(camel);
    }

    public static string EscapeIdentifier(string name)
    {
        if (ReservedKeywords.Contains(name))
        {
            return "@" + name;
        }
        return name;
    }

    private static string GetExternalPrefix(UniFFIType type, string? currentCrate, Func<string, string>? namespaceResolver = null)
    {
        if (string.IsNullOrEmpty(currentCrate) || string.IsNullOrEmpty(type.ModulePath))
            return string.Empty;

        string crate = type.ModulePath.Split(new[] { "::" }, StringSplitOptions.None)[0].Replace('-', '_');
        string curr = currentCrate!.Replace('-', '_');
        if (!string.Equals(crate, curr, StringComparison.OrdinalIgnoreCase))
        {
            if (namespaceResolver != null)
            {
                string targetNs = namespaceResolver(crate);
                string currNs = namespaceResolver(curr);
                if (!string.Equals(targetNs, currNs, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(targetNs))
                {
                    return targetNs + ".";
                }
                return string.Empty;
            }
            return ToPascalCase(crate) + ".";
        }
        return string.Empty;
    }

    public static string ToCSharpType(UniFFIType type, string? currentCrate = null, Func<string, string>? namespaceResolver = null)
    {
        string prefix = GetExternalPrefix(type, currentCrate, namespaceResolver);
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
            TypeKind.Option => ToCSharpType(type.InnerType!, currentCrate, namespaceResolver) + "?",
            TypeKind.Sequence => $"List<{ToCSharpType(type.InnerType!, currentCrate, namespaceResolver)}>",
            TypeKind.Map => $"Dictionary<{ToCSharpType(type.KeyType!, currentCrate, namespaceResolver)}, {ToCSharpType(type.ValueType!, currentCrate, namespaceResolver)}>",
            TypeKind.Set => $"HashSet<{ToCSharpType(type.InnerType!, currentCrate, namespaceResolver)}>",
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

    public static string ConverterClassName(UniFFIType type, string? currentCrate = null, Func<string, string>? namespaceResolver = null)
    {
        string prefix = GetExternalPrefix(type, currentCrate, namespaceResolver);
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

    public static string ConverterInstance(UniFFIType type, string? currentCrate = null, Func<string, string>? namespaceResolver = null)
    {
        return $"{ConverterClassName(type, currentCrate, namespaceResolver)}.INSTANCE";
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
