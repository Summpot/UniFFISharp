using System;
using System.Runtime.CompilerServices;
using System.Text;
using UniFFISharp.Streams;
using UniFFISharp.Types;

namespace UniFFISharp.Converters;

public sealed class FfiConverterUInt8 : FfiConverter<byte, byte>
{
    public static readonly FfiConverterUInt8 INSTANCE = new();

    public override byte Lift(byte value) => value;
    public override byte Lower(byte value) => value;
    public override byte Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(byte value) => AllocationSizeStatic(value);
    public override void Write(byte value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ReadStatic(ref BigEndianStream stream) => stream.ReadUInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(byte value) => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(byte value, ref BigEndianStream stream) => stream.WriteUInt8(value);
}

public sealed class FfiConverterInt8 : FfiConverter<sbyte, sbyte>
{
    public static readonly FfiConverterInt8 INSTANCE = new();

    public override sbyte Lift(sbyte value) => value;
    public override sbyte Lower(sbyte value) => value;
    public override sbyte Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(sbyte value) => AllocationSizeStatic(value);
    public override void Write(sbyte value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ReadStatic(ref BigEndianStream stream) => stream.ReadInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(sbyte value) => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(sbyte value, ref BigEndianStream stream) => stream.WriteInt8(value);
}

public sealed class FfiConverterUInt16 : FfiConverter<ushort, ushort>
{
    public static readonly FfiConverterUInt16 INSTANCE = new();

    public override ushort Lift(ushort value) => value;
    public override ushort Lower(ushort value) => value;
    public override ushort Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(ushort value) => AllocationSizeStatic(value);
    public override void Write(ushort value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadStatic(ref BigEndianStream stream) => stream.ReadUInt16();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(ushort value) => 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(ushort value, ref BigEndianStream stream) => stream.WriteUInt16(value);
}

public sealed class FfiConverterInt16 : FfiConverter<short, short>
{
    public static readonly FfiConverterInt16 INSTANCE = new();

    public override short Lift(short value) => value;
    public override short Lower(short value) => value;
    public override short Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(short value) => AllocationSizeStatic(value);
    public override void Write(short value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadStatic(ref BigEndianStream stream) => stream.ReadInt16();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(short value) => 2;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(short value, ref BigEndianStream stream) => stream.WriteInt16(value);
}

public sealed class FfiConverterUInt32 : FfiConverter<uint, uint>
{
    public static readonly FfiConverterUInt32 INSTANCE = new();

    public override uint Lift(uint value) => value;
    public override uint Lower(uint value) => value;
    public override uint Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(uint value) => AllocationSizeStatic(value);
    public override void Write(uint value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadStatic(ref BigEndianStream stream) => stream.ReadUInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(uint value) => 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(uint value, ref BigEndianStream stream) => stream.WriteUInt32(value);
}

public sealed class FfiConverterInt32 : FfiConverter<int, int>
{
    public static readonly FfiConverterInt32 INSTANCE = new();

    public override int Lift(int value) => value;
    public override int Lower(int value) => value;
    public override int Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(int value) => AllocationSizeStatic(value);
    public override void Write(int value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadStatic(ref BigEndianStream stream) => stream.ReadInt32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(int value) => 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(int value, ref BigEndianStream stream) => stream.WriteInt32(value);
}

public sealed class FfiConverterUInt64 : FfiConverter<ulong, ulong>
{
    public static readonly FfiConverterUInt64 INSTANCE = new();

    public override ulong Lift(ulong value) => value;
    public override ulong Lower(ulong value) => value;
    public override ulong Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(ulong value) => AllocationSizeStatic(value);
    public override void Write(ulong value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadStatic(ref BigEndianStream stream) => stream.ReadUInt64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(ulong value) => 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(ulong value, ref BigEndianStream stream) => stream.WriteUInt64(value);
}

public sealed class FfiConverterInt64 : FfiConverter<long, long>
{
    public static readonly FfiConverterInt64 INSTANCE = new();

    public override long Lift(long value) => value;
    public override long Lower(long value) => value;
    public override long Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(long value) => AllocationSizeStatic(value);
    public override void Write(long value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadStatic(ref BigEndianStream stream) => stream.ReadInt64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(long value) => 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(long value, ref BigEndianStream stream) => stream.WriteInt64(value);
}

public sealed class FfiConverterFloat32 : FfiConverter<float, float>
{
    public static readonly FfiConverterFloat32 INSTANCE = new();

    public override float Lift(float value) => value;
    public override float Lower(float value) => value;
    public override float Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(float value) => AllocationSizeStatic(value);
    public override void Write(float value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadStatic(ref BigEndianStream stream) => stream.ReadFloat32();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(float value) => 4;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(float value, ref BigEndianStream stream) => stream.WriteFloat32(value);
}

public sealed class FfiConverterFloat64 : FfiConverter<double, double>
{
    public static readonly FfiConverterFloat64 INSTANCE = new();

    public override double Lift(double value) => value;
    public override double Lower(double value) => value;
    public override double Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(double value) => AllocationSizeStatic(value);
    public override void Write(double value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadStatic(ref BigEndianStream stream) => stream.ReadFloat64();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(double value) => 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(double value, ref BigEndianStream stream) => stream.WriteFloat64(value);
}

public sealed class FfiConverterBoolean : FfiConverter<bool, sbyte>
{
    public static readonly FfiConverterBoolean INSTANCE = new();

    public override bool Lift(sbyte value) => value != 0;
    public override sbyte Lower(bool value) => value ? (sbyte)1 : (sbyte)0;
    public override bool Read(ref BigEndianStream stream) => ReadStatic(ref stream);
    public override int AllocationSize(bool value) => AllocationSizeStatic(value);
    public override void Write(bool value, ref BigEndianStream stream) => WriteStatic(value, ref stream);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ReadStatic(ref BigEndianStream stream) => stream.ReadInt8() != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AllocationSizeStatic(bool value) => 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteStatic(bool value, ref BigEndianStream stream) => stream.WriteInt8(value ? (sbyte)1 : (sbyte)0);
}
