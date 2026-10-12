uniffi::setup_scaffolding!();

#[derive(uniffi::Record, Clone, Copy, Debug)]
#[boltffi::data]
pub struct Point3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

#[derive(uniffi::Record, Clone, Copy, Debug)]
#[boltffi::data]
pub struct UserInfo {
    pub id: u64,
    pub age: u32,
    pub score: f64,
    pub is_active: bool,
}

// 1. Primitives
#[uniffi::export]
#[boltffi::export]
pub fn bench_noop() {}

#[uniffi::export]
#[boltffi::export]
pub fn bench_add_i64(a: i64, b: i64) -> i64 {
    a + b
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_scalar_math(a: i32, b: i64, c: f32, d: f64, flag: bool) -> f64 {
    let base = (a as f64) + (b as f64) + (c as f64) + d;
    if flag {
        base * 2.0
    } else {
        base
    }
}

// 2. Strings
#[uniffi::export]
#[boltffi::export]
pub fn bench_string_length(s: String) -> u64 {
    s.len() as u64
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_string_greeting(name: String) -> String {
    format!("Hello, {}!", name)
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_string_roundtrip(s: String) -> String {
    let mut res = String::with_capacity(s.len() + 8);
    res.push_str("echo: ");
    res.push_str(&s);
    res
}

// 3. Byte Buffers
#[uniffi::export]
#[boltffi::export]
pub fn bench_sum_bytes(data: Vec<u8>) -> u64 {
    data.iter().map(|&x| x as u64).sum()
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_generate_bytes(size: u64) -> Vec<u8> {
    vec![42u8; size as usize]
}

// 4. Structs / Records
#[uniffi::export]
#[boltffi::export]
pub fn bench_pass_point(p: Point3D) -> f64 {
    p.x * p.x + p.y * p.y + p.z * p.z
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_roundtrip_point(p: Point3D, dx: f64, dy: f64, dz: f64) -> Point3D {
    Point3D {
        x: p.x + dx,
        y: p.y + dy,
        z: p.z + dz,
    }
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_pass_user_info(u: UserInfo) -> u64 {
    if u.is_active {
        u.id + (u.age as u64) + (u.score as u64)
    } else {
        u.id
    }
}

// 5. Objects
#[derive(uniffi::Object)]
pub struct BenchCounter {
    val: std::sync::atomic::AtomicU64,
}

#[uniffi::export]
impl BenchCounter {
    #[uniffi::constructor]
    pub fn new(val: u64) -> std::sync::Arc<Self> {
        std::sync::Arc::new(Self {
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

pub struct BoltCounter {
    val: std::sync::atomic::AtomicU64,
}

#[boltffi::export]
impl BoltCounter {
    pub fn new(val: u64) -> Self {
        Self {
            val: std::sync::atomic::AtomicU64::new(val),
        }
    }

    pub fn increment(&self, amount: u64) {
        self.val.fetch_add(amount, std::sync::atomic::Ordering::Relaxed);
    }

    pub fn get(&self) -> u64 {
        self.val.load(std::sync::atomic::Ordering::Relaxed)
    }
}

// 6. Errors & Results
#[derive(Debug, thiserror::Error, uniffi::Error)]
pub enum BenchError {
    #[error("Invalid value")]
    InvalidValue,
}

#[uniffi::export]
pub fn bench_parse_positive(val: i64) -> Result<i64, BenchError> {
    if val >= 0 {
        Ok(val * 2)
    } else {
        Err(BenchError::InvalidValue)
    }
}

#[boltffi::export]
pub fn bolt_bench_parse_positive(val: i64) -> Result<i64, String> {
    if val >= 0 {
        Ok(val * 2)
    } else {
        Err("Invalid value".into())
    }
}
