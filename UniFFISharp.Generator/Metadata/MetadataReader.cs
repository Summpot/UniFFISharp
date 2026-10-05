using System;
using System.Collections.Generic;
using System.Text;

namespace UniFFISharp.Generator.Metadata;

public class MetadataReader
{
    private readonly byte[] _data;
    private int _pos;
    public const int MaxTypeNestingDepth = 64;
    private static readonly Encoding Utf8Strict = new UTF8Encoding(false, true);

    public MetadataReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _pos = 0;
    }

    public int Position => _pos;

    public byte PeekUInt8()
    {
        if (_pos >= _data.Length) throw new InvalidOperationException("End of metadata buffer reached.");
        return _data[_pos];
    }

    public byte ReadUInt8()
    {
        if (_pos >= _data.Length) throw new InvalidOperationException("End of metadata buffer reached.");
        return _data[_pos++];
    }

    public ushort ReadUInt16()
    {
        if (_pos + 2 > _data.Length) throw new InvalidOperationException("End of metadata buffer reached.");
        ushort val = (ushort)(_data[_pos] | (_data[_pos + 1] << 8));
        _pos += 2;
        return val;
    }

    public uint ReadUInt32()
    {
        if (_pos + 4 > _data.Length) throw new InvalidOperationException("End of metadata buffer reached.");
        uint val = (uint)(_data[_pos] | (_data[_pos + 1] << 8) | (_data[_pos + 2] << 16) | (_data[_pos + 3] << 24));
        _pos += 4;
        return val;
    }

    public bool ReadBool() => ReadUInt8() == 1;

    public string ReadString()
    {
        int length = ReadUInt8();
        if (_pos + length > _data.Length) throw new InvalidOperationException("String length exceeds buffer.");
        string str = Utf8Strict.GetString(_data, _pos, length);
        _pos += length;
        return str;
    }

    public string? ReadOptionalString()
    {
        return ReadBool() ? ReadString() : null;
    }

    public string ReadLongString()
    {
        int length = ReadUInt16();
        if (_pos + length > _data.Length) throw new InvalidOperationException("Long string length exceeds buffer.");
        string str = Utf8Strict.GetString(_data, _pos, length);
        _pos += length;
        return str;
    }

    public string? ReadOptionalLongString()
    {
        string str = ReadLongString();
        return string.IsNullOrEmpty(str) ? null : str;
    }

    public string? ReadLiteral(UniFFIType? type)
    {
        while (true)
        {
            byte kind = ReadUInt8();
            switch (kind)
            {
                case 0: // LIT_STR
                {
                    string s = ReadString();
                    return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
                }
                case 1: // LIT_INT
                {
                    string s = ReadString();
                    if (type != null)
                    {
                        if (type.Kind == TypeKind.UInt64) return s + "UL";
                        if (type.Kind == TypeKind.Int64) return s + "L";
                        if (type.Kind == TypeKind.UInt32) return s + "U";
                    }
                    return s;
                }
                case 2: // LIT_FLOAT
                {
                    string s = ReadString();
                    if (type != null && type.Kind == TypeKind.Float32)
                    {
                        return s + "f";
                    }
                    return s;
                }
                case 3: // LIT_BOOL
                {
                    return ReadBool() ? "true" : "false";
                }
                case 4: // LIT_NONE
                {
                    return "null";
                }
                case 5: // LIT_SOME
                {
                    type = type?.InnerType;
                    continue;
                }
                case 6: // LIT_EMPTY_SEQ
                case 7: // LIT_EMPTY_MAP
                {
                    return "null";
                }
                default:
                    return null;
            }
        }
    }

    public string? ReadOptionalDefault(string name, UniFFIType? type)
    {
        if (ReadBool())
        {
            byte defaultKind = ReadUInt8();
            if (defaultKind == 1) // DEFVALUE_LITERAL
            {
                return ReadLiteral(type);
            }
            return null; // DEFVALUE_DEFAULT
        }
        return null;
    }

    public string? ReadOptionalLiteral(UniFFIType? type)
    {
        if (ReadBool())
        {
            return ReadLiteral(type);
        }
        return null;
    }

    public void SkipLiteral()
    {
        while (true)
        {
            byte kind = ReadUInt8();
            if (kind == 5) // LIT_SOME
            {
                continue;
            }
            switch (kind)
            {
                case 0: // LIT_STR
                case 1: // LIT_INT
                case 2: // LIT_FLOAT
                    ReadString();
                    return;
                case 3: // LIT_BOOL
                    ReadBool();
                    return;
                case 4: // LIT_NONE
                case 6: // LIT_EMPTY_SEQ
                case 7: // LIT_EMPTY_MAP
                case 8: // LIT_EMPTY_SET
                    return;
                default:
                    return;
            }
        }
    }

    public void SkipOptionalLiteral()
    {
        if (ReadBool())
        {
            SkipLiteral();
        }
    }

    public void SkipOptionalDefault()
    {
        if (ReadBool())
        {
            byte defaultKind = ReadUInt8();
            if (defaultKind == 1)
            {
                SkipLiteral();
            }
        }
    }

    private enum FrameKind
    {
        Option,
        Sequence,
        Custom,
        Box,
        Set,
        MapKey,
        MapValue
    }

    private sealed class TypeFrame
    {
        public FrameKind Kind;
        public string? ModulePath;
        public string? Name;
        public UniFFIType? KeyType;
    }

    public UniFFIType ReadType()
    {
        var stack = new Stack<TypeFrame>();
        UniFFIType? result = null;

        while (result == null)
        {
            byte code = ReadUInt8();
            UniFFIType? currentType = null;

            switch (code)
            {
                case 0:  currentType = UniFFIType.Primitive(TypeKind.UInt8); break;
                case 1:  currentType = UniFFIType.Primitive(TypeKind.UInt16); break;
                case 2:  currentType = UniFFIType.Primitive(TypeKind.UInt32); break;
                case 3:  currentType = UniFFIType.Primitive(TypeKind.UInt64); break;
                case 4:  currentType = UniFFIType.Primitive(TypeKind.Int8); break;
                case 5:  currentType = UniFFIType.Primitive(TypeKind.Int16); break;
                case 6:  currentType = UniFFIType.Primitive(TypeKind.Int32); break;
                case 7:  currentType = UniFFIType.Primitive(TypeKind.Int64); break;
                case 8:  currentType = UniFFIType.Primitive(TypeKind.Float32); break;
                case 9:  currentType = UniFFIType.Primitive(TypeKind.Float64); break;
                case 10: currentType = UniFFIType.Primitive(TypeKind.Boolean); break;
                case 11: currentType = UniFFIType.Primitive(TypeKind.String); break;
                case 13: currentType = new UniFFIType { Kind = TypeKind.Record, ModulePath = ReadString(), Name = ReadString() }; break;
                case 14: currentType = new UniFFIType { Kind = TypeKind.Enum, ModulePath = ReadString(), Name = ReadString() }; break;
                case 16: currentType = new UniFFIType { Kind = TypeKind.Interface, ModulePath = ReadString(), Name = ReadString() }; break;
                case 19: currentType = UniFFIType.Primitive(TypeKind.Timestamp); break;
                case 20: currentType = UniFFIType.Primitive(TypeKind.Duration); break;
                case 21: currentType = new UniFFIType { Kind = TypeKind.CallbackInterface, ModulePath = ReadString(), Name = ReadString() }; break;
                case 24: currentType = ReadTraitInterfaceType(); break;

                case 12: // Option
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.Option });
                    break;
                case 17: // Vec
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.Sequence });
                    break;
                case 18: // Map
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.MapKey });
                    break;
                case 22: // Custom
                {
                    string mod = ReadString();
                    string name = ReadString();
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.Custom, ModulePath = mod, Name = name });
                    break;
                }
                case 26: // Box (treated as Option)
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.Box });
                    break;
                case 27: // Set
                    PushFrame(stack, new TypeFrame { Kind = FrameKind.Set });
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected UniFFI type code: {code}");
            }

            if (currentType != null)
            {
                result = UnwindStack(stack, currentType);
            }
        }

        return result;
    }

    private static void PushFrame(Stack<TypeFrame> stack, TypeFrame frame)
    {
        if (stack.Count >= MaxTypeNestingDepth)
        {
            throw new InvalidOperationException($"Type nesting depth exceeded limit ({MaxTypeNestingDepth}).");
        }
        stack.Push(frame);
    }

    private static UniFFIType? UnwindStack(Stack<TypeFrame> stack, UniFFIType completed)
    {
        while (stack.Count > 0)
        {
            var frame = stack.Pop();
            switch (frame.Kind)
            {
                case FrameKind.Option:
                case FrameKind.Box:
                    completed = new UniFFIType { Kind = TypeKind.Option, InnerType = completed };
                    break;
                case FrameKind.Sequence:
                    completed = completed.Kind == TypeKind.UInt8
                        ? UniFFIType.Primitive(TypeKind.Bytes)
                        : new UniFFIType { Kind = TypeKind.Sequence, InnerType = completed };
                    break;
                case FrameKind.Custom:
                    completed = new UniFFIType
                    {
                        Kind = TypeKind.Custom,
                        ModulePath = frame.ModulePath!,
                        Name = frame.Name!,
                        InnerType = completed
                    };
                    break;
                case FrameKind.Set:
                    completed = new UniFFIType { Kind = TypeKind.Set, InnerType = completed };
                    break;
                case FrameKind.MapKey:
                    frame.Kind = FrameKind.MapValue;
                    frame.KeyType = completed;
                    stack.Push(frame);
                    return null;
                case FrameKind.MapValue:
                    completed = new UniFFIType
                    {
                        Kind = TypeKind.Map,
                        KeyType = frame.KeyType!,
                        ValueType = completed
                    };
                    break;
            }
        }

        return completed;
    }

    private UniFFIType ReadTraitInterfaceType()
    {
        string modulePath = ReadString();
        string name = ReadString();
        _ = ReadUInt8(); // skip TraitKind
        return new UniFFIType { Kind = TypeKind.Interface, ModulePath = modulePath, Name = name };
    }

    public UniFFIType? ReadOptionalType()
    {
        if (PeekUInt8() == 255) // TYPE_UNIT
        {
            ReadUInt8();
            return null;
        }
        return ReadType();
    }

    public (UniFFIType? ReturnType, UniFFIType? Throws) ReadReturnType()
    {
        byte code = PeekUInt8();
        if (code == 255) // TYPE_UNIT
        {
            ReadUInt8();
            return (null, null);
        }
        if (code == 23) // TYPE_RESULT
        {
            ReadUInt8();
            return (ReadOptionalType(), ReadOptionalType());
        }
        return (ReadType(), null);
    }

    public List<FnParamMetadata> ReadInputs()
    {
        int count = ReadUInt8();
        var inputs = new List<FnParamMetadata>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString();
            var ty = ReadType();
            bool byRef = ReadBool();
            PassBy passBy = byRef ? PassBy.Ref : PassBy.Value;
            string? defVal = ReadOptionalDefault(name, ty);
            inputs.Add(new FnParamMetadata { Name = name, Type = ty, PassBy = passBy, DefaultValue = defVal });
        }
        return inputs;
    }

    public List<FieldMetadata> ReadFields()
    {
        int count = ReadUInt8();
        var fields = new List<FieldMetadata>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString();
            string? origName = ReadOptionalString();
            var ty = ReadType();
            string? defVal = ReadOptionalDefault(name, ty);
            string? doc = ReadOptionalLongString();
            fields.Add(new FieldMetadata { Name = name, Type = ty, Docstring = doc, DefaultValue = defVal });
        }
        return fields;
    }

    public List<VariantMetadata> ReadVariants()
    {
        int count = ReadUInt8();
        var variants = new List<VariantMetadata>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString();
            string? origName = ReadOptionalString();
            SkipOptionalLiteral(); // discr
            var fields = ReadFields();
            string? doc = ReadOptionalLongString();
            variants.Add(new VariantMetadata { Name = name, Fields = fields, Docstring = doc });
        }
        return variants;
    }

    public List<VariantMetadata> ReadFlatVariants()
    {
        int count = ReadUInt8();
        var variants = new List<VariantMetadata>(count);
        for (int i = 0; i < count; i++)
        {
            string name = ReadString();
            string? origName = ReadOptionalString();
            string? doc = ReadOptionalLongString();
            variants.Add(new VariantMetadata { Name = name, Fields = new List<FieldMetadata>(), Docstring = doc });
        }
        return variants;
    }

    public object? ReadItem()
    {
        if (_pos >= _data.Length) return null;
        byte code = ReadUInt8();

        switch (code)
        {
            case 6: // NAMESPACE
            {
                return new NamespaceMetadata
                {
                    CrateName = ReadString(),
                    Name = ReadString()
                };
            }
            case 0: // FUNC
            {
                string modulePath = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                bool isAsync = ReadBool();
                var inputs = ReadInputs();
                var (ret, throws) = ReadReturnType();
                string? doc = ReadOptionalLongString();
                ushort checksum = Checksum.Calculate(_data, _pos);
                return new FnMetadata
                {
                    ModulePath = modulePath,
                    Name = name,
                    IsAsync = isAsync,
                    Inputs = inputs,
                    ReturnType = ret,
                    Throws = throws,
                    Docstring = doc,
                    Checksum = checksum
                };
            }
            case 7: // CONSTRUCTOR
            {
                string modulePath = ReadString();
                string selfName = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                bool isAsync = ReadBool();
                var inputs = ReadInputs();
                var (_, throws) = ReadReturnType();
                string? doc = ReadOptionalLongString();
                ushort checksum = Checksum.Calculate(_data, _pos);
                return new ConstructorMetadata
                {
                    ModulePath = modulePath,
                    SelfName = selfName,
                    Name = name,
                    IsAsync = isAsync,
                    Inputs = inputs,
                    Throws = throws,
                    Docstring = doc,
                    Checksum = checksum
                };
            }
            case 1: // METHOD
            {
                string modulePath = ReadString();
                string selfName = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                bool isAsync = ReadBool();
                var inputs = ReadInputs();
                var (ret, throws) = ReadReturnType();
                string? doc = ReadOptionalLongString();
                ushort checksum = Checksum.Calculate(_data, _pos);
                return new MethodMetadata
                {
                    ModulePath = modulePath,
                    SelfName = selfName,
                    Name = name,
                    IsAsync = isAsync,
                    Inputs = inputs,
                    ReturnType = ret,
                    Throws = throws,
                    Docstring = doc,
                    Checksum = checksum
                };
            }
            case 2: // RECORD
            {
                string modulePath = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                var fields = ReadFields();
                string? doc = ReadOptionalLongString();
                return new RecordMetadata
                {
                    ModulePath = modulePath,
                    Name = name,
                    Fields = fields,
                    Docstring = doc
                };
            }
            case 3: // ENUM
            {
                string modulePath = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                byte shapeVal = ReadUInt8();
                var shape = (EnumShape)shapeVal;
                UniFFIType? discrType = ReadBool() ? ReadType() : null;
                var variants = shape == EnumShape.ErrorFlat ? ReadFlatVariants() : ReadVariants();
                bool nonExhaustive = ReadBool();
                string? doc = ReadOptionalLongString();
                return new EnumMetadata
                {
                    ModulePath = modulePath,
                    Name = name,
                    Shape = shape,
                    DiscrType = discrType,
                    Variants = variants,
                    NonExhaustive = nonExhaustive,
                    Docstring = doc
                };
            }
            case 4: // INTERFACE
            {
                string modulePath = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                string? doc = ReadOptionalLongString();
                return new ObjectMetadata
                {
                    ModulePath = modulePath,
                    Name = name,
                    Docstring = doc
                };
            }
            case 8: // UDL_FILE / old TRAIT_INTERFACE
            case 12: // TRAIT_INTERFACE
            {
                string modulePath = ReadString();
                string name = ReadString();
                string? origName = ReadOptionalString();
                string? doc = ReadOptionalLongString();
                _ = ReadUInt8(); // skip TraitKind byte
                return new ObjectMetadata
                {
                    ModulePath = modulePath,
                    Name = name,
                    Docstring = doc
                };
            }
            case 9: // CALLBACK_INTERFACE
            {
                return new CallbackInterfaceMetadata
                {
                    ModulePath = ReadString(),
                    Name = ReadString(),
                    Docstring = ReadOptionalLongString()
                };
            }
            case 10: // TRAIT_METHOD
            {
                string modulePath = ReadString();
                string traitName = ReadString();
                uint index = ReadUInt32();
                string name = ReadString();
                string? origName = ReadOptionalString();
                bool isAsync = ReadBool();
                var inputs = ReadInputs();
                var (ret, throws) = ReadReturnType();
                string? doc = ReadOptionalLongString();
                ushort checksum = Checksum.Calculate(_data, _pos);
                return new TraitMethodMetadata
                {
                    ModulePath = modulePath,
                    TraitName = traitName,
                    Index = index,
                    Name = name,
                    IsAsync = isAsync,
                    Inputs = inputs,
                    ReturnType = ret,
                    Throws = throws,
                    Docstring = doc,
                    Checksum = checksum
                };
            }
            default:
                return null;
        }
    }
}
