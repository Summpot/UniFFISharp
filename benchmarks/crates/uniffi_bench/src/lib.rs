uniffi::setup_scaffolding!();

// -----------------------------------------------------------------------------
// 1. Primitive Structs
// -----------------------------------------------------------------------------

#[derive(uniffi::Record, Clone, Copy, Debug)]
#[repr(C)]
#[boltffi::data]
pub struct Point3D {
    pub x: f64,
    pub y: f64,
    pub z: f64,
}

#[derive(uniffi::Record, Clone, Copy, Debug)]
#[repr(C)]
#[boltffi::data]
pub struct UserInfo {
    pub id: u64,
    pub age: u32,
    pub score: f64,
    pub is_active: bool,
}

// -----------------------------------------------------------------------------
// 2. Micro Benchmark Primitives
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// 3. Strings & Slices
// -----------------------------------------------------------------------------

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

#[uniffi::export]
#[boltffi::export]
pub fn bench_process_large_buffer(mut data: Vec<u8>) -> Vec<u8> {
    for x in &mut data {
        *x ^= 0x2A;
    }
    data
}

// -----------------------------------------------------------------------------
// 4. Structs Passing
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// 5. Basic Objects (Counter)
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// 6. Errors & Results
// -----------------------------------------------------------------------------

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

// -----------------------------------------------------------------------------
// 7. Callbacks
// -----------------------------------------------------------------------------

#[uniffi::export(callback_interface)]
pub trait BenchSimpleCallback: Send + Sync {
    fn on_invoke(&self, value: i64) -> i64;
}

#[uniffi::export]
pub fn bench_invoke_callback(cb: Box<dyn BenchSimpleCallback>, value: i64) -> i64 {
    cb.on_invoke(value)
}

#[uniffi::export(callback_interface)]
pub trait BenchProgressCallback: Send + Sync {
    fn on_progress(&self, current: u64, total: u64, percentage: f64);
}

#[uniffi::export]
pub fn bench_trigger_progress(cb: Box<dyn BenchProgressCallback>, times: u32) {
    for i in 1..=times {
        let cur = i as u64;
        let tot = times as u64;
        cb.on_progress(cur, tot, (cur as f64) / (tot as f64) * 100.0);
    }
}

// -----------------------------------------------------------------------------
// 8. Complex Entities (Nested Records & Collections)
// -----------------------------------------------------------------------------

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
#[boltffi::data]
pub struct BenchNestedTag {
    pub id: u64,
    pub name: String,
    pub description: Option<String>,
}

#[derive(uniffi::Record, Clone, Debug, PartialEq)]
#[boltffi::data]
pub struct BenchComplexEntity {
    pub id: i64,
    pub title: String,
    pub status: i32,
    pub tags: Vec<BenchNestedTag>,
    pub attributes: Vec<String>,
    pub metadata_json: Option<String>,
    pub is_active: bool,
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_pass_complex_entities(entities: Vec<BenchComplexEntity>) -> u64 {
    let mut count = 0u64;
    for e in entities {
        count += e.tags.len() as u64 + e.attributes.len() as u64;
    }
    count
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_query_complex_entities(count: u32) -> Vec<BenchComplexEntity> {
    let mut list = Vec::with_capacity(count as usize);
    for i in 0..count {
        list.push(BenchComplexEntity {
            id: i as i64,
            title: format!("Entity_{i}"),
            status: (i % 5) as i32,
            tags: vec![
                BenchNestedTag { id: 1, name: "TagA".into(), description: Some("DescA".into()) },
                BenchNestedTag { id: 2, name: "TagB".into(), description: None },
            ],
            attributes: vec!["alpha".into(), "beta".into(), "gamma".into()],
            metadata_json: Some(format!("{{\"key\": {i}}}")),
            is_active: i % 2 == 0,
        });
    }
    list
}

// -----------------------------------------------------------------------------
// 9. Tagged Union (Algebraic Data Types / AST)
// -----------------------------------------------------------------------------

#[derive(uniffi::Enum, Clone, Debug, PartialEq)]
#[boltffi::data]
pub enum BenchTaggedUnion {
    Empty,
    Integer { value: i64 },
    Text { content: String },
    Pair { key: String, value: String },
    Nested { children: Vec<BenchTaggedUnion> },
}

#[uniffi::export]
#[boltffi::export]
pub fn bench_eval_tagged_union(node: BenchTaggedUnion) -> u64 {
    match node {
        BenchTaggedUnion::Empty => 0,
        BenchTaggedUnion::Integer { value } => value.unsigned_abs(),
        BenchTaggedUnion::Text { content } => content.len() as u64,
        BenchTaggedUnion::Pair { key, value } => (key.len() + value.len()) as u64,
        BenchTaggedUnion::Nested { children } => children.into_iter().map(bench_eval_tagged_union).sum(),
    }
}

// -----------------------------------------------------------------------------
// 10. Modern Runtime: Async & Streams
// -----------------------------------------------------------------------------

#[uniffi::export(async_runtime = "tokio")]
pub async fn bench_async_compute(input: u64, delay_micros: u64) -> u64 {
    if delay_micros > 0 {
        tokio::time::sleep(std::time::Duration::from_micros(delay_micros)).await;
    }
    input * 2
}

#[derive(uniffi::Object)]
pub struct BenchAsyncStreamEngine {
    total: u32,
    current: std::sync::atomic::AtomicU32,
    cancelled: std::sync::atomic::AtomicBool,
}

#[uniffi::export(async_runtime = "tokio")]
impl BenchAsyncStreamEngine {
    #[uniffi::constructor]
    pub fn new(total_items: u32) -> std::sync::Arc<Self> {
        std::sync::Arc::new(Self {
            total: total_items,
            current: std::sync::atomic::AtomicU32::new(0),
            cancelled: std::sync::atomic::AtomicBool::new(false),
        })
    }

    pub async fn next(&self) -> Option<BenchComplexEntity> {
        if self.cancelled.load(std::sync::atomic::Ordering::Relaxed) {
            return None;
        }
        let cur = self.current.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
        if cur >= self.total {
            return None;
        }
        Some(BenchComplexEntity {
            id: cur as i64,
            title: format!("StreamEntity_{cur}"),
            status: (cur % 3) as i32,
            tags: vec![BenchNestedTag { id: 10, name: "StreamTag".into(), description: None }],
            attributes: vec!["stream".into()],
            metadata_json: None,
            is_active: true,
        })
    }

    pub fn cancel(&self) {
        self.cancelled.store(true, std::sync::atomic::Ordering::Relaxed);
    }
}

// -----------------------------------------------------------------------------
// 11. Concurrency Shared Service
// -----------------------------------------------------------------------------

#[derive(uniffi::Object)]
pub struct BenchSharedService {
    state: std::sync::atomic::AtomicI64,
}

#[uniffi::export]
impl BenchSharedService {
    #[uniffi::constructor]
    pub fn new() -> std::sync::Arc<Self> {
        std::sync::Arc::new(Self {
            state: std::sync::atomic::AtomicI64::new(0),
        })
    }

    pub fn read_state(&self) -> i64 {
        self.state.load(std::sync::atomic::Ordering::Relaxed)
    }

    pub fn write_state(&self, delta: i64) -> i64 {
        self.state.fetch_add(delta, std::sync::atomic::Ordering::Relaxed) + delta
    }
}
