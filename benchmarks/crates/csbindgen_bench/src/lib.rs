#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct Point3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

#[repr(C)]
#[derive(Clone, Copy, Debug)]
pub struct UserInfo {
    pub id: u64,
    pub age: u32,
    pub score: f64,
    pub is_active: bool,
}

#[repr(C)]
pub struct ByteBuffer {
    pub ptr: *mut u8,
    pub len: usize,
    pub cap: usize,
}

#[repr(C)]
pub struct CsResultInt64 {
    pub is_ok: bool,
    pub ok: i64,
    pub err_code: i32,
}

pub struct BenchCounter {
    pub val: u64,
}

// 1. Primitives
#[no_mangle]
pub extern "C" fn cs_bench_noop() {}

#[no_mangle]
pub extern "C" fn cs_bench_add_i64(a: i64, b: i64) -> i64 {
    a + b
}

#[no_mangle]
pub extern "C" fn cs_bench_scalar_math(a: i32, b: i64, c: f32, d: f64, flag: bool) -> f64 {
    let base = (a as f64) + (b as f64) + (c as f64) + d;
    if flag {
        base * 2.0
    } else {
        base
    }
}

// 2. Strings
#[no_mangle]
pub unsafe extern "C" fn cs_bench_string_length(ptr: *const u8, len: usize) -> u64 {
    len as u64
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_string_greeting(ptr: *const u8, len: usize) -> ByteBuffer {
    let name = std::str::from_utf8_unchecked(std::slice::from_raw_parts(ptr, len));
    let mut s = format!("Hello, {}!", name).into_bytes();
    let buf = ByteBuffer {
        ptr: s.as_mut_ptr(),
        len: s.len(),
        cap: s.capacity(),
    };
    std::mem::forget(s);
    buf
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_string_roundtrip(ptr: *const u8, len: usize) -> ByteBuffer {
    let text = std::str::from_utf8_unchecked(std::slice::from_raw_parts(ptr, len));
    let mut s = format!("echo: {}", text).into_bytes();
    let buf = ByteBuffer {
        ptr: s.as_mut_ptr(),
        len: s.len(),
        cap: s.capacity(),
    };
    std::mem::forget(s);
    buf
}

// 3. Byte Buffers
#[no_mangle]
pub unsafe extern "C" fn cs_bench_sum_bytes(ptr: *const u8, len: usize) -> u64 {
    let slice = std::slice::from_raw_parts(ptr, len);
    slice.iter().map(|&x| x as u64).sum()
}

#[no_mangle]
pub extern "C" fn cs_bench_generate_bytes(size: usize) -> ByteBuffer {
    let mut v = vec![42u8; size];
    let buf = ByteBuffer {
        ptr: v.as_mut_ptr(),
        len: v.len(),
        cap: v.capacity(),
    };
    std::mem::forget(v);
    buf
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_free_byte_buffer(buf: ByteBuffer) {
    if !buf.ptr.is_null() && buf.cap > 0 {
        let _ = Vec::from_raw_parts(buf.ptr, buf.len, buf.cap);
    }
}

// 4. Structs
#[no_mangle]
pub extern "C" fn cs_bench_pass_point(p: Point3D) -> f64 {
    p.x * p.x + p.y * p.y + p.z * p.z
}

#[no_mangle]
pub extern "C" fn cs_bench_roundtrip_point(p: Point3D, dx: f64, dy: f64, dz: f64) -> Point3D {
    Point3D {
        x: p.x + dx,
        y: p.y + dy,
        z: p.z + dz,
    }
}

#[no_mangle]
pub extern "C" fn cs_bench_pass_user_info(u: UserInfo) -> u64 {
    if u.is_active {
        u.id + (u.age as u64) + (u.score as u64)
    } else {
        u.id
    }
}

// 5. Objects
#[no_mangle]
pub extern "C" fn cs_bench_counter_new(val: u64) -> *mut BenchCounter {
    Box::into_raw(Box::new(BenchCounter { val }))
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_counter_increment(ptr: *mut BenchCounter, amount: u64) {
    if let Some(counter) = ptr.as_mut() {
        counter.val += amount;
    }
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_counter_get(ptr: *const BenchCounter) -> u64 {
    if let Some(counter) = ptr.as_ref() {
        counter.val
    } else {
        0
    }
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_counter_destroy(ptr: *mut BenchCounter) {
    if !ptr.is_null() {
        drop(Box::from_raw(ptr));
    }
}

// 6. Errors & Results
#[no_mangle]
pub extern "C" fn cs_bench_parse_positive(val: i64) -> CsResultInt64 {
    if val >= 0 {
        CsResultInt64 {
            is_ok: true,
            ok: val * 2,
            err_code: 0,
        }
    } else {
        CsResultInt64 {
            is_ok: false,
            ok: 0,
            err_code: 1,
        }
    }
}

// 7. Callbacks
#[no_mangle]
pub unsafe extern "C" fn cs_bench_invoke_callback(cb: extern "C" fn(i64) -> i64, val: i64) -> i64 {
    cb(val)
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_trigger_progress(cb: extern "C" fn(u64, u64, f64), times: u32) {
    for i in 1..=times {
        let cur = i as u64;
        let tot = times as u64;
        cb(cur, tot, (cur as f64) / (tot as f64) * 100.0);
    }
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_invoke_callback_context(
    cb: extern "C" fn(i64, *mut std::ffi::c_void) -> i64,
    context: *mut std::ffi::c_void,
    val: i64,
) -> i64 {
    cb(val, context)
}

#[no_mangle]
pub unsafe extern "C" fn cs_bench_trigger_progress_context(
    cb: extern "C" fn(u64, u64, f64, *mut std::ffi::c_void),
    context: *mut std::ffi::c_void,
    times: u32,
) {
    for i in 1..=times {
        let cur = i as u64;
        let tot = times as u64;
        cb(cur, tot, (cur as f64) / (tot as f64) * 100.0, context);
    }
}


// 8. Large Buffer
#[no_mangle]
pub unsafe extern "C" fn cs_bench_process_large_buffer(ptr: *mut u8, len: usize) {
    let slice = std::slice::from_raw_parts_mut(ptr, len);
    for x in slice {
        *x ^= 0x2A;
    }
}

// 9. Async Task completion via callback
#[no_mangle]
pub unsafe extern "C" fn cs_bench_async_compute(input: u64, delay_micros: u64, context: *mut std::ffi::c_void, on_complete: extern "C" fn(*mut std::ffi::c_void, u64)) {
    if delay_micros > 0 {
        std::thread::sleep(std::time::Duration::from_micros(delay_micros));
    }
    on_complete(context, input * 2);
}

