fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_class_name("CsBindgenNative")
        .csharp_namespace("CsBindgenBench")
        .csharp_dll_name("csbindgen_bench")
        .csharp_class_accessibility("public")
        .generate_csharp_file("../../UniFFISharp.Benchmark/Generated/CsBindgen/CsBindgenNative.g.cs")
        .unwrap();
}
