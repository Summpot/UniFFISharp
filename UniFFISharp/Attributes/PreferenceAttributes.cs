using System;

namespace UniFFISharp;

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
