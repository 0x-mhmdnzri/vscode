# CSharpSense — Language Feature Matrix

Status values:
- `planned` — will be implemented in a future milestone
- `parsed` — syntax tree node exists; no semantic support yet
- `bound` — symbol/type resolution works
- `completion-aware` — completions fire correctly at relevant positions
- `not-supported` — explicitly out of scope (documented reason)

Milestone references: M2=Lexer, M3=Parser(6-12), M4=Parser(13-15), M7=Binder, M8=Completion, M9=EF, M10=Ranking.

---

## C# 6.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Expression-bodied members (methods, properties) | planned | M3 | |
| Null-conditional operators (`?.`, `?[]`) | planned | M3 / M7 | Type: `T?` when inner is `T` |
| String interpolation (`$"..."`) | planned | M2 / M3 | Lexer handles `$"` as special token; parser handles holes |
| `nameof` expression | planned | M3 / M7 | Resolves to string of the named symbol |
| `using static` | planned | M3 / M7 | Members imported into scope |
| Exception filters (`when`) | planned | M3 | |
| Auto-property initializers | planned | M3 | |
| Index initializers (`new D { [k] = v }`) | planned | M3 | |
| `await` in catch/finally | planned | M3 | |

---

## C# 7.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Tuples (`(T1 x, T2 y)`) | planned | M3 / M7 | `System.ValueTuple<>` backing; element names |
| Deconstruction (`var (a, b) = t`) | planned | M3 / M7 | Introduces named locals into scope |
| Local functions | planned | M3 / M7 | Captured variables; recursive |
| Pattern matching — `is` with type pattern | planned | M3 / M7 | Introduces scoped local |
| `out` variable declarations (`M(out var x)`) | planned | M3 / M7 | `x` in scope after the call |
| `ref` returns and `ref` locals | planned | M3 | Semantic: flag members as ref-returning |
| Throw expressions | planned | M3 | |
| Digit separators (`1_000_000`) | planned | M2 | Lexer |
| Binary literals (`0b1010`) | planned | M2 | Lexer |
| Async `Main` | planned | M3 | |

---

## C# 7.1

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| `default` literal expression | planned | M3 / M7 | Type-inferred `default` |
| Inferred tuple element names | planned | M3 / M7 | |
| Pattern matching with generics | planned | M7 | |
| `async Main` returning `Task` | planned | M3 | |

---

## C# 7.2

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| `private protected` accessibility | planned | M3 / M7 | |
| Non-trailing named arguments | planned | M3 | |
| Leading `_` digit separator | planned | M2 | Lexer |
| `ref struct` | planned | M3 / M7 | Struct restricted to stack |
| `in` parameter modifier | planned | M3 | |
| `readonly struct` | planned | M3 | |

---

## C# 7.3

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| `unmanaged` / `enum` / `delegate` generic constraints | planned | M3 / M7 | |
| `stackalloc` with initializers | planned | M3 | |
| `ref` local reassignment | planned | M3 | |
| Attributes on backing field of auto-properties | planned | M3 | |
| `fixed` on any supported type | planned | M3 | |

---

## C# 8.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Nullable reference types (`?` annotations, `#nullable`) | planned | M3 / M7 | NRT flow analysis is best-effort; at minimum we track annotated nullability |
| Switch expressions (`x switch { ... }`) | planned | M3 / M7 | |
| Ranges and indices (`1..^1`, `^1`) | planned | M3 / M7 | `System.Range` / `System.Index` |
| Async streams (`IAsyncEnumerable<T>`, `await foreach`) | planned | M3 / M7 | |
| Default interface members | planned | M3 / M7 | Interface methods with bodies |
| Recursive patterns (`{ Prop: pattern }`) | planned | M3 / M7 | |
| `using` declarations (auto-dispose) | planned | M3 | |
| Null-coalescing assignment (`??=`) | planned | M3 | |
| Static local functions | planned | M3 | |
| `readonly` members in struct | planned | M3 | |
| Disposable `ref struct` | planned | M3 | |

---

## C# 9.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Records (`record class`) | planned | M3 / M7 | Synthesized Equals/GetHashCode/ToString/`with`; positional params |
| `init`-only properties | planned | M3 / M7 | |
| Top-level statements | planned | M3 | Implicit `Main` wrapper |
| Pattern matching enhancements (`and`, `or`, `not`) | planned | M3 / M7 | |
| Target-typed `new` expressions | planned | M3 / M7 | Type inferred from context |
| Function pointers (`delegate*`) | planned | M3 | Semantic: limited — parse + represent; no call completion |
| Covariant return types | planned | M7 | |
| Native-sized integers (`nint`, `nuint`) | planned | M3 / M7 | |
| Source generators (output consumed) | planned | M6 | We read the generated source files if they appear in `obj/` |

---

## C# 10.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Record structs (`record struct`) | planned | M3 / M7 | |
| Global `using` directives | planned | M3 / M5 | Collected from project; applied to all files |
| File-scoped namespace declaration | planned | M3 | |
| Extended property patterns (`{ A.B: v }`) | planned | M3 / M7 | |
| `const` interpolated strings | planned | M3 | |
| Lambda attributes and return type annotations | planned | M3 / M7 | |
| `with` on structs and anonymous types | planned | M3 / M7 | |
| Caller argument expression attribute | planned | M3 | |
| Improved definite assignment | planned | M7 | Best-effort |

---

## C# 11.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Generic attributes | planned | M3 / M7 | `[Attr<T>]` |
| `required` member modifier | planned | M3 / M7 | |
| File-local types (`file` modifier) | planned | M3 / M7 | Scoped to the declaring file |
| Raw string literals (`"""..."""`) | planned | M2 / M3 | Lexer handles multi-line raw strings |
| List patterns (`[a, b, ..]`) | planned | M3 / M7 | |
| Checked user-defined operators | planned | M3 | |
| Static abstract / virtual members in interfaces | planned | M3 / M7 | |
| UTF-8 string literals (`"..."u8`) | planned | M2 | Lexer suffix |
| Newlines in interpolated string holes | planned | M2 / M3 | Lexer adjustment |
| `scoped` parameter/local modifier | planned | M3 | |
| `nint`/`nuint` as numeric types | planned | M7 | Already from C#9; full arithmetic model M7 |

---

## C# 12.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Primary constructors (classes and structs) | planned | M3 / M7 | Parameters captured as fields; completion inside body |
| Collection expressions (`[1, 2, 3]`) | planned | M3 / M7 | Target-typed |
| Inline arrays | planned | M3 | |
| Default lambda parameters | planned | M3 / M7 | |
| `using` alias for any type (including tuples, arrays) | planned | M3 | |
| Experimental interceptors | not-supported | — | Compile-time source generation mechanism; no completion benefit |

---

## C# 13.0

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| `params ReadOnlySpan<T>` | planned | M4 / M7 | |
| `allows ref struct` type parameter constraint | planned | M4 / M7 | |
| `field` keyword in accessors | planned | M4 / M7 | Refers to compiler-synthesised backing field |
| `partial` properties and indexers | planned | M4 / M7 | |
| New `lock` object (`System.Threading.Lock`) | planned | M4 / M7 | |
| Escape sequence `\e` | planned | M2 (update) | Lexer |

---

## C# 14.0 (preview / RC as of knowledge cutoff)

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| Extension types (`extension` keyword) | planned | M4 | Will be extended once spec stabilises |
| `field` keyword stable | planned | M4 | Merged from C#13 preview |
| Implicit span conversions | planned | M4 / M7 | |

> C# 14 features will be tracked as the spec finalises. The parser will emit `ParsedPreview` nodes for unstable syntax until stabilisation.

---

## C# 15.0 (future)

| Feature | Status | Milestone | Notes |
|---------|--------|-----------|-------|
| (TBD) | planned | M4 update | Will be added as features ship |

---

## Explicitly not supported

| Feature | Reason |
|---------|--------|
| `unsafe` code / pointer arithmetic | Out of scope for completion; we parse it but no semantic support |
| Preprocessor macro evaluation | We track `#if`/`#else`/`#endif` as trivia; we do not evaluate conditions (take both branches) |
| Dynamic dispatch (`dynamic`) | Type-erased at compile time; we treat as `object` for completion |
| COM interop attributes | No completion value; parsed, not bound |
| Source generators (authoring) | We consume generator output; we do not generate or analyse generator code |
| Interceptors | Experimental, complex, negligible completion surface |
| `__arglist` / `__makeref` | Undocumented; not in C# spec |
