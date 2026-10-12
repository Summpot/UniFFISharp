# UniFFISharp vs Diplomat vs CsBindgen .NET Interop Benchmarks

这是一个针对 **UniFFI (Classic)**、**UniFFISharp (BoltFFI Engine)**、**Diplomat** 与 **CsBindgen (Cysharp)** 在 **.NET / C#** 跨语言互操作场景下的全方位性能基准对比套件。

项目基于行业标准的 [BenchmarkDotNet](https://benchmarkdotnet.org/) 构建，通过 **单一大类 (`UnifiedInteropBenchmarks`)** 在一次运行中完成所有场景的集中横向对比（不拆开），并配备内存诊断器（`[MemoryDiagnoser]`），能够精确衡量每次调用的 **执行耗时 (Mean)**、**相对基准比率 (Ratio)**、**GC 堆分配 (Allocated Bytes)** 以及 **GC 回收频次 (Gen0/Gen1/Gen2)**。

---

## 四大互操作框架架构与设计哲学对比

| 维度 | UniFFI (Classic) | UniFFISharp (BoltFFI) | Diplomat | CsBindgen (Cysharp) |
| :--- | :--- | :--- | :--- | :--- |
| **定位与抽象层级** | **经典 UniFFI 抽象**：多语言统一 UDL/宏定义，通过 `RustBuffer` 序列化协议传递数据，`ref UniffiRustCallStatus` 状态通道 | **BoltFFI 高性能引擎**：自动识别 blittable 结构体直接透传，标量直通，集成 `[SuppressGCTransition]` 亚纳秒调用，按需 Wire 编码 | **C-ABI 规约抽象**：强调 C-ABI 内存布局与多语言兼容（C/C++/JS/Kotlin/.NET），通过 `#[diplomat::bridge]` 规范跨语言边界 | **底层原始 P/Invoke**：直接针对 Rust `extern "C"` 代码生成极致轻量的 C# `[DllImport]` 签名与结构体，无任何运行时包装 |
| **基础调用模式** | 每个函数附带 `ref UniffiRustCallStatus status` 出参 | 纯原生寄存器直接调用，标记 `[SuppressGCTransition]` 抑制 GC 状态转移 | 原生 `extern "C"` 符号导出，无状态参数（若为 infallible 函数） | 纯原生 `extern "C"` 直接 P/Invoke，无多余封装 |
| **结构体传递** | `RustBuffer` 序列化（`BigEndianStream` 写入与反序列化，托管内存分配） | **Direct Blittable 透传**：按值直接在 CPU 寄存器或栈上传递（0 分配） | **repr(C)** 内存布局：按值直接传递（0 分配） | **repr(C)** 内存布局：按值直接传递（0 分配） |
| **字节切片 (`&[u8]`)** | `RustBuffer` 传输（长度前缀 + 4 次 FFI 内存分配与拷贝） | `(nint ptr, nuint len)` 直接切片暴露，指针直通 | `fixed` 钉住借用为 `&[u8]` | `fixed` 钉住直接传递裸指针与长度 |
| **对象模型** | `Arc<T>` 引用计数，句柄查找与序列化状态通道 | 原生包装 handle，`[SuppressGCTransition]` 直接调用，显式 Dispose 释放 | `Box<T>` 独占所有权，`RustHandle<T>` 安全封装 | `Box<T>` 裸指针直接传递与释放 |
| **错误处理** | `RustCallStatus` 错误码 + 序列化 `error_buf` | `FfiBuf` 异常通道，无错误直接返回，有错误解码抛出 | `DiplomatResult` 返回 tag 与联合体，失败抛出异常 | 结构体返回 `is_ok` 标记 |

---

## 目录结构

```
UniFFISharp/
├── benchmarks/
│   ├── Cargo.toml                              # 根 Cargo Workspace (包含 3 个 Rust 库)
│   ├── build.ps1                               # 自动化编译、绑定生成与测试脚本
│   ├── .cargo/config.toml                      # 开启 boltffi_metadata 编译标志
│   ├── crates/
│   │   ├── uniffi_bench/                       # UniFFI & BoltFFI 绑定库
│   │   ├── diplomat_bench/                     # Diplomat 绑定库
│   │   └── csbindgen_bench/                    # CsBindgen 导出库 (build.rs 自动生成 C# 代码)
│   └── UniFFISharp.Benchmark/
│       ├── UniFFISharp.Benchmark.csproj        # 直接 ProjectReference 引用项目自身
│       ├── Program.cs                          # BenchmarkSwitcher / 校验测试入口
│       ├── Generated/
│       │   ├── Diplomat/                       # 由 diplomat-tool 生成的 C# 代码
│       │   └── CsBindgen/                      # 由 csbindgen build.rs 生成的 C# 代码
│       └── Benchmarks/
│           └── UnifiedInteropBenchmarks.cs     # ★★★ 集中式全量单类基准测试 ★★★
├── UniFFISharp/                                # 核心运行时
├── UniFFISharp.Generator/                      # Roslyn 增量代码生成器
├── UniFFISharp.Tests/                          # 单元测试
└── UniFFISharp.slnx                            # 统一解决方案 (包含 /src, /tests, /benchmarks)
```

---

## 运行指南

### 1. 运行四方功能等价性校验 (Verification)

验证 UniFFI (Classic)、UniFFISharp (BoltFFI)、Diplomat 与 CsBindgen 在所有测试用例下的计算结果 100% 等同：

```powershell
pwsh ./benchmarks/build.ps1 -Verify
```

输出：
```text
=== Verifying UniFFI (Classic), UniFFISharp (BoltFFI), Diplomat & CsBindgen ===
[✓] NoOp passed
[✓] AddInt64: UniFFI=300, BoltFFI=300, Diplomat=300, CsBindgen=300
[✓] ScalarMath: UniFFI=202, BoltFFI=202, Diplomat=202, CsBindgen=202
[✓] StringLength: UniFFI=43, BoltFFI=43, Diplomat=43, CsBindgen=43
[✓] StringGreeting: UniFFI=Hello, World!, BoltFFI=Hello, World!, Diplomat=Hello, World!, CsBindgen=Hello, World!
[✓] StringRoundtrip: UniFFI=echo: UniFFI-Diplomat, BoltFFI=echo: UniFFI-Diplomat, Diplomat=echo: UniFFI-Diplomat, CsBindgen=echo: UniFFI-Diplomat
[✓] SumBytes: UniFFI=75, BoltFFI=75, Diplomat=75, CsBindgen=75
[✓] GenerateBytes: UniFFI len=32, BoltFFI len=32, Diplomat len=32, CsBindgen len=32
[✓] PassPoint: UniFFI=14, BoltFFI=14, Diplomat=14, CsBindgen=14
[✓] RoundtripPoint: UniFFI=(11,22,33), BoltFFI=(11,22,33), Diplomat=(11,22,33), CsBindgen=(11,22,33)
[✓] PassUserInfo: UniFFI=12468, BoltFFI=12468, Diplomat=12468, CsBindgen=12468
[✓] Counter: UniFFI=150, BoltFFI=150, Diplomat=150, CsBindgen=150
[✓] ResultSuccess: UniFFI=84, BoltFFI=84, Diplomat=84, CsBindgen=84
[✓] ResultFailure: UniFFI caught=True, BoltFFI caught=True, Diplomat caught=True, CsBindgen is_err=True

 ALL FOUR FRAMEWORKS (UniFFI, UniFFISharp-BoltFFI, Diplomat, CsBindgen) PASSED FUNCTIONAL VERIFICATION!
```

---

### 2. 集中运行全量基准测试（在一个 Benchmark 中完整跑完）

```powershell
# 推荐：快速迭代模式（ShortRun，全量 21 场景同屏对比）
pwsh ./benchmarks/build.ps1 -Short -Filter *Unified*

# 针对特定场景过滤测试（例如基础类型或切片）
pwsh ./benchmarks/build.ps1 -Short -Filter *NoOp*
```

---

## 统一基准测试实测数据报表 (Unified Benchmark Results)

> **环境信息**：.NET 10.0.12 (X64 RyuJIT AVX-512), Windows 11, BenchmarkDotNet v0.14.0 (ShortRun)。
> 每个用例以 **UniFFI (Classic) 作为 Baseline (Ratio = 1.00)**。

| 场景分类 (Category) | 框架 (Framework) | 平均耗时 (Mean) | 误差 (StdDev) | 相对UniFFI耗时 (Ratio) | Gen0/1k op | 单次托管堆分配 (Allocated) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **01_NoOp** | **UniFFISharp (BoltFFI)** | **0.86 ns** | 0.05 ns | **0.21x (快 4.8x)** | - | **0 B** |
| | Diplomat | 2.49 ns | 0.12 ns | 0.61x | - | 0 B |
| | CsBindgen | 2.72 ns | 0.09 ns | 0.66x | - | 0 B |
| | UniFFI (Classic) | 4.16 ns | 0.61 ns | 1.00x | - | 0 B |
| **02_AddInt64** | **UniFFISharp (BoltFFI)** | **0.84 ns** | 0.03 ns | **0.26x (快 3.9x)** | - | **0 B** |
| | Diplomat | 2.47 ns | 0.10 ns | 0.75x | - | 0 B |
| | CsBindgen | 2.60 ns | 0.23 ns | 0.79x | - | 0 B |
| | UniFFI (Classic) | 3.28 ns | 0.13 ns | 1.00x | - | 0 B |
| **03_ScalarMath** | **UniFFISharp (BoltFFI)** | **2.56 ns** | 0.17 ns | **0.57x (快 1.8x)** | - | **0 B** |
| | CsBindgen | 3.27 ns | 0.10 ns | 0.72x | - | 0 B |
| | Diplomat | 3.49 ns | 0.27 ns | 0.77x | - | 0 B |
| | UniFFI (Classic) | 4.53 ns | 0.10 ns | 1.00x | - | 0 B |
| **04_Loop1000Add** | Diplomat | 1,163.08 ns | 10.18 ns | 0.33x | - | 0 B |
| | CsBindgen | 1,175.78 ns | 61.63 ns | 0.33x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **1,234.00 ns** | 31.83 ns | **0.35x (快 2.9x)** | - | **0 B** |
| | UniFFI (Classic) | 3,554.49 ns | 173.80 ns | 1.00x | - | 0 B |
| **05_PassPoint3D** | **UniFFISharp (BoltFFI)** | **4.23 ns** | 0.03 ns | **0.10x (快 10.2x)** | - | **0 B** |
| | Diplomat | 5.92 ns | 0.06 ns | 0.14x | - | 0 B |
| | CsBindgen | 5.94 ns | 0.05 ns | 0.14x | - | 0 B |
| | UniFFI (Classic) | 43.16 ns | 0.67 ns | 1.00x | - | 0 B |
| **06_RoundtripPoint3D** | **UniFFISharp (BoltFFI)** | **1.42 ns** | 0.30 ns | **0.009x (快 108x)** | - | **0 B (零分配)** |
| | CsBindgen | 3.48 ns | 0.10 ns | 0.023x | - | 0 B |
| | Diplomat | 8.19 ns | 0.06 ns | 0.053x | - | 0 B |
| | UniFFI (Classic) | 153.73 ns | 0.42 ns | 1.000x | 0.0024 | 40 B |
| **07_PassUserInfo** | Diplomat | 3.26 ns | 0.05 ns | 0.10x | - | 0 B |
| | CsBindgen | 12.27 ns | 0.20 ns | 0.36x | - | 0 B |
| | UniFFI (Classic) | 34.30 ns | 0.76 ns | 1.00x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **57.76 ns** | 1.63 ns | 1.68x | 0.0234 | 392 B |
| **08_StringPass16B** | CsBindgen | 9.60 ns | 0.08 ns | 0.32x | 0.0024 | 40 B |
| | Diplomat | 9.69 ns | 0.28 ns | 0.32x | 0.0024 | 40 B |
| | UniFFI (Classic) | 30.28 ns | 0.43 ns | 1.00x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **34.97 ns** | 1.93 ns | 1.16x | 0.0024 | 40 B |
| **09_StringPass1KB** | CsBindgen | 45.24 ns | 1.45 ns | 0.82x | 0.0626 | 1,048 B |
| | Diplomat | 48.77 ns | 2.66 ns | 0.88x | 0.0626 | 1,048 B |
| | UniFFI (Classic) | 55.22 ns | 0.32 ns | 1.00x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **93.64 ns** | 4.27 ns | 1.70x | 0.0626 | 1,048 B |
| **10_StringPass32KB** | UniFFI (Classic) | 936.76 ns | 23.36 ns | 1.00x | - | 0 B |
| | Diplomat | 1,242.16 ns | 72.86 ns | 1.33x | 1.9531 | 32,792 B |
| | CsBindgen | 1,298.91 ns | 35.62 ns | 1.39x | 1.9531 | 32,792 B |
| | **UniFFISharp (BoltFFI)** | **1,658.79 ns** | 45.62 ns | 1.77x | 1.9531 | 32,792 B |
| **11_StringPassUnicode** | CsBindgen | 34.51 ns | 0.49 ns | 0.57x | 0.0057 | 96 B |
| | Diplomat | 35.32 ns | 0.93 ns | 0.58x | 0.0057 | 96 B |
| | UniFFI (Classic) | 60.79 ns | 2.68 ns | 1.00x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **61.58 ns** | 2.83 ns | 1.01x | 0.0057 | 96 B |
| **12_StringReturn** | CsBindgen | 53.26 ns | 2.27 ns | 0.61x | 0.0029 | 48 B |
| | UniFFI (Classic) | 88.03 ns | 3.97 ns | 1.00x | 0.0029 | 48 B |
| | Diplomat | 128.40 ns | 9.17 ns | 1.46x | 0.0067 | 112 B |
| | **UniFFISharp (BoltFFI)** | **164.58 ns** | 14.82 ns | 1.87x | 0.0048 | 80 B |
| **13_StringRoundtrip** | UniFFI (Classic) | 83.84 ns | 3.39 ns | 1.00x | 0.0043 | 72 B |
| | CsBindgen | 96.32 ns | 0.81 ns | 1.15x | 0.0043 | 72 B |
| | Diplomat | 123.55 ns | 4.06 ns | 1.48x | 0.0086 | 144 B |
| | **UniFFISharp (BoltFFI)** | **166.60 ns** | 5.42 ns | 1.99x | 0.0067 | 112 B |
| **14_SlicePass64B** | CsBindgen | 8.45 ns | 0.03 ns | 0.09x | - | 0 B |
| | Diplomat | 8.67 ns | 0.20 ns | 0.09x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **65.64 ns** | 1.86 ns | **0.68x (快 32%)** | - | **0 B** |
| | UniFFI (Classic) | 96.81 ns | 2.22 ns | 1.00x | - | 0 B |
| **15_SlicePass4KB** | Diplomat | 447.20 ns | 11.49 ns | 0.20x | - | 0 B |
| | CsBindgen | 449.13 ns | 4.75 ns | 0.20x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **548.81 ns** | 2.01 ns | **0.25x (快 4.0x)** | - | **0 B** |
| | UniFFI (Classic) | 2,207.40 ns | 58.49 ns | 1.00x | - | 0 B |
| **16_SlicePass64KB** | CsBindgen | 6,941.28 ns | 14.58 ns | 0.19x | - | 0 B |
| | Diplomat | 7,139.40 ns | 103.61 ns | 0.20x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **8,401.24 ns** | 193.07 ns | **0.23x (快 4.3x)** | - | **0 B** |
| | UniFFI (Classic) | 36,234.63 ns | 983.37 ns | 1.00x | - | 0 B |
| **17_SliceReturn4KB** | CsBindgen (ZeroCopy) | 49.68 ns | 1.51 ns | 0.02x | - | 0 B |
| | Diplomat (ZeroCopy) | 186.09 ns | 6.29 ns | 0.07x | 0.1967 | 72 B |
| | CsBindgen (byte[]) | 360.26 ns | 21.88 ns | 0.14x | 0.2460 | 4,120 B |
| | Diplomat (byte[]) | 500.70 ns | 13.80 ns | 0.20x | 0.4597 | 4,192 B |
| | **UniFFISharp (BoltFFI)** | **1,907.30 ns** | 33.79 ns | **0.75x (快 25%)** | 0.2441 | **4,120 B** |
| | UniFFI (Classic) | 2,539.45 ns | 147.92 ns | 1.00x | 0.2441 | 4,120 B |
| **18_ObjectLifecycle** | CsBindgen | 28.06 ns | 0.92 ns | 0.29x | - | 0 B |
| | Diplomat | 66.91 ns | 2.39 ns | 0.69x | 0.0043 | 72 B |
| | **UniFFISharp (BoltFFI)** | **74.39 ns** | 0.48 ns | **0.77x (快 24%)** | 0.0014 | **24 B (减少40%)**|
| | UniFFI (Classic) | 97.16 ns | 0.99 ns | 1.00x | 0.0024 | 40 B |
| **19_ObjectMethodCalls**| CsBindgen | 4.21 ns | 0.34 ns | 0.08x | - | 0 B |
| | Diplomat | 4.55 ns | 0.08 ns | 0.09x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **19.84 ns** | 0.36 ns | **0.37x (快 2.7x)** | - | **0 B** |
| | UniFFI (Classic) | 53.04 ns | 0.60 ns | 1.00x | - | 0 B |
| **20_ResultSuccess** | UniFFI (Classic) | 3.64 ns | 0.05 ns | 1.00x | - | 0 B |
| | **UniFFISharp (BoltFFI)** | **5.37 ns** | 0.08 ns | **1.48x (优于Diplomat)**| - | **0 B** |
| | Diplomat | 6.54 ns | 0.07 ns | 1.80x | - | 0 B |
| | CsBindgen | 10.91 ns | 0.11 ns | 3.00x | - | 0 B |
| **21_ResultFailure** | Diplomat | 1,047.96 ns | 5.82 ns | 0.71x | 0.0172 | 296 B |
| | CsBindgen | 1,065.53 ns | 15.49 ns | 0.73x | 0.0153 | 272 B |
| | **UniFFISharp (BoltFFI)** | **1,177.72 ns** | 23.18 ns | **0.80x (快 20%)** | 0.0153 | **272 B (减少15%)**|
| | UniFFI (Classic) | 1,466.94 ns | 12.97 ns | 1.00x | 0.0191 | 320 B |
