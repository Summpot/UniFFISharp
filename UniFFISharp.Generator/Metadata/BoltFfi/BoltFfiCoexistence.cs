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
                var function = decl.Function;
                // Objects have engine-specific handles. Unsupported signatures must retain
                // their UniFFI implementation and its existing managed types.
                if (function == null || !CanBind(function) || !symbols.Add(function.Symbol)) continue;
                string crate = crates.FirstOrDefault(n => function.Symbol.StartsWith(
                    "boltffi_function_" + n + "_", StringComparison.Ordinal)) ?? envelope.Package.PackageName;
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
        if (function.IsAsync || function.HasError || string.IsNullOrEmpty(function.Symbol)) return false;
        if (!function.Parameters.All(p => IsValue(p.Type))) return false;
        if (IsValue(function.ReturnType)) return true;
        if (function.ReturnType.Kind != BoltFfiTypeKind.Void) return false;
        var plan = function.RawNode?["callable"]?["returns"]?["plan"];
        return plan?.AsString() == "Void" || plan?["Void"] != null;
    }

    private static bool IsValue(BoltFfiType type) => type.Kind switch
    {
        BoltFfiTypeKind.Bool or BoltFfiTypeKind.Int8 or BoltFfiTypeKind.UInt8 or
        BoltFfiTypeKind.Int16 or BoltFfiTypeKind.UInt16 or BoltFfiTypeKind.Int32 or
        BoltFfiTypeKind.UInt32 or BoltFfiTypeKind.Int64 or BoltFfiTypeKind.UInt64 or
        BoltFfiTypeKind.Float32 or BoltFfiTypeKind.Float64 or
        BoltFfiTypeKind.String or BoltFfiTypeKind.Bytes => true,
        _ => false
    };
}
