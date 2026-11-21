# CSharpSense — Architecture

## Guiding principles

1. **Correct before fast.** A binder that resolves types accurately lifts all categories at once; weight-tuning only shuffles. Build the semantic model right.
2. **Incremental by design.** Every layer defines what it depends on and what must be invalidated on a file change, so we never rebuild more than we have to.
3. **Explicit failure.** No layer silently returns a wrong answer. `BindResult`, `ResolveResult`, etc., are discriminated unions that force callers to handle the unresolved case.
4. **Zero runtime deps.** All logic is BCL + self-written. No Roslyn, no OmniSharp, no NuGet packages in published artifacts.

---

## Layer map and dependency direction

```
┌──────────────────────────────────────────────────────────────┐
│  Server  (entry point, DI wiring, stdio loop)                │
└────────────────┬─────────────────────────────────────────────┘
                 │
     ┌───────────▼──────────┐
     │  Protocol             │  JSON-RPC 2.0 / LSP types, framing
     └───────────┬──────────┘
                 │
     ┌───────────▼──────────┐
     │  Ranking              │  scores + explain mode
     └──┬────┬──────┬───────┘
        │    │      │
        │  ┌─▼──┐ ┌─▼──────────┐
        │  │ EF │ │ Knowledge  │
        │  │Mdl │ │ (pack)     │
        │  └─┬──┘ └────────────┘
        │    │
   ┌────▼────▼─────────────────────┐
   │  Semantics                    │  binder, type resolver, symbol tables
   └────────┬──────────────────────┘
            │
   ┌────────▼──────────┐  ┌──────────────────┐
   │  Syntax            │  │  ProjectSystem    │
   │  (lexer+parser)    │  │  (sln/csproj/     │
   └────────────────────┘  │   assets)         │
                           └────────┬──────────┘
                                    │
                           ┌────────▼──────────┐
                           │  PackageIndex      │
                           │  (SRM, XML docs)   │
                           └───────────────────┘
```

Dependencies flow strictly downward. No cycles. Test projects mirror each layer.

---

## Layer descriptions

### Syntax

**Responsibilities:** Lex C# source into tokens; parse tokens into an immutable syntax tree; recover from errors without crashing.

**Design:** A two-level tree (green/red):
- *Green nodes* are position-independent, interned, structurally shared across edits. Stored as `SyntaxNode` records with a `Kind`, `FullSpan` (relative), `LeadingTrivia`, `TrailingTrivia`, and `IReadOnlyList<SyntaxNode> Children`.
- *Red nodes* (a thin projection, `SyntaxTree` + position cursor) add absolute position and parent navigation lazily.

Error recovery: the parser uses panic-mode recovery — on an unexpected token it emits an error node, skips tokens until a synchronisation point (`;`, `}`, `{`, or a keyword that starts a new declaration), and continues. Every source position always maps to exactly one node; no region is silently skipped.

**Public API:**
```csharp
SyntaxTree SyntaxTree.Parse(string sourceText, ParseOptions options);
SyntaxToken[] Lexer.Tokenize(string sourceText);
```

**Testing:** Golden-file tests under `tests/Syntax.Tests/Golden/`. Each `.cs` fixture has a `.golden` sibling (the pretty-printed tree). Roundtrip invariant: `Print(Parse(src)) == src` for all input.

---

### Semantics

**Responsibilities:** Build symbol tables; bind use-sites to declarations; infer types (`var`, generics, lambdas); resolve extension methods; model LINQ query syntax and pattern matching.

**Design — two-pass binding:**
1. *Declaration pass*: walk all syntax trees in a compilation; build a `SymbolTable` mapping qualified names → `ISymbol` (namespace, type, member, local, parameter). Handles partial types by merging declarations. Detects implicit/global usings from project settings.
2. *Use-site pass*: for each expression/statement, resolve names against the in-scope symbol table, infer types, return a `BoundTree` that decorates each syntax node with its `ITypeSymbol`.

Incremental: the `Workspace` tracks per-file `SyntaxTree` and `BoundTree`. On `textDocument/didChange`, re-parse only the changed file, then re-run the declaration pass for that file, then re-run use-site pass for that file and any file whose public API it may affect (tracked via a dependency graph).

**Type resolution priority:**
1. Local variables and parameters (innermost scope first)
2. Members of `this` / static members of the enclosing type
3. Types in imported namespaces (`using`, `using static`)
4. Types in the global namespace
5. Extension methods (resolved last, with type compatibility check)

**Key interfaces:**
```csharp
interface ISymbol { string Name; SymbolKind Kind; ITypeSymbol? Type; Accessibility Accessibility; }
interface ITypeSymbol : ISymbol { IReadOnlyList<ISymbol> Members; bool IsGeneric; ... }
interface ICompilation { ISymbol? Resolve(SyntaxNode node); ITypeSymbol? GetType(SyntaxNode expr); }
```

---

### ProjectSystem

**Responsibilities:** Discover and parse `.sln` / `.slnx`, `.csproj`, `Directory.Build.props/targets`, `Directory.Packages.props`. Evaluate MSBuild properties and conditions to a pragmatic level (string substitution, standard `$(Configuration)`, `$(TargetFramework)`, boolean conditions). Produce a `ProjectModel` per project (TargetFramework, LangVersion, Nullable, ImplicitUsings, source globs, project references, package references).

**Design:** Pure parsing — no MSBuild SDK, no exec of `msbuild`. The evaluator handles `$(Prop)` expansion, `Condition="'$(X)'=='Y'"`, `<Choose>`/`<When>`, and property inheritance from parent props files. Unresolved properties evaluate to empty string with a structured log warning.

A `SolutionWatcher` uses `FileSystemWatcher` on the solution root to detect `.csproj`, `*.props`, and `*.targets` changes and invalidate only the affected `ProjectModel`.

---

### PackageIndex

**Responsibilities:** Given a resolved `PackageReference` list (from `project.assets.json`), locate the assembly files; read them with `System.Reflection.Metadata`; build a searchable in-memory index of public types, members (including extension methods), generics, attributes, and XML documentation. Fall back to the NuGet global-packages folder if `project.assets.json` is absent.

**Index structure:** A `TypeIndex` keyed by fully-qualified name and by simple name, with pre-built extension-method buckets (keyed by the extended type's name). Access is O(1) on exact name, O(log n) on prefix search.

**XML docs:** Parsed from the companion `.xml` file; stored per-member as raw markdown (stripped of CDATA wrappers). Returned with hover items and signature help.

**Incremental:** On `PackageReference` addition/removal, re-index only the affected package's assemblies.

---

### EfModel

**Responsibilities:** On top of Semantics, detect Entity Framework patterns: `DbContext` subclasses, `DbSet<T>` properties (entity type extraction), entity class shapes (properties, navigation properties, keys). Parse `OnModelCreating` Fluent API calls and `[Key]` / `[ForeignKey]` / etc. data annotations. Understand `Include` / `ThenInclude` / `Select` / `Where` completion positions in LINQ-to-Entities chains.

**Design:** EfModel depends on Semantics; it does not re-implement type resolution. It adds an `EfContext` overlay that maps LINQ expression trees (as `BoundTree` nodes) to EF-specific completion contexts:
- After `.Include(x => x.`, complete navigation properties of the entity type.
- After `.ThenInclude(x => x.`, complete the second-level navigation.
- Inside `modelBuilder.Entity<T>().`, complete the Fluent API surface.
- After `context.Set<T>().` or `context.DbSetProperty.`, complete LINQ queryable methods.

**Testing:** The `CleanArchEf` fixture solution (in `fixtures/`) exercises all patterns.

---

### Ranking

**Responsibilities:** Given a list of candidate `ISymbol` items from Semantics, produce an ordered `RankedCompletion[]` with an attached `RankingExplanation` (in explain mode).

**Signals (documented, testable, each with a weight coefficient):**

| Signal | Description |
|--------|-------------|
| `TypeCompatibility` | Does the symbol's type match the expected type at the cursor? Exact match > assignable > coercible. |
| `ScopeProximity` | Innermost scope first; local var > member > imported namespace > global. |
| `UsageFrequency` | How often this symbol is used elsewhere in the same solution (normalized per type). |
| `ArchitectureLayer` | Does completing this cross a dependency arrow? Downward > same-layer > upward. |
| `PackageFrequency` | How often this package's API appears in the user's own code (from PackageIndex usage stats). |
| `LangVersionAvailability` | Is the feature/syntax available at the project's `LangVersion`? Penalise if not. |
| `ObsoleteStatus` | `[Obsolete]` members are ranked last unless no compatible member exists. |
| `KnowledgePack` | Boost from the offline knowledge pack (best-practice patterns, common call chains). |
| `EfContext` | Inside EF/LINQ chains, boost EF-aware completions. |

Final score: weighted sum (weights stored in `Ranking/Weights.cs`, adjustable at runtime for experimentation). The explain mode prints each signal's raw value and contribution.

---

### Knowledge

**Responsibilities:** Load `data/knowledge.json` at startup. Expose a `IKnowledgeQuery` interface for the Ranking layer to look up boost values by pattern (e.g., `HttpClient` + `.GetAsync` after `await`). The data file is generated offline by the KnowledgePack CLI and committed as an artifact; the engine never fetches it.

**Format:** A flat JSON dictionary: `{ "patterns": [ { "context": "...", "symbol": "...", "boost": 0.1 } ] }`. Loaded once into a `Dictionary<PatternKey, float>` at startup.

---

### Protocol

**Responsibilities:** Implement JSON-RPC 2.0 Content-Length framing over stdio. Provide C# POCO types for the LSP subset we support. No dependency on any external LSP library.

**Framing:**
```
Content-Length: <n>\r\n
\r\n
<n bytes of UTF-8 JSON>
```

**LSP subset implemented:**
- `initialize` / `initialized` / `shutdown` / `exit`
- `textDocument/didOpen` / `didChange` / `didClose`
- `textDocument/completion` (with `completionItem/resolve`)
- `textDocument/signatureHelp`
- `textDocument/hover`
- `workspace/didChangeWatchedFiles`
- `$/cancelRequest`
- `window/logMessage` (outbound, for structured server→client logging)

Requests are dispatched on a single async loop; completion requests are cancellable.

---

### Server

**Responsibilities:** Entry point. Wires up all layers via constructor injection (no DI framework — a manual composition root in `Program.cs`). Owns the `Workspace` (the mutable top-level state: open documents, solution model, bound trees). Runs the JSON-RPC dispatch loop.

**Concurrency model:** Single-threaded logical flow with `async`/`await`. All state mutation is serialized through the dispatch loop. Completion requests that take longer than the latency budget emit a cancel and return a partial result rather than timing out the client.

---

### TypeScript client (`client/`)

**Responsibilities:** Spawn the .NET server process (`dotnet CSharpSense.Server.dll`), pipe stdio, implement `vscode.CompletionItemProvider`, `vscode.SignatureHelpProvider`, `vscode.HoverProvider` by translating VS Code API calls into LSP JSON-RPC requests and translating responses back.

**What it must NOT do:** Any symbol lookup, ranking, type inference, or business logic.

**JSON-RPC framing (self-implemented):** A ~100-line `JsonRpcTransport` class in `client/src/transport.ts` reads Content-Length headers and buffers partial messages. No third-party library.

---

## Workspace and incremental invalidation

```
Workspace
├── SolutionModel          (from ProjectSystem)
│   └── ProjectModel[]
│       ├── SourceFiles[]
│       └── PackageReferences[]
├── SyntaxForest           (SyntaxTree per file, keyed by path)
├── PackageIndexes[]       (one per resolved package)
├── SymbolTable            (declaration pass output)
├── BoundForest            (BoundTree per file, keyed by path)
├── EfOverlay              (EfContext per DbContext found)
└── FileVersionMap         (file → content hash, for change detection)
```

On `didChange(file)`:
1. Re-parse file → new `SyntaxTree`.
2. Re-run declaration pass for file → patch `SymbolTable`.
3. Re-run use-site pass for file and dependents → patch `BoundForest`.
4. If file is a `.csproj` or `.props`, rebuild the affected `ProjectModel` and re-index any changed packages.

---

## Fixture solutions

### `fixtures/Basic/`

A three-project solution (Library, App, Tests). Exercises: member completion, namespace imports, generics, `var` inference, LINQ, delegates, async/await, pattern matching. Used for parser golden files and basic completion benchmarks.

### `fixtures/CleanArchEf/`

A five-project Clean Architecture solution: `Domain`, `Application`, `Infrastructure` (EF Core), `WebApi`, `Tests`. Exercises: EF DbContext + DbSets, navigation properties, Include/ThenInclude, LINQ-to-Entities, MediatR handlers, repository pattern, DI registrations. The EF module is validated here.

---

## Evaluation harness (`scripts/eval`)

A .NET console app (`tests/Eval/`) run by the shell script. It:
1. Loads each fixture solution through the full stack.
2. For each cursor-masking position (after `.`, inside arg list, after `new`, etc.), calls the completion engine and checks whether the ground-truth token appears in the Top-1/3/5.
3. Computes Top-1/3/5 hit rate, MRR, absent rate, noise rate, per category and overall.
4. Measures p50/p95/p99 latency (in-process and over real stdio).
5. Writes `eval/results-<timestamp>.json` and `eval/report-<timestamp>.md`.
6. If a baseline file exists, diffs and reports regressions.

Data split: positions are assigned to train/dev/holdout by a seeded hash of `(file, offset)`. The holdout is only evaluated with `--holdout`.

---

## What we explicitly do NOT build

- A full MSBuild evaluation engine (we evaluate a pragmatic subset; edge cases log a warning).
- Code formatting / refactoring / rename (out of scope; not an LSP feature we register).
- Debugger integration.
- Cross-file rename or find-all-references (possible later; not in the milestones).
- Network access from the engine.
