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
}

public class MetadataAggregator
{
    private readonly ComponentInterface _ci = new();
    private readonly Dictionary<string, ObjectMetadata> _objectsByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<ConstructorMetadata> _pendingConstructors = new();
    private readonly List<MethodMetadata> _pendingMethods = new();
    private readonly Dictionary<string, CallbackInterfaceMetadata> _callbacksByName = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<TraitMethodMetadata> _pendingTraitMethods = new();

    public ComponentInterface ComponentInterface => _ci;

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
