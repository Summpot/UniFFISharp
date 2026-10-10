using System;
using UniFFISharp.Exceptions;
using UniFFISharp.Streams;
using UniFFISharp.Types;

namespace UniFFISharp.Converters;

public abstract class FfiConverter<TCsType, TFfiType>
{
    public abstract TCsType Lift(TFfiType value);
    public abstract TFfiType Lower(TCsType value);
    public abstract TCsType Read(ref BigEndianStream stream);
    public abstract int AllocationSize(TCsType value);
    public abstract void Write(TCsType value, ref BigEndianStream stream);
}

public abstract class FfiConverterRustBuffer<TCsType> : FfiConverter<TCsType, RustBuffer>
{
    public abstract RustBuffer Alloc(int size);
    public abstract void Free(RustBuffer buffer);

    public override RustBuffer Lower(TCsType value)
    {
        var rbuf = Alloc(AllocationSize(value));
        try
        {
            var stream = rbuf.AsWriteableStream();
            Write(value, ref stream);
            rbuf.len = Convert.ToUInt64(stream.Position);
            return rbuf;
        }
        catch
        {
            Free(rbuf);
            throw;
        }
    }

    public override TCsType Lift(RustBuffer rbuf)
    {
        var stream = rbuf.AsStream();
        try
        {
            var item = Read(ref stream);
            if (stream.HasRemaining())
            {
                throw new InternalException("junk remaining in buffer after lifting, something is very wrong!!");
            }
            return item;
        }
        finally
        {
            Free(rbuf);
        }
    }
}
