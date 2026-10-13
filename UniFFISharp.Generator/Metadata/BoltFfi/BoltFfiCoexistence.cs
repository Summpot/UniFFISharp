using System;
using System.Collections.Generic;
using System.Linq;

namespace UniFFISharp.Generator.Metadata.BoltFfi;

/// <summary>Routes aggregate BoltFFI exports to their UniFFI component namespaces.</summary>
internal static class BoltFfiCoexistence
{
    internal static IReadOnlyList<BoltFfiMetadataEnvelope> Prepare(
        IReadOnlyList<BoltFfiMetadataEnvelope> envelopes,
        IEnumerable<string> crateNames)
    {
        var crates = crateNames.Select(n => n.Replace('-', '_')).OrderByDescending(n => n.Length).ToArray();
        var groups = new Dictionary<string, BoltFfiMetadataEnvelope>(StringComparer.OrdinalIgnoreCase);
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        foreach (var envelope in envelopes)
        {
            if (envelope.Bindings.Native == null) continue;
            foreach (var decl in envelope.Bindings.Native.Decls)
            {
                string? symbol = null;
                if (decl.Function != null)
                {
                    if (!CanBind(decl.Function)) continue;
                    symbol = decl.Function.Symbol;
                }
                else if (decl.Callback != null)
                {
                    symbol = decl.Callback.RegisterSymbol ?? decl.Callback.Name.ToSnakeCase();
                }
                else if (decl.Stream != null)
                {
                    symbol = decl.Stream.SubscribeSymbol ?? decl.Stream.Name.ToSnakeCase();
                }
                else if (decl.Constant != null)
                {
                    symbol = decl.Constant.Name.ToSnakeCase();
                }
                else if (decl.Record != null)
                {
                    symbol = decl.Record.Name.ToSnakeCase();
                }
                else if (decl.Enum != null)
                {
                    symbol = decl.Enum.Name.ToSnakeCase();
                }
                else if (decl.Class != null)
                {
                    symbol = decl.Class.Name.ToSnakeCase();
                }

                if (symbol != null && !symbols.Add(symbol)) continue;

                string crate = (symbol != null ? crates.FirstOrDefault(n =>
                    symbol.StartsWith("boltffi_function_" + n + "_", StringComparison.Ordinal) ||
                    symbol.StartsWith("boltffi_callback_" + n + "_", StringComparison.Ordinal) ||
                    symbol.StartsWith("boltffi_stream_" + n + "_", StringComparison.Ordinal) ||
                    symbol.StartsWith("boltffi_constant_" + n + "_", StringComparison.Ordinal) ||
                    symbol.StartsWith("boltffi_class_" + n + "_", StringComparison.Ordinal) ||
                    symbol.StartsWith(n + "_", StringComparison.Ordinal)) : null) ?? envelope.Package.PackageName;

                if (!groups.TryGetValue(crate, out var group))
                {
                    var package = new BoltFfiPackageInfo { Version = envelope.Package.Version };
                    package.Name.Parts.Add(crate);
                    group = new BoltFfiMetadataEnvelope
                    {
                        Magic = envelope.Magic,
                        Format = envelope.Format,
                        Surface = envelope.Surface,
                        Package = package,
                        Bindings = new BoltFfiSerializedBindings
                        {
                            Native = new BoltFfiNativeBindings { Package = package }
                        }
                    };
                    groups.Add(crate, group);
                }
                group.Bindings.Native!.Decls.Add(decl);
            }
        }
        return groups.Values.ToArray();
    }

    internal static bool CanBind(BoltFfiFunctionDecl function)
    {
        if (string.IsNullOrEmpty(function.Symbol)) return false;
        if (!function.Parameters.All(p => IsSupported(p.Type))) return false;
        if (!IsSupported(function.ReturnType, allowVoid: true)) return false;
        return true;
    }

    private static bool IsSupported(BoltFfiType? type, bool allowVoid = false)
    {
        if (type == null) return false;
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
                type.InnerType != null && IsSupported(type.InnerType),

            BoltFfiTypeKind.Map =>
                type.KeyType != null && type.ValueType != null &&
                IsSupported(type.KeyType) && IsSupported(type.ValueType),

            _ => false
        };
    }
}
