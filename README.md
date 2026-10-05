# UniFFISharp

**UniFFISharp** is a Roslyn-native, zero-CLI UniFFI binding generator and runtime for .NET and Rust.

## Features

- **Zero-CLI Workflow**: Driven directly by MSBuild and Roslyn Incremental Source Generators. No manual code generation scripts or CLI bindgen tools needed.
- **Auto Cargo Integration**: Automatically detects and builds Cargo projects (`cargo build` / `cargo rustc`), extracts UniFFI metadata directly from PE (`.dll`/`.lib`), ELF (`.so`/`.a`), or Mach-O (`.dylib`/`.a`) binaries using `AsmResolver`.
- **First-class NativeAOT & Static Linking**: Seamless static linking into NativeAOT applications via `DirectPInvoke` with automatic Windows/Linux system library resolution.
- **Async & Task Support**: Idiomatic two-way `Task` / `Task<T>` asynchronous Rust FFI.
- **Async Streams (`IAsyncEnumerable<T>`)**: Automatic `IAsyncEnumerable<T>` generation for Rust async iterator / stream objects via method signature heuristics.
- **Two-way Callback Interfaces**: Pass C# delegates / interface implementations to Rust.
- **Cross-Compilation Ready**: Built-in integration with Zig toolsets for easy cross-platform compilation.

## Quickstart

### 1. Rust crate (`Cargo.toml`)

```toml
[package]
name = "my_rust_lib"
version = "0.1.0"
edition = "2024"

[lib]
crate-type = ["cdylib", "staticlib"]

[dependencies]
uniffi = { version = "0.32", features = ["tokio"] }
```

In `src/lib.rs`:

```rust
uniffi::setup_scaffolding!();

#[uniffi::export]
pub fn add(a: u64, b: u64) -> u64 {
    a + b
}
```

### 2. C# Project (`MyProject.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>

    <!-- Point to Cargo.toml (or omit if in project/parent directory) -->
    <UniFFICargoProject>..\my_rust_lib\Cargo.toml</UniFFICargoProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="UniFFISharp" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### 3. Build & Run

```bash
dotnet run
```

```csharp
using MyRustLib;

ulong sum = MyRustLibMethods.Add(40, 2);
Console.WriteLine($"Result: {sum}"); // Result: 42
```

## Multi-Crate Support (`uniffi_reexport_scaffolding!`)

UniFFISharp provides out-of-the-box support for Rust projects aggregating multiple component crates via `uniffi_reexport_scaffolding!()`.

- **Multi-File Generation**: Emits an isolated `UniFFIBindings.{crateNorm}.g.cs` for each component crate.
- **Isolated P/Invokes**: Each crate gets its own internal library handler and entry points matching its crate prefix (`uniffi_{sub_crate}_...`).
- **Cross-Crate Types**: Automatically resolves types and converters across crate boundaries with appropriate namespace prefixes.

### Configuring Namespaces

Use the `<UniFFINamespace>` MSBuild property to configure root and sub-crate namespaces in a single property separated by commas `,`, semicolons `;`, or pipes `|`:

```xml
<PropertyGroup>
  <!-- 
    Format: [RootNamespace], [crate1=SubNamespace], [crate2=global::AbsoluteNamespace]
    - Sub-crate namespaces without 'global::' are relative to RootNamespace.
    - Sub-crates using 'global::' escape to an absolute namespace.
    - Unconfigured sub-crates default to {RootNamespace}.{SubCratePascalCase}.
    - Delimiters: Comma ',', semicolon ';', or pipe '|' are supported.
      (UniFFISharp automatically URL-encodes MSBuild properties to prevent Roslyn 
       EditorConfig from truncating semicolons or hashes).
    - Advanced: 'base64:...' and 'hex:...' prefixes are also supported.
  -->
  <UniFFINamespace>MyCompany.Sdk, sub_alpha=Security, sub_legacy=global::LegacyVendor</UniFFINamespace>
</PropertyGroup>
```

In the example above:
- Umbrella crate maps to `MyCompany.Sdk`
- `sub_alpha` maps to `MyCompany.Sdk.Security`
- `sub_legacy` maps to `LegacyVendor`
- Unconfigured sub-crates (e.g. `sub_beta`) map to `MyCompany.Sdk.SubBeta`

## License

MIT OR Apache-2.0
