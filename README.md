# UniFFISharp

**UniFFISharp** is a Roslyn-native, zero-CLI UniFFI binding generator and runtime for .NET and Rust.

## Features

- **Zero-CLI Workflow**: Driven directly by MSBuild and Roslyn Incremental Source Generators. No manual code generation scripts or CLI bindgen tools needed.
- **Auto Cargo Integration**: Automatically detects and builds Cargo projects (`cargo build` / `cargo rustc`), extracts UniFFI metadata directly from PE (`.dll`/`.lib`), ELF (`.so`/`.a`), or Mach-O (`.dylib`/`.a`) binaries using `AsmResolver`.
- **First-class NativeAOT & Static Linking**: Seamless static linking into NativeAOT applications via `DirectPInvoke` with automatic Windows/Linux system library resolution.
- **Async & Task Support**: Idiomatic two-way `Task` / `Task<T>` asynchronous Rust FFI.
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

## License

MIT OR Apache-2.0
