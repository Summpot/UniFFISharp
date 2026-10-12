<#
.SYNOPSIS
    UniFFISharp vs Diplomat .NET Interop Benchmark Build & Run Script
.DESCRIPTION
    Automates Rust compilation, Diplomat C# bindings generation, and .NET BenchmarkDotNet execution.
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
    Write-Host "[1/4] Cleaning previous build artifacts..." -ForegroundColor Cyan
    cargo clean
    dotnet clean -c Release
}

Write-Host "[1/3] Building Rust Workspace (crates/uniffi_bench & crates/diplomat_bench)..." -ForegroundColor Cyan
cargo build --release

Write-Host "[2/3] Generating Diplomat C# bindings..." -ForegroundColor Cyan
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

Write-Host "[3/3] Building .NET Benchmark Project..." -ForegroundColor Cyan
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
    Write-Host "  pwsh ./build.ps1 -Verify                (Run functional verification)"
    Write-Host "  pwsh ./build.ps1 -Short -Filter *Prim*  (Quick run primitive benchmarks)"
    Write-Host "  pwsh ./build.ps1 -Filter *String*       (Run string benchmarks)"
    Write-Host "  pwsh ./build.ps1 -Filter *              (Run all benchmarks)"
}
