using System;
using System.Linq;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;

namespace UniFFISharp.Generator.Arbitration;

/// <summary>
/// Selects the optimal interop engine (UniFFI vs BoltFFI) based on empirical performance
/// cost models calibrated through rigorous benchmarks, with support for user overrides.
/// </summary>
public static class InteropArbitrator
{
    /// <summary>
    /// Arbitrates between UniFFI and BoltFFI for a given function signature.
    /// </summary>
    /// <param name="uniffiFn">Metadata of the UniFFI function declaration, if available.</param>
    /// <param name="boltFn">Metadata of the BoltFFI function declaration, if available.</param>
    /// <param name="overrideEngine">Explicit override engine specified by user attribute or configuration.</param>
    /// <returns>The winning <see cref="InteropEngine"/> that should emit the method.</returns>
    public static InteropEngine SelectBestEngine(
        FnMetadata? uniffiFn,
        BoltFfiFunctionDecl? boltFn,
        InteropEngine overrideEngine = InteropEngine.Auto)
    {
        // If exported only by one engine, use that engine
        if (uniffiFn == null && boltFn != null) return InteropEngine.BoltFFI;
        if (boltFn == null && uniffiFn != null) return InteropEngine.UniFFI;
        if (uniffiFn == null && boltFn == null) return InteropEngine.Auto;

        if (overrideEngine != InteropEngine.Auto)
        {
            return overrideEngine;
        }

        // --- Signature Cost Model ---

        // 1. Fallible pure scalar operations (Result<Scalar, Error>):
        // UniFFI returns the scalar directly in the CPU register (RAX/XMM0) and passes 'ref UniffiRustCallStatus'
        // for error signaling. BoltFFI error ABI returns a 16-byte FfiBuf and passes 'out' pointers,
        // which introduces stack frame spilling and memory write overhead (UniFFI 3.81 ns vs BoltFFI 6.53 ns, 72% faster).
        // This also applies to C-style enums which are returned as register integers.
        bool isFallible = (uniffiFn!.Throws != null) || boltFn!.HasError;
        if (isFallible)
        {
            bool allInputsScalar = uniffiFn.Inputs.All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));
            bool returnIsScalarOrVoid = uniffiFn.ReturnType == null || IsScalarType(uniffiFn.ReturnType, boltFn!.ReturnType);
            if (allInputsScalar && returnIsScalarOrVoid)
            {
                return InteropEngine.UniFFI;
            }
        }

        // 2. String echo / single string return (StringReturn, StringRoundtrip, Option<String>):
        // UniFFI RustBuffer lift uses direct pointer read and fast free without wire envelope overhead
        // (StringReturn: UniFFI 115.88 ns vs BoltFFI 158.26 ns, 37% faster; StringRoundtrip: UniFFI 83.28 ns vs BoltFFI 101.41 ns, 22% faster).
        // However, if there are multiple string inputs (like concat) or string queries, BoltFFI's
        // zero-allocation stackalloc wire encoder is 2x to 5x faster (6.27 ns vs 32.58 ns, 0B alloc vs RustBuffer).
        bool returnsStringOrOptionalString =
            (uniffiFn.ReturnType != null && (uniffiFn.ReturnType.Kind == TypeKind.String ||
                (uniffiFn.ReturnType.Kind == TypeKind.Option && uniffiFn.ReturnType.InnerType?.Kind == TypeKind.String))) ||
            (boltFn!.ReturnType.Kind == BoltFfiTypeKind.String ||
                (boltFn.ReturnType.Kind == BoltFfiTypeKind.Option && boltFn.ReturnType.InnerType?.Kind == BoltFfiTypeKind.String));

        int stringInputsCount = uniffiFn.Inputs.Count(p => p.Type.Kind == TypeKind.String);
        if (returnsStringOrOptionalString && stringInputsCount <= 1)
        {
            bool otherInputsScalar = uniffiFn.Inputs
                .Where(p => p.Type.Kind != TypeKind.String)
                .All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));

            if (otherInputsScalar)
            {
                return InteropEngine.UniFFI;
            }
        }

        // 3. Structs & Records (Direct Blittable 144x faster, Complex Wire 1.07x faster):
        // BoltFFI passes blittable structs directly across C-ABI with [SuppressGCTransition] (2.33 ns vs 336.32 ns, 144x faster, 0B alloc).
        // For complex nested records, BoltFFI's WireWriter/WireReader is also leaner than UniFFI RustBuffer (40.5 us vs 43.27 us).
        bool hasRecord = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Record) ||
                         (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Record) ||
                         boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Record) ||
                         boltFn.ReturnType.Kind == BoltFfiTypeKind.Record;
        if (hasRecord)
        {
            return InteropEngine.BoltFFI;
        }

        // 4. Objects / Classes (Native Handle Table vs Arc Pointer):
        // BoltFFI handle table calls with [SuppressGCTransition] are 8.6x to 12x faster (11.08 ns vs 94.98 ns),
        // and allocate less memory on lifecycle creation (24 B vs 40 B).
        bool hasClass = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Interface) ||
                        (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Interface) ||
                        boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Class) ||
                        boltFn.ReturnType.Kind == BoltFfiTypeKind.Class;
        if (hasClass)
        {
            return InteropEngine.BoltFFI;
        }

        // 5. Byte arrays / Slices / Large Buffers (Vec<u8>, byte[]):
        // BoltFFI stackalloc / ArrayPool pointer passing is 2.5x to 7x faster (SlicePass64B: 75.71 ns vs 192.71 ns;
        // LargeBuffer 1MB: 566 us vs 3114 us, 5.5x faster).
        bool hasBytes = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Bytes) ||
                        (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Bytes) ||
                        boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Bytes) ||
                        boltFn.ReturnType.Kind == BoltFfiTypeKind.Bytes;
        if (hasBytes)
        {
            return InteropEngine.BoltFFI;
        }

        // 6. Collections & Tagged Unions (Vec<T>, List<T>, Map, Set, ADT Enums):
        // BoltFFI WireWriter/WireReader with BinaryPrimitives is faster and leaner than UniFFI RustBuffer
        // (ComplexEntity pass: 40.50 us vs 43.27 us; TaggedUnion AST: 483.57 ns vs 508.04 ns).
        bool hasCollectionOrDataEnum =
            uniffiFn.Inputs.Any(p => p.Type.Kind is TypeKind.Sequence or TypeKind.Map or TypeKind.Set ||
                                     (p.Type.Kind == TypeKind.Enum && !IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)))) ||
            (uniffiFn.ReturnType != null && (uniffiFn.ReturnType.Kind is TypeKind.Sequence or TypeKind.Map or TypeKind.Set ||
                                             (uniffiFn.ReturnType.Kind == TypeKind.Enum && !IsScalarType(uniffiFn.ReturnType, boltFn!.ReturnType)))) ||
            boltFn!.Parameters.Any(p => p.Type.Kind is BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Map or BoltFfiTypeKind.Set ||
                                       (p.Type.Kind == BoltFfiTypeKind.Enum && !p.Type.IsCStyleEnum)) ||
            (boltFn.ReturnType.Kind is BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Map or BoltFfiTypeKind.Set ||
             (boltFn.ReturnType.Kind == BoltFfiTypeKind.Enum && !boltFn.ReturnType.IsCStyleEnum));
        if (hasCollectionOrDataEnum)
        {
            return InteropEngine.BoltFFI;
        }

        // 7. String inputs (infallible or multi-string):
        // BoltFFI single-pass stackalloc UTF-8 encoding is 2x to 5x faster with 0B allocations
        // (StringPass16B: 6.27 ns vs 32.58 ns; StringPass1KB: 16.83 ns vs 57.18 ns).
        bool hasStringInput = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.String) ||
                              boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.String);
        if (hasStringInput)
        {
            return InteropEngine.BoltFFI;
        }

        // 8. Infallible pure scalars:
        // Direct C-ABI call with [SuppressGCTransition] without status check overhead (1.53 ns vs 3.83 ns, 2.5x faster).
        bool allInputsScalarOrVoid = uniffiFn.Inputs.All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));
        bool returnScalarOrVoid = uniffiFn.ReturnType == null || IsScalarType(uniffiFn.ReturnType, boltFn!.ReturnType);
        if (allInputsScalarOrVoid && returnScalarOrVoid)
        {
            return InteropEngine.BoltFFI;
        }

        // 9. Default fallback: BoltFFI (ExactSpelling + SuppressGCTransition)
        return InteropEngine.BoltFFI;
    }

    private static bool IsScalarType(UniFFIType uniffiType, BoltFfiType? boltType = null)
    {
        if (IsScalarPrimitive(uniffiType)) return true;
        if (boltType != null && boltType.Kind == BoltFfiTypeKind.Enum && boltType.IsCStyleEnum) return true;
        return false;
    }

    private static BoltFfiType? GetBoltParamType(BoltFfiFunctionDecl? boltFn, string paramName)
    {
        if (boltFn == null) return null;
        var match = boltFn.Parameters.FirstOrDefault(p =>
            string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Name.Replace("_", ""), paramName.Replace("_", ""), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Codegen.TypeHelper.ToPascalCase(p.Name), Codegen.TypeHelper.ToPascalCase(paramName), StringComparison.OrdinalIgnoreCase));
        return match?.Type;
    }

    private static bool IsScalarPrimitive(UniFFIType type)
    {
        return type.Kind switch
        {
            TypeKind.UInt8 or TypeKind.Int8 or
            TypeKind.UInt16 or TypeKind.Int16 or
            TypeKind.UInt32 or TypeKind.Int32 or
            TypeKind.UInt64 or TypeKind.Int64 or
            TypeKind.Float32 or TypeKind.Float64 or
            TypeKind.Boolean => true,
            _ => false
        };
    }

    private static bool IsScalarPrimitive(BoltFfiType type)
    {
        return type.Kind switch
        {
            BoltFfiTypeKind.Bool or
            BoltFfiTypeKind.UInt8 or BoltFfiTypeKind.Int8 or
            BoltFfiTypeKind.UInt16 or BoltFfiTypeKind.Int16 or
            BoltFfiTypeKind.UInt32 or BoltFfiTypeKind.Int32 or
            BoltFfiTypeKind.UInt64 or BoltFfiTypeKind.Int64 or
            BoltFfiTypeKind.Float32 or BoltFfiTypeKind.Float64 => true,
            BoltFfiTypeKind.Enum when type.IsCStyleEnum => true,
            _ => false
        };
    }
}
