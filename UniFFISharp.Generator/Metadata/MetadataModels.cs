using System.Collections.Generic;

namespace UniFFISharp.Generator.Metadata;

public enum TypeKind
{
    UInt8,
    Int8,
    UInt16,
    Int16,
    UInt32,
    Int32,
    UInt64,
    Int64,
    Float32,
    Float64,
    Boolean,
    String,
    Bytes,
    Timestamp,
    Duration,
    Record,
    Enum,
    Interface,
    CallbackInterface,
    Option,
    Sequence,
    Map,
    Set,
    Custom
}

public class UniFFIType
{
    public TypeKind Kind { get; set; }
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UniFFIType? InnerType { get; set; }
    public UniFFIType? KeyType { get; set; }
    public UniFFIType? ValueType { get; set; }

    public static UniFFIType Primitive(TypeKind kind) => new() { Kind = kind };
}

public enum PassBy
{
    Value,
    Ref,
    MutRef
}

public class FnParamMetadata
{
    public string Name { get; set; } = string.Empty;
    public UniFFIType Type { get; set; } = new();
    public PassBy PassBy { get; set; } = PassBy.Value;
    public string? DefaultValue { get; set; }
    public string? Docstring { get; set; }
}

public class FnMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public bool IsAsync { get; set; }
    public List<FnParamMetadata> Inputs { get; set; } = new();
    public UniFFIType? ReturnType { get; set; }
    public UniFFIType? Throws { get; set; }
    public string? Docstring { get; set; }
    public ushort? Checksum { get; set; }
}

public class ConstructorMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string SelfName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public bool IsAsync { get; set; }
    public List<FnParamMetadata> Inputs { get; set; } = new();
    public UniFFIType? Throws { get; set; }
    public string? Docstring { get; set; }
    public ushort? Checksum { get; set; }
}

public class MethodMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string SelfName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public bool IsAsync { get; set; }
    public List<FnParamMetadata> Inputs { get; set; } = new();
    public UniFFIType? ReturnType { get; set; }
    public UniFFIType? Throws { get; set; }
    public string? Docstring { get; set; }
    public ushort? Checksum { get; set; }
}

public class FieldMetadata
{
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public UniFFIType Type { get; set; } = new();
    public string? Docstring { get; set; }
    public string? DefaultValue { get; set; }
}

public class RecordMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
    public string? Docstring { get; set; }
}

public enum EnumShape
{
    Enum = 0,
    ErrorComplex = 1,
    ErrorFlat = 2
}

public class VariantMetadata
{
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
    public string? Docstring { get; set; }
}

public class EnumMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public EnumShape Shape { get; set; } = EnumShape.Enum;
    public UniFFIType? DiscrType { get; set; }
    public List<VariantMetadata> Variants { get; set; } = new();
    public bool NonExhaustive { get; set; }
    public string? Docstring { get; set; }
}

public class ObjectMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public string? Docstring { get; set; }
    public List<ConstructorMetadata> Constructors { get; set; } = new();
    public List<MethodMetadata> Methods { get; set; } = new();
}

public class NamespaceMetadata
{
    public string CrateName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class TraitMethodMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string TraitName { get; set; } = string.Empty;
    public uint Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? OrigName { get; set; }
    public bool IsAsync { get; set; }
    public List<FnParamMetadata> Inputs { get; set; } = new();
    public UniFFIType? ReturnType { get; set; }
    public UniFFIType? Throws { get; set; }
    public string? Docstring { get; set; }
    public ushort? Checksum { get; set; }
}

public class CallbackInterfaceMetadata
{
    public string ModulePath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Docstring { get; set; }
    public List<TraitMethodMetadata> Methods { get; set; } = new();
}

public class ComponentInterface
{
    public string CrateName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public List<FnMetadata> Functions { get; set; } = new();
    public List<RecordMetadata> Records { get; set; } = new();
    public List<EnumMetadata> Enums { get; set; } = new();
    public List<ObjectMetadata> Objects { get; set; } = new();
    public List<CallbackInterfaceMetadata> CallbackInterfaces { get; set; } = new();
    public List<string> DiscoveredSymbols { get; set; } = new();
    public List<string> AllExports { get; set; } = new();

    public bool IsEmpty => Functions.Count == 0 &&
                           Records.Count == 0 &&
                           Enums.Count == 0 &&
                           Objects.Count == 0 &&
                           CallbackInterfaces.Count == 0;

    private HashSet<string>? _flatEnumNames;

    public bool IsFlatEnum(string name)
    {
        if (_flatEnumNames == null)
        {
            var set = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var e in Enums)
            {
                if (e.Shape == EnumShape.Enum)
                {
                    bool allEmpty = true;
                    foreach (var v in e.Variants)
                    {
                        if (v.Fields.Count > 0)
                        {
                            allEmpty = false;
                            break;
                        }
                    }
                    if (allEmpty)
                    {
                        set.Add(e.Name);
                    }
                }
            }
            _flatEnumNames = set;
        }
        return _flatEnumNames.Contains(name);
    }
}

public class SingleCrateAggregator
{
    private readonly ComponentInterface _ci;
    private readonly Dictionary<string, ObjectMetadata> _objectsByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<ConstructorMetadata> _pendingConstructors = new();
    private readonly List<MethodMetadata> _pendingMethods = new();
    private readonly Dictionary<string, CallbackInterfaceMetadata> _callbacksByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<TraitMethodMetadata> _pendingTraitMethods = new();

    public ComponentInterface ComponentInterface => _ci;

    public SingleCrateAggregator(string crateName)
    {
        _ci = new ComponentInterface
        {
            CrateName = crateName,
            Namespace = crateName
        };
    }

    public void AddItem(object item)
    {
        switch (item)
        {
            case NamespaceMetadata ns:
                _ci.CrateName = ns.CrateName;
                _ci.Namespace = ns.Name;
                break;
            case FnMetadata fn:
                _ci.Functions.Add(fn);
                break;
            case RecordMetadata rec:
                _ci.Records.Add(rec);
                break;
            case EnumMetadata enm:
                _ci.Enums.Add(enm);
                break;
            case ObjectMetadata obj:
                _objectsByName[obj.Name] = obj;
                _ci.Objects.Add(obj);
                break;
            case ConstructorMetadata ctor:
                _pendingConstructors.Add(ctor);
                break;
            case MethodMetadata method:
                _pendingMethods.Add(method);
                break;
            case CallbackInterfaceMetadata cbi:
                _callbacksByName[cbi.Name] = cbi;
                _ci.CallbackInterfaces.Add(cbi);
                break;
            case TraitMethodMetadata tm:
                _pendingTraitMethods.Add(tm);
                break;
        }
    }

    public ComponentInterface Build()
    {
        foreach (var ctor in _pendingConstructors)
        {
            if (_objectsByName.TryGetValue(ctor.SelfName, out var obj))
            {
                obj.Constructors.Add(ctor);
            }
            else
            {
                var newObj = new ObjectMetadata
                {
                    ModulePath = ctor.ModulePath,
                    Name = ctor.SelfName,
                    Constructors = new List<ConstructorMetadata> { ctor }
                };
                _objectsByName[ctor.SelfName] = newObj;
                _ci.Objects.Add(newObj);
            }
        }

        foreach (var method in _pendingMethods)
        {
            if (_objectsByName.TryGetValue(method.SelfName, out var obj))
            {
                obj.Methods.Add(method);
            }
            else
            {
                var newObj = new ObjectMetadata
                {
                    ModulePath = method.ModulePath,
                    Name = method.SelfName,
                    Methods = new List<MethodMetadata> { method }
                };
                _objectsByName[method.SelfName] = newObj;
                _ci.Objects.Add(newObj);
            }
        }

        foreach (var tm in _pendingTraitMethods)
        {
            if (_callbacksByName.TryGetValue(tm.TraitName, out var cbi))
            {
                cbi.Methods.Add(tm);
            }
            else
            {
                var newCbi = new CallbackInterfaceMetadata
                {
                    ModulePath = tm.ModulePath,
                    Name = tm.TraitName,
                    Methods = new List<TraitMethodMetadata> { tm }
                };
                _callbacksByName[tm.TraitName] = newCbi;
                _ci.CallbackInterfaces.Add(newCbi);
            }
        }

        foreach (var cbi in _ci.CallbackInterfaces)
        {
            cbi.Methods.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        return _ci;
    }
}

public class MetadataAggregator
{
    private readonly Dictionary<string, SingleCrateAggregator> _crates = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<SingleCrateAggregator> _orderedCrates = new();
    private SingleCrateAggregator? _primaryCrate;
    private readonly List<string> _discoveredSymbols = new();
    private readonly List<string> _allExports = new();

    public ComponentInterface ComponentInterface => _primaryCrate?.ComponentInterface ?? GetOrCreate("default").ComponentInterface;
    public List<string> DiscoveredSymbols => _discoveredSymbols;
    public List<string> AllExports => _allExports;

    public static string GetCrateName(object item)
    {
        string raw = item switch
        {
            NamespaceMetadata ns => ns.CrateName,
            FnMetadata fn => fn.ModulePath,
            RecordMetadata rec => rec.ModulePath,
            EnumMetadata enm => enm.ModulePath,
            ObjectMetadata obj => obj.ModulePath,
            ConstructorMetadata ctor => ctor.ModulePath,
            MethodMetadata method => method.ModulePath,
            CallbackInterfaceMetadata cbi => cbi.ModulePath,
            TraitMethodMetadata tm => tm.ModulePath,
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(raw)) return "default";
        int idx = raw.IndexOf("::", System.StringComparison.Ordinal);
        string crate = idx >= 0 ? raw.Substring(0, idx) : raw;
        return crate.Replace('-', '_');
    }

    private SingleCrateAggregator GetOrCreate(string crateNorm)
    {
        if (!_crates.TryGetValue(crateNorm, out var agg))
        {
            agg = new SingleCrateAggregator(crateNorm);
            _crates[crateNorm] = agg;
            _orderedCrates.Add(agg);
            _primaryCrate ??= agg;
        }
        return agg;
    }

    public void AddItem(object item)
    {
        string crateNorm = GetCrateName(item);
        var agg = GetOrCreate(crateNorm);
        agg.AddItem(item);
    }

    public ComponentInterface Build()
    {
        var all = BuildAll();
        return all.Count > 0 ? all[0] : new ComponentInterface();
    }

    public IReadOnlyList<ComponentInterface> BuildAll()
    {
        var list = new List<ComponentInterface>();
        foreach (var agg in _orderedCrates)
        {
            var ci = agg.Build();
            if (!ci.IsEmpty)
            {
                ci.DiscoveredSymbols = _discoveredSymbols;
                ci.AllExports = _allExports;
                list.Add(ci);
            }
        }
        return list;
    }
}
