using System;
using System.Linq;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;

namespace UniFFISharp.Generator.Arbitration;

/// <summary>
/// Selects UniFFI or BoltFFI using signature heuristics,
/// with support for user overrides.
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

        // 1. Fallible pure scalar operations (Result<Scalar, Error>):
        // UniFFI returns the scalar directly in the CPU register (RAX/XMM0) and passes 'ref UniffiRustCallStatus'
        // for error signaling. BoltFFI error ABI returns a 16-byte FfiBuf and passes 'out' pointers,
        // which adds overhead on the success path (UniFFI 2.801 ns vs BoltFFI 5.350 ns).
        // Failure is cheaper with BoltFFI (922.154 ns vs 1122.956 ns), but signatures do not reveal failure rates.
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
        // (StringReturn: UniFFI 68.975 ns vs BoltFFI 91.196 ns; StringRoundtrip: 60.722 ns vs 82.930 ns).
        // However, if there are multiple string inputs (like concat) or string queries, BoltFFI's
        // stackalloc wire encoder favors BoltFFI (StringPass16B: 5.891 ns vs 28.784 ns, both 0B allocated).
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

        // Complex record returns favor UniFFI's decoder: QueryComplexEntities50 takes
        // 18.17 us vs BoltFFI's 19.15 us, with the same 25,656B allocation.
        // Apply this decoding heuristic through optional/collection wrappers, but keep direct records on BoltFFI.
        if (ContainsEncodedRecord(boltFn!.ReturnType))
        {
            return InteropEngine.UniFFI;
        }

        // 3. Structs & Records:
        // BoltFFI direct Point3D roundtrip: 0.960 ns vs UniFFI 123.094 ns, both 0B allocated.
        // Complex record inputs are effectively tied (PassComplexEntities50: 16.48 us vs 16.46 us);
        // retain BoltFFI for encoding rather than extending the return-decoding heuristic to inputs.
        bool hasRecord = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Record) ||
                         (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Record) ||
                         boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Record) ||
                         boltFn.ReturnType.Kind == BoltFfiTypeKind.Record;
        if (hasRecord)
        {
            return InteropEngine.BoltFFI;
        }

        // 4. Objects / Classes (Native Handle Table vs Arc Pointer):
        // BoltFFI handle table calls with [SuppressGCTransition] take 4.239 ns vs UniFFI 48.882 ns,
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
        // BoltFFI wins for inputs and managed byte[] returns (BytesPass64B: 30.413 ns vs 80.331 ns;
        // BytesReturn4KB: 241.134 ns vs 1692.989 ns; LargeBuffer1MB: 270.856 us vs 1568.729 us).
        bool hasBytes = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Bytes) ||
                        (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Bytes) ||
                        boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Bytes) ||
                        boltFn.ReturnType.Kind == BoltFfiTypeKind.Bytes;
        if (hasBytes)
        {
            return InteropEngine.BoltFFI;
        }

        // 6. Collections & Tagged Unions (Vec<T>, List<T>, Map, Set, ADT Enums):
        // BoltFFI wins for tagged-union input (AST evaluation: 221.36 ns vs 289.92 ns).
        // Other collection shapes retain BoltFFI unless the complex-record return rule above applies.
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
        // (StringPass16B: 5.891 ns vs 28.784 ns; StringPass1KB: 18.644 ns vs 53.149 ns).
        bool hasStringInput = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.String) ||
                              boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.String);
        if (hasStringInput)
        {
            return InteropEngine.BoltFFI;
        }

        // 8. Infallible pure scalars:
        // Direct C-ABI call with [SuppressGCTransition] (AddInt64: 0.743 ns vs UniFFI 2.756 ns).
        bool allInputsScalarOrVoid = uniffiFn.Inputs.All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));
        bool returnScalarOrVoid = uniffiFn.ReturnType == null || IsScalarType(uniffiFn.ReturnType, boltFn!.ReturnType);
        if (allInputsScalarOrVoid && returnScalarOrVoid)
        {
            return InteropEngine.BoltFFI;
        }

        // 9. Other signature shapes use BoltFFI.
        return InteropEngine.BoltFFI;
    }

    private static bool ContainsEncodedRecord(BoltFfiType type)
    {
        return type.Kind switch
        {
            BoltFfiTypeKind.Record => !type.IsDirectRecord,
            BoltFfiTypeKind.Option or BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Set =>
                type.InnerType != null && ContainsEncodedRecord(type.InnerType),
            BoltFfiTypeKind.Map =>
                (type.KeyType != null && ContainsEncodedRecord(type.KeyType)) ||
                (type.ValueType != null && ContainsEncodedRecord(type.ValueType)),
            _ => false
        };
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
