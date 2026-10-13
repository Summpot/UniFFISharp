using System;
using System.Linq;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;

namespace UniFFISharp.Generator.Arbitration;

/// <summary>
/// Selects the optimal interop engine (UniFFI vs BoltFFI) based on empirical performance
/// cost models across function signatures, with support for user overrides.
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
        // UniFFI returns the scalar directly in the CPU register (RAX/XMM0) and passes 'ref status'
        // for error signaling. BoltFFI error ABI returns a 16-byte FfiBuf and passes 'out' pointers,
        // which introduces stack spill and memory write overhead (UniFFI 2.90 ns vs BoltFFI 5.24 ns).
        bool isFallible = (uniffiFn!.Throws != null) || boltFn!.HasError;
        if (isFallible)
        {
            bool allInputsScalar = uniffiFn.Inputs.All(p => IsScalarPrimitive(p.Type));
            bool returnIsScalarOrVoid = uniffiFn.ReturnType == null || IsScalarPrimitive(uniffiFn.ReturnType);
            if (allInputsScalar && returnIsScalarOrVoid)
            {
                return InteropEngine.UniFFI;
            }
        }

        // 2. String echo / single string return:
        // UniFFI RustBuffer lift uses direct pointer read and fast free (67.61 ns vs BoltFFI 89.72 ns).
        // However, if there are multiple string inputs (like concat) or string length queries, BoltFFI's
        // zero-allocation stackalloc wire encoder is 3.5x to 5x faster (16.29 ns vs 57.06 ns).
        bool returnsString = (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.String) ||
                             (boltFn!.ReturnType.Kind == BoltFfiTypeKind.String);
        int stringInputsCount = uniffiFn.Inputs.Count(p => p.Type.Kind == TypeKind.String);
        if (returnsString && stringInputsCount <= 1)
        {
            bool otherInputsScalar = uniffiFn.Inputs.Where(p => p.Type.Kind != TypeKind.String).All(p => IsScalarPrimitive(p.Type));
            if (otherInputsScalar)
            {
                return InteropEngine.UniFFI;
            }
        }

        // 3. Direct Blittable Structs / Records (Point3D, UserInfo):
        // BoltFFI passes blittable structs directly across C-ABI with [SuppressGCTransition] (1.12 ns vs 135 ns, 120x faster, 0B alloc).
        bool hasBlittableStruct = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Record) ||
                                  (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Record) ||
                                  boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Record && p.Type.IsDirectRecord) ||
                                  (boltFn.ReturnType.Kind == BoltFfiTypeKind.Record && boltFn.ReturnType.IsDirectRecord);
        if (hasBlittableStruct)
        {
            return InteropEngine.BoltFFI;
        }

        // 4. Objects / Classes (Handle vs Pointer):
        // BoltFFI handle table calls with [SuppressGCTransition] are 12x faster (4.31 ns vs 49.87 ns).
        bool hasClass = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Interface) ||
                        (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Interface) ||
                        boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Class) ||
                        boltFn.ReturnType.Kind == BoltFfiTypeKind.Class;
        if (hasClass)
        {
            return InteropEngine.BoltFFI;
        }

        // 5. Byte arrays / Slices (Vec<u8>):
        // BoltFFI stackalloc / ArrayPool zero-copy slice passing is 2.5x to 7x faster (6.77 ns vs 17.52 ns).
        bool hasBytes = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Bytes) ||
                        (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Bytes) ||
                        boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Bytes) ||
                        boltFn.ReturnType.Kind == BoltFfiTypeKind.Bytes;
        if (hasBytes)
        {
            return InteropEngine.BoltFFI;
        }

        // 6. String inputs:
        // BoltFFI single-pass stackalloc UTF-8 encoding is 3x to 5x faster with 0B allocations.
        bool hasStringInput = uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.String) ||
                              boltFn!.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.String);
        if (hasStringInput)
        {
            return InteropEngine.BoltFFI;
        }

        // 7. Infallible pure scalars:
        // Direct C-ABI call with [SuppressGCTransition] without status check overhead (0.80 ns vs 2.86 ns, 3.5x faster).
        bool allInputsScalarOrVoid = uniffiFn.Inputs.All(p => IsScalarPrimitive(p.Type));
        bool returnScalarOrVoid = uniffiFn.ReturnType == null || IsScalarPrimitive(uniffiFn.ReturnType);
        if (allInputsScalarOrVoid && returnScalarOrVoid)
        {
            return InteropEngine.BoltFFI;
        }

        // 8. Default fallback: BoltFFI (ExactSpelling + SuppressGCTransition)
        return InteropEngine.BoltFFI;
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
            _ => false
        };
    }
}
