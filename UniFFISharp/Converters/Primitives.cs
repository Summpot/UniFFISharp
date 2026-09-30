using System;
using System.Text;
using UniFFISharp.Streams;
using UniFFISharp.Types;

namespace UniFFISharp.Converters;

public sealed class FfiConverterUInt8 : FfiConverter<byte, byte>
{
    public static readonly FfiConverterUInt8 INSTANCE = new();

    public override byte Lift(byte value) => value;
    public override byte Lower(byte value) => value;
    public override byte Read(BigEndianStream stream) => stream.ReadUInt8();
    public override int AllocationSize(byte value) => 1;
    public override void Write(byte value, BigEndianStream stream) => stream.WriteUInt8(value);
}

public sealed class FfiConverterInt8 : FfiConverter<sbyte, sbyte>
{
    public static readonly FfiConverterInt8 INSTANCE = new();

    public override sbyte Lift(sbyte value) => value;
    public override sbyte Lower(sbyte value) => value;
    public override sbyte Read(BigEndianStream stream) => stream.ReadInt8();
    public override int AllocationSize(sbyte value) => 1;
    public override void Write(sbyte value, BigEndianStream stream) => stream.WriteInt8(value);
}

public sealed class FfiConverterUInt16 : FfiConverter<ushort, ushort>
{
    public static readonly FfiConverterUInt16 INSTANCE = new();

    public override ushort Lift(ushort value) => value;
    public override ushort Lower(ushort value) => value;
    public override ushort Read(BigEndianStream stream) => stream.ReadUInt16();
    public override int AllocationSize(ushort value) => 2;
    public override void Write(ushort value, BigEndianStream stream) => stream.WriteUInt16(value);
}

public sealed class FfiConverterInt16 : FfiConverter<short, short>
{
    public static readonly FfiConverterInt16 INSTANCE = new();

    public override short Lift(short value) => value;
    public override short Lower(short value) => value;
    public override short Read(BigEndianStream stream) => stream.ReadInt16();
    public override int AllocationSize(short value) => 2;
    public override void Write(short value, BigEndianStream stream) => stream.WriteInt16(value);
}

public sealed class FfiConverterUInt32 : FfiConverter<uint, uint>
{
    public static readonly FfiConverterUInt32 INSTANCE = new();

    public override uint Lift(uint value) => value;
    public override uint Lower(uint value) => value;
    public override uint Read(BigEndianStream stream) => stream.ReadUInt32();
    public override int AllocationSize(uint value) => 4;
    public override void Write(uint value, BigEndianStream stream) => stream.WriteUInt32(value);
}

public sealed class FfiConverterInt32 : FfiConverter<int, int>
{
    public static readonly FfiConverterInt32 INSTANCE = new();

    public override int Lift(int value) => value;
    public override int Lower(int value) => value;
    public override int Read(BigEndianStream stream) => stream.ReadInt32();
    public override int AllocationSize(int value) => 4;
    public override void Write(int value, BigEndianStream stream) => stream.WriteInt32(value);
}

public sealed class FfiConverterUInt64 : FfiConverter<ulong, ulong>
{
    public static readonly FfiConverterUInt64 INSTANCE = new();

    public override ulong Lift(ulong value) => value;
    public override ulong Lower(ulong value) => value;
    public override ulong Read(BigEndianStream stream) => stream.ReadUInt64();
    public override int AllocationSize(ulong value) => 8;
    public override void Write(ulong value, BigEndianStream stream) => stream.WriteUInt64(value);
}

public sealed class FfiConverterInt64 : FfiConverter<long, long>
{
    public static readonly FfiConverterInt64 INSTANCE = new();

    public override long Lift(long value) => value;
    public override long Lower(long value) => value;
    public override long Read(BigEndianStream stream) => stream.ReadInt64();
    public override int AllocationSize(long value) => 8;
    public override void Write(long value, BigEndianStream stream) => stream.WriteInt64(value);
}

public sealed class FfiConverterFloat32 : FfiConverter<float, float>
{
    public static readonly FfiConverterFloat32 INSTANCE = new();

    public override float Lift(float value) => value;
    public override float Lower(float value) => value;
    public override float Read(BigEndianStream stream) => stream.ReadFloat32();
    public override int AllocationSize(float value) => 4;
    public override void Write(float value, BigEndianStream stream) => stream.WriteFloat32(value);
}

public sealed class FfiConverterFloat64 : FfiConverter<double, double>
{
    public static readonly FfiConverterFloat64 INSTANCE = new();

    public override double Lift(double value) => value;
    public override double Lower(double value) => value;
    public override double Read(BigEndianStream stream) => stream.ReadFloat64();
    public override int AllocationSize(double value) => 8;
    public override void Write(double value, BigEndianStream stream) => stream.WriteFloat64(value);
}

public sealed class FfiConverterBoolean : FfiConverter<bool, sbyte>
{
    public static readonly FfiConverterBoolean INSTANCE = new();

    public override bool Lift(sbyte value) => value != 0;
    public override sbyte Lower(bool value) => value ? (sbyte)1 : (sbyte)0;
    public override bool Read(BigEndianStream stream) => stream.ReadInt8() != 0;
    public override int AllocationSize(bool value) => 1;
    public override void Write(bool value, BigEndianStream stream) => stream.WriteInt8(value ? (sbyte)1 : (sbyte)0);
}
