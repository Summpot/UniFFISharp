<#
.SYNOPSIS
    UniFFISharp vs BoltFFI vs Diplomat vs CsBindgen vs Interoptopus .NET Interop Benchmark Build & Run Script
.DESCRIPTION
    Automates Rust workspace compilation, Diplomat & Interoptopus C# bindings generation, and .NET BenchmarkDotNet execution.
#>

[CmdletBinding()]
param(
    [switch]$Verify,
    [string]$Filter = "",
    [switch]$Short,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Set-Location $ScriptDir

if ($Clean) {
    Write-Host "[1/5] Cleaning previous build artifacts..." -ForegroundColor Cyan
    cargo clean
    dotnet clean -c Release
}

Write-Host "[1/4] Building Rust Workspace (crates/*)..." -ForegroundColor Cyan
cargo build --release

Write-Host "[2/4] Generating Interoptopus C# bindings..." -ForegroundColor Cyan
cargo run --release --bin generate_interoptopus

Write-Host "[3/4] Generating Diplomat C# bindings..." -ForegroundColor Cyan
$DiplomatOut = "UniFFISharp.Benchmark/Generated/Diplomat"
if (-not (Get-Command "diplomat-tool" -ErrorAction SilentlyContinue)) {
    Write-Error "diplomat-tool CLI not found in PATH. Please run 'cargo install diplomat-tool'."
}

diplomat-tool dotnet $DiplomatOut -e crates/diplomat_bench/src/lib.rs

# Ensure BenchCounter.Manual.cs exists for deterministic IDisposable
$ManualDisposable = Join-Path $DiplomatOut "BenchCounter.Manual.cs"
if (-not (Test-Path $ManualDisposable)) {
    @"
using System;

namespace DiplomatBench;

public partial class BenchCounter : IDisposable
{
    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
"@ | Out-File -Encoding utf8 -FilePath $ManualDisposable
}

Write-Host "[4/4] Building .NET Benchmark Project..." -ForegroundColor Cyan
dotnet build UniFFISharp.Benchmark/UniFFISharp.Benchmark.csproj -c Release

if ($Verify) {
    Write-Host "`n=== Running Functional Verification Tests ===" -ForegroundColor Green
    dotnet run -c Release --project UniFFISharp.Benchmark --no-build -- --verify
    exit 0
}

$BenchArgs = @()
if ($Filter -ne "") {
    $BenchArgs += "--filter"
    $BenchArgs += $Filter
}
if ($Short) {
    $BenchArgs += "--job"
    $BenchArgs += "short"
}

if ($BenchArgs.Count -gt 0) {
    Write-Host "`n=== Launching BenchmarkDotNet with args: $BenchArgs ===" -ForegroundColor Green
    dotnet run -c Release --project UniFFISharp.Benchmark --no-build -- $BenchArgs
} else {
    Write-Host "`nBuild complete. To run benchmarks:" -ForegroundColor Green
    Write-Host "  pwsh ./build.ps1 -Verify                (Run functional verification for all 5 frameworks)"
    Write-Host "  pwsh ./build.ps1 -Short -Filter *NoOp*  (Quick run NoOp benchmark)"
    Write-Host "  pwsh ./build.ps1 -Short -Filter *Async* (Run async benchmarks)"
    Write-Host "  pwsh ./build.ps1 -Filter *              (Run all benchmarks)"
}
