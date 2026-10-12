#[diplomat::config(lib_name = "diplomat_bench")]
struct Config;

#[diplomat::bridge]
pub mod ffi {
    use diplomat_runtime::DiplomatWrite;

    pub struct Point3D {
        pub x: f64,
        pub y: f64,
        pub z: f64,
    }

    pub struct UserInfo {
        pub id: u64,
        pub age: u32,
        pub score: f64,
        pub is_active: bool,
    }

    #[diplomat::opaque_mut]
    pub struct BenchCounter {
        val: u64,
    }

    pub enum BenchError {
        InvalidValue = 1,
    }

    impl BenchCounter {
        pub fn new(val: u64) -> Box<BenchCounter> {
            Box::new(BenchCounter { val })
        }

        pub fn increment(&mut self, amount: u64) {
            self.val += amount;
        }

        pub fn get(&self) -> u64 {
            self.val
        }
    }

    #[diplomat::opaque]
    pub struct BenchOps;

    impl BenchOps {
        // 1. Primitives
        pub fn bench_noop() {}

        pub fn bench_add_i64(a: i64, b: i64) -> i64 {
            a + b
        }

        pub fn bench_scalar_math(a: i32, b: i64, c: f32, d: f64, flag: bool) -> f64 {
            let base = (a as f64) + (b as f64) + (c as f64) + d;
            if flag {
                base * 2.0
            } else {
                base
            }
        }

        // 2. Strings
        pub fn bench_string_length(s: &str) -> u64 {
            s.len() as u64
        }

        pub fn bench_string_greeting(name: &str, write: &mut DiplomatWrite) {
            use std::fmt::Write;
            let _ = write!(write, "Hello, {}!", name);
        }

        pub fn bench_string_roundtrip(s: &str, write: &mut DiplomatWrite) {
            use std::fmt::Write;
            let _ = write!(write, "echo: {}", s);
        }

        // 3. Byte Buffers
        pub fn bench_sum_bytes(data: &[u8]) -> u64 {
            data.iter().map(|&x| x as u64).sum()
        }

        pub fn bench_generate_bytes(size: usize) -> Box<[u8]> {
            vec![42u8; size].into_boxed_slice()
        }

        // 4. Structs
        pub fn bench_pass_point(p: Point3D) -> f64 {
            p.x * p.x + p.y * p.y + p.z * p.z
        }

        pub fn bench_roundtrip_point(p: Point3D, dx: f64, dy: f64, dz: f64) -> Point3D {
            Point3D {
                x: p.x + dx,
                y: p.y + dy,
                z: p.z + dz,
            }
        }

        pub fn bench_pass_user_info(u: UserInfo) -> u64 {
            if u.is_active {
                u.id + (u.age as u64) + (u.score as u64)
            } else {
                u.id
            }
        }

        // 5. Results & Errors
        pub fn bench_parse_positive(val: i64) -> Result<i64, BenchError> {
            if val >= 0 {
                Ok(val * 2)
            } else {
                Err(BenchError::InvalidValue)
            }
        }
    }
}
