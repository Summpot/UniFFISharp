use interoptopus::{builtins_string, builtins_vec, callback, ffi, function, service, inventory::RustInventory, pattern::asynk::Async, AsyncRuntime};
use interoptopus::rt::Tokio;

// -----------------------------------------------------------------------------
// 1. Errors & Primitive Structs
// -----------------------------------------------------------------------------

#[ffi]
#[derive(Copy, Clone, Debug, PartialEq, Eq)]
pub enum FfiError {
    Ok = 0,
    InvalidValue = 1,
}

#[ffi]
#[derive(Copy, Clone, Debug)]
#[repr(C)]
pub struct Point3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

#[ffi]
#[derive(Copy, Clone, Debug)]
#[repr(C)]
pub struct UserInfo {
    pub id: u64,
    pub age: u32,
    pub score: f64,
    pub is_active: bool,
}

// -----------------------------------------------------------------------------
// 2. Primitives
// -----------------------------------------------------------------------------

#[ffi]
pub fn interop_bench_noop() {}

#[ffi]
pub fn interop_bench_add_i64(a: i64, b: i64) -> i64 {
    a + b
}

#[ffi]
pub fn interop_bench_scalar_math(a: i32, b: i64, c: f32, d: f64, flag: bool) -> f64 {
    let base = (a as f64) + (b as f64) + (c as f64) + d;
    if flag {
        base * 2.0
    } else {
        base
    }
}

// -----------------------------------------------------------------------------
// 3. Structs
// -----------------------------------------------------------------------------

#[ffi]
pub fn interop_bench_pass_point(p: Point3D) -> f64 {
    p.x * p.x + p.y * p.y + p.z * p.z
}

#[ffi]
pub fn interop_bench_roundtrip_point(p: Point3D, dx: f64, dy: f64, dz: f64) -> Point3D {
    Point3D {
        x: p.x + dx,
        y: p.y + dy,
        z: p.z + dz,
    }
}

#[ffi]
pub fn interop_bench_pass_user_info(u: UserInfo) -> u64 {
    if u.is_active {
        u.id + (u.age as u64) + (u.score as u64)
    } else {
        u.id
    }
}

// -----------------------------------------------------------------------------
// 4. Strings & Buffers
// -----------------------------------------------------------------------------

#[ffi]
pub fn interop_bench_string_length(s: ffi::String) -> u64 {
    s.as_str().len() as u64
}

#[ffi]
pub fn interop_bench_string_greeting(s: ffi::String) -> ffi::String {
    ffi::String::from_string(format!("Hello, {}!", s.as_str()))
}

#[ffi]
pub fn interop_bench_string_roundtrip(s: ffi::String) -> ffi::String {
    let mut res = String::with_capacity(s.as_str().len() + 8);
    res.push_str("echo: ");
    res.push_str(s.as_str());
    ffi::String::from_string(res)
}

#[ffi]
pub fn interop_bench_sum_bytes(slice: ffi::Slice<u8>) -> u64 {
    slice.as_slice().iter().map(|&x| x as u64).sum()
}

#[ffi]
pub fn interop_bench_generate_bytes(size: u64) -> ffi::Vec<u8> {
    ffi::Vec::from_vec(vec![42u8; size as usize])
}

#[ffi]
pub fn interop_bench_process_large_buffer(data: ffi::Vec<u8>) -> ffi::Vec<u8> {
    let mut v = data.into_vec();
    for x in &mut v {
        *x ^= 0x2A;
    }
    ffi::Vec::from_vec(v)
}

// -----------------------------------------------------------------------------
// 5. Callbacks
// -----------------------------------------------------------------------------

callback!(BenchSimpleCallback(val: i64) -> i64);

#[ffi]
pub fn interop_bench_invoke_callback(cb: BenchSimpleCallback, val: i64) -> i64 {
    cb.call(val)
}

callback!(BenchProgressCallback(current: u64, total: u64, percentage: f64));

#[ffi]
pub fn interop_bench_trigger_progress(cb: BenchProgressCallback, times: u32) {
    for i in 1..=times {
        let cur = i as u64;
        let tot = times as u64;
        cb.call(cur, tot, (cur as f64) / (tot as f64) * 100.0);
    }
}

// -----------------------------------------------------------------------------
// 6. Objects / Services
// -----------------------------------------------------------------------------

#[ffi(service)]
pub struct BenchCounter {
    val: std::sync::atomic::AtomicU64,
}

#[ffi]
impl BenchCounter {
    pub fn create(val: u64) -> ffi::Result<Self, FfiError> {
        ffi::Ok(Self {
            val: std::sync::atomic::AtomicU64::new(val),
        })
    }

    pub fn increment(&self, amount: u64) {
        self.val.fetch_add(amount, std::sync::atomic::Ordering::Relaxed);
    }

    pub fn get(&self) -> u64 {
        self.val.load(std::sync::atomic::Ordering::Relaxed)
    }
}

// -----------------------------------------------------------------------------
// 7. Results & Errors
// -----------------------------------------------------------------------------

#[ffi]
pub fn interop_bench_parse_positive(val: i64) -> ffi::Result<i64, FfiError> {
    if val >= 0 {
        ffi::Ok(val * 2)
    } else {
        ffi::Err(FfiError::InvalidValue)
    }
}

// -----------------------------------------------------------------------------
// 8. Async Service
// -----------------------------------------------------------------------------

#[ffi(service)]
#[derive(AsyncRuntime)]
pub struct BenchAsyncService {
    runtime: Tokio,
}

#[ffi]
impl BenchAsyncService {
    pub fn create() -> ffi::Result<Self, FfiError> {
        ffi::Ok(Self {
            runtime: Tokio::new(),
        })
    }

    pub async fn compute(_this: Async<Self>, input: u64, delay_micros: u64) -> ffi::Result<u64, FfiError> {
        if delay_micros > 0 {
            tokio::time::sleep(std::time::Duration::from_micros(delay_micros)).await;
        }
        ffi::Ok(input * 2)
    }
}

// -----------------------------------------------------------------------------
// Inventory
// -----------------------------------------------------------------------------

pub fn my_inventory() -> RustInventory {
    RustInventory::new()
        .register(builtins_string!())
        .register(builtins_vec!(u8))
        .register(function!(interop_bench_noop))
        .register(function!(interop_bench_add_i64))
        .register(function!(interop_bench_scalar_math))
        .register(function!(interop_bench_pass_point))
        .register(function!(interop_bench_roundtrip_point))
        .register(function!(interop_bench_pass_user_info))
        .register(function!(interop_bench_string_length))
        .register(function!(interop_bench_string_greeting))
        .register(function!(interop_bench_string_roundtrip))
        .register(function!(interop_bench_sum_bytes))
        .register(function!(interop_bench_generate_bytes))
        .register(function!(interop_bench_process_large_buffer))
        .register(function!(interop_bench_invoke_callback))
        .register(function!(interop_bench_trigger_progress))
        .register(function!(interop_bench_parse_positive))
        .register(service!(BenchCounter))
        .register(service!(BenchAsyncService))
        .validate()
}
