using System;
using System.Collections.Generic;
using System.Text;

namespace UniFFISharp.Generator.Metadata.BoltFfi;

/// <summary>
/// Root envelope stored in the .boltffi or __DATA,__boltffi linker section.
/// Header: BFFIMD01 (8 bytes) + Length (8 bytes u64 le) + JSON payload.
/// </summary>
public sealed class BoltFfiMetadataEnvelope
{
    public string Magic { get; set; } = string.Empty;
    public BoltFfiFormat Format { get; set; } = new();
    public string Surface { get; set; } = string.Empty;
    public BoltFfiPackageInfo Package { get; set; } = new();
    public string ContractHash { get; set; } = string.Empty;
    public BoltFfiSerializedBindings Bindings { get; set; } = new();

    public bool IsValid => Magic == "boltffi.bindings";

    public static BoltFfiMetadataEnvelope? Parse(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            return FromJson(node);
        }
        catch
        {
            return null;
        }
    }

    public static BoltFfiMetadataEnvelope? FromJson(JsonNode? node)
    {
        if (node == null) return null;
        var env = new BoltFfiMetadataEnvelope
        {
            Magic = node["magic"]?.AsString() ?? string.Empty,
            Surface = node["surface"]?.AsString() ?? string.Empty,
            ContractHash = node["contract_hash"]?.ToString() ?? string.Empty
        };

        if (node["format"] is JsonObject fmtObj)
        {
            env.Format = new BoltFfiFormat
            {
                Major = (ushort)(fmtObj["major"]?.AsInt64() ?? 0),
                Minor = (ushort)(fmtObj["minor"]?.AsInt64() ?? 0)
            };
        }

        if (node["package"] is JsonObject pkgObj)
        {
            env.Package = BoltFfiPackageInfo.FromJson(pkgObj);
        }

        if (node["bindings"] is JsonObject bndObj)
        {
            env.Bindings = BoltFfiSerializedBindings.FromJson(bndObj);
        }

        return env;
    }
}

public sealed class BoltFfiFormat
{
    public ushort Major { get; set; }
    public ushort Minor { get; set; }
}

public sealed class BoltFfiPackageInfo
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public string? Version { get; set; }

    public string PackageName => Name.ToSnakeCase();

    public static BoltFfiPackageInfo FromJson(JsonNode? node)
    {
        var pkg = new BoltFfiPackageInfo();
        if (node == null) return pkg;

        pkg.Name = BoltFfiCanonicalName.FromJson(node["name"]);
        pkg.Version = node["version"]?.AsString();
        return pkg;
    }
}

public sealed class BoltFfiCanonicalName
{
    public List<string> Parts { get; set; } = new();
    public string? SourceSpelling { get; set; }

    public string ToPascalCase()
    {
        if (Parts.Count == 0) return SourceSpelling ?? string.Empty;
        var sb = new StringBuilder();
        foreach (var part in Parts)
        {
            if (string.IsNullOrEmpty(part)) continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1) sb.Append(part.Substring(1));
        }
        return sb.ToString();
    }

    public string ToCamelCase()
    {
        string pascal = ToPascalCase();
        if (string.IsNullOrEmpty(pascal)) return string.Empty;
        return char.ToLowerInvariant(pascal[0]) + (pascal.Length > 1 ? pascal.Substring(1) : string.Empty);
    }

    public string ToSnakeCase() => string.Join("_", Parts);

    public override string ToString() => ToPascalCase();

    public static BoltFfiCanonicalName FromJson(JsonNode? node)
    {
        var name = new BoltFfiCanonicalName();
        if (node == null) return name;

        if (node is JsonString str)
        {
            name.Parts.Add(str.Value);
            return name;
        }

        if (node["parts"] is JsonArray partsArr)
        {
            foreach (var item in partsArr.Items)
            {
                string? s = item.AsString();
                if (!string.IsNullOrEmpty(s)) name.Parts.Add(s!);
            }
        }

        name.SourceSpelling = node["source_spelling"]?.AsString();
        return name;
    }
}

public sealed class BoltFfiSerializedBindings
{
    public BoltFfiNativeBindings? Native { get; set; }

    public static BoltFfiSerializedBindings FromJson(JsonNode? node)
    {
        var bnd = new BoltFfiSerializedBindings();
        if (node == null) return bnd;

        if (node["Native"] is JsonObject natObj)
        {
            bnd.Native = BoltFfiNativeBindings.FromJson(natObj);
        }
        return bnd;
    }
}

public sealed class BoltFfiNativeBindings
{
    public BoltFfiPackageInfo Package { get; set; } = new();
    public List<BoltFfiDecl> Decls { get; set; } = new();

    public static BoltFfiNativeBindings FromJson(JsonNode? node)
    {
        var nat = new BoltFfiNativeBindings();
        if (node == null) return nat;

        nat.Package = BoltFfiPackageInfo.FromJson(node["package"]);
        if (node["decls"] is JsonArray declsArr)
        {
            foreach (var item in declsArr.Items)
            {
                var decl = BoltFfiDecl.FromJson(item);
                if (decl != null)
                {
                    nat.Decls.Add(decl);
                }
            }
        }
        return nat;
    }
}

public enum BoltFfiDeclKind
{
    Unknown,
    Record,
    Enum,
    Function,
    Class,
    Callback,
    Stream,
    Constant,
    CustomType
}

public sealed class BoltFfiDecl
{
    public BoltFfiDeclKind Kind { get; set; }
    public BoltFfiRecordDecl? Record { get; set; }
    public BoltFfiEnumDecl? Enum { get; set; }
    public BoltFfiFunctionDecl? Function { get; set; }
    public BoltFfiClassDecl? Class { get; set; }
    public JsonNode? RawNode { get; set; }

    public string DeclName => Kind switch
    {
        BoltFfiDeclKind.Record when Record != null => Record.Name.ToPascalCase(),
        BoltFfiDeclKind.Enum when Enum != null => Enum.Name.ToPascalCase(),
        BoltFfiDeclKind.Function when Function != null => Function.Name.ToPascalCase(),
        BoltFfiDeclKind.Class when Class != null => Class.Name.ToPascalCase(),
        _ => string.Empty
    };

    public static BoltFfiDecl? FromJson(JsonNode? node)
    {
        if (node is not JsonObject obj) return null;

        var decl = new BoltFfiDecl { RawNode = node };

        foreach (var kv in obj.Properties)
        {
            switch (kv.Key)
            {
                case "Record":
                    decl.Kind = BoltFfiDeclKind.Record;
                    decl.Record = BoltFfiRecordDecl.FromJson(kv.Value);
                    break;
                case "Function":
                    decl.Kind = BoltFfiDeclKind.Function;
                    decl.Function = BoltFfiFunctionDecl.FromJson(kv.Value);
                    break;
                case "Enum":
                    decl.Kind = BoltFfiDeclKind.Enum;
                    decl.Enum = BoltFfiEnumDecl.FromJson(kv.Value);
                    break;
                case "Class":
                    decl.Kind = BoltFfiDeclKind.Class;
                    decl.Class = BoltFfiClassDecl.FromJson(kv.Value);
                    break;
                case "Callback":
                    decl.Kind = BoltFfiDeclKind.Callback;
                    break;
                case "Stream":
                    decl.Kind = BoltFfiDeclKind.Stream;
                    break;
                case "Constant":
                    decl.Kind = BoltFfiDeclKind.Constant;
                    break;
                case "CustomType":
                    decl.Kind = BoltFfiDeclKind.CustomType;
                    break;
                default:
                    decl.Kind = BoltFfiDeclKind.Unknown;
                    break;
            }
        }

        return decl;
    }
}

public sealed class BoltFfiRecordDecl
{
    public bool IsDirect { get; set; }
    public BoltFfiCanonicalName Name { get; set; } = new();
    public List<BoltFfiFieldDecl> Fields { get; set; } = new();
    public List<BoltFfiFunctionDecl> Methods { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiRecordDecl FromJson(JsonNode? node)
    {
        var record = new BoltFfiRecordDecl();
        if (node == null) return record;

        JsonNode target = node;
        if (node["Direct"] != null)
        {
            record.IsDirect = true;
            target = node["Direct"]!;
        }
        else if (node["Encoded"] != null)
        {
            record.IsDirect = false;
            target = node["Encoded"]!;
        }

        record.Name = BoltFfiCanonicalName.FromJson(target["name"]);

        if (target["fields"] is JsonArray fieldsArr)
        {
            foreach (var f in fieldsArr.Items)
            {
                var field = new BoltFfiFieldDecl
                {
                    Name = ParseFieldKey(f["key"]),
                    Type = BoltFfiType.FromJson(f["ty"])
                };
                record.Fields.Add(field);
            }
        }

        return record;
    }

    private static string ParseFieldKey(JsonNode? keyNode)
    {
        if (keyNode == null) return string.Empty;
        if (keyNode is JsonString s) return s.Value;
        if (keyNode["Name"] != null) return keyNode["Name"]?.AsString() ?? string.Empty;
        if (keyNode["Index"] != null) return $"item_{keyNode["Index"]?.AsInt64()}";
        return keyNode.ToString();
    }
}

public sealed class BoltFfiFieldDecl
{
    public string Name { get; set; } = string.Empty;
    public BoltFfiType Type { get; set; } = new();
    public string? DocComment { get; set; }
}

public sealed class BoltFfiEnumDecl
{
    public bool IsCStyle { get; set; }
    public BoltFfiCanonicalName Name { get; set; } = new();
    public List<BoltFfiEnumVariant> Variants { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiEnumDecl FromJson(JsonNode? node)
    {
        var enumDecl = new BoltFfiEnumDecl();
        if (node == null) return enumDecl;

        JsonNode target = node;
        if (node["CStyle"] != null)
        {
            enumDecl.IsCStyle = true;
            target = node["CStyle"]!;
        }
        else if (node["Data"] != null)
        {
            enumDecl.IsCStyle = false;
            target = node["Data"]!;
        }

        enumDecl.Name = BoltFfiCanonicalName.FromJson(target["name"]);

        if (target["variants"] is JsonArray varsArr)
        {
            foreach (var v in varsArr.Items)
            {
                var variant = new BoltFfiEnumVariant
                {
                    Name = BoltFfiCanonicalName.FromJson(v["name"]).ToPascalCase(),
                    Discriminant = v["discriminant"]?.AsInt64()
                };
                enumDecl.Variants.Add(variant);
            }
        }

        return enumDecl;
    }
}

public sealed class BoltFfiEnumVariant
{
    public string Name { get; set; } = string.Empty;
    public long? Discriminant { get; set; }
    public List<BoltFfiFieldDecl> PayloadFields { get; set; } = new();
    public string? DocComment { get; set; }
}

public sealed class BoltFfiFunctionDecl
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public string Symbol { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public bool HasError { get; set; }
    public BoltFfiType ReturnType { get; set; } = new();
    public List<BoltFfiParamDecl> Parameters { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiFunctionDecl FromJson(JsonNode? node)
    {
        var func = new BoltFfiFunctionDecl();
        if (node == null) return func;

        func.Name = BoltFfiCanonicalName.FromJson(node["name"]);

        var symNode = node["symbol"];
        if (symNode is JsonString sStr)
        {
            func.Symbol = sStr.Value;
        }
        else if (symNode?["name"] != null)
        {
            func.Symbol = symNode["name"]?.AsString() ?? string.Empty;
        }

        var callable = node["callable"];
        if (callable != null)
        {
            var exec = callable["execution"];
            if (exec?.AsString() == "Async" || exec?["Async"] != null)
            {
                func.IsAsync = true;
            }

            var err = callable["error"];
            if (err is JsonObject && err["None"] == null)
            {
                func.HasError = true;
            }

            var returns = callable["returns"];
            if (returns != null)
            {
                if (returns["ty"] != null)
                {
                    func.ReturnType = BoltFfiType.FromJson(returns["ty"]);
                }
                else if (returns["plan"] is JsonNode plan)
                {
                    if (plan["DirectViaReturnSlot"] != null)
                    {
                        func.ReturnType = BoltFfiType.FromDirectValue(plan["DirectViaReturnSlot"]);
                    }
                }
            }

            if (callable["params"] is JsonArray paramsArr)
            {
                foreach (var p in paramsArr.Items)
                {
                    var param = new BoltFfiParamDecl
                    {
                        Name = BoltFfiCanonicalName.FromJson(p["name"]).ToCamelCase(),
                        Type = p["ty"] != null ? BoltFfiType.FromJson(p["ty"]) : BoltFfiType.FromParamPlan(p["plan"])
                    };
                    func.Parameters.Add(param);
                }
            }
        }

        return func;
    }
}

public sealed class BoltFfiParamDecl
{
    public string Name { get; set; } = string.Empty;
    public BoltFfiType Type { get; set; } = new();
    public string? DefaultValue { get; set; }
}

public sealed class BoltFfiClassDecl
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public string ReleaseSymbol { get; set; } = string.Empty;
    public List<BoltFfiFunctionDecl> Initializers { get; set; } = new();
    public List<BoltFfiFunctionDecl> Methods { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiClassDecl FromJson(JsonNode? node)
    {
        var cls = new BoltFfiClassDecl();
        if (node == null) return cls;

        cls.Name = BoltFfiCanonicalName.FromJson(node["name"]);
        cls.ReleaseSymbol = node["release_symbol"]?.AsString()
            ?? node["release_entry"]?.AsString()
            ?? node["release"]?.AsString()
            ?? node["release"]?["symbol"]?.AsString()
            ?? string.Empty;
        return cls;
    }
}

public enum BoltFfiTypeKind
{
    Void,
    Bool,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float32,
    Float64,
    String,
    Bytes,
    Record,
    Enum,
    Class,
    Option,
    Sequence,
    Custom
}

public sealed class BoltFfiType
{
    public BoltFfiTypeKind Kind { get; set; }
    public string CustomTypeName { get; set; } = string.Empty;
    public BoltFfiType? InnerType { get; set; }

    public static BoltFfiType Void => new() { Kind = BoltFfiTypeKind.Void };
    public static BoltFfiType Primitive(BoltFfiTypeKind kind) => new() { Kind = kind };

    public static BoltFfiType FromJson(JsonNode? node)
    {
        if (node == null) return Void;

        if (node is JsonString str)
        {
            return ParsePrimitive(str.Value);
        }

        if (node["Primitive"] != null)
        {
            return ParsePrimitive(node["Primitive"]?.AsString());
        }

        if (node["Record"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Record,
                CustomTypeName = node["Record"]?.ToString() ?? string.Empty
            };
        }

        if (node["Enum"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Enum,
                CustomTypeName = node["Enum"]?.ToString() ?? string.Empty
            };
        }

        if (node["Option"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Option,
                InnerType = FromJson(node["Option"])
            };
        }

        if (node["Vec"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Sequence,
                InnerType = FromJson(node["Vec"])
            };
        }

        if (node["Builtin"] != null)
        {
            string? b = node["Builtin"]?.AsString();
            if (b == "String") return Primitive(BoltFfiTypeKind.String);
            return Primitive(BoltFfiTypeKind.Bytes);
        }

        return Void;
    }

    public static BoltFfiType FromDirectValue(JsonNode? node)
    {
        if (node == null) return Primitive(BoltFfiTypeKind.Int32);
        if (node is JsonString js)
        {
            return ParsePrimitive(js.Value);
        }
        if (node["Primitive"] != null)
        {
            return ParsePrimitive(node["Primitive"]?.AsString());
        }
        if (node["Record"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Record,
                CustomTypeName = node["Record"]?.ToString() ?? string.Empty
            };
        }
        if (node["Class"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Class,
                CustomTypeName = node["Class"]?.ToString() ?? string.Empty
            };
        }
        if (node["Enum"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Enum,
                CustomTypeName = node["Enum"]?.ToString() ?? string.Empty
            };
        }
        return Primitive(BoltFfiTypeKind.Int32);
    }

    public static BoltFfiType FromParamPlan(JsonNode? node)
    {
        if (node == null) return Primitive(BoltFfiTypeKind.Bytes);
        if (node["Direct"] != null)
        {
            return FromDirectValue(node["Direct"]);
        }
        return Primitive(BoltFfiTypeKind.Bytes);
    }

    public static BoltFfiType ParsePrimitive(string? primName)
    {
        return primName switch
        {
            "Bool" => Primitive(BoltFfiTypeKind.Bool),
            "I8" => Primitive(BoltFfiTypeKind.Int8),
            "U8" => Primitive(BoltFfiTypeKind.UInt8),
            "I16" => Primitive(BoltFfiTypeKind.Int16),
            "U16" => Primitive(BoltFfiTypeKind.UInt16),
            "I32" => Primitive(BoltFfiTypeKind.Int32),
            "U32" => Primitive(BoltFfiTypeKind.UInt32),
            "I64" => Primitive(BoltFfiTypeKind.Int64),
            "U64" => Primitive(BoltFfiTypeKind.UInt64),
            "F32" => Primitive(BoltFfiTypeKind.Float32),
            "F64" => Primitive(BoltFfiTypeKind.Float64),
            "String" => Primitive(BoltFfiTypeKind.String),
            _ => Primitive(BoltFfiTypeKind.Void)
        };
    }
}
