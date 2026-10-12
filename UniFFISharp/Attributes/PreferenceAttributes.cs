using System;

namespace UniFFISharp;

/// <summary>
/// Specifies the interop engine to use for generating FFI bindings.
/// </summary>
public enum InteropEngine
{
    /// <summary>
    /// Automatically choose the best performing engine based on signature cost model.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Use the UniFFI engine.
    /// </summary>
    UniFFI = 1,

    /// <summary>
    /// Use the BoltFFI engine.
    /// </summary>
    BoltFFI = 2
}

/// <summary>
/// Specifies the preferred interop engine for a method, class, or interface.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class UniFFIPreferenceAttribute : Attribute
{
    public InteropEngine Engine { get; }

    public UniFFIPreferenceAttribute(InteropEngine engine)
    {
        Engine = engine;
    }
}

/// <summary>
/// Instructs the generator to use the UniFFI engine for this partial method or class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class PreferUniFFIAttribute : Attribute
{
}

/// <summary>
/// Instructs the generator to use the BoltFFI engine for this partial method or class.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class PreferBoltFFIAttribute : Attribute
{
}
