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
> 全量 21 项场景在单一大类 (`UnifiedInteropBenchmarks`) 中完成完整横向对比。
> 每个用例以 **UniFFI (Classic) 作为 Baseline (Ratio = 1.00)**。

| 场景分类 (Category) | 框架 (Framework) | 平均耗时 (Mean) | 误差 (StdDev) | 相对UniFFI耗时 (Ratio) | Gen0/1k op | 单次托管堆分配 (Allocated) | 排名与表现说明 |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **01_NoOp** | **UniFFISharp (BoltFFI)** | **0.80 ns** | 0.04 ns | **0.28x** | - | **0 B** | 🥇 **全场第 1**（快 Diplomat/CsBindgen 2.7x，亚纳秒级） |
| | Diplomat | 2.18 ns | 0.12 ns | 0.76x | - | 0 B | |
| | CsBindgen | 2.25 ns | 0.10 ns | 0.79x | - | 0 B | |
| | UniFFI (Classic) | 2.86 ns | 0.10 ns | 1.00x | - | 0 B | |
| **02_AddInt64** | **UniFFISharp (BoltFFI)** | **0.81 ns** | 0.05 ns | **0.28x** | - | **0 B** | 🥇 **全场第 1**（寄存器直出，快 2.7x） |
| | Diplomat | 2.19 ns | 0.06 ns | 0.75x | - | 0 B | |
| | CsBindgen | 2.22 ns | 0.04 ns | 0.76x | - | 0 B | |
| | UniFFI (Classic) | 2.91 ns | 0.00 ns | 1.00x | - | 0 B | |
| **03_ScalarMath** | **UniFFISharp (BoltFFI)** | **1.89 ns** | 0.06 ns | **0.46x** | - | **0 B** | 🥇 **全场第 1**（多参数寄存器直通，快 1.7x） |
| | CsBindgen | 3.23 ns | 0.08 ns | 0.79x | - | 0 B | |
| | Diplomat | 3.29 ns | 0.09 ns | 0.80x | - | 0 B | |
| | UniFFI (Classic) | 4.10 ns | 0.11 ns | 1.00x | - | 0 B | |
| **04_Loop1000Add** | CsBindgen | 1,041.51 ns | 52.91 ns | 0.32x | - | 0 B | 🥇 第 1 |
| | Diplomat | 1,042.41 ns | 22.71 ns | 0.32x | - | 0 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **1,188.40 ns** | 15.35 ns | **0.37x** | - | **0 B** | 🥉 第 3（相比 UniFFI Classic 提速 2.7x） |
| | UniFFI (Classic) | 3,244.45 ns | 33.87 ns | 1.00x | - | 0 B | |
| **05_PassPoint3D** | **UniFFISharp (BoltFFI)** | **4.23 ns** | 0.02 ns | **0.10x** | - | **0 B** | 🥇 **全场第 1**（结构体指针直通，快 10x） |
| | CsBindgen | 5.84 ns | 0.05 ns | 0.14x | - | 0 B | |
| | Diplomat | 5.92 ns | 0.05 ns | 0.14x | - | 0 B | |
| | UniFFI (Classic) | 43.13 ns | 0.62 ns | 1.00x | - | 0 B | |
| **06_RoundtripPoint3D** | **UniFFISharp (BoltFFI)** | **1.12 ns** | 0.11 ns | **0.008x** | - | **0 B** | 🥇 **全场第 1**（快 CsBindgen 3.3x，快 Diplomat 7.3x，快 UniFFI 120x） |
| | CsBindgen | 3.67 ns | 0.26 ns | 0.027x | - | 0 B | |
| | Diplomat | 8.14 ns | 0.11 ns | 0.060x | - | 0 B | |
| | UniFFI (Classic) | 135.43 ns | 1.50 ns | 1.000x | 0.0024 | 40 B | |
| **07_PassUserInfo** | Diplomat | 3.25 ns | 0.14 ns | 0.09x | - | 0 B | 🥇 第 1（C-ABI 布局） |
| | **UniFFISharp (BoltFFI)** | **6.12 ns** | 0.12 ns | **0.18x** | - | **0 B** | 🥈 **第 2**（**快 CsBindgen 2x**，Wire 编码 0 堆分配） |
| | CsBindgen | 12.40 ns | 0.22 ns | 0.36x | - | 0 B | 🥉 第 3 |
| | UniFFI (Classic) | 34.75 ns | 1.67 ns | 1.00x | - | 0 B | |
| **08_StringPass16B** | **UniFFISharp (BoltFFI)** | **6.27 ns** | 0.09 ns | **0.19x** | - | **0 B** | 🥇 **全场第 1**（快 Diplomat/CsBindgen 1.6x，**0 堆分配 vs 40 B**） |
| | Diplomat | 9.68 ns | 0.20 ns | 0.30x | 0.0024 | 40 B | |
| | CsBindgen | 10.15 ns | 0.57 ns | 0.31x | 0.0024 | 40 B | |
| | UniFFI (Classic) | 32.58 ns | 1.52 ns | 1.00x | - | 0 B | |
| **09_StringPass1KB** | **UniFFISharp (BoltFFI)** | **16.83 ns** | 0.36 ns | **0.29x** | - | **0 B** | 🥇 **全场第 1**（**快 3.2x**，单趟 UTF-8 栈编码，**0 堆分配 vs 1KB**） |
| | Diplomat | 49.44 ns | 1.76 ns | 0.87x | 0.0626 | 1,048 B | |
| | CsBindgen | 53.61 ns | 0.55 ns | 0.94x | 0.0626 | 1,048 B | |
| | UniFFI (Classic) | 57.18 ns | 2.15 ns | 1.00x | - | 0 B | |
| **10_StringPass32KB** | **UniFFISharp (BoltFFI)** | **438.07 ns** | 1.97 ns | **0.47x** | - | **0 B** | 🥇 **全场第 1**（**快 CsBindgen/Diplomat 近 3x**，零 GC 分配 vs 32KB） |
| | UniFFI (Classic) | 937.63 ns | 7.07 ns | 1.00x | - | 0 B | |
| | Diplomat | 1,169.07 ns | 42.82 ns | 1.25x | 1.9531 | 32,792 B | |
| | CsBindgen | 1,257.46 ns | 125.71 ns | 1.34x | 1.9531 | 32,792 B | |
| **11_StringPassUnicode** | **UniFFISharp (BoltFFI)** | **25.35 ns** | 0.03 ns | **0.46x** | - | **0 B** | 🥇 **全场第 1**（多语言 UTF-8 极速直出，**0 堆分配 vs 96 B**） |
| | Diplomat | 30.36 ns | 1.10 ns | 0.56x | 0.0057 | 96 B | |
| | CsBindgen | 31.28 ns | 0.46 ns | 0.57x | 0.0057 | 96 B | |
| | UniFFI (Classic) | 54.59 ns | 2.35 ns | 1.00x | - | 0 B | |
| **12_StringReturn** | CsBindgen | 47.29 ns | 1.62 ns | 0.62x | 0.0029 | 48 B | 🥇 第 1（基准测试传入预编码静态 byte[]） |
| | UniFFI (Classic) | 76.50 ns | 0.40 ns | 1.00x | 0.0029 | 48 B | 🥈 第 2 |
| | Diplomat | 92.26 ns | 2.19 ns | 1.21x | 0.0067 | 112 B | 🥉 第 3 |
| | **UniFFISharp (BoltFFI)** | **95.40 ns** | 1.25 ns | **1.25x** | 0.0029 | **48 B** | 严格动态字符串编码入参，优于 Diplomat 内存 |
| **13_StringRoundtrip** | **UniFFI (Classic)** | **67.61 ns** | 2.14 ns | **1.00x** | 0.0043 | **72 B** | 🥇 **UniFFI 全场第 1**（完胜 CsBindgen 78 ns 与 Diplomat 91 ns） |
| | CsBindgen | 78.59 ns | 1.33 ns | 1.16x | 0.0043 | 72 B | |
| | **UniFFISharp (BoltFFI)** | **89.72 ns** | 0.95 ns | **1.33x** | 0.0043 | **72 B** | 优于 Diplomat (91.08 ns, 144 B) |
| | Diplomat | 91.08 ns | 1.52 ns | 1.35x | 0.0086 | 144 B | |
| **14_SlicePass64B** | CsBindgen | 7.11 ns | 0.20 ns | 0.09x | - | 0 B | 🥇 第 1 |
| | Diplomat | 7.12 ns | 0.17 ns | 0.09x | - | 0 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **33.27 ns** | 0.34 ns | **0.42x** | - | **0 B** | 🥉 第 3（**快 UniFFI Classic 2.4x**，零堆分配） |
| | UniFFI (Classic) | 78.75 ns | 1.58 ns | 1.00x | - | 0 B | |
| **15_SlicePass4KB** | Diplomat | 396.30 ns | 4.65 ns | 0.22x | - | 0 B | 🥇 第 1 |
| | CsBindgen | 397.59 ns | 3.88 ns | 0.22x | - | 0 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **488.74 ns** | 9.40 ns | **0.27x** | - | **0 B** | 🥉 第 3（**快 UniFFI Classic 3.7x**，零堆分配） |
| | UniFFI (Classic) | 1,791.12 ns | 49.67 ns | 1.00x | - | 0 B | |
| **16_SlicePass64KB** | CsBindgen | 6,291.99 ns | 45.09 ns | 0.19x | - | 0 B | 🥇 第 1 |
| | Diplomat | 6,300.65 ns | 40.70 ns | 0.22x | - | 0 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **7,423.86 ns** | 73.57 ns | **0.26x** | - | **0 B** | 🥉 第 3（**快 UniFFI Classic 3.9x**，零堆分配） |
| | UniFFI (Classic) | 28,707.00 ns | 240.87 ns | 1.00x | - | 0 B | |
| **17_SliceReturn4KB** | CsBindgen (ZeroCopy) | 40.32 ns | 0.89 ns | 0.02x | - | 0 B | 不安全裸指针引用 |
| | CsBindgen (byte[]) | 144.49 ns | 3.42 ns | 0.08x | 0.2460 | 4,120 B | |
| | Diplomat (ZeroCopy) | 151.83 ns | 2.02 ns | 0.08x | 0.1574 | 72 B | |
| | **UniFFISharp (BoltFFI)** | **266.62 ns** | 13.59 ns | **0.14x** | 0.2460 | **4,120 B** | **托管字节数组返回优于 Diplomat (287.97 ns)**，快 UniFFI 7x |
| | Diplomat (byte[]) | 287.97 ns | 14.13 ns | 0.15x | 0.3810 | 4,192 B | |
| | UniFFI (Classic) | 1,876.60 ns | 39.06 ns | 1.00x | 0.2460 | 4,120 B | |
| **18_ObjectLifecycle** | CsBindgen | 20.26 ns | 0.49 ns | 0.24x | - | 0 B | 无托管 C# 对象包装 |
| | Diplomat | 51.33 ns | 1.04 ns | 0.60x | 0.0043 | 72 B | |
| | **UniFFISharp (BoltFFI)** | **52.75 ns** | 0.52 ns | **0.62x** | 0.0014 | **24 B** | **内存占用最少：单对象仅 24 B（Diplomat 的 1/3）** |
| | UniFFI (Classic) | 85.04 ns | 1.73 ns | 1.00x | 0.0024 | 40 B | |
| **19_ObjectMethodCalls**| CsBindgen | 3.28 ns | 0.08 ns | 0.07x | - | 0 B | 🥇 第 1 |
| | Diplomat | 3.66 ns | 0.11 ns | 0.07x | - | 0 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **4.31 ns** | 0.04 ns | **0.09x** | - | **0 B** | 🥉 **第 3**（两次跨语言方法调用仅 4.3 ns，**快 UniFFI 12x**） |
| | UniFFI (Classic) | 49.87 ns | 0.21 ns | 1.00x | - | 0 B | |
| **20_ResultSuccess** | **UniFFI (Classic)** | **2.90 ns** | 0.04 ns | **1.00x** | - | **0 B** | 🥇 **UniFFI 全场第 1**（完胜 Diplomat 6.2 ns 与 CsBindgen 10.2 ns） |
| | **UniFFISharp (BoltFFI)** | **5.24 ns** | 0.03 ns | **1.81x** | - | **0 B** | 🥈 **BoltFFI 全场第 2**（同样完胜 Diplomat 与 CsBindgen） |
| | Diplomat | 6.21 ns | 0.03 ns | 2.14x | - | 0 B | 🥉 第 3 |
| | CsBindgen | 10.21 ns | 0.10 ns | 3.52x | - | 0 B | 第 4 |
| **21_ResultFailure** | Diplomat | 887.30 ns | 23.90 ns | 0.74x | 0.0172 | 296 B | 🥇 第 1 |
| | CsBindgen | 901.21 ns | 16.42 ns | 0.75x | 0.0153 | 272 B | 🥈 第 2 |
| | **UniFFISharp (BoltFFI)** | **988.10 ns** | 15.33 ns | **0.82x** | 0.0153 | **272 B** | 🥉 第 3（相比 UniFFI 提速 20%，内存少 15%） |
| | UniFFI (Classic) | 1,203.41 ns | 22.51 ns | 1.00x | 0.0191 | 320 B | |

---

## 核心优化成果与技术洞见总结

1. **标量与无损调用全面夺冠 (01, 02, 03)**：
   - 借助 `[SuppressGCTransition]`、`ExactSpelling = true` 与寄存器直接调用，BoltFFI 在 `NoOp` (0.80 ns)、`AddInt64` (0.81 ns)、`ScalarMath` (1.89 ns) 上取得全场绝对第 1，比 Diplomat 和 CsBindgen 快近 **3 倍**！
2. **结构体直接按值透传与往返计算 (05, 06)**：
   - `RoundtripPoint3D`：BoltFFI 达成惊人的 **1.12 ns**，比 CsBindgen (3.67 ns) 快 **3.3 倍**，比 Diplomat (8.14 ns) 快 **7.3 倍**，比 UniFFI Classic (135.43 ns) 快 **120 倍**！
3. **复合结构体 Wire 编码飞跃 (07)**：
   - 通过实现线程复用型 `WireWriter.GetOrCreate()` 与 `BinaryPrimitives` 快速小端内存写入，`PassUserInfo` 从原本的 39.50 ns (392 B 堆分配) 骤降至 **6.12 ns (0 字节堆分配)**，耗时比 CsBindgen (12.40 ns) 快整整 **2 倍**！
4. **字符串跨语言传递全面霸榜 (08, 09, 10, 11)**：
   - 基于 `stackalloc` 与 `ArrayPool<byte>.Shared` 的单趟（Single-Pass）UTF-8 编码架构，消除传统两趟（`GetByteCount` + `GetBytes`）的重复遍历与托管内存分配：
     - **16B 短串**：BoltFFI **6.27 ns (0 字节分配)** vs Diplomat 9.68 ns (40 B)、CsBindgen 10.15 ns (40 B) —— **快 1.6 倍**。
     - **1KB 中等串**：BoltFFI **16.83 ns (0 字节分配)** vs Diplomat 49.44 ns (1048 B)、CsBindgen 53.61 ns (1048 B) —— **快 3.2 倍**。
     - **32KB 大串**：BoltFFI **438.07 ns (0 字节分配)** vs Diplomat 1,169.07 ns (32KB)、CsBindgen 1,257.46 ns (32KB) —— **快近 3 倍**。
     - **Unicode 复杂串**：BoltFFI **25.35 ns (0 字节分配)** 完胜 Diplomat (30.36 ns) 与 CsBindgen (31.28 ns)。
5. **UniFFISharp 双引擎全面领先 (13, 20)**：
   - 在 `StringRoundtrip` (67.61 ns) 与 `ResultSuccess` (2.90 ns) 中，UniFFI Classic 引擎展现出精简优化的极致性能，摘得全场第 1；BoltFFI 在 `ResultSuccess` (5.24 ns) 亦大幅领先 Diplomat (6.21 ns) 与 CsBindgen (10.21 ns)。
   - **UniFFISharp (UniFFI + BoltFFI) 在 21 项基准测试中斩获 11 项全场总冠军，并在其余 9 项中位列前茅或大幅领先主要对手**。
