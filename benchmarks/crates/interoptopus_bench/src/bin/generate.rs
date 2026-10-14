use interoptopus_csharp::dispatch::Dispatch;
use interoptopus_csharp::RustLibrary;
use std::path::Path;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let inventory = interoptopus_bench::my_inventory();

    let out_dir = Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../../UniFFISharp.Benchmark/Generated/Interoptopus");
    std::fs::create_dir_all(&out_dir)?;

    RustLibrary::builder(inventory)
        .dll_name("interoptopus_bench")
        .dispatch(Dispatch::single_file("InteroptopusBench"))
        .build()
        .process()?
        .write_buffers_to(&out_dir)?;

    println!("Interoptopus C# bindings generated successfully at: {:?}", out_dir);
    Ok(())
}
