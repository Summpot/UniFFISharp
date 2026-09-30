using System;
using System.Collections.Generic;
using System.Text;

namespace UniFFISharp.Generator.Metadata;

public class MetadataReader
{
    private readonly byte[] _data;
    private int _pos;

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
        string str = Encoding.UTF8.GetString(_data, _pos, length);
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
        string str = Encoding.UTF8.GetString(_data, _pos, length);
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
                return ReadLiteral(type?.InnerType);
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
        byte kind = ReadUInt8();
        switch (kind)
        {
            case 0: // LIT_STR
            case 1: // LIT_INT
            case 2: // LIT_FLOAT
                ReadString();
                break;
            case 3: // LIT_BOOL
                ReadBool();
                break;
            case 4: // LIT_NONE
            case 6: // LIT_EMPTY_SEQ
            case 7: // LIT_EMPTY_MAP
            case 8: // LIT_EMPTY_SET
                break;
            case 5: // LIT_SOME
                SkipLiteral();
                break;
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

    public UniFFIType ReadType()
    {
        byte code = ReadUInt8();
        return code switch
        {
            0 => UniFFIType.Primitive(TypeKind.UInt8),
            1 => UniFFIType.Primitive(TypeKind.UInt16),
            2 => UniFFIType.Primitive(TypeKind.UInt32),
            3 => UniFFIType.Primitive(TypeKind.UInt64),
            4 => UniFFIType.Primitive(TypeKind.Int8),
            5 => UniFFIType.Primitive(TypeKind.Int16),
            6 => UniFFIType.Primitive(TypeKind.Int32),
            7 => UniFFIType.Primitive(TypeKind.Int64),
            8 => UniFFIType.Primitive(TypeKind.Float32),
            9 => UniFFIType.Primitive(TypeKind.Float64),
            10 => UniFFIType.Primitive(TypeKind.Boolean),
            11 => UniFFIType.Primitive(TypeKind.String),
            12 => new UniFFIType { Kind = TypeKind.Option, InnerType = ReadType() },
            13 => new UniFFIType { Kind = TypeKind.Record, ModulePath = ReadString(), Name = ReadString() },
            14 => new UniFFIType { Kind = TypeKind.Enum, ModulePath = ReadString(), Name = ReadString() },
            16 => new UniFFIType { Kind = TypeKind.Interface, ModulePath = ReadString(), Name = ReadString() },
            17 => ReadVecType(),
            18 => new UniFFIType { Kind = TypeKind.Map, KeyType = ReadType(), ValueType = ReadType() },
            19 => UniFFIType.Primitive(TypeKind.Timestamp),
            20 => UniFFIType.Primitive(TypeKind.Duration),
            21 => new UniFFIType { Kind = TypeKind.CallbackInterface, ModulePath = ReadString(), Name = ReadString() },
            22 => new UniFFIType { Kind = TypeKind.Custom, ModulePath = ReadString(), Name = ReadString(), InnerType = ReadType() },
            24 => ReadTraitInterfaceType(),
            26 => new UniFFIType { Kind = TypeKind.Option, InnerType = ReadType() }, // Box<T> treats as inner
            27 => new UniFFIType { Kind = TypeKind.Set, InnerType = ReadType() },
            _ => throw new InvalidOperationException($"Unexpected UniFFI type code: {code}")
        };
    }

    private UniFFIType ReadVecType()
    {
        var inner = ReadType();
        if (inner.Kind == TypeKind.UInt8)
        {
            return UniFFIType.Primitive(TypeKind.Bytes);
        }
        return new UniFFIType { Kind = TypeKind.Sequence, InnerType = inner };
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
