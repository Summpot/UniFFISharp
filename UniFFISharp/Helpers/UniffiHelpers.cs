using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using UniFFISharp.Exceptions;
using UniFFISharp.Types;

namespace UniFFISharp.Helpers;

public interface CallStatusErrorHandler<E> where E : Exception
{
    E Lift(RustBuffer errorBuf);
}

public sealed class NullCallStatusErrorHandler : CallStatusErrorHandler<UniffiException>
{
    public static readonly NullCallStatusErrorHandler INSTANCE = new();

    public UniffiException Lift(RustBuffer errorBuf)
    {
        return new UndeclaredErrorException("Library returned an error not declared in the UniFFI interface.");
    }
}

public static class UniffiHelpers
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCallStatus<E>(ref UniffiRustCallStatus status, CallStatusErrorHandler<E> errorHandler)
        where E : Exception
    {
        if (status.IsError())
        {
            throw errorHandler.Lift(status.error_buf);
        }

        if (status.IsPanic())
        {
            string msg = "Rust panic";
            if (status.error_buf.len > 0 && status.error_buf.data != IntPtr.Zero)
            {
                try
                {
                    unsafe
                    {
                        var stream = status.error_buf.AsStream();
                        msg = stream.ReadString();
                    }
                }
                catch (Exception ex)
                {
                    msg = $"Rust panic (failed to decode panic message: {ex.Message})";
                }
            }
            throw new PanicException(msg);
        }

        throw new InternalException($"Unknown rust call status: {status.code}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowCallStatus(ref UniffiRustCallStatus status)
    {
        ThrowCallStatus(ref status, NullCallStatusErrorHandler.INSTANCE);
    }
}

public static class UniffiLibraryResolver
{
    private readonly record struct LibraryEntry(string DllName, string ContractSymbol);

    private sealed class AssemblyResolverState
    {
        public readonly ConcurrentDictionary<string, LibraryEntry> Libraries = new(StringComparer.Ordinal);
        public int Registered;

        public void AddLibrary(string dllName, string contractSymbol)
        {
            var entry = new LibraryEntry(dllName, contractSymbol);
            Libraries[dllName] = entry;
            if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                Libraries.TryAdd(dllName + ".dll", entry);
            }
        }

        public IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (Libraries.TryGetValue(libraryName, out LibraryEntry entry))
            {
                return LoadEntry(entry, assembly, searchPath);
            }
            if (libraryName == "__Internal")
            {
                foreach (var kvp in Libraries)
                {
                    IntPtr handle = LoadEntry(kvp.Value, assembly, searchPath);
                    if (handle != IntPtr.Zero) return handle;
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr LoadEntry(LibraryEntry entry, Assembly assembly, DllImportSearchPath? searchPath)
        {
            IntPtr mainHandle = NativeLibrary.GetMainProgramHandle();
            if (NativeLibrary.TryGetExport(mainHandle, entry.ContractSymbol, out _))
            {
                return mainHandle;
            }
            if (NativeLibrary.TryLoad(entry.DllName, assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }
            if (!entry.DllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                NativeLibrary.TryLoad(entry.DllName + ".dll", assembly, searchPath, out IntPtr handleDll))
            {
                return handleDll;
            }
            return IntPtr.Zero;
        }
    }

    private static readonly ConditionalWeakTable<Assembly, AssemblyResolverState> s_states = new();

    public static void Register(Assembly assembly, string dllName, string contractSymbol)
    {
        var state = s_states.GetOrCreateValue(assembly);
        state.AddLibrary(dllName, contractSymbol);
        if (Interlocked.CompareExchange(ref state.Registered, 1, 0) == 0)
        {
            try
            {
                NativeLibrary.SetDllImportResolver(assembly, state.Resolve);
            }
            catch (InvalidOperationException)
            {
                // Resolver already registered for this assembly
            }
        }
    }
}
