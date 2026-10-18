using System;
using System.Linq;
using UniFFISharp.Generator.Metadata;
using UniFFISharp.Generator.Metadata.BoltFfi;

namespace UniFFISharp.Generator.Arbitration;

/// <summary>
/// Result of interop engine arbitration for a function.
/// </summary>
public sealed class ArbitrationDecision
{
    public InteropEngine SelectedEngine { get; }
    public InteropEngine OptimalEngine { get; }
    public bool IsSuboptimalMissingExport { get; }
    public string? PerformanceReason { get; }

    public ArbitrationDecision(
        InteropEngine selectedEngine,
        InteropEngine optimalEngine,
        bool isSuboptimalMissingExport,
        string? performanceReason)
    {
        SelectedEngine = selectedEngine;
        OptimalEngine = optimalEngine;
        IsSuboptimalMissingExport = isSuboptimalMissingExport;
        PerformanceReason = performanceReason;
    }

    /// <summary>
    /// Gets the recommended macro name (e.g. "boltffi" or "uniffi") if an export is missing.
    /// </summary>
    public string? MissingMacro => OptimalEngine switch
    {
        InteropEngine.BoltFFI => "boltffi",
        InteropEngine.UniFFI => "uniffi",
        _ => null
    };
}

/// <summary>
/// Selects UniFFI or BoltFFI using signature heuristics,
/// with support for user overrides and missing export detection.
/// </summary>
public static class InteropArbitrator
{
    /// <summary>
    /// Arbitrates between UniFFI and BoltFFI for a given function signature,
    /// returning detailed decision metadata including suboptimal missing export detection.
    /// </summary>
    public static ArbitrationDecision Arbitrate(
        FnMetadata? uniffiFn,
        BoltFfiFunctionDecl? boltFn,
        InteropEngine overrideEngine = InteropEngine.Auto)
    {
        if (uniffiFn == null && boltFn == null)
        {
            return new ArbitrationDecision(InteropEngine.Auto, InteropEngine.Auto, false, null);
        }

        var (optimalEngine, reason) = PredictOptimalEngine(uniffiFn, boltFn);

        InteropEngine selectedEngine;
        if (uniffiFn != null && boltFn != null)
        {
            selectedEngine = overrideEngine != InteropEngine.Auto ? overrideEngine : optimalEngine;
        }
        else if (uniffiFn != null)
        {
            selectedEngine = InteropEngine.UniFFI;
        }
        else if (boltFn != null)
        {
            selectedEngine = InteropEngine.BoltFFI;
        }
        else
        {
            selectedEngine = InteropEngine.Auto;
        }

        bool isSuboptimal = false;
        if (overrideEngine == InteropEngine.Auto)
        {
            if (uniffiFn != null && boltFn == null && optimalEngine == InteropEngine.BoltFFI)
            {
                isSuboptimal = CanHypotheticallyBindBoltFfi(uniffiFn);
            }
            else if (boltFn != null && uniffiFn == null && optimalEngine == InteropEngine.UniFFI)
            {
                isSuboptimal = CanHypotheticallyBindUniFfi(boltFn);
            }
        }

        return new ArbitrationDecision(selectedEngine, optimalEngine, isSuboptimal, isSuboptimal ? reason : null);
    }

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
        return Arbitrate(uniffiFn, boltFn, overrideEngine).SelectedEngine;
    }

    /// <summary>
    /// Predicts the theoretical optimal engine for a signature based on performance heuristics,
    /// regardless of whether both exports currently exist.
    /// </summary>
    public static (InteropEngine Optimal, string? Reason) PredictOptimalEngine(
        FnMetadata? uniffiFn,
        BoltFfiFunctionDecl? boltFn)
    {
        // 1. Fallible pure scalar operations (Result<Scalar, Error>):
        // UniFFI returns the scalar directly in the CPU register (RAX/XMM0) and passes 'ref UniffiRustCallStatus'
        // for error signaling. BoltFFI error ABI returns a 16-byte FfiBuf and passes 'out' pointers,
        // which adds overhead on the success path (UniFFI 2.801 ns vs BoltFFI 5.350 ns).
        // Failure is cheaper with BoltFFI (922.154 ns vs 1122.956 ns), but signatures do not reveal failure rates.
        // This also applies to C-style enums which are returned as register integers.
        bool isFallible = (uniffiFn?.Throws != null) || (boltFn?.HasError == true);
        if (isFallible && IsPureScalarSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.UniFFI, "register RAX scalar return avoids wire buffer allocations on success (~47% lower latency)");
        }

        // 2. String echo / single string return (StringReturn, StringRoundtrip, Option<String>):
        // UniFFI RustBuffer lift uses direct pointer read and fast free without wire envelope overhead
        // (StringReturn: UniFFI 68.975 ns vs BoltFFI 91.196 ns; StringRoundtrip: 60.722 ns vs 82.930 ns).
        // However, if there are multiple string inputs (like concat) or string queries, BoltFFI's
        // stackalloc wire encoder favors BoltFFI (StringPass16B: 5.891 ns vs 28.784 ns, both 0B allocated).
        if (IsSingleStringReturnSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.UniFFI, "direct RustBuffer lift avoids wire envelope overhead (~25% lower latency)");
        }

        // Complex record returns favor UniFFI's decoder: QueryComplexEntities50 takes
        // 18.17 us vs BoltFFI's 19.15 us, with the same 25,656B allocation.
        // Apply this decoding heuristic through optional/collection wrappers, but keep direct records on BoltFFI.
        if (ContainsEncodedRecordReturn(uniffiFn, boltFn))
        {
            return (InteropEngine.UniFFI, "UniFFI record decoding has lower overhead on returns");
        }

        // 3. Structs & Records:
        // BoltFFI direct Point3D roundtrip: 0.960 ns vs UniFFI 123.094 ns, both 0B allocated.
        // Complex record inputs are effectively tied (PassComplexEntities50: 16.48 us vs 16.46 us);
        // retain BoltFFI for encoding rather than extending the return-decoding heuristic to inputs.
        if (HasRecordSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "zero-copy C-ABI avoids record serialization (up to 99% lower latency)");
        }

        // 4. Objects / Classes (Native Handle Table vs Arc Pointer):
        // BoltFFI direct handle table calls with [SuppressGCTransition] achieve ~4.2 ns vs optimized UniFFI ~18 ns
        // (optimized from unoptimized ~48.88 ns by eliminating redundant CloneRustArcPtr P/Invoke & CAS spin).
        // BoltFFI also allocates less memory on lifecycle creation (24 B vs 40 B).
        if (HasClassSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "direct handle table calls avoid CAS spin and Arc clone overhead");
        }

        // 5. Byte arrays / Slices / Large Buffers (Vec<u8>, byte[]):
        // BoltFFI wins for inputs and managed byte[] returns (BytesPass64B: 30.413 ns vs 80.331 ns;
        // BytesReturn4KB: 241.134 ns vs 1692.989 ns; LargeBuffer1MB: 270.856 us vs 1568.729 us).
        if (HasBytesSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "zero-copy slice passing avoids buffer allocation (~70% lower latency)");
        }

        // 6. Collections & Tagged Unions (Vec<T>, List<T>, Map, Set, ADT Enums):
        // BoltFFI wins for tagged-union input (AST evaluation: 221.36 ns vs 289.92 ns).
        // Other collection shapes retain BoltFFI unless the complex-record return rule above applies.
        if (HasCollectionOrDataEnumSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "wire AST / collection encoding");
        }

        // 7. String inputs (infallible or multi-string):
        // BoltFFI single-pass stackalloc UTF-8 encoding is 2x to 5x faster with 0B allocations
        // (StringPass16B: 5.891 ns vs 28.784 ns; StringPass1KB: 18.644 ns vs 53.149 ns).
        if (HasStringInputSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "single-pass stackalloc UTF-8 encoding avoids allocation (2x-5x faster)");
        }

        // 8. Infallible pure scalars:
        // Direct C-ABI call with [SuppressGCTransition] (AddInt64: 0.743 ns vs UniFFI 2.756 ns).
        if (!isFallible && IsPureScalarSignature(uniffiFn, boltFn))
        {
            return (InteropEngine.BoltFFI, "direct P/Invoke with [SuppressGCTransition] avoids status channel (~73% lower latency)");
        }

        // 9. Other signature shapes use BoltFFI.
        return (InteropEngine.BoltFFI, "direct wire ABI");
    }

    private static bool IsPureScalarSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null)
        {
            bool inputsScalar = uniffiFn.Inputs.All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));
            bool returnScalar = uniffiFn.ReturnType == null || IsScalarType(uniffiFn.ReturnType, boltFn?.ReturnType);
            return inputsScalar && returnScalar;
        }
        if (boltFn != null)
        {
            bool inputsScalar = boltFn.Parameters.All(p => IsScalarPrimitive(p.Type));
            bool returnScalar = boltFn.ReturnType.Kind == BoltFfiTypeKind.Void || IsScalarPrimitive(boltFn.ReturnType);
            return inputsScalar && returnScalar;
        }
        return false;
    }

    private static bool IsSingleStringReturnSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        bool returnsStringOrOptionalString = false;
        if (uniffiFn?.ReturnType != null)
        {
            returnsStringOrOptionalString = uniffiFn.ReturnType.Kind == TypeKind.String ||
                (uniffiFn.ReturnType.Kind == TypeKind.Option && uniffiFn.ReturnType.InnerType?.Kind == TypeKind.String);
        }
        else if (boltFn != null)
        {
            returnsStringOrOptionalString = boltFn.ReturnType.Kind == BoltFfiTypeKind.String ||
                (boltFn.ReturnType.Kind == BoltFfiTypeKind.Option && boltFn.ReturnType.InnerType?.Kind == BoltFfiTypeKind.String);
        }

        if (!returnsStringOrOptionalString) return false;

        int stringInputsCount = 0;
        bool otherInputsScalar = true;

        if (uniffiFn != null)
        {
            stringInputsCount = uniffiFn.Inputs.Count(p => p.Type.Kind == TypeKind.String);
            otherInputsScalar = uniffiFn.Inputs
                .Where(p => p.Type.Kind != TypeKind.String)
                .All(p => IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name)));
        }
        else if (boltFn != null)
        {
            stringInputsCount = boltFn.Parameters.Count(p => p.Type.Kind == BoltFfiTypeKind.String);
            otherInputsScalar = boltFn.Parameters
                .Where(p => p.Type.Kind != BoltFfiTypeKind.String)
                .All(p => IsScalarPrimitive(p.Type));
        }

        return stringInputsCount <= 1 && otherInputsScalar;
    }

    private static bool ContainsEncodedRecordReturn(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (boltFn != null)
        {
            return ContainsEncodedRecord(boltFn.ReturnType);
        }
        if (uniffiFn?.ReturnType != null)
        {
            return ContainsUniFFICollectionOfRecords(uniffiFn.ReturnType);
        }
        return false;
    }

    private static bool ContainsUniFFICollectionOfRecords(UniFFIType type)
    {
        return type.Kind switch
        {
            TypeKind.Sequence or TypeKind.Set =>
                type.InnerType != null && (type.InnerType.Kind == TypeKind.Record || ContainsUniFFICollectionOfRecords(type.InnerType)),
            TypeKind.Map =>
                (type.KeyType != null && (type.KeyType.Kind == TypeKind.Record || ContainsUniFFICollectionOfRecords(type.KeyType))) ||
                (type.ValueType != null && (type.ValueType.Kind == TypeKind.Record || ContainsUniFFICollectionOfRecords(type.ValueType))),
            TypeKind.Option =>
                type.InnerType != null && ContainsUniFFICollectionOfRecords(type.InnerType),
            _ => false
        };
    }

    private static bool HasRecordSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null)
        {
            if (uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Record) ||
                (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Record))
                return true;
        }
        if (boltFn != null)
        {
            if (boltFn.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Record) ||
                boltFn.ReturnType.Kind == BoltFfiTypeKind.Record)
                return true;
        }
        return false;
    }

    private static bool HasClassSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null)
        {
            if (uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Interface) ||
                (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Interface))
                return true;
        }
        if (boltFn != null)
        {
            if (boltFn.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Class) ||
                boltFn.ReturnType.Kind == BoltFfiTypeKind.Class)
                return true;
        }
        return false;
    }

    private static bool HasBytesSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null)
        {
            if (uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.Bytes) ||
                (uniffiFn.ReturnType != null && uniffiFn.ReturnType.Kind == TypeKind.Bytes))
                return true;
        }
        if (boltFn != null)
        {
            if (boltFn.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.Bytes) ||
                boltFn.ReturnType.Kind == BoltFfiTypeKind.Bytes)
                return true;
        }
        return false;
    }

    private static bool HasCollectionOrDataEnumSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null)
        {
            bool inputs = uniffiFn.Inputs.Any(p => p.Type.Kind is TypeKind.Sequence or TypeKind.Map or TypeKind.Set ||
                (p.Type.Kind == TypeKind.Enum && !IsScalarType(p.Type, GetBoltParamType(boltFn, p.Name))));
            bool ret = uniffiFn.ReturnType != null && (uniffiFn.ReturnType.Kind is TypeKind.Sequence or TypeKind.Map or TypeKind.Set ||
                (uniffiFn.ReturnType.Kind == TypeKind.Enum && !IsScalarType(uniffiFn.ReturnType, boltFn?.ReturnType)));
            if (inputs || ret) return true;
        }
        if (boltFn != null)
        {
            bool inputs = boltFn.Parameters.Any(p => p.Type.Kind is BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Map or BoltFfiTypeKind.Set ||
                (p.Type.Kind == BoltFfiTypeKind.Enum && !p.Type.IsCStyleEnum));
            bool ret = boltFn.ReturnType.Kind is BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Map or BoltFfiTypeKind.Set ||
                (boltFn.ReturnType.Kind == BoltFfiTypeKind.Enum && !boltFn.ReturnType.IsCStyleEnum);
            if (inputs || ret) return true;
        }
        return false;
    }

    private static bool HasStringInputSignature(FnMetadata? uniffiFn, BoltFfiFunctionDecl? boltFn)
    {
        if (uniffiFn != null && uniffiFn.Inputs.Any(p => p.Type.Kind == TypeKind.String))
            return true;
        if (boltFn != null && boltFn.Parameters.Any(p => p.Type.Kind == BoltFfiTypeKind.String))
            return true;
        return false;
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

    /// <summary>
    /// Checks whether a UniFFI function signature can hypothetically be bound by BoltFFI.
    /// </summary>
    public static bool CanHypotheticallyBindBoltFfi(FnMetadata fn)
    {
        if (fn.Inputs.Any(p => !IsTypeSupportedByBoltFfi(p.Type))) return false;
        if (!IsTypeSupportedByBoltFfi(fn.ReturnType, allowVoid: true)) return false;
        return true;
    }

    private static bool IsTypeSupportedByBoltFfi(UniFFIType? type, bool allowVoid = false)
    {
        if (type == null) return allowVoid;
        return type.Kind switch
        {
            TypeKind.Boolean or
            TypeKind.Int8 or TypeKind.UInt8 or
            TypeKind.Int16 or TypeKind.UInt16 or
            TypeKind.Int32 or TypeKind.UInt32 or
            TypeKind.Int64 or TypeKind.UInt64 or
            TypeKind.Float32 or TypeKind.Float64 or
            TypeKind.String or TypeKind.Bytes or
            TypeKind.Duration or TypeKind.Timestamp or
            TypeKind.Record or TypeKind.Enum or
            TypeKind.Interface or TypeKind.CallbackInterface => true,

            TypeKind.Option or TypeKind.Sequence =>
                type.InnerType != null && IsTypeSupportedByBoltFfi(type.InnerType),

            TypeKind.Map =>
                type.KeyType != null && type.ValueType != null &&
                IsTypeSupportedByBoltFfi(type.KeyType) && IsTypeSupportedByBoltFfi(type.ValueType),

            _ => false
        };
    }

    /// <summary>
    /// Checks whether a BoltFFI function signature can hypothetically be bound by UniFFI.
    /// </summary>
    public static bool CanHypotheticallyBindUniFfi(BoltFfiFunctionDecl fn)
    {
        if (!fn.Parameters.All(p => IsTypeSupportedByUniFfi(p.Type))) return false;
        if (!IsTypeSupportedByUniFfi(fn.ReturnType, allowVoid: true)) return false;
        return true;
    }

    private static bool IsTypeSupportedByUniFfi(BoltFfiType? type, bool allowVoid = false)
    {
        if (type == null) return allowVoid;
        if (type.Kind == BoltFfiTypeKind.Void) return allowVoid;
        return type.Kind switch
        {
            BoltFfiTypeKind.Bool or BoltFfiTypeKind.Int8 or BoltFfiTypeKind.UInt8 or
            BoltFfiTypeKind.Int16 or BoltFfiTypeKind.UInt16 or BoltFfiTypeKind.Int32 or
            BoltFfiTypeKind.UInt32 or BoltFfiTypeKind.Int64 or BoltFfiTypeKind.UInt64 or
            BoltFfiTypeKind.Float32 or BoltFfiTypeKind.Float64 or
            BoltFfiTypeKind.String or BoltFfiTypeKind.Bytes or
            BoltFfiTypeKind.Duration or BoltFfiTypeKind.DateTime or
            BoltFfiTypeKind.Guid or BoltFfiTypeKind.Uri or
            BoltFfiTypeKind.Record or BoltFfiTypeKind.Enum or
            BoltFfiTypeKind.Class or BoltFfiTypeKind.Callback => true,

            BoltFfiTypeKind.Option or BoltFfiTypeKind.Sequence or BoltFfiTypeKind.Set =>
                type.InnerType != null && IsTypeSupportedByUniFfi(type.InnerType),

            BoltFfiTypeKind.Map =>
                type.KeyType != null && type.ValueType != null &&
                IsTypeSupportedByUniFfi(type.KeyType) && IsTypeSupportedByUniFfi(type.ValueType),

            _ => false
        };
    }
}
