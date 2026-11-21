# CSharpSense — Roadmap

Each milestone has a letter ID, a scope, and acceptance criteria. Work proceeds in order. No milestone starts with failing tests from a prior one.

---

## M0 — Planning (this document) ✓ (approved)

**Deliverables:** CLAUDE.md, ARCHITECTURE.md, ROADMAP.md, LANGUAGE_FEATURES.md.  
**AC:** All four docs exist; architecture is reviewed and approved.

---

## M1 — LSP skeleton + TypeScript shim

**Goal:** A round-trip that VS Code can connect to. The server responds to `initialize` and returns empty completions for every `textDocument/completion` request.

**Scope:**
- `Protocol/`: JSON-RPC Content-Length reader (`JsonRpcReader.cs`) and writer (`JsonRpcWriter.cs`); `RpcMessage`, `RpcRequest`, `RpcResponse`, `RpcNotification` POCOs; `LspSerializer` using `System.Text.Json`.
- `Protocol/Lsp/`: `InitializeParams`, `InitializeResult`, `ServerCapabilities`, `CompletionList`, `CompletionItem`, `Position`, `Range`, `TextDocumentIdentifier`.
- `Server/Program.cs`: composition root; async dispatch loop over stdin/stdout; handle `initialize`, `initialized`, `shutdown`, `exit`, `textDocument/completion` (empty list).
- `client/src/extension.ts`: activate → spawn server process → `JsonRpcTransport` → register `CompletionItemProvider` that sends `textDocument/completion` and maps the response.
- `client/src/transport.ts`: Content-Length framing (read/write), request ID counter, pending-response map.

**Status:** done, except for one manual check (see below).

**AC:**
- [ ] Extension activates in VS Code without error. *(Not yet verified in a real VS Code host. Run `scripts/build.ps1`, open `client/` in VS Code, press F5.)*
- [ ] Opening a `.cs` file and triggering IntelliSense shows (empty) suggestion popup — no crash. *(Same manual check.)*
- [x] `shutdown` + `exit` cleanly terminates the server process (exit code 0; exit code 1 without a prior shutdown). Covered by `tests/Server.Tests/StdioEndToEndTests.cs` against the real executable.
- [x] Protocol tests pass: POCO round-trips, framing (multi-message, UTF-8 byte counting, 500 KB body, EOF), 20 concurrent writes.

**Notes:**
- The .NET 10 SDK creates `CSharpSense.slnx` (XML solution format), not `.sln`.
- The published server (`client/server/`) contains only `CSharpSense.Server` and `CSharpSense.Protocol`. It has no third-party assemblies.
- The header reader reads one byte at a time. That is fine for header-sized input. It will be revisited during the M12 profiling pass, but only if it shows up in traces.

---

## M2 — Lexer

**Goal:** Tokenize any C# source string into a complete, trivia-preserving token stream.

**Scope:**
- `Syntax/Lexer.cs`: single-pass, error-tolerant. Produces `SyntaxToken` (Kind, Text, LeadingTrivia, TrailingTrivia, Span).
- Token kinds: all C# keywords (versioned — e.g., `required` is contextual in C# 11+), identifiers, all numeric/string/char/interpolated/raw-string literals, all operators and punctuators, `#`-directives (preprocessor directives lexed as trivia), `//` and `/* */` comments (trivia), whitespace (trivia).
- Error token: an `ErrorToken` for unrecognised sequences; logs the character and position.
- Roundtrip invariant: `string.Concat(tokens.Select(t => t.FullText)) == sourceText`.

**AC:**
- [ ] Roundtrip invariant holds for every file in both fixture solutions.
- [ ] Golden-file tests for ≥ 20 representative inputs (keywords, interpolated strings, raw strings, verbatim strings, directives, edge cases).
- [ ] Error tokens never cause a crash; error positions are logged.
- [ ] Benchmark: lex 100k-line file in < 100 ms on a development machine.

---

## M3 — Parser (C# 6–12)

**Goal:** Parse all C# 6–12 constructs into an immutable syntax tree with error recovery.

**Scope:**
- `Syntax/Parser.cs`: recursive-descent, produces `SyntaxNode` trees (compilation unit → namespace → type → member → statement → expression).
- Error recovery: panic-mode — emit error node, skip to synchronisation point (`{`, `}`, `;`, declaration keyword), resume.
- Nodes for: compilation unit, namespace declarations (block + file-scoped), using directives (plain, alias, static, global), type declarations (class, struct, interface, enum, delegate, record), member declarations (field, property, method, constructor, destructor, operator, indexer, event, explicit interface impl), statement kinds (all C# 6–12), expression kinds (all C# 6–12 incl. switch expressions, ranges, indices, null-conditional, null-coalescing-assign, pattern matching through C# 11 `and`/`or`/`not`/list patterns).
- `ParseOptions`: `LangVersion` enum (C#6 through C#15); features unavailable at the specified version are parsed as error nodes.

**AC:**
- [ ] Golden-file tests for every node type (≥ 100 fixtures).
- [ ] Roundtrip invariant holds for all fixture files.
- [ ] Parse every `.cs` file in both fixture solutions without crash.
- [ ] Error recovery: parse deliberately broken files (missing braces, truncated methods) without infinite loop or stack overflow; error nodes appear, valid siblings still parse correctly.
- [ ] LANGUAGE_FEATURES.md updated: all C# 6–12 features marked `parsed`.

---

## M4 — Parser (C# 13–15)

**Goal:** Extend the parser to cover C# 13–15 syntax.

**Scope:** Incremental additions to `Syntax/Parser.cs`:
- C# 13: `params ReadOnlySpan<T>`, `allows ref struct` on type parameters, `field` keyword in accessors, `partial` properties and indexers, new `lock` object syntax.
- C# 14: Extension types (`extension` keyword), stable `field` keyword.
- C# 15: Track spec; implement what has shipped.

**AC:**
- [ ] Golden-file tests for each C# 13–15 feature.
- [ ] LANGUAGE_FEATURES.md updated: C# 13–15 features marked `parsed`.
- [ ] No regression in C# 6–12 golden files.

---

## M5 — Project system

**Goal:** Discover a `.sln` / `.csproj` solution and produce a `SolutionModel` with all project metadata.

**Scope:**
- `ProjectSystem/SlnParser.cs`: parse `.sln` (project entries, section data) and `.slnx` (XML format).
- `ProjectSystem/CsprojParser.cs`: parse `.csproj` XML; evaluate `$(Prop)` substitution and simple `Condition` attributes; collect `TargetFramework(s)`, `LangVersion`, `Nullable`, `ImplicitUsings`, `<Compile>`/`<None>` globs (default: `**/*.cs`), `<ProjectReference>`, `<PackageReference>`.
- `ProjectSystem/PropsReader.cs`: walk the directory tree upward looking for `Directory.Build.props/targets`; merge their properties into each project. Handle `<Import>`, `<Choose>/<When>/<Otherwise>`.
- `ProjectSystem/PackagesPropsReader.cs`: read `Directory.Packages.props` (central package management); merge versions into `PackageReference` items.
- `ProjectSystem/AssetsReader.cs`: parse `obj/project.assets.json`; extract the resolved package-to-assembly mapping for the chosen TFM.
- `ProjectSystem/SolutionWatcher.cs`: `FileSystemWatcher` on the root; invalidates `ProjectModel` for changed projects.

**AC:**
- [ ] Load both fixture solutions; assert correct `ProjectModel` for each project (target framework, lang version, source files, package references, project references).
- [ ] `Directory.Build.props` property inheritance tested.
- [ ] Central package management tested.
- [ ] `project.assets.json` fall-through to global-packages folder tested.
- [ ] Property conditions: `'$(Configuration)'=='Release'` evaluated correctly.

---

## M6 — Package index / metadata reader

**Goal:** Given a list of assembly paths, produce a searchable `TypeIndex` with types, members, and XML docs.

**Scope:**
- `PackageIndex/AssemblyReader.cs`: use `System.Reflection.Metadata.MetadataReader` to enumerate all public types, their members (methods, properties, fields, events), parameter types, return types, generics (constraints, variance), attributes (`Obsolete`, `Extension`, `NullableContext`, etc.). Produce `IndexedType` / `IndexedMember` immutable records.
- `PackageIndex/ExtensionMethodBucket.cs`: bucket extension methods by the name of the type they extend (simple name and fully-qualified). Used for fast extension method resolution.
- `PackageIndex/XmlDocReader.cs`: parse the companion `.xml` file; store the `<summary>`, `<param>`, `<returns>`, `<exception>` text per member key.
- `PackageIndex/TypeIndex.cs`: hash-map by FQN + simple name; prefix-search tree for partial name matching; `IEnumerable<IndexedMember> GetExtensionsFor(string typeName)`.
- Index the BCL (mscorlib/System.Runtime) + all packages from both fixtures on startup.

**AC:**
- [ ] Retrieve all public members of `System.String`, `System.Collections.Generic.List<T>`, `System.Linq.Enumerable`.
- [ ] Extension methods for `IEnumerable<T>` include all `Enumerable` LINQ operators.
- [ ] XML doc text retrieved for ≥ 10 BCL types.
- [ ] Index a 20-package fixture in < 2 s.
- [ ] Obsolete attribute detected and surfaced on `IndexedMember`.

---

## M7 — Binder and type resolver

**Goal:** Bind a full solution's symbol tables and resolve types for every expression in the fixture solutions.

**Scope:**
- `Semantics/SymbolTable.cs`: declaration pass. Namespace tree, type hierarchy, member dictionaries. Handle partial types, nested types, generic type definitions.
- `Semantics/Scope.cs`: lexical scope chain (file → type → method → block).
- `Semantics/Binder.cs`: use-site pass. For each `IdentifierNameSyntax`, `MemberAccessExpressionSyntax`, `InvocationExpressionSyntax`, `ObjectCreationExpressionSyntax` etc., resolve to an `ISymbol` and infer an `ITypeSymbol`. Handle:
  - `var` / `dynamic` type inference (from initialiser type).
  - Generic type argument inference (from call-site argument types).
  - Lambda parameter types (from delegate type context).
  - LINQ query syntax desugaring (`from x in src select ...` → method chain).
  - Null-conditional: `a?.b` has type `T?` if `a.b` has type `T`.
  - Pattern matching: `is T x` introduces `x : T` into scope.
  - Tuple deconstruction.
  - `out var` introduction.
  - Extension method resolution (last in chain, with type compatibility check).
  - `using static` members.
  - Global and implicit usings from project settings.
- `Semantics/CompletionContext.cs`: given a cursor position in a syntax tree and bound tree, classify the completion context: MemberAccess, NameResolution, UsingDirective, Override, Implement, ArgumentLabel, AttributeName, PatternMatch, TypeConstraint, GenericArgument, InterpolatedString, Directive.

**AC:**
- [ ] Bind every file in both fixture solutions; zero unresolved symbols for in-solution code (non-generated).
- [ ] `var` inference correct for primitive assignments, collection initializers, LINQ queries, `new` expressions.
- [ ] Extension method resolution correct for `IEnumerable<T>` LINQ operators.
- [ ] Generic inference correct for `List<T>.Add(T)`, `Dictionary<K,V>.TryGetValue`.
- [ ] Pattern matching introduces correct local type into scope.
- [ ] LINQ query syntax desugars correctly.
- [ ] Completion context classified correctly for ≥ 50 hand-written position test cases.

---

## M8 — Completion MVP

**Goal:** Return semantically meaningful completions from the LSP endpoint. Top-1 ≥ 60% on the cursor-masking benchmark (train+dev).

**Scope:**
- `Server/Handlers/CompletionHandler.cs`: on `textDocument/completion`, get the `CompletionContext`, query Semantics for candidates, produce `CompletionItem[]` with label, kind, detail, documentation.
- Member access: return all accessible members of the resolved type (filtered by accessibility, `LangVersion`).
- Name resolution: return all in-scope symbols matching the prefix typed so far.
- Using directive: return namespace names from the package index + project.
- Override/implement: return unimplemented base members with generated signature stub.
- Insert text: smart snippet for method completions (parameter placeholders via `$0`/`$1`).
- Wire `textDocument/didOpen` / `textDocument/didChange` / `textDocument/didClose` into the Workspace.
- Wire `workspace/didChangeWatchedFiles` to re-parse csproj/props changes.
- Basic ranking: type compatibility + scope proximity only (full ranking is M10).

**AC:**
- [ ] Member completion after `.` on a typed variable returns the correct member set.
- [ ] Completion inside `new ClassName(` returns accessible constructors.
- [ ] `using` completion returns valid namespace names.
- [ ] Override completion generates a syntactically correct stub.
- [ ] No inaccessible (private/internal cross-assembly) members appear.
- [ ] Top-1 ≥ 60% on cursor-masking benchmark (train+dev); report saved.

---

## M9 — EF module

**Goal:** Context-aware completions inside EF Core LINQ chains, DbContext access, and model builder configurations.

**Scope:**
- `EfModel/EfContextDetector.cs`: find `DbContext` subclasses; enumerate `DbSet<T>` properties; resolve entity types.
- `EfModel/EntityShapeBuilder.cs`: for each entity type, enumerate properties, navigation properties (using type graph + `[ForeignKey]`/`[InverseProperty]` + Fluent API calls in `OnModelCreating`).
- `EfModel/EfCompletionProvider.cs`: called by `CompletionHandler` when the context is inside an EF chain. Maps LINQ call chain shapes to completion sets:
  - `.Include(x => x.` → navigation properties of `T`.
  - `.ThenInclude(x => x.` → next-level navigation.
  - `.Where(x => x.` → all properties of `T`.
  - `.Select(x => new { x.` → all properties.
  - `modelBuilder.Entity<T>().` → Fluent API methods.
  - `migration.CreateTable(` → known migration builder methods.
- EF fixture solution (`fixtures/CleanArchEf/`) covers all patterns.

**AC:**
- [ ] Include/ThenInclude completions return correct navigation properties for the entity type.
- [ ] `modelBuilder.Entity<T>().HasKey(` prompts for a property selector lambda.
- [ ] No non-navigation properties appear in ThenInclude completions.
- [ ] All EF fixture test cases pass.

---

## M10 — Ranking and explain mode

**Goal:** All ranking signals implemented; explain mode prints per-signal contributions; Top-1 ≥ 75% on benchmark.

**Scope:**
- `Ranking/Signals/`: one class per signal (TypeCompatibility, ScopeProximity, UsageFrequency, ArchitectureLayer, PackageFrequency, LangVersionAvailability, ObsoleteStatus, KnowledgePack, EfContext).
- `Ranking/RankingEngine.cs`: weighted sum; exposed coefficients in `Ranking/Weights.cs`.
- `Ranking/RankingExplanation.cs`: per-item explanation DTO: `{ signal: string, raw: float, contribution: float }[]`.
- `Server/Handlers/CompletionHandler.cs`: when query param `_explain=true` is present (or via a VS Code command), attach `RankingExplanation` to each `CompletionItem.data`.
- `Ranking/UsageCollector.cs`: walk the BoundForest to count per-symbol usage frequencies in the current solution.

**AC:**
- [ ] Each signal has a unit test with known inputs and expected scores.
- [ ] Explain mode output is human-readable and shows all 9 signals.
- [ ] Top-1 ≥ 75% on cursor-masking benchmark (train+dev).
- [ ] No category regresses by > 2 points vs M8 baseline.
- [ ] Obsolete symbols never appear in Top-3 when non-obsolete alternatives exist.

---

## M11 — Knowledge pack

**Goal:** Offline CLI produces a `data/knowledge.json` that the server loads, boosting completions for well-known patterns.

**Scope:**
- `tools/KnowledgePack/KnowledgeCollector.cs`: offline CLI. Reads curated source files (Microsoft docs snippets, hand-written patterns). Produces `data/knowledge.json` from pattern definitions (no network at runtime).
- `Knowledge/KnowledgeStore.cs`: load `knowledge.json` on startup; expose `float GetBoost(string contextPattern, string symbolFqn)`.
- `Ranking/Signals/KnowledgePack.cs`: query `KnowledgeStore`.
- Seed the knowledge pack with ≥ 50 patterns covering: `HttpClient` usage, `ILogger` calls, `IOptions<T>`, common ASP.NET Core middleware, EF Core idioms, `IDisposable`/`IAsyncDisposable` patterns.

**AC:**
- [ ] Knowledge pack loads in < 50 ms.
- [ ] ≥ 50 patterns present; queried correctly.
- [ ] At least 5 benchmark positions improve Top-1 due to knowledge pack (documented in report).
- [ ] No regression in other categories.

---

## M12 — Performance and incremental updates

**Goal:** Warm p95 ≤ 150 ms, p99 ≤ 300 ms. Solution load within budget for a 50-project synthetic solution.

**Scope:**
- Incremental parse: only re-parse changed file; reuse unchanged `SyntaxTree` objects from cache.
- Incremental bind: file-level dependency graph; re-bind only files whose public API may be affected by the change.
- Pre-computed member lookup: cache `ITypeSymbol → IReadOnlyList<ISymbol>` per type; invalidate on package re-index only.
- Fast-path / refine split for large candidate sets: return first 20 candidates immediately (synchronous, < 10 ms), then refine in the background and push an updated list.
- Profile with stopwatch traces (`Diagnostics/Profiler.cs`); log p50/p95/p99 for each completion request to stderr in a parseable format.
- Generate a synthetic 50-project solution in `scripts/gen-large-solution` for latency testing.

**AC:**
- [ ] Warm p95 ≤ 150 ms, p99 ≤ 300 ms (measured over 1000 completions on the 50-project solution).
- [ ] Solution load: 50-project solution loaded in ≤ 15 s cold.
- [ ] No accuracy regression from M10 baseline (Top-1 within 0.5 pt).
- [ ] Memory: server steady-state ≤ 500 MB on the 50-project solution.

---

## M13 — Evaluation harness and continuous improvement loop

**Goal:** A single `scripts/eval` command that produces a reproducible benchmark report and supports the loop described in the mission spec.

**Scope:**
- `tests/Eval/`: .NET console app. Loads fixture solutions, generates cursor-masking positions (seeded, deterministic), calls completion engine, computes metrics.
- Metrics: Top-1/3/5 hit rate, MRR, absent rate, noise rate — per category (MemberAccess, NameResolution, Using, Override, Implement, ArgumentLabel, EfChain, PatternMatch) and overall.
- Latency: in-process and stdio round-trip; p50/p95/p99.
- Output: `eval/results-<timestamp>.json` and `eval/report-<timestamp>.md`.
- Baseline diff: `--baseline <path>` shows metric deltas; flags regressions > 0.5 pt.
- Holdout gate: `--holdout` runs the held-out split; used only at end of an experiment batch.
- `docs/EXPERIMENTS.md`: template for hypothesis → change → result entries.

**AC:**
- [ ] `scripts/eval` runs end-to-end without manual steps.
- [ ] Produces JSON + Markdown report.
- [ ] Baseline diff correctly flags regressions.
- [ ] Holdout results are withheld from normal runs.
- [ ] Report matches the continuous improvement loop spec from the mission.

---

## Post-M13: Continuous improvement loop

Refer to the continuous improvement loop specification. Iterate: eval → error analysis → hypothesis → implement → eval → accept/revert → commit. Stop conditions and reporting as specified.
