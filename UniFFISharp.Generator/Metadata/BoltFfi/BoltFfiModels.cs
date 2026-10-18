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
            foreach (var word in part.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1) sb.Append(word.Substring(1));
            }
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

            // Resolve ID references (e.g. {"Record": 0}, {"Class": 0}) to names
            var recordMap = new Dictionary<string, string>();
            var recordDirectMap = new Dictionary<string, bool>();
            var classMap = new Dictionary<string, string>();
            var enumMap = new Dictionary<string, string>();
            var enumCStyleMap = new Dictionary<string, bool>();
            var callbackMap = new Dictionary<string, string>();

            foreach (var decl in nat.Decls)
            {
                if (decl.Record != null)
                {
                    string pName = decl.Record.Name.ToPascalCase();
                    string? id = decl.RawNode?["Record"]?["Direct"]?["id"]?.ToString()
                        ?? decl.RawNode?["Record"]?["Encoded"]?["id"]?.ToString()
                        ?? decl.RawNode?["Record"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id)) recordMap[id!] = pName;
                    recordDirectMap[pName] = decl.Record.IsDirect;
                    if (!string.IsNullOrEmpty(id)) recordDirectMap[id!] = decl.Record.IsDirect;
                }
                if (decl.Class != null)
                {
                    string cName = decl.Class.Name.ToPascalCase();
                    string? id = decl.RawNode?["Class"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id)) classMap[id!] = cName;
                }
                if (decl.Enum != null)
                {
                    string eName = decl.Enum.Name.ToPascalCase();
                    string? id = decl.RawNode?["Enum"]?["CStyle"]?["id"]?.ToString()
                        ?? decl.RawNode?["Enum"]?["Data"]?["id"]?.ToString()
                        ?? decl.RawNode?["Enum"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        enumMap[id!] = eName;
                        enumCStyleMap[id!] = decl.Enum.IsCStyle;
                    }
                    enumCStyleMap[eName] = decl.Enum.IsCStyle;
                }
                if (decl.Callback != null)
                {
                    string cbName = decl.Callback.Name.ToPascalCase();
                    string? id = decl.RawNode?["Callback"]?["id"]?.ToString();
                    if (!string.IsNullOrEmpty(id)) callbackMap[id!] = cbName;
                }
            }

            void ResolveType(BoltFfiType? ty)
            {
                if (ty == null) return;
                if (ty.Kind == BoltFfiTypeKind.Record)
                {
                    if (recordDirectMap.TryGetValue(ty.CustomTypeName, out var isDirect))
                        ty.IsDirectRecord = isDirect;
                    if (recordMap.TryGetValue(ty.CustomTypeName, out var rName))
                    {
                        ty.CustomTypeName = rName;
                        if (recordDirectMap.TryGetValue(rName, out var isDirect2))
                            ty.IsDirectRecord = isDirect2;
                    }
                }
                else if (ty.Kind == BoltFfiTypeKind.Class && classMap.TryGetValue(ty.CustomTypeName, out var cName))
                    ty.CustomTypeName = cName;
                else if (ty.Kind == BoltFfiTypeKind.Enum)
                {
                    if (enumCStyleMap.TryGetValue(ty.CustomTypeName, out var isCStyle))
                        ty.IsCStyleEnum = isCStyle;
                    if (enumMap.TryGetValue(ty.CustomTypeName, out var eName))
                    {
                        ty.CustomTypeName = eName;
                        if (enumCStyleMap.TryGetValue(eName, out var isCStyle2))
                            ty.IsCStyleEnum = isCStyle2;
                    }
                }
                else if (ty.Kind == BoltFfiTypeKind.Callback && callbackMap.TryGetValue(ty.CustomTypeName, out var cbName))
                    ty.CustomTypeName = cbName;

                if (ty.InnerType != null) ResolveType(ty.InnerType);
                if (ty.KeyType != null) ResolveType(ty.KeyType);
                if (ty.ValueType != null) ResolveType(ty.ValueType);
            }

            foreach (var decl in nat.Decls)
            {
                if (decl.Function != null)
                {
                    ResolveType(decl.Function.ReturnType);
                    ResolveType(decl.Function.ErrorType);
                    foreach (var p in decl.Function.Parameters) ResolveType(p.Type);
                }
                if (decl.Class != null)
                {
                    foreach (var init in decl.Class.Initializers)
                    {
                        init.ReturnType = new BoltFfiType { Kind = BoltFfiTypeKind.Class, CustomTypeName = decl.Class.Name.ToPascalCase() };
                        foreach (var p in init.Parameters) ResolveType(p.Type);
                    }
                    foreach (var m in decl.Class.Methods)
                    {
                        ResolveType(m.ReturnType);
                        ResolveType(m.ErrorType);
                        foreach (var p in m.Parameters) ResolveType(p.Type);
                    }
                }
                if (decl.Record != null)
                {
                    foreach (var f in decl.Record.Fields) ResolveType(f.Type);
                }
                if (decl.Enum != null)
                {
                    foreach (var v in decl.Enum.Variants)
                    {
                        foreach (var f in v.PayloadFields) ResolveType(f.Type);
                    }
                }
                if (decl.Callback != null)
                {
                    foreach (var m in decl.Callback.Methods)
                    {
                        ResolveType(m.ReturnType);
                        foreach (var p in m.Parameters) ResolveType(p.Type);
                    }
                }
                if (decl.Stream != null)
                {
                    ResolveType(decl.Stream.ItemType);
                }
                if (decl.Constant != null)
                {
                    ResolveType(decl.Constant.Type);
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
    public BoltFfiCallbackDecl? Callback { get; set; }
    public BoltFfiStreamDecl? Stream { get; set; }
    public BoltFfiConstantDecl? Constant { get; set; }
    public JsonNode? RawNode { get; set; }

    public string DeclName => Kind switch
    {
        BoltFfiDeclKind.Record when Record != null => Record.Name.ToPascalCase(),
        BoltFfiDeclKind.Enum when Enum != null => Enum.Name.ToPascalCase(),
        BoltFfiDeclKind.Function when Function != null => Function.Name.ToPascalCase(),
        BoltFfiDeclKind.Class when Class != null => Class.Name.ToPascalCase(),
        BoltFfiDeclKind.Callback when Callback != null => Callback.Name.ToPascalCase(),
        BoltFfiDeclKind.Stream when Stream != null => Stream.Name.ToPascalCase(),
        BoltFfiDeclKind.Constant when Constant != null => Constant.Name.ToPascalCase(),
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
                    decl.Callback = BoltFfiCallbackDecl.FromJson(kv.Value);
                    break;
                case "Stream":
                    decl.Kind = BoltFfiDeclKind.Stream;
                    decl.Stream = BoltFfiStreamDecl.FromJson(kv.Value);
                    break;
                case "Constant":
                    decl.Kind = BoltFfiDeclKind.Constant;
                    decl.Constant = BoltFfiConstantDecl.FromJson(kv.Value);
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
        record.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
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
        if (string.IsNullOrEmpty(record.DocComment))
        {
            record.DocComment = target["doc"]?.AsString() ?? target["docstring"]?.AsString() ?? target["comment"]?.AsString();
        }

        record.Name = BoltFfiCanonicalName.FromJson(target["name"]);

        if (target["fields"] is JsonArray fieldsArr)
        {
            foreach (var f in fieldsArr.Items)
            {
                var field = new BoltFfiFieldDecl
                {
                    Name = ParseFieldKey(f["key"]),
                    Type = BoltFfiType.FromJson(f["ty"]),
                    DefaultValue = f["default"]?.AsString() ?? f["default_value"]?.AsString(),
                    DocComment = f["doc"]?.AsString() ?? f["docstring"]?.AsString() ?? f["comment"]?.AsString()
                };
                record.Fields.Add(field);
            }
        }

        return record;
    }

    internal static string ParseFieldKey(JsonNode? keyNode)
    {
        if (keyNode == null) return string.Empty;
        if (keyNode is JsonString s) return s.Value;
        if (keyNode["Named"] != null)
        {
            var name = BoltFfiCanonicalName.FromJson(keyNode["Named"]);
            return !string.IsNullOrEmpty(name.SourceSpelling) ? name.SourceSpelling! : name.ToSnakeCase();
        }
        if (keyNode["Name"] != null)
        {
            if (keyNode["Name"] is JsonString ns) return ns.Value;
            var name = BoltFfiCanonicalName.FromJson(keyNode["Name"]);
            return !string.IsNullOrEmpty(name.SourceSpelling) ? name.SourceSpelling! : name.ToSnakeCase();
        }
        if (keyNode["Index"] != null) return $"item_{keyNode["Index"]?.AsInt64()}";
        return keyNode.AsString() ?? string.Empty;
    }
}

public sealed class BoltFfiFieldDecl
{
    public string Name { get; set; } = string.Empty;
    public BoltFfiType Type { get; set; } = new();
    public string? DefaultValue { get; set; }
    public string? DocComment { get; set; }
}

public sealed class BoltFfiEnumDecl
{
    public bool IsCStyle { get; set; }
    public bool IsError { get; set; }
    public BoltFfiCanonicalName Name { get; set; } = new();
    public List<BoltFfiEnumVariant> Variants { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiEnumDecl FromJson(JsonNode? node)
    {
        var enumDecl = new BoltFfiEnumDecl();
        if (node == null) return enumDecl;

        JsonNode target = node;
        enumDecl.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        enumDecl.IsError = node["is_error"]?.AsBool() ?? false;
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
        else
        {
            enumDecl.IsCStyle = true;
        }
        if (target["is_error"]?.AsBool() == true)
        {
            enumDecl.IsError = true;
        }
        if (string.IsNullOrEmpty(enumDecl.DocComment))
        {
            enumDecl.DocComment = target["doc"]?.AsString() ?? target["docstring"]?.AsString() ?? target["comment"]?.AsString();
        }

        enumDecl.Name = BoltFfiCanonicalName.FromJson(target["name"]);

        if (target["variants"] is JsonArray varsArr)
        {
            foreach (var v in varsArr.Items)
            {
                var variant = new BoltFfiEnumVariant
                {
                    Name = BoltFfiCanonicalName.FromJson(v["name"]).ToPascalCase(),
                    Discriminant = v["discriminant"]?.AsInt64() ?? v["tag"]?.AsInt64(),
                    DocComment = v["doc"]?.AsString() ?? v["docstring"]?.AsString() ?? v["comment"]?.AsString()
                };

                var payloadNode = v["payload"];
                if (payloadNode != null)
                {
                    var fieldsArr = payloadNode["Tuple"] as JsonArray
                        ?? payloadNode["Struct"] as JsonArray;
                    if (fieldsArr != null)
                    {
                        foreach (var f in fieldsArr.Items)
                        {
                            var field = new BoltFfiFieldDecl
                            {
                                Name = BoltFfiRecordDecl.ParseFieldKey(f["key"]),
                                Type = BoltFfiType.FromJson(f["ty"]),
                                DefaultValue = f["default"]?.AsString() ?? f["default_value"]?.AsString(),
                                DocComment = f["doc"]?.AsString() ?? f["docstring"]?.AsString() ?? f["comment"]?.AsString()
                            };
                            variant.PayloadFields.Add(field);
                        }
                    }
                }
                else if (v["fields"] is JsonArray directFields)
                {
                    foreach (var f in directFields.Items)
                    {
                        var field = new BoltFfiFieldDecl
                        {
                            Name = BoltFfiRecordDecl.ParseFieldKey(f["key"]),
                            Type = BoltFfiType.FromJson(f["ty"]),
                            DefaultValue = f["default"]?.AsString() ?? f["default_value"]?.AsString(),
                            DocComment = f["doc"]?.AsString() ?? f["docstring"]?.AsString() ?? f["comment"]?.AsString()
                        };
                        variant.PayloadFields.Add(field);
                    }
                }

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
    public JsonNode? RawNode { get; set; }
    public BoltFfiCanonicalName Name { get; set; } = new();
    public string Symbol { get; set; } = string.Empty;
    public bool IsAsync { get; set; }
    public bool HasError { get; set; }
    public string AsyncPollSymbol { get; set; } = string.Empty;
    public string AsyncCompleteSymbol { get; set; } = string.Empty;
    public string AsyncCancelSymbol { get; set; } = string.Empty;
    public string AsyncFreeSymbol { get; set; } = string.Empty;
    public BoltFfiType? ErrorType { get; set; }
    public BoltFfiType ReturnType { get; set; } = new();
    public List<BoltFfiParamDecl> Parameters { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiFunctionDecl FromJson(JsonNode? node)
    {
        var func = new BoltFfiFunctionDecl { RawNode = node };
        if (node == null) return func;

        func.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        func.Name = BoltFfiCanonicalName.FromJson(node["name"]);

        var symNode = node["symbol"] ?? node["target"];
        if (symNode is JsonString sStr)
        {
            func.Symbol = sStr.Value;
        }
        else if (symNode?["name"] != null)
        {
            func.Symbol = symNode["name"]?.AsString() ?? string.Empty;
        }
        else if (symNode?["symbol"] != null)
        {
            func.Symbol = symNode["symbol"]?.AsString() ?? string.Empty;
        }

        var callable = node["callable"];
        if (callable != null)
        {
            var exec = callable["execution"];
            if (exec?.AsString() == "Async" || exec?["Async"] != null || exec?["Asynchronous"] != null)
            {
                func.IsAsync = true;
                var pollHandle = exec?["PollHandle"] ?? exec?["Asynchronous"]?["PollHandle"];
                if (pollHandle != null)
                {
                    func.AsyncPollSymbol = pollHandle["poll"]?["name"]?.AsString() ?? pollHandle["poll"]?.AsString() ?? string.Empty;
                    func.AsyncCompleteSymbol = pollHandle["complete"]?["name"]?.AsString() ?? pollHandle["complete"]?.AsString() ?? string.Empty;
                    func.AsyncCancelSymbol = pollHandle["cancel"]?["name"]?.AsString() ?? pollHandle["cancel"]?.AsString() ?? string.Empty;
                    func.AsyncFreeSymbol = pollHandle["free"]?["name"]?.AsString() ?? pollHandle["free"]?.AsString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(func.AsyncPollSymbol) && !string.IsNullOrEmpty(func.Symbol))
                {
                    string baseSym = func.Symbol.StartsWith("boltffi_") ? func.Symbol.Substring(8) : func.Symbol;
                    func.AsyncPollSymbol = $"boltffi_async_{baseSym}_poll";
                    func.AsyncCompleteSymbol = $"boltffi_async_{baseSym}_complete";
                    func.AsyncCancelSymbol = $"boltffi_async_{baseSym}_cancel";
                    func.AsyncFreeSymbol = $"boltffi_async_{baseSym}_free";
                }
            }

            var err = callable["error"];
            if (err is JsonObject && err["None"] == null)
            {
                func.HasError = true;
                if (err["Payload"] is JsonNode pNode) func.ErrorType = BoltFfiType.FromJson(pNode["ty"] ?? pNode);
                else if (err["Custom"] != null) func.ErrorType = BoltFfiType.FromJson(err["Custom"]);
                else if (err["Enum"] != null) func.ErrorType = BoltFfiType.FromJson(err);
                else if (err["Record"] != null) func.ErrorType = BoltFfiType.FromJson(err);
            }

            var returns = callable["returns"] ?? node["returns"];
            if (returns != null)
            {
                if (returns["ty"] != null)
                {
                    func.ReturnType = BoltFfiType.FromJson(returns["ty"]);
                }
                else if (returns["plan"] is JsonNode plan)
                {
                    if (plan is JsonString ps && ps.Value == "Void")
                    {
                        func.ReturnType = BoltFfiType.Void;
                    }
                    else if (plan["Void"] != null)
                    {
                        func.ReturnType = BoltFfiType.Void;
                    }
                    else if (plan["DirectViaReturnSlot"] is JsonNode slot1)
                    {
                        func.ReturnType = BoltFfiType.FromDirectValue(slot1["ty"] ?? slot1);
                    }
                    else if (plan["EncodedViaReturnSlot"] is JsonNode slot2)
                    {
                        func.ReturnType = BoltFfiType.FromJson(slot2["ty"] ?? slot2);
                    }
                    else if (plan["HandleViaReturnSlot"] is JsonNode slot3)
                    {
                        func.ReturnType = BoltFfiType.FromJson(slot3["target"] ?? slot3);
                    }
                    else if (plan["DirectViaOutPointer"] is JsonNode slot4)
                    {
                        func.ReturnType = BoltFfiType.FromDirectValue(slot4["ty"] ?? slot4);
                    }
                }
                else if (returns["Value"] != null)
                {
                    func.ReturnType = BoltFfiType.FromJson(returns["Value"]);
                }
            }

            if (callable["params"] is JsonArray paramsArr)
            {
                foreach (var p in paramsArr.Items)
                {
                    var param = new BoltFfiParamDecl
                    {
                        Name = BoltFfiCanonicalName.FromJson(p["name"]).ToCamelCase(),
                        Type = BoltFfiType.FromParamPayload(p["payload"] ?? p["plan"] ?? p["ty"]),
                        DefaultValue = p["default"]?.AsString() ?? p["default_value"]?.AsString(),
                        DocComment = p["doc"]?.AsString() ?? p["docstring"]?.AsString() ?? p["comment"]?.AsString()
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
    public string? DocComment { get; set; }
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

        cls.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        cls.Name = BoltFfiCanonicalName.FromJson(node["name"]);
        cls.ReleaseSymbol = node["release_symbol"]?.AsString()
            ?? node["release_entry"]?.AsString()
            ?? node["release"]?.AsString()
            ?? node["release"]?["symbol"]?.AsString()
            ?? node["release"]?["name"]?.AsString()
            ?? string.Empty;

        if (node["initializers"] is JsonArray initsArr)
        {
            foreach (var item in initsArr.Items)
            {
                var func = BoltFfiFunctionDecl.FromJson(item);
                if (func != null) cls.Initializers.Add(func);
            }
        }

        if (node["methods"] is JsonArray methodsArr)
        {
            foreach (var item in methodsArr.Items)
            {
                var func = BoltFfiFunctionDecl.FromJson(item);
                if (func != null) cls.Methods.Add(func);
            }
        }

        return cls;
    }
}

public sealed class BoltFfiCallbackDecl
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public string RegisterSymbol { get; set; } = string.Empty;
    public string CreateHandleSymbol { get; set; } = string.Empty;
    public List<BoltFfiFunctionDecl> Methods { get; set; } = new();
    public string? DocComment { get; set; }

    public static BoltFfiCallbackDecl FromJson(JsonNode? node)
    {
        var cb = new BoltFfiCallbackDecl();
        if (node == null) return cb;
        cb.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        cb.Name = BoltFfiCanonicalName.FromJson(node["name"]);

        var proto = node["protocol"];
        if (proto != null)
        {
            cb.RegisterSymbol = proto["register"]?["name"]?.AsString() ?? proto["register"]?.AsString() ?? string.Empty;
            cb.CreateHandleSymbol = proto["create_handle"]?["name"]?.AsString() ?? proto["create_handle"]?.AsString() ?? string.Empty;
        }
        string cbSnake = cb.Name.ToSnakeCase();
        if (string.IsNullOrEmpty(cb.RegisterSymbol)) cb.RegisterSymbol = $"boltffi_register_callback_{cbSnake}";
        if (string.IsNullOrEmpty(cb.CreateHandleSymbol)) cb.CreateHandleSymbol = $"boltffi_create_callback_{cbSnake}";

        var methodsNode = node["methods"] ?? node["protocol"]?["methods"] ?? node["protocol"]?["vtable"]?["methods"];
        if (methodsNode is JsonArray arr)
        {
            foreach (var m in arr.Items)
            {
                var method = BoltFfiFunctionDecl.FromJson(m);
                if (method != null) cb.Methods.Add(method);
            }
        }
        return cb;
    }
}

public sealed class BoltFfiStreamDecl
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public BoltFfiType ItemType { get; set; } = new();
    public string SubscribeSymbol { get; set; } = string.Empty;
    public string PopBatchSymbol { get; set; } = string.Empty;
    public string WaitSymbol { get; set; } = string.Empty;
    public string PollSymbol { get; set; } = string.Empty;
    public string UnsubscribeSymbol { get; set; } = string.Empty;
    public string FreeSymbol { get; set; } = string.Empty;
    public string? DocComment { get; set; }

    public static BoltFfiStreamDecl FromJson(JsonNode? node)
    {
        var s = new BoltFfiStreamDecl();
        if (node == null) return s;
        s.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        s.Name = BoltFfiCanonicalName.FromJson(node["name"]);
        s.ItemType = BoltFfiType.FromJson(node["item"]?["ty"] ?? node["item"]);
        var proto = node["protocol"];
        if (proto != null)
        {
            s.SubscribeSymbol = proto["subscribe"]?["name"]?.AsString() ?? proto["subscribe"]?.AsString() ?? string.Empty;
            s.PopBatchSymbol = proto["pop_batch"]?["name"]?.AsString() ?? proto["pop_batch"]?.AsString() ?? string.Empty;
            s.WaitSymbol = proto["wait"]?["name"]?.AsString() ?? proto["wait"]?.AsString() ?? string.Empty;
            s.PollSymbol = proto["poll"]?["name"]?.AsString() ?? proto["poll"]?.AsString() ?? string.Empty;
            s.UnsubscribeSymbol = proto["unsubscribe"]?["name"]?.AsString() ?? proto["unsubscribe"]?.AsString() ?? string.Empty;
            s.FreeSymbol = proto["free"]?["name"]?.AsString() ?? proto["free"]?.AsString() ?? string.Empty;
        }
        string streamSnake = s.Name.ToSnakeCase();
        if (string.IsNullOrEmpty(s.SubscribeSymbol)) s.SubscribeSymbol = $"boltffi_stream_{streamSnake}_subscribe";
        if (string.IsNullOrEmpty(s.PopBatchSymbol)) s.PopBatchSymbol = $"boltffi_stream_{streamSnake}_pop_batch";
        if (string.IsNullOrEmpty(s.WaitSymbol)) s.WaitSymbol = $"boltffi_stream_{streamSnake}_wait";
        if (string.IsNullOrEmpty(s.PollSymbol)) s.PollSymbol = $"boltffi_stream_{streamSnake}_poll";
        if (string.IsNullOrEmpty(s.UnsubscribeSymbol)) s.UnsubscribeSymbol = $"boltffi_stream_{streamSnake}_unsubscribe";
        if (string.IsNullOrEmpty(s.FreeSymbol)) s.FreeSymbol = $"boltffi_stream_{streamSnake}_free";
        return s;
    }
}

public sealed class BoltFfiConstantDecl
{
    public BoltFfiCanonicalName Name { get; set; } = new();
    public BoltFfiType Type { get; set; } = new();
    public string ValueLiteral { get; set; } = string.Empty;
    public string? DocComment { get; set; }

    public static BoltFfiConstantDecl FromJson(JsonNode? node)
    {
        var c = new BoltFfiConstantDecl();
        if (node == null) return c;
        c.DocComment = node["doc"]?.AsString() ?? node["docstring"]?.AsString() ?? node["comment"]?.AsString();
        c.Name = BoltFfiCanonicalName.FromJson(node["name"]);
        c.Type = BoltFfiType.FromJson(node["ty"] ?? node["type"]);
        c.ValueLiteral = node["value"]?.ToString() ?? node["literal"]?.ToString() ?? string.Empty;
        return c;
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
    Duration,
    DateTime,
    Guid,
    Uri,
    Record,
    Enum,
    Class,
    Callback,
    Option,
    Sequence,
    Map,
    Set,
    Custom
}

public sealed class BoltFfiType
{
    public BoltFfiTypeKind Kind { get; set; }
    public string CustomTypeName { get; set; } = string.Empty;
    public BoltFfiType? InnerType { get; set; }
    public BoltFfiType? KeyType { get; set; }
    public BoltFfiType? ValueType { get; set; }
    public bool IsDirectRecord { get; set; } = true;
    public bool IsCStyleEnum { get; set; } = true;

    public static BoltFfiType Void => new() { Kind = BoltFfiTypeKind.Void };
    public static BoltFfiType Primitive(BoltFfiTypeKind kind) => new() { Kind = kind };
    public static BoltFfiType Class(string className) => new() { Kind = BoltFfiTypeKind.Class, CustomTypeName = className };

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

        if (node["Class"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Class,
                CustomTypeName = node["Class"]?.ToString() ?? string.Empty
            };
        }

        if (node["Callback"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Callback,
                CustomTypeName = node["Callback"]?.ToString() ?? string.Empty
            };
        }

        if (node["Option"] != null || node["Optional"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Option,
                InnerType = FromJson(node["Option"] ?? node["Optional"])
            };
        }

        if (node["Vec"] != null || node["Sequence"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Sequence,
                InnerType = FromJson(node["Vec"] ?? node["Sequence"])
            };
        }

        if (node["Map"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Map,
                KeyType = FromJson(node["Map"]?["key"]),
                ValueType = FromJson(node["Map"]?["value"])
            };
        }

        if (node["Set"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Set,
                InnerType = FromJson(node["Set"])
            };
        }

        if (node["Builtin"] != null)
        {
            string? b = node["Builtin"]?.AsString();
            return b switch
            {
                "String" => Primitive(BoltFfiTypeKind.String),
                "Bytes" => Primitive(BoltFfiTypeKind.Bytes),
                "Duration" => Primitive(BoltFfiTypeKind.Duration),
                "DateTime" => Primitive(BoltFfiTypeKind.DateTime),
                "Guid" => Primitive(BoltFfiTypeKind.Guid),
                "Uri" => Primitive(BoltFfiTypeKind.Uri),
                _ => Primitive(BoltFfiTypeKind.Bytes)
            };
        }
        if (node["Custom"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Custom,
                CustomTypeName = node["Custom"]?.ToString() ?? string.Empty
            };
        }

        if (node is JsonObject)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Custom,
                CustomTypeName = node.ToString()
            };
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
        if (node["ty"] != null)
        {
            return FromJson(node["ty"]);
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
        if (node["Callback"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Callback,
                CustomTypeName = node["Callback"]?.ToString() ?? string.Empty
            };
        }
        if (node["Custom"] != null)
        {
            return new BoltFfiType
            {
                Kind = BoltFfiTypeKind.Custom,
                CustomTypeName = node["Custom"]?.ToString() ?? string.Empty
            };
        }
        return ParsePrimitive(node.AsString());
    }

    public static BoltFfiType FromParamPayload(JsonNode? node)
    {
        if (node == null) return Primitive(BoltFfiTypeKind.Bytes);
        if (node["Value"] is JsonNode val)
        {
            if (val["Direct"] is JsonNode d) return FromDirectValue(d["ty"] ?? d);
            if (val["Encoded"] is JsonNode e) return FromJson(e["ty"] ?? e);
            if (val["Handle"] is JsonNode h)
            {
                var target = h["target"];
                if (target?["Callback"] != null)
                {
                    return new BoltFfiType
                    {
                        Kind = BoltFfiTypeKind.Callback,
                        CustomTypeName = target["Callback"]?.ToString() ?? string.Empty
                    };
                }
                if (target?["Class"] != null)
                {
                    return new BoltFfiType
                    {
                        Kind = BoltFfiTypeKind.Class,
                        CustomTypeName = target["Class"]?.ToString() ?? string.Empty
                    };
                }
            }
        }
        if (node["Handle"] is JsonNode hnd)
        {
            var target = hnd["target"];
            if (target?["Callback"] != null)
            {
                return new BoltFfiType
                {
                    Kind = BoltFfiTypeKind.Callback,
                    CustomTypeName = target["Callback"]?.ToString() ?? string.Empty
                };
            }
            if (target?["Class"] != null)
            {
                return new BoltFfiType
                {
                    Kind = BoltFfiTypeKind.Class,
                    CustomTypeName = target["Class"]?.ToString() ?? string.Empty
                };
            }
        }
        if (node["Direct"] is JsonNode dir)
        {
            return FromDirectValue(dir["ty"] ?? dir);
        }
        if (node["Encoded"] is JsonNode enc)
        {
            return FromJson(enc["ty"] ?? enc);
        }
        return FromJson(node);
    }

    public static BoltFfiType FromParamPlan(JsonNode? node) => FromParamPayload(node);

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
            "Bytes" => Primitive(BoltFfiTypeKind.Bytes),
            "Duration" => Primitive(BoltFfiTypeKind.Duration),
            "DateTime" => Primitive(BoltFfiTypeKind.DateTime),
            "Guid" => Primitive(BoltFfiTypeKind.Guid),
            "Uri" => Primitive(BoltFfiTypeKind.Uri),
            _ => Primitive(BoltFfiTypeKind.Void)
        };
    }
}
