# .NET Interop Benchmarks

BenchmarkDotNet comparisons of UniFFISharp (UniFFI), UniFFISharp (BoltFFI), Diplomat, CsBindgen, and Interoptopus. The suite covers scalars, strings, buffers, records, objects, errors, callbacks, concurrency, async calls, and streams. Framework coverage varies by scenario.

## Auto engine selection

[InteropArbitrator](../UniFFISharp.Generator/Arbitration/InteropArbitrator.cs) chooses one engine per function signature when both engines export the function. The table pairs equivalent operations from the specialized benchmarks; **bold timing** marks the engine Auto selects. Percentages compare the paired mean times directly.

| Operation | UniFFISharp (UniFFI) | UniFFISharp (BoltFFI) | Auto selects | Why |
| --- | ---: | ---: | --- | --- |
| Scalar addition | 2.7556 ns | **0.7430 ns** | **BoltFFI** | 73.0% lower mean. Direct scalar ABI avoids the UniFFI status channel. |
| Fallible scalar: success | **2.801 ns** | 5.350 ns | **UniFFI** | 47.6% lower mean. Direct scalar return makes the success path cheaper. |
| Fallible scalar: failure | **1,122.956 ns** | 922.154 ns | **UniFFI** | BoltFFI has the lower mean on failure, but Auto chooses once for the signature and favors its success path. |
| String input (16B ASCII) | 28.784 ns | **5.891 ns** | **BoltFFI** | 79.5% lower mean. Input encoding is cheaper; both engines allocate 0B. |
| String input (32KB ASCII) | 867.488 ns | **423.530 ns** | **BoltFFI** | 51.2% lower mean. The input advantage also holds for this larger payload. |
| String return | **68.975 ns** | 91.196 ns | **UniFFI** | 24.4% lower mean. The simple string-return path has less decoding overhead. |
| String roundtrip | **60.722 ns** | 82.930 ns | **UniFFI** | 26.8% lower mean. With one string input, the return-path saving outweighs input encoding. |
| Direct record roundtrip (Point3D) | 123.0942 ns | **0.9600 ns** | **BoltFFI** | 99.2% lower mean. Direct C ABI avoids record serialization. |
| Encoded record input (UserInfo) | 32.6794 ns | **5.7492 ns** | **BoltFFI** | 82.4% lower mean. This small encoded record is cheaper to pass; both engines allocate 0B. |
| Complex record input (50 entities) | 16.46 μs | **16.48 μs** | **BoltFFI** | Practical tie: the 0.02 us gap is smaller than either reported error. Use the default engine. |
| Complex record return (50 entities) | **18.17 μs** | 19.15 μs | **UniFFI** | 5.1% lower mean. Return decoding has the lower mean; both allocate 25,656B. |
| Byte input (4KB) | 1,668.429 ns | **454.145 ns** | **BoltFFI** | 72.8% lower mean. The byte-input path is cheaper; both engines allocate 0B. |
| Managed byte[] return (4KB) | 1,692.989 ns | **241.134 ns** | **BoltFFI** | 85.8% lower mean. Lower mean with the same managed result and 4,120B allocation. |
| Large buffer roundtrip (1MB) | 1,568.729 μs | **270.856 μs** | **BoltFFI** | 82.7% lower mean. Lower copying/serialization cost dominates this payload. |
| Tagged-union AST input | 289.92 ns | **221.36 ns** | **BoltFFI** | 23.6% lower mean. The wire input/evaluation path has the lower mean. |
| Callback input (interface fallback) | 49.346 ns | **32.429 ns** | **BoltFFI** | 34.3% lower mean. Even the interface fallback beats the UniFFI callback path; the default choice is BoltFFI. |

**Reading the exceptions:** a fallible scalar call keeps UniFFI for both success and failure; Auto cannot switch after the outcome is known. Complex inputs are effectively tied, so BoltFFI is the default rather than a measured winner. The complex-return advantage is modest and has the same allocation cost.

String-return preference applies to `String` or `Option<String>` with at most one direct string input and scalar remaining inputs. Encoded-record return preference applies recursively through `Option`, `Sequence`, `Set`, and `Map`; direct records are excluded. Wrapped types and mixed signatures follow these rules without separate measurements for every combination. Other shapes use BoltFFI. Explicit preferences override Auto when both exports exist; a single export always uses its available engine.

Object benchmarks support the BoltFFI handle preference: method calls take 4.239 ns vs 48.882 ns; create/destroy takes 49.629 ns vs 81.499 ns and allocates 24B vs 40B. These engine-owned object operations are supporting evidence, not independently routed free functions. Async and stream results have no paired BoltFFI measurement, so they provide no direct engine speed comparison.

## Running

Requires the .NET 10 SDK, Rust toolchain, and PowerShell. Install `diplomat-tool` to regenerate Diplomat bindings; the build script can use existing bindings when the tool is unavailable.

Run from the repository root:

```powershell
# Build native libraries and bindings, then run functional verification.
pwsh ./benchmarks/build.ps1 -Verify

# Quick comparison of the unified scenarios.
pwsh ./benchmarks/build.ps1 -Short -Filter '*Unified*'

# Run the full suite with the default benchmark job.
pwsh ./benchmarks/build.ps1 -Filter '*'
```

Use a narrower filter such as `'*ComplexEntity*'` for individual benchmark classes. Reports are written to `benchmarks/BenchmarkDotNet.Artifacts/results/`.

## Results

### Environment

| Setting | Value |
| --- | --- |
| OS | Windows 11 x64 (10.0.26300.9711) |
| Runtime | .NET 10.0.13 (10.0.1326.50504), X64 RyuJIT, AVX-512 |
| SDK | 11.0.100-rc.1.26425.128 |
| BenchmarkDotNet | 0.14.0, DefaultJob |
| GC | Concurrent Workstation |
| Executions | 224 total; 217 with measurements, 7 without measurements |

Specialized and unified benchmarks are separate measurements and should not be averaged together.

`Mean` is time per operation; `Error` is half the 99.9% confidence interval; `StdDev` is the standard deviation. `Allocated` is managed memory per operation; `Gen0`/`Gen1`/`Gen2` count collections per 1,000 operations. Ratios use each table's BenchmarkDotNet baseline, which may cover multiple operations in specialized tables. Compare matching operations directly. `NA` or `?` in ratio columns indicates an unavailable comparison, not a missing timing measurement.

Borrowed buffers, owned zero-copy buffers, and managed arrays have different ownership and allocation costs. Async adapters and stream baselines also differ in scheduling and execution model; their timings are not interchangeable engine comparisons.

### Measurement notes

Seven executions produced no timing measurements: CsBindgen's `QueryComplexEntities50` and Interoptopus's six unified string scenarios (`StringPass16B`, `StringPass1KB`, `StringPass32KB`, `StringPassUnicode`, `StringReturn`, and `StringRoundtrip`). These rows are excluded from the numeric tables.

BenchmarkDotNet reported multimodal distributions for Interoptopus callbacks, UniFFISharp (UniFFI) large buffers, UniFFISharp (BoltFFI) Unicode string input, and Diplomat's unified managed byte-array return.

<details>
<summary>AsyncMethodBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `CsBindgen (Equiv TaskCompletionSource)` | 01_SingleAsyncImmediate | 42.94 ns | 0.834 ns | 0.696 ns | 0.19 | 0.00 | 0.0100 | 168 B | 0.44 |
| `UniFFISharp (UniFFI): Rust Task Poll` | 01_SingleAsyncImmediate | 224.60 ns | 2.208 ns | 1.724 ns | 1.00 | 0.01 | 0.0229 | 384 B | 1.00 |
| `Interoptopus (Tokio Native Task)` | 01_SingleAsyncImmediate | 2,012.51 ns | 39.916 ns | 115.803 ns | 8.96 | 0.52 | 0.0267 | 457 B | 1.19 |
| `CsBindgen (Equiv TaskCompletionSource)` | 02_ConcurrentFanout20 | 972.10 ns | 14.361 ns | 11.992 ns | 0.22 | 0.00 | 0.1564 | 2632 B | 0.39 |
| `UniFFISharp (UniFFI): Rust Task Poll` | 02_ConcurrentFanout20 | 4,499.70 ns | 52.353 ns | 46.409 ns | 1.00 | 0.01 | 0.4044 | 6808 B | 1.00 |
| `Interoptopus (Tokio Native Task)` | 02_ConcurrentFanout20 | 13,035.03 ns | 256.248 ns | 638.146 ns | 2.90 | 0.14 | 0.4578 | 7771 B | 1.14 |

</details>

<details>
<summary>AsyncStreamBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Channel-based Push Stream (Semantic Equiv)` | 5.613 μs | 0.0854 μs | 0.0713 μs | 0.19 | 0.00 | 0.1221 | 2123 B | 0.06 |
| `Pure C# Async Generator (Baseline)` | 21.235 μs | 0.3850 μs | 0.3413 μs | 0.73 | 0.01 | - | 398 B | 0.01 |
| `UniFFISharp (UniFFI): Inferred IAsyncEnumerable` | 29.176 μs | 0.4199 μs | 0.3507 μs | 1.00 | 0.02 | 2.0752 | 34832 B | 1.00 |

</details>

<details>
<summary>CallbackBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `CsBindgen (Raw FnPtr Static)` | 01_SimpleCallback | 4.414 ns | 0.0410 ns | 0.0342 ns | 4.412 ns | 0.09 | 0.00 | - | - | NA |
| `CsBindgen (Instance Context Cached)` | 01_SimpleCallback | 4.823 ns | 0.0618 ns | 0.0516 ns | 4.798 ns | 0.10 | 0.00 | - | - | NA |
| `CsBindgen (Instance Context PerCall Alloc)` | 01_SimpleCallback | 29.316 ns | 0.1597 ns | 0.1416 ns | 29.319 ns | 0.59 | 0.01 | - | - | NA |
| `UniFFISharp (BoltFFI): Guarded Devirtualized` | 01_SimpleCallback | 31.629 ns | 0.2070 ns | 0.1835 ns | 31.569 ns | 0.64 | 0.01 | - | - | NA |
| `UniFFISharp (BoltFFI): Interface Fallback` | 01_SimpleCallback | 32.429 ns | 0.2068 ns | 0.1727 ns | 32.361 ns | 0.66 | 0.01 | - | - | NA |
| `UniFFISharp (UniFFI)` | 01_SimpleCallback | 49.346 ns | 0.4375 ns | 0.3653 ns | 49.210 ns | 1.00 | 0.01 | - | - | NA |
| `Interoptopus (InteropDelegate)` | 01_SimpleCallback | 409.367 ns | 8.3348 ns | 24.3129 ns | 408.178 ns | 8.30 | 0.49 | 0.0076 | 128 B | NA |
| `CsBindgen (Raw FnPtr Static)` | 02_ProgressCallback_100x | 263.829 ns | 2.4659 ns | 2.0591 ns | 263.268 ns | 0.36 | 0.01 | - | - | NA |
| `CsBindgen (Instance Context Cached)` | 02_ProgressCallback_100x | 266.102 ns | 5.1747 ns | 4.5872 ns | 264.279 ns | 0.36 | 0.01 | - | - | NA |
| `CsBindgen (Instance Context PerCall Alloc)` | 02_ProgressCallback_100x | 272.623 ns | 0.7036 ns | 0.5876 ns | 272.502 ns | 0.37 | 0.01 | - | - | NA |
| `UniFFISharp (BoltFFI): Guarded Devirtualized` | 02_ProgressCallback_100x | 329.909 ns | 6.0180 ns | 5.0253 ns | 329.056 ns | 0.45 | 0.01 | - | - | NA |
| `UniFFISharp (BoltFFI): Interface Fallback` | 02_ProgressCallback_100x | 436.348 ns | 8.3696 ns | 20.6875 ns | 426.834 ns | 0.59 | 0.03 | - | - | NA |
| `UniFFISharp (UniFFI)` | 02_ProgressCallback_100x | 734.461 ns | 11.1230 ns | 10.4044 ns | 729.809 ns | 1.00 | 0.02 | - | - | NA |
| `Interoptopus (InteropDelegate)` | 02_ProgressCallback_100x | 984.444 ns | 18.9964 ns | 43.2644 ns | 977.554 ns | 1.34 | 0.06 | 0.0076 | 128 B | NA |

</details>

<details>
<summary>ComplexEntityBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Gen1 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (UniFFI): RustBuffer` | 01_PassComplexEntities50 | 16.46 μs | 0.243 μs | 0.250 μs | 1.00 | 0.02 | - | - | - | NA |
| `CsBindgen (Equiv Json Wire)` | 01_PassComplexEntities50 | 16.46 μs | 0.268 μs | 0.209 μs | 1.00 | 0.02 | 0.8545 | - | 14624 B | NA |
| `UniFFISharp (BoltFFI): Direct Wire` | 01_PassComplexEntities50 | 16.48 μs | 0.230 μs | 0.192 μs | 1.00 | 0.02 | - | - | - | NA |
| `UniFFISharp (UniFFI): RustBuffer` | 02_QueryComplexEntities50 | 18.17 μs | 0.354 μs | 0.364 μs | 1.00 | 0.03 | 1.5259 | 0.1221 | 25656 B | 1.00 |
| `UniFFISharp (BoltFFI): Direct Wire` | 02_QueryComplexEntities50 | 19.15 μs | 0.366 μs | 0.360 μs | 1.05 | 0.03 | 1.5259 | 0.1221 | 25656 B | 1.00 |

</details>

<details>
<summary>ConcurrentContentionBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Ratio | Gen0 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (BoltFFI): Atomic Read` | 01_ConcurrentReads_16Threads | 4.131 μs | 0.0807 μs | 0.0863 μs | 0.02 | 0.2289 | 3.71 KB | 0.81 |
| `Interoptopus (Atomic Read)` | 01_ConcurrentReads_16Threads | 5.160 μs | 0.1005 μs | 0.1504 μs | 0.02 | 0.2365 | 3.88 KB | 0.84 |
| `CsBindgen (Atomic Read)` | 01_ConcurrentReads_16Threads | 5.167 μs | 0.1029 μs | 0.1661 μs | 0.02 | 0.2365 | 3.86 KB | 0.84 |
| `UniFFISharp (UniFFI): Arc<T> Shared Read` | 01_ConcurrentReads_16Threads | 212.035 μs | 1.4346 μs | 1.3419 μs | 1.00 | 0.2441 | 4.6 KB | 1.00 |
| `CsBindgen (Atomic Read/Write)` | 02_ConcurrentReadWrite_16Threads | 32.871 μs | 0.2285 μs | 0.2138 μs | 0.17 | 0.2747 | 4.44 KB | 0.97 |
| `UniFFISharp (BoltFFI): Atomic Read/Write` | 02_ConcurrentReadWrite_16Threads | 40.626 μs | 0.2166 μs | 0.2026 μs | 0.21 | 0.2441 | 4.61 KB | 1.00 |
| `Interoptopus (Atomic Read/Write)` | 02_ConcurrentReadWrite_16Threads | 44.104 μs | 0.4914 μs | 0.4356 μs | 0.23 | 0.2441 | 4.53 KB | 0.99 |
| `UniFFISharp (UniFFI): Arc<T> Shared Read/Write` | 02_ConcurrentReadWrite_16Threads | 189.045 μs | 0.5791 μs | 0.5417 μs | 1.00 | 0.2441 | 4.59 KB | 1.00 |

</details>

<details>
<summary>ErrorBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (UniFFI): Result Success` | 2.801 ns | 0.0436 ns | 0.0364 ns | 1.00 | 0.02 | - | - | NA |
| `UniFFISharp (BoltFFI): Result Success` | 5.350 ns | 0.0539 ns | 0.0450 ns | 1.91 | 0.03 | - | - | NA |
| `Diplomat: Result Success` | 6.277 ns | 0.0395 ns | 0.0308 ns | 2.24 | 0.03 | - | - | NA |
| `Diplomat: Result Failure (Catch)` | 806.767 ns | 10.3484 ns | 8.0793 ns | 288.06 | 4.52 | 0.0172 | 296 B | NA |
| `UniFFISharp (BoltFFI): Result Failure (Catch)` | 922.154 ns | 17.1576 ns | 14.3273 ns | 329.25 | 6.40 | 0.0153 | 264 B | NA |
| `UniFFISharp (UniFFI): Result Failure (Catch)` | 1,122.956 ns | 15.4255 ns | 12.8810 ns | 400.95 | 6.66 | 0.0191 | 320 B | NA |

</details>

<details>
<summary>LargeBufferBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Gen1 | Gen2 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `CsBindgen (In-Place Zero-Copy)` | 01_LargeBuffer100KB | 978.0 ns | 7.42 ns | 6.57 ns | 0.01 | 0.00 | - | - | - | - | 0.000 |
| `Interoptopus (VecByte)` | 01_LargeBuffer100KB | 1,872.7 ns | 36.44 ns | 37.42 ns | 0.02 | 0.00 | 0.0038 | - | - | 80 B | 0.001 |
| `UniFFISharp (BoltFFI)` | 01_LargeBuffer100KB | 7,698.7 ns | 137.69 ns | 201.83 ns | 0.10 | 0.01 | 17.5171 | 17.5171 | 17.5171 | 102433 B | 1.000 |
| `UniFFISharp (UniFFI): RustBuffer` | 01_LargeBuffer100KB | 79,567.6 ns | 2,047.08 ns | 6,035.85 ns | 1.01 | 0.11 | 18.5547 | 18.5547 | 18.5547 | 102430 B | 1.000 |
| `CsBindgen (In-Place Zero-Copy)` | 02_LargeBuffer1MB | 9,647.1 ns | 61.02 ns | 54.10 ns | 0.006 | 0.00 | - | - | - | - | 0.000 |
| `Interoptopus (VecByte)` | 02_LargeBuffer1MB | 169,620.8 ns | 3,291.07 ns | 3,521.40 ns | 0.108 | 0.00 | - | - | - | 80 B | 0.000 |
| `UniFFISharp (BoltFFI)` | 02_LargeBuffer1MB | 270,855.9 ns | 7,351.72 ns | 21,445.31 ns | 0.173 | 0.01 | 119.1406 | 119.1406 | 119.1406 | 1048678 B | 1.000 |
| `UniFFISharp (UniFFI): RustBuffer` | 02_LargeBuffer1MB | 1,568,729.0 ns | 31,284.55 ns | 24,424.92 ns | 1.000 | 0.02 | 158.2031 | 158.2031 | 158.2031 | 1048650 B | 1.000 |
| `CsBindgen (In-Place Zero-Copy)` | 03_LargeBuffer5MB | 51,515.5 ns | 924.79 ns | 772.24 ns | 0.006 | 0.00 | - | - | - | - | 0.000 |
| `Interoptopus (VecByte)` | 03_LargeBuffer5MB | 899,461.0 ns | 14,160.17 ns | 11,824.39 ns | 0.110 | 0.00 | - | - | - | 80 B | 0.000 |
| `UniFFISharp (BoltFFI)` | 03_LargeBuffer5MB | 1,640,221.3 ns | 32,699.13 ns | 44,758.94 ns | 0.200 | 0.01 | 246.0938 | 246.0938 | 246.0938 | 5243065 B | 1.000 |
| `UniFFISharp (UniFFI): RustBuffer` | 03_LargeBuffer5MB | 8,197,712.1 ns | 163,289.53 ns | 174,717.97 ns | 1.000 | 0.03 | 312.5000 | 312.5000 | 312.5000 | 5243008 B | 1.000 |

</details>

<details>
<summary>ObjectBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Diplomat: Object Method Calls` | 3.415 ns | 0.0218 ns | 0.0182 ns | 0.04 | 0.00 | - | - | 0.00 |
| `UniFFISharp (BoltFFI): Object Method Calls` | 4.239 ns | 0.0272 ns | 0.0227 ns | 0.05 | 0.00 | - | - | 0.00 |
| `Diplomat: Object Create & Destroy` | 48.685 ns | 0.8710 ns | 0.7274 ns | 0.60 | 0.01 | 0.0043 | 72 B | 1.80 |
| `UniFFISharp (UniFFI): Object Method Calls` | 48.882 ns | 0.4152 ns | 0.3467 ns | 0.60 | 0.01 | - | - | 0.00 |
| `UniFFISharp (BoltFFI): Object Create & Destroy` | 49.629 ns | 0.6536 ns | 0.5458 ns | 0.61 | 0.01 | 0.0014 | 24 B | 0.60 |
| `UniFFISharp (UniFFI): Object Create & Destroy` | 81.499 ns | 1.3269 ns | 1.1080 ns | 1.00 | 0.02 | 0.0024 | 40 B | 1.00 |

</details>

<details>
<summary>PrimitiveBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (BoltFFI): AddInt64` | 0.7430 ns | 0.0177 ns | 0.0157 ns | 0.30 | 0.01 | - | NA |
| `UniFFISharp (BoltFFI): NoOp` | 0.7544 ns | 0.0215 ns | 0.0168 ns | 0.31 | 0.01 | - | NA |
| `UniFFISharp (BoltFFI): ScalarMath (5 args)` | 1.6724 ns | 0.0211 ns | 0.0177 ns | 0.68 | 0.01 | - | NA |
| `Diplomat: AddInt64` | 2.0200 ns | 0.0593 ns | 0.0609 ns | 0.82 | 0.03 | - | NA |
| `Diplomat: NoOp` | 2.0380 ns | 0.0316 ns | 0.0264 ns | 0.83 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): NoOp` | 2.4541 ns | 0.0441 ns | 0.0344 ns | 1.00 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): AddInt64` | 2.7556 ns | 0.0445 ns | 0.0347 ns | 1.12 | 0.02 | - | NA |
| `Diplomat: ScalarMath (5 args)` | 3.0458 ns | 0.0225 ns | 0.0176 ns | 1.24 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): ScalarMath (5 args)` | 3.8640 ns | 0.0643 ns | 0.0537 ns | 1.57 | 0.03 | - | NA |
| `Diplomat: Loop 1000 AddInt64` | 961.9232 ns | 18.1687 ns | 16.1061 ns | 392.03 | 8.17 | - | NA |
| `UniFFISharp (BoltFFI): Loop 1000 AddInt64` | 1,070.8592 ns | 21.4232 ns | 45.6546 ns | 436.42 | 19.32 | - | NA |
| `UniFFISharp (UniFFI): Loop 1000 AddInt64` | 3,049.7079 ns | 32.0312 ns | 25.0079 ns | 1,242.89 | 19.04 | - | NA |

</details>

<details>
<summary>SliceBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Gen1 | Gen2 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Diplomat: BytesPass (64B)` | 6.573 ns | 0.1156 ns | 0.1025 ns | 0.08 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI): BytesPass (64B)` | 30.413 ns | 0.3508 ns | 0.2739 ns | 0.38 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (UniFFI): BytesPass (64B)` | 80.331 ns | 1.5847 ns | 1.8249 ns | 1.00 | 0.03 | - | - | - | - | NA |
| `Diplomat: BytesReturn (4KB Zero-Copy RustVec)` | 141.090 ns | 2.8193 ns | 2.3542 ns | 1.76 | 0.05 | 0.1419 | 0.1419 | 0.1419 | 72 B | NA |
| `UniFFISharp (BoltFFI): BytesReturn (4KB to byte[])` | 241.134 ns | 4.8466 ns | 13.0202 ns | 3.00 | 0.17 | 0.2460 | - | - | 4120 B | NA |
| `Diplomat: BytesReturn (4KB to byte[])` | 241.140 ns | 4.8431 ns | 9.7834 ns | 3.00 | 0.14 | 0.4334 | 0.2513 | 0.2432 | 4192 B | NA |
| `Diplomat: BytesPass (4KB)` | 387.839 ns | 4.8392 ns | 4.2898 ns | 4.83 | 0.12 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI): BytesPass (4KB)` | 454.145 ns | 4.2957 ns | 3.5871 ns | 5.66 | 0.13 | - | - | - | - | NA |
| `UniFFISharp (UniFFI): BytesPass (4KB)` | 1,668.429 ns | 25.6657 ns | 21.4320 ns | 20.78 | 0.52 | - | - | - | - | NA |
| `UniFFISharp (UniFFI): BytesReturn (4KB to byte[])` | 1,692.989 ns | 33.5946 ns | 41.2572 ns | 21.09 | 0.68 | 0.2460 | - | - | 4120 B | NA |
| `Diplomat: BytesPass (64KB)` | 6,033.231 ns | 71.2702 ns | 63.1792 ns | 75.14 | 1.79 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI): BytesPass (64KB)` | 7,159.256 ns | 111.0362 ns | 98.4307 ns | 89.16 | 2.26 | - | - | - | - | NA |
| `UniFFISharp (UniFFI): BytesPass (64KB)` | 26,291.202 ns | 207.1346 ns | 172.9668 ns | 327.44 | 7.37 | - | - | - | - | NA |

</details>

<details>
<summary>StringBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Gen0 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (BoltFFI): StringPass (16B ASCII)` | 5.891 ns | 0.0699 ns | 0.0546 ns | 0.20 | 0.00 | - | - | NA |
| `Diplomat: StringPass (16B ASCII)` | 8.787 ns | 0.1476 ns | 0.1309 ns | 0.31 | 0.01 | 0.0024 | 40 B | NA |
| `UniFFISharp (BoltFFI): StringPass (1KB ASCII)` | 18.644 ns | 0.3508 ns | 0.2929 ns | 0.65 | 0.01 | - | - | NA |
| `UniFFISharp (BoltFFI): StringPass (Unicode/Emoji)` | 24.565 ns | 0.5053 ns | 1.0435 ns | 0.85 | 0.04 | - | - | NA |
| `Diplomat: StringPass (Unicode/Emoji)` | 27.787 ns | 0.5583 ns | 0.5483 ns | 0.97 | 0.02 | 0.0057 | 96 B | NA |
| `UniFFISharp (UniFFI): StringPass (16B ASCII)` | 28.784 ns | 0.3490 ns | 0.2725 ns | 1.00 | 0.01 | - | - | NA |
| `Diplomat: StringPass (1KB ASCII)` | 40.320 ns | 0.8256 ns | 1.7594 ns | 1.40 | 0.06 | 0.0626 | 1048 B | NA |
| `UniFFISharp (UniFFI): StringPass (Unicode/Emoji)` | 49.917 ns | 0.5035 ns | 0.3931 ns | 1.73 | 0.02 | - | - | NA |
| `UniFFISharp (UniFFI): StringPass (1KB ASCII)` | 53.149 ns | 0.6744 ns | 0.5632 ns | 1.85 | 0.03 | - | - | NA |
| `UniFFISharp (UniFFI): StringRoundtrip` | 60.722 ns | 1.1119 ns | 0.9285 ns | 2.11 | 0.04 | 0.0043 | 72 B | NA |
| `UniFFISharp (UniFFI): StringReturn` | 68.975 ns | 0.8784 ns | 0.7787 ns | 2.40 | 0.03 | 0.0029 | 48 B | NA |
| `Diplomat: StringReturn` | 82.058 ns | 1.1961 ns | 0.9988 ns | 2.85 | 0.04 | 0.0067 | 112 B | NA |
| `UniFFISharp (BoltFFI): StringRoundtrip` | 82.930 ns | 0.7905 ns | 0.6601 ns | 2.88 | 0.03 | 0.0043 | 72 B | NA |
| `Diplomat: StringRoundtrip` | 83.079 ns | 1.0277 ns | 0.8582 ns | 2.89 | 0.04 | 0.0086 | 144 B | NA |
| `UniFFISharp (BoltFFI): StringReturn` | 91.196 ns | 1.8356 ns | 2.4505 ns | 3.17 | 0.09 | 0.0029 | 48 B | NA |
| `UniFFISharp (BoltFFI): StringPass (32KB ASCII)` | 423.530 ns | 3.2471 ns | 2.5352 ns | 14.72 | 0.16 | - | - | NA |
| `UniFFISharp (UniFFI): StringPass (32KB ASCII)` | 867.488 ns | 11.9465 ns | 9.9759 ns | 30.14 | 0.43 | - | - | NA |
| `Diplomat: StringPass (32KB ASCII)` | 1,038.641 ns | 20.6822 ns | 36.7626 ns | 36.09 | 1.30 | 1.9531 | 32792 B | NA |

</details>

<details>
<summary>StructBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (BoltFFI): Roundtrip Point3D` | 0.9600 ns | 0.0270 ns | 0.0266 ns | 0.02 | 0.00 | - | NA |
| `Diplomat: Pass UserInfo` | 3.0894 ns | 0.0802 ns | 0.0788 ns | 0.08 | 0.00 | - | NA |
| `UniFFISharp (BoltFFI): Pass Point3D` | 4.1398 ns | 0.0292 ns | 0.0244 ns | 0.10 | 0.00 | - | NA |
| `Diplomat: Pass Point3D` | 5.7153 ns | 0.0444 ns | 0.0371 ns | 0.14 | 0.00 | - | NA |
| `UniFFISharp (BoltFFI): Pass UserInfo` | 5.7492 ns | 0.1260 ns | 0.1400 ns | 0.14 | 0.00 | - | NA |
| `Diplomat: Roundtrip Point3D` | 7.8321 ns | 0.0850 ns | 0.0664 ns | 0.19 | 0.00 | - | NA |
| `UniFFISharp (UniFFI): Pass UserInfo` | 32.6794 ns | 0.6416 ns | 0.5009 ns | 0.80 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): Pass Point3D` | 40.7581 ns | 0.5952 ns | 0.4970 ns | 1.00 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): Roundtrip Point3D` | 123.0942 ns | 1.9843 ns | 1.9488 ns | 3.02 | 0.06 | - | NA |

</details>

<details>
<summary>TaggedUnionAstBenchmarks</summary>

| Method | Mean | Error | StdDev | Ratio | RatioSD | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `Pure C# Managed Evaluation` | 12.66 ns | 0.215 ns | 0.179 ns | 0.04 | 0.00 | - | NA |
| `UniFFISharp (BoltFFI): Direct Wire` | 221.36 ns | 2.149 ns | 1.795 ns | 0.76 | 0.02 | - | NA |
| `UniFFISharp (UniFFI): RustBuffer Wire` | 289.92 ns | 5.693 ns | 9.354 ns | 1.00 | 0.04 | - | NA |

</details>

<details>
<summary>UnifiedInteropBenchmarks</summary>

| Method | Categories | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen0 | Gen1 | Gen2 | Allocated | Alloc Ratio |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| `UniFFISharp (BoltFFI)` | 01_NoOp | 0.7454 ns | 0.0163 ns | 0.0128 ns | 0.7380 ns | 0.28 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 01_NoOp | 1.9838 ns | 0.0381 ns | 0.0318 ns | 1.9752 ns | 0.74 | 0.04 | - | - | - | - | NA |
| `CsBindgen` | 01_NoOp | 2.0124 ns | 0.0186 ns | 0.0156 ns | 2.0104 ns | 0.75 | 0.04 | - | - | - | - | NA |
| `Diplomat` | 01_NoOp | 2.0241 ns | 0.0228 ns | 0.0191 ns | 2.0192 ns | 0.75 | 0.04 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 01_NoOp | 2.7011 ns | 0.0722 ns | 0.1459 ns | 2.6339 ns | 1.00 | 0.07 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 02_AddInt64 | 0.7381 ns | 0.0110 ns | 0.0086 ns | 0.7357 ns | 0.28 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 02_AddInt64 | 2.0057 ns | 0.0194 ns | 0.0151 ns | 2.0048 ns | 0.76 | 0.01 | - | - | - | - | NA |
| `CsBindgen` | 02_AddInt64 | 2.0112 ns | 0.0243 ns | 0.0203 ns | 2.0081 ns | 0.76 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 02_AddInt64 | 2.0700 ns | 0.0436 ns | 0.0364 ns | 2.0597 ns | 0.78 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 02_AddInt64 | 2.6424 ns | 0.0301 ns | 0.0235 ns | 2.6355 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 03_ScalarMath | 1.6768 ns | 0.0360 ns | 0.0281 ns | 1.6689 ns | 0.43 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 03_ScalarMath | 2.2423 ns | 0.0372 ns | 0.0290 ns | 2.2284 ns | 0.57 | 0.01 | - | - | - | - | NA |
| `CsBindgen` | 03_ScalarMath | 2.9746 ns | 0.0531 ns | 0.0443 ns | 2.9602 ns | 0.76 | 0.01 | - | - | - | - | NA |
| `Diplomat` | 03_ScalarMath | 3.0391 ns | 0.0227 ns | 0.0189 ns | 3.0360 ns | 0.77 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 03_ScalarMath | 3.9257 ns | 0.0605 ns | 0.0473 ns | 3.9105 ns | 1.00 | 0.02 | - | - | - | - | NA |
| `CsBindgen` | 04_Loop1000Add | 952.9313 ns | 17.9485 ns | 16.7890 ns | 951.2344 ns | 0.30 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 04_Loop1000Add | 956.9029 ns | 18.8963 ns | 34.5530 ns | 937.7159 ns | 0.30 | 0.01 | - | - | - | - | NA |
| `Diplomat` | 04_Loop1000Add | 975.4679 ns | 19.3160 ns | 22.9944 ns | 974.3103 ns | 0.31 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 04_Loop1000Add | 1,104.9773 ns | 22.0620 ns | 53.2822 ns | 1,099.9310 ns | 0.35 | 0.02 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 04_Loop1000Add | 3,158.6830 ns | 20.1687 ns | 15.7464 ns | 3,157.9664 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 05_PassPoint3D | 4.1162 ns | 0.0174 ns | 0.0136 ns | 4.1145 ns | 0.10 | 0.00 | - | - | - | - | NA |
| `CsBindgen` | 05_PassPoint3D | 5.6543 ns | 0.0422 ns | 0.0352 ns | 5.6459 ns | 0.14 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 05_PassPoint3D | 5.6810 ns | 0.0271 ns | 0.0226 ns | 5.6813 ns | 0.14 | 0.00 | - | - | - | - | NA |
| `Interoptopus` | 05_PassPoint3D | 5.7066 ns | 0.0554 ns | 0.0462 ns | 5.6960 ns | 0.14 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 05_PassPoint3D | 40.3331 ns | 0.4923 ns | 0.4111 ns | 40.1504 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 06_RoundtripPoint3D | 0.7988 ns | 0.0321 ns | 0.0284 ns | 0.7853 ns | 0.006 | 0.00 | - | - | - | - | NA |
| `CsBindgen` | 06_RoundtripPoint3D | 3.3241 ns | 0.0485 ns | 0.0430 ns | 3.3022 ns | 0.027 | 0.00 | - | - | - | - | NA |
| `Interoptopus` | 06_RoundtripPoint3D | 7.7700 ns | 0.0532 ns | 0.0416 ns | 7.7579 ns | 0.063 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 06_RoundtripPoint3D | 7.7774 ns | 0.0337 ns | 0.0263 ns | 7.7741 ns | 0.063 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 06_RoundtripPoint3D | 123.5225 ns | 2.4759 ns | 2.8512 ns | 122.3424 ns | 1.000 | 0.03 | - | - | - | - | NA |
| `Interoptopus` | 07_PassUserInfo | 2.9375 ns | 0.0192 ns | 0.0161 ns | 2.9358 ns | 0.09 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 07_PassUserInfo | 2.9996 ns | 0.0693 ns | 0.0712 ns | 2.9723 ns | 0.10 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 07_PassUserInfo | 5.7384 ns | 0.0830 ns | 0.0648 ns | 5.7170 ns | 0.18 | 0.00 | - | - | - | - | NA |
| `CsBindgen` | 07_PassUserInfo | 11.5765 ns | 0.1021 ns | 0.0797 ns | 11.5615 ns | 0.37 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 07_PassUserInfo | 31.3692 ns | 0.4043 ns | 0.3376 ns | 31.2117 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 08_StringPass16B | 5.9638 ns | 0.0913 ns | 0.0712 ns | 5.9519 ns | 0.21 | 0.00 | - | - | - | - | NA |
| `CsBindgen` | 08_StringPass16B | 8.5680 ns | 0.1082 ns | 0.0845 ns | 8.5630 ns | 0.30 | 0.01 | 0.0024 | - | - | 40 B | NA |
| `Diplomat` | 08_StringPass16B | 8.8835 ns | 0.1712 ns | 0.1429 ns | 8.8440 ns | 0.32 | 0.01 | 0.0024 | - | - | 40 B | NA |
| `UniFFISharp (UniFFI)` | 08_StringPass16B | 28.1768 ns | 0.3859 ns | 0.5776 ns | 28.0895 ns | 1.00 | 0.03 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 09_StringPass1KB | 16.5676 ns | 0.2965 ns | 0.2476 ns | 16.4659 ns | 0.32 | 0.01 | - | - | - | - | NA |
| `CsBindgen` | 09_StringPass1KB | 41.6976 ns | 1.1005 ns | 3.1928 ns | 40.4378 ns | 0.80 | 0.06 | 0.0626 | - | - | 1048 B | NA |
| `Diplomat` | 09_StringPass1KB | 41.8314 ns | 1.1425 ns | 3.2965 ns | 40.8671 ns | 0.81 | 0.06 | 0.0626 | - | - | 1048 B | NA |
| `UniFFISharp (UniFFI)` | 09_StringPass1KB | 51.8767 ns | 0.7125 ns | 0.5563 ns | 51.5965 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 10_StringPass32KB | 423.9728 ns | 8.0956 ns | 7.1765 ns | 422.1918 ns | 0.49 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 10_StringPass32KB | 861.3414 ns | 10.5552 ns | 8.8140 ns | 857.7665 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `CsBindgen` | 10_StringPass32KB | 1,065.7921 ns | 21.3403 ns | 51.5392 ns | 1,040.6481 ns | 1.24 | 0.06 | 1.9531 | - | - | 32792 B | NA |
| `Diplomat` | 10_StringPass32KB | 1,068.9389 ns | 21.0675 ns | 42.5574 ns | 1,065.5111 ns | 1.24 | 0.05 | 1.9531 | - | - | 32792 B | NA |
| `UniFFISharp (BoltFFI)` | 11_StringPassUnicode | 23.2869 ns | 0.3864 ns | 0.3226 ns | 23.1185 ns | 0.47 | 0.01 | - | - | - | - | NA |
| `CsBindgen` | 11_StringPassUnicode | 27.1200 ns | 0.4377 ns | 0.3418 ns | 27.0585 ns | 0.55 | 0.01 | 0.0057 | - | - | 96 B | NA |
| `Diplomat` | 11_StringPassUnicode | 27.4764 ns | 0.5216 ns | 0.7480 ns | 27.3770 ns | 0.56 | 0.02 | 0.0057 | - | - | 96 B | NA |
| `UniFFISharp (UniFFI)` | 11_StringPassUnicode | 49.5199 ns | 1.0100 ns | 0.8953 ns | 49.1544 ns | 1.00 | 0.02 | - | - | - | - | NA |
| `CsBindgen` | 12_StringReturn | 41.3650 ns | 0.6228 ns | 0.5201 ns | 41.1903 ns | 0.62 | 0.01 | 0.0029 | - | - | 48 B | 1.00 |
| `UniFFISharp (UniFFI)` | 12_StringReturn | 66.6989 ns | 1.0451 ns | 1.1616 ns | 66.4516 ns | 1.00 | 0.02 | 0.0029 | - | - | 48 B | 1.00 |
| `Diplomat` | 12_StringReturn | 85.1589 ns | 1.7024 ns | 1.7482 ns | 84.7080 ns | 1.28 | 0.03 | 0.0067 | - | - | 112 B | 2.33 |
| `UniFFISharp (BoltFFI)` | 12_StringReturn | 92.1172 ns | 1.4005 ns | 1.1695 ns | 91.4059 ns | 1.38 | 0.03 | 0.0029 | - | - | 48 B | 1.00 |
| `UniFFISharp (UniFFI)` | 13_StringRoundtrip | 66.9002 ns | 0.9204 ns | 0.7686 ns | 66.7818 ns | 1.00 | 0.02 | 0.0043 | - | - | 72 B | 1.00 |
| `CsBindgen` | 13_StringRoundtrip | 72.5763 ns | 1.3786 ns | 1.1512 ns | 72.1770 ns | 1.08 | 0.02 | 0.0043 | - | - | 72 B | 1.00 |
| `UniFFISharp (BoltFFI)` | 13_StringRoundtrip | 83.9279 ns | 1.6616 ns | 1.3875 ns | 83.5581 ns | 1.25 | 0.02 | 0.0043 | - | - | 72 B | 1.00 |
| `Diplomat` | 13_StringRoundtrip | 84.9303 ns | 1.6374 ns | 1.3673 ns | 84.3987 ns | 1.27 | 0.02 | 0.0086 | - | - | 144 B | 2.00 |
| `CsBindgen` | 14_SlicePass64B | 6.3446 ns | 0.1177 ns | 0.1043 ns | 6.3263 ns | 0.08 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 14_SlicePass64B | 6.5015 ns | 0.0918 ns | 0.0717 ns | 6.4705 ns | 0.08 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 14_SlicePass64B | 34.1037 ns | 0.4399 ns | 0.3674 ns | 34.0086 ns | 0.44 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 14_SlicePass64B | 53.5318 ns | 0.9478 ns | 0.8402 ns | 53.6997 ns | 0.69 | 0.01 | 0.0081 | - | - | 136 B | NA |
| `UniFFISharp (UniFFI)` | 14_SlicePass64B | 77.6631 ns | 1.0925 ns | 0.9123 ns | 77.5448 ns | 1.00 | 0.02 | - | - | - | - | NA |
| `CsBindgen` | 15_SlicePass4KB | 384.7052 ns | 7.6348 ns | 7.4984 ns | 382.4215 ns | 0.23 | 0.01 | - | - | - | - | NA |
| `Diplomat` | 15_SlicePass4KB | 388.3759 ns | 6.8895 ns | 6.4445 ns | 385.7981 ns | 0.23 | 0.01 | - | - | - | - | NA |
| `Interoptopus` | 15_SlicePass4KB | 424.5637 ns | 6.2847 ns | 5.5712 ns | 422.7657 ns | 0.25 | 0.00 | 0.0081 | - | - | 136 B | NA |
| `UniFFISharp (BoltFFI)` | 15_SlicePass4KB | 456.5196 ns | 4.0647 ns | 3.1734 ns | 455.3857 ns | 0.27 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 15_SlicePass4KB | 1,679.8871 ns | 29.7318 ns | 26.3565 ns | 1,668.8463 ns | 1.00 | 0.02 | - | - | - | - | NA |
| `CsBindgen` | 16_SlicePass64KB | 6,009.8203 ns | 48.7681 ns | 38.0749 ns | 5,998.1125 ns | 0.23 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 16_SlicePass64KB | 6,023.4950 ns | 68.5623 ns | 57.2526 ns | 5,992.9405 ns | 0.23 | 0.00 | - | - | - | - | NA |
| `Interoptopus` | 16_SlicePass64KB | 6,090.4922 ns | 61.4657 ns | 51.3267 ns | 6,085.4706 ns | 0.23 | 0.00 | 0.0076 | - | - | 136 B | NA |
| `UniFFISharp (BoltFFI)` | 16_SlicePass64KB | 7,107.4670 ns | 32.9134 ns | 25.6966 ns | 7,105.8868 ns | 0.27 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 16_SlicePass64KB | 26,278.5904 ns | 333.9402 ns | 260.7186 ns | 26,275.7202 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `CsBindgen (ZeroCopy ByteBuffer)` | 17_SliceReturn4KB | 36.0668 ns | 0.5136 ns | 0.4289 ns | 35.9039 ns | 0.02 | 0.00 | - | - | - | - | 0.000 |
| `Interoptopus (ZeroCopy VecByte)` | 17_SliceReturn4KB | 44.9099 ns | 0.9168 ns | 0.8576 ns | 44.6252 ns | 0.03 | 0.00 | 0.0024 | - | - | 40 B | 0.010 |
| `CsBindgen (To managed byte[])` | 17_SliceReturn4KB | 117.0808 ns | 2.3729 ns | 5.7761 ns | 114.9759 ns | 0.07 | 0.00 | 0.2460 | - | - | 4120 B | 1.000 |
| `Diplomat (ZeroCopy RustVec)` | 17_SliceReturn4KB | 136.3913 ns | 2.2318 ns | 2.5701 ns | 135.2827 ns | 0.08 | 0.00 | 0.1423 | 0.1423 | 0.1423 | 72 B | 0.017 |
| `UniFFISharp (BoltFFI)` | 17_SliceReturn4KB | 234.0280 ns | 4.7108 ns | 12.3273 ns | 230.0766 ns | 0.14 | 0.01 | 0.2460 | - | - | 4120 B | 1.000 |
| `Diplomat (To managed byte[])` | 17_SliceReturn4KB | 248.5060 ns | 4.9994 ns | 11.1819 ns | 248.8885 ns | 0.14 | 0.01 | 0.3982 | 0.3223 | 0.3195 | 4192 B | 1.017 |
| `UniFFISharp (UniFFI): To managed byte[]` | 17_SliceReturn4KB | 1,724.8328 ns | 34.4161 ns | 83.1189 ns | 1,692.4267 ns | 1.00 | 0.07 | 0.2460 | - | - | 4120 B | 1.000 |
| `Interoptopus (To managed byte[])` | 17_SliceReturn4KB | 34,250.8334 ns | 616.1635 ns | 1,111.0700 ns | 33,951.7761 ns | 19.90 | 1.12 | 6.1035 | 0.0610 | - | 102465 B | 24.870 |
| `CsBindgen` | 18_ObjectLifecycle | 18.6953 ns | 0.3883 ns | 0.4472 ns | 18.4465 ns | 0.23 | 0.01 | - | - | - | - | 0.00 |
| `Interoptopus` | 18_ObjectLifecycle | 33.8542 ns | 0.4972 ns | 0.3882 ns | 33.8035 ns | 0.41 | 0.01 | 0.0019 | - | - | 32 B | 0.80 |
| `Diplomat` | 18_ObjectLifecycle | 48.0153 ns | 0.9357 ns | 1.8688 ns | 47.4384 ns | 0.59 | 0.02 | 0.0043 | - | - | 72 B | 1.80 |
| `UniFFISharp (BoltFFI)` | 18_ObjectLifecycle | 50.2095 ns | 1.0168 ns | 1.3221 ns | 49.6899 ns | 0.61 | 0.02 | 0.0014 | - | - | 24 B | 0.60 |
| `UniFFISharp (UniFFI)` | 18_ObjectLifecycle | 81.7968 ns | 1.2806 ns | 1.0694 ns | 81.4380 ns | 1.00 | 0.02 | 0.0024 | - | - | 40 B | 1.00 |
| `CsBindgen` | 19_ObjectMethodCalls | 3.1311 ns | 0.0692 ns | 0.0578 ns | 3.1087 ns | 0.06 | 0.00 | - | - | - | - | NA |
| `Diplomat` | 19_ObjectMethodCalls | 3.9792 ns | 0.0687 ns | 0.0574 ns | 3.9522 ns | 0.08 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 19_ObjectMethodCalls | 4.2796 ns | 0.0908 ns | 0.0805 ns | 4.2443 ns | 0.09 | 0.00 | - | - | - | - | NA |
| `Interoptopus` | 19_ObjectMethodCalls | 5.4703 ns | 0.1279 ns | 0.1256 ns | 5.4290 ns | 0.11 | 0.00 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 19_ObjectMethodCalls | 48.8231 ns | 0.3253 ns | 0.2539 ns | 48.6634 ns | 1.00 | 0.01 | - | - | - | - | NA |
| `UniFFISharp (UniFFI)` | 20_ResultSuccess | 2.7338 ns | 0.0623 ns | 0.0486 ns | 2.7427 ns | 1.00 | 0.02 | - | - | - | - | NA |
| `Interoptopus` | 20_ResultSuccess | 2.8360 ns | 0.0745 ns | 0.1635 ns | 2.7658 ns | 1.04 | 0.06 | - | - | - | - | NA |
| `UniFFISharp (BoltFFI)` | 20_ResultSuccess | 5.2894 ns | 0.0546 ns | 0.0426 ns | 5.2766 ns | 1.94 | 0.04 | - | - | - | - | NA |
| `Diplomat` | 20_ResultSuccess | 6.1816 ns | 0.1397 ns | 0.1663 ns | 6.0887 ns | 2.26 | 0.07 | - | - | - | - | NA |
| `CsBindgen` | 20_ResultSuccess | 9.9842 ns | 0.2127 ns | 0.2690 ns | 9.9275 ns | 3.65 | 0.11 | - | - | - | - | NA |
| `Diplomat` | 21_ResultFailure | 825.5359 ns | 16.4743 ns | 35.1080 ns | 804.0089 ns | 0.74 | 0.03 | 0.0172 | - | - | 296 B | 0.93 |
| `CsBindgen` | 21_ResultFailure | 827.7852 ns | 13.5237 ns | 10.5584 ns | 824.9174 ns | 0.74 | 0.01 | 0.0162 | - | - | 272 B | 0.85 |
| `Interoptopus` | 21_ResultFailure | 853.0613 ns | 16.8769 ns | 36.3294 ns | 839.2697 ns | 0.77 | 0.03 | 0.0181 | - | - | 304 B | 0.95 |
| `UniFFISharp (BoltFFI)` | 21_ResultFailure | 942.8427 ns | 18.7384 ns | 35.6517 ns | 926.4020 ns | 0.85 | 0.03 | 0.0153 | - | - | 264 B | 0.82 |
| `UniFFISharp (UniFFI)` | 21_ResultFailure | 1,114.7426 ns | 21.5492 ns | 19.1028 ns | 1,107.1647 ns | 1.00 | 0.02 | 0.0191 | - | - | 320 B | 1.00 |

</details>
