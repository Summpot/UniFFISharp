using System.Runtime.InteropServices;

namespace DiplomatBench.Diplomat;

// Shared native-library name for every `[DllImport]` in the raw layer.
// Emitted once per generation run so the iOS framework-bundle path and the
// bare name aren't duplicated into every `Raw*` struct.
internal static class DiplomatNativeLib
{
#if __IOS__
    internal const string Name = "libdiplomat_bench.framework/libdiplomat_bench";
#else
    internal const string Name = "diplomat_bench";
#endif
}