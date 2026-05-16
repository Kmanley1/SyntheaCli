---
title: SyntheaCli — Architectural Design Review
status: notes
domain-type: tool
area: synthea-cli
last-updated: 2026-05-15
reviewer: Claude (Opus 4.7)
scope: src/Synthea.Cli/**, tests/**, docs/deliverables/Architecture.md, build/CI
version-reviewed: 1.0.0 (commit on branch master, 2026-05-15)
---

# SyntheaCli — Architectural Design Review

A complete and thorough architectural design review of the SyntheaCli .NET 8
global tool, covering structure, design qualities, cross-cutting concerns,
dependencies, build pipeline, testability, and documentation. Findings are
graded by severity and accompanied by concrete recommendations.

---

## 1. Executive Summary

**Verdict: B+ (Solid for its scope; not yet enterprise-grade).**

SyntheaCli is a small, focused .NET 8 wrapper that downloads, caches, and
launches the MITRE Synthea Java JAR with user-friendly options. The code is
well-organized for a tool of its size, has strong input validation, and uses
testable seams that enable a respectable unit-test suite (≈40 unit + 4
integration tests).

The design is **fit for purpose as a developer convenience tool** but exhibits
the typical shape of a project that grew organically without an architectural
guardrail:

- Testability is achieved through **mutable static state** rather than
  dependency injection, which forces test parallelization off and limits
  scalability.
- A second concern (`CodexTaskProcessor`) was bolted into the same assembly
  without integration into the CLI command surface — it is dead code from the
  user's perspective.
- Cross-cutting concerns (logging, telemetry, configuration, cancellation,
  observability) are essentially absent or improvised via `Console.Write`.
- The build/CI pipeline has a likely-real branch-filter bug (`main` vs.
  `master`) and runs only on Linux despite Windows being a first-class target.
- README and Architecture documentation contain visible drift from the
  implementation (e.g., garbled "Scripts" section at the top of README;
  `XDG_CACHE_HOME` claim does not match the code).

None of these issues block the tool's stated purpose. Most are remediable
in 1–10 days of focused work. The recommendations section prioritizes them.

### One-line strengths

> Pure argument-builder, immutable options record, interface-based process
> abstraction, atomic-rename JAR install, SHA-256 verification, and strong
> per-option validators.

### One-line weaknesses

> Static service-location seams, dead code in the published assembly, no
> structured logging, CI runs on the wrong branch, and a 473-line option
> registration method.

---

## 2. System Context

```
                                +-----------------------------+
                                |  GitHub releases API        |
                                |  (synthetichealth/synthea)  |
                                +--------------+--------------+
                                               |
                                               | HTTP GET (unauthenticated)
                                               v
+----------+    args      +--------------------+---------------------+
|   User   | -----------> |             Synthea.Cli (.NET 8)         |
| (shell)  |              |                                          |
+----------+              | +--------------+   +-------------------+ |
     ^                    | |  Program     |-->| RunCommand        | |
     | stdout/stderr      | |  (entry)     |   | (System.CmdLine)  | |
     |                    | +------+-------+   +---------+---------+ |
     |                    |        |                     |           |
     |                    |        v                     v           |
     |                    | +--------------+   +-------------------+ |
     |                    | | JarManager   |   | RunOptions (DTO)  | |
     |                    | | (download/   |   +-------------------+ |
     |                    | |  cache/SHA)  |             |           |
     |                    | +------+-------+             v           |
     |                    |        |           +-------------------+ |
     |                    |        |           | IProcessRunner    | |
     |                    |        |           | (interface)       | |
     |                    |        v           +---------+---------+ |
     |                    | %LOCALAPPDATA%\              |           |
     |                    |   Synthea.Cli\*.jar          |           |
     |                    +------------------------------+-----------+
     |                                                   |
     |                                                   v
     |                                       +-----------+-----------+
     |                                       |   java -jar synthea   |
     +-------------- relayed ----------------|     ... arguments     |
                                             +-----------------------+
```

The runtime collaboration is straightforward:

1. **Program** wires the System.CommandLine `RootCommand` and forwards `--help`
   when invoked with no arguments.
2. **RunCommand.Build** constructs the `run` sub-command with 16 typed options
   and a passthru argument array. The handler:
   - parses options into the immutable `RunOptions` record,
   - performs cross-option validation (city requires state, zip requires state),
   - calls `JarManager.EnsureJarAsync` to materialize the JAR on disk,
   - calls `BuildArgumentList(RunOptions)` (pure function) to assemble
     command-line arguments,
   - constructs a `ProcessStartInfo` via `CreateProcessStartInfo`,
   - launches the child Java process via `IProcessRunner.Start`,
   - relays stdout/stderr until exit.
3. **JarManager** uses a mutable static `HttpClient` to call the GitHub
   `releases/latest` endpoint, downloads the `*with-dependencies.jar`, verifies
   SHA-256 if a `.sha256` asset is present, and atomically moves the temp file
   into the cache directory.

**Out-of-band (unwired):** `CodexTaskProcessor` is compiled into the same
assembly but is never invoked from `Program.Main` or `RunCommand`. It is
referenced only by its dedicated test class. See finding A-7.

---

## 3. Component-by-Component Review

### 3.1 `Program.cs` — Composition root / entry point

**Lines:** 35. **Role:** boot.

**Strengths**

- Single responsibility: build the root command, register sub-commands,
  dispatch `--help` on no-args, return exit code.
- The two static "seams" (`Runner`, `EnsureJarAsyncFunc`) are simple and small
  enough to read at a glance.
- Async `Main` returning `Task<int>` is idiomatic.

**Weaknesses**

- `Program.Runner` and `Program.EnsureJarAsyncFunc` are **mutable static
  state**. They are the project's primary testability hook, but:
  - They make parallel test execution unsafe (the `[assembly:
    CollectionBehavior(DisableTestParallelization = true)]` line in
    `tests/Synthea.Cli.UnitTests/AssemblyInfo.cs` confirms this is being
    worked around).
  - They tightly couple the entry point to global mutable state, which is
    awkward when the CLI grows beyond one command.
- `Program` is `public` (and exposes the static fields publicly). This forces
  the entire tool's API surface into the public contract. For a `dotnet tool`
  package consumers should never reference `Synthea.Cli` as a library — the
  type and its static fields should be `internal`, with `InternalsVisibleTo`
  for tests.
- No top-level exception handling. A failure inside `RunCommand`'s handler
  surfaces as a stack trace on stderr; users get the .NET ICE-like view rather
  than a friendly "Could not reach GitHub. Check your network."

**Severity:** Low (architecture), Low (visibility).

---

### 3.2 `RunCommand.cs` — CLI definition + handler

**Lines:** 473. **Role:** option registration, validation, handler
orchestration, argument list construction.

**Strengths**

- `BuildArgumentList(RunOptions)` is a **pure** function: no I/O, no side
  effects, fully testable, deterministic. This is the single best design
  decision in the codebase.
- `CreateProcessStartInfo(RunOptions, FileInfo)` is similarly pure and
  small.
- `ProcessStartInfo.ArgumentList` is used (not a concatenated command
  string). Cross-platform safe, immune to shell injection.
- Each option has a focused validator with a clear, user-readable
  error message.
- The `--state`/`--city`/`--zip` co-dependency check is enforced inside
  the handler before any expensive work happens.
- `TreatUnmatchedTokensAsErrors = false` lets users forward unknown flags
  to Synthea via positional `args` (passthru), which is the right escape
  hatch for a wrapper.

**Weaknesses**

- **Size.** 473 lines for a single command's worth of registration is on the
  high side. About 250 lines are nearly-identical `Create*Option()` factories.
  Extracting a small helper (`Option<T> Define(name, parse, validate)`) would
  remove ~40% of the file. This is a maintainability concern, not a defect.

- **Hardcoded state allowlist (50 entries, no territories).** Synthea's geo
  data may include DC, PR, GU, VI, etc. The current validator silently rejects
  them with a generic "must be a valid two letter code" message. Either the
  list should be loaded from a resource (and kept in sync with the JAR) or the
  validator should be relaxed and the Synthea JAR allowed to do the rejection.

- **`--format` semantics are surprising.** The first instance of `--format`
  switches the command into *exclusive-list* mode, emitting
  `--exporter.<format>.export=false` for every format the user did **not**
  list — overriding Synthea's `synthea.properties` defaults. A user passing
  `--format CSV` to "add CSV" will inadvertently disable FHIR. There is no
  flag to "merge into defaults" vs. "replace defaults".

- **Passthru ordering bug surface.** `BuildArgumentList` appends:
  ```
  [option-derived flags] + [passthru tokens] + [state] + [city] + [zip]
  ```
  Synthea's parser expects state/city/zip as the **last** positional tokens.
  If a user passthrough flag itself takes a value (e.g.
  `-- --some-flag value`), Synthea may consume "OH" as the value for the
  passthrough flag instead of as the state. The handler does not separate
  passthrough flags from passthrough values.

- **No cancellation propagation to the child process.** The handler awaits
  `proc.WaitForExitAsync()` but does not register the cancellation token to
  kill the process on Ctrl+C. If the user cancels mid-run, the Java process
  becomes orphaned.

- **`--days-forward` requires positive integer**, but the underlying Synthea
  flag (`-t`) is semantically "days to advance from snapshot". Zero is
  arguably valid (no-op time advance); the current validator rejects it.

- **Console output is not UTF-8 forced.** On Windows the default code page is
  often 437 or 1252; patient names with diacritics will be mojibake'd.

- **Progress line uses `\r` unconditionally.** When stdout is redirected to a
  file or pipe, the line becomes a stream of `\r` characters with each MB
  update. Should check `Console.IsOutputRedirected`.

**Severity:** Medium (size/maintenance), Medium (format semantics), Medium
(passthru ordering), Medium (no cancel-on-Ctrl-C), Low (state allowlist),
Low (UTF-8/progress).

---

### 3.3 `RunOptions.cs` — Options DTO

**Lines:** 23. **Role:** immutable record carrying all parsed options.

**Strengths**

- Positional record → value equality, deconstruction, immutability.
- All option fields in one place; easy to read.

**Weaknesses**

- 19 positional parameters → call-sites are dense and error-prone. The two
  constructions in `ProgramRefactorTests.cs` already show this: every
  parameter must be passed by name to remain legible.
- Mixes input flags (`Refresh`, `JavaPath`) with Synthea-domain flags. Two
  smaller records (`HostingOptions` and `SyntheaArgs`) would communicate
  intent better.
- `Modules` is `string[]?` while `Formats` and `Passthru` are non-nullable
  `string[]`. Asymmetric nullability is a minor smell.

**Severity:** Low.

---

### 3.4 `JarManager.cs` — Download & cache

**Lines:** 136. **Role:** locate cached JAR or fetch the latest release.

**Strengths**

- **Atomic install:** downloads to a temp file, verifies, then `File.Move`
  with `overwrite: true`. A half-finished JAR never appears in the cache.
- **Optional SHA-256 verification** when the upstream provides a `.sha256`
  asset.
- Uses `Path.Combine` and `Environment.GetFolderPath` correctly for
  cross-platform paths.
- Progress reporting through `IProgress<(long,long)>` decouples
  reporter implementation from the download loop.

**Weaknesses**

- **Unauthenticated GitHub API call.** `https://api.github.com/repos/.../
  releases/latest` is limited to 60 req/hour per source IP without an API
  token. In shared CI environments (or behind a NAT) this fails
  intermittently with HTTP 403. No backoff, no retry, no `GITHUB_TOKEN`
  support.
- **Mutable static `HttpClient`** (`internal static HttpClient Http`). The
  field is reassigned freely by tests. Production code never disposes the
  original, which is acceptable for a long-lived HttpClient but the seam is
  awkward.
- **No proxy or alternate URL support.** Enterprise networks with proxies or
  air-gapped environments cannot use the tool. A `SYNTHEA_JAR_URL` /
  `SYNTHEA_JAR_PATH` environment override (or `--jar` flag) would solve
  both.
- **No cache eviction.** Every `--refresh` writes a new
  `synthea-with-dependencies-<version>.jar` (the upstream filename includes
  the version) and the old one stays forever. There is no `--clear-cache`
  command and no size cap.
- **`Path.GetTempFileName()`** has a 65,535-name limit and creates a 0-byte
  file as a side effect. Prefer `Path.Combine(Path.GetTempPath(),
  Path.GetRandomFileName())`.
- **Unhandled JSON-shape changes.** `doc.RootElement.GetProperty("assets")`
  throws `KeyNotFoundException` raw if GitHub changes the response shape;
  the user sees a stack trace instead of a friendly message.
- **`catch { }`** around the temp-file delete swallows everything. Best
  effort cleanup is fine; silently swallowing OOM is not.
- **README/implementation drift:** README claims the cache lives at
  `%LOCALAPPDATA%\Synthea.Cli` / `$XDG_CACHE_HOME/Synthea.Cli`, but the code
  uses `Environment.SpecialFolder.LocalApplicationData`, which on Linux/Mac
  resolves to `~/.local/share`, not `$XDG_CACHE_HOME`. The README is wrong.
- **No SHA-256 *requirement*.** If the upstream stops publishing
  `.sha256` files, the tool silently falls back to "no verification" with
  no warning to the user.

**Severity:** Medium (rate limit, proxy, eviction), Medium (no required
checksum), Low (others).

---

### 3.5 `ProcessHelpers.cs` — Process abstraction

**Lines:** 46. **Role:** `IProcessRunner` / `IProcess` interfaces +
`Relay` helper.

**Strengths**

- Minimal interface surface: `Start(ProcessStartInfo) → IProcess`. Easy to
  mock.
- `IProcess` exposes only what callers need (`StandardOutput`,
  `StandardError`, `WaitForExitAsync`, `ExitCode`).
- `Relay` is a one-liner doing the obvious thing.

**Weaknesses**

- Both interfaces and the default implementation are `public`, leaking
  internals into the tool's package API surface. Should be `internal`.
- No exit-code semantic enum, no timeout overload, no kill-on-cancel.
- `Relay` reads line-by-line; very long Synthea log lines (rare but possible)
  could pause the stream pump.

**Severity:** Low.

---

### 3.6 `CodexTaskProcessor.cs` — Task automation processor

**Lines:** 218. **Role:** processes markdown task files in a folder
structure, runs pre/post context files, captures logs, generates feedback.

**Critical finding:** This component is **not wired into the CLI**.
`Program.Main` and `RunCommand.Build` do not reference it. The only callers
are inside `tests/Synthea.Cli.UnitTests/CodexTaskProcessorTests.cs`.

**Implications**

- It inflates `Synthea.Cli.dll` by ~9 KB of source plus its test fixtures.
- It widens the public API of the tool package (`CodexTaskProcessor`,
  `ITaskImplementer`, `TeeTextWriter`) — none of which a `dotnet tool`
  consumer should ever invoke.
- It introduces a separate concern (codex automation / build orchestration)
  into a project whose stated purpose is "wrap the Synthea JAR".
- The `docs/deliverables/codex-automation.md` explains the intended
  workflow but does not mention how the tool exposes it to users.
- If this is intentional (called from an external script via reflection,
  for example), the linkage is undocumented and the surface is fragile.

**Recommendation:** decide one of:

1. **Promote** to a real sub-command (`synthea codex run-tasks <dir>`),
   document it, and test the command line.
2. **Extract** to a separate assembly (`Synthea.Cli.CodexAutomation`) that
   is not packed into the global tool.
3. **Remove** if the workflow is no longer used.

Until that decision is made, the assembly is shipping mystery code.

**Severity:** Medium (architectural; dead-code from user's POV).

---

## 4. Cross-Cutting Concerns

### 4.1 Logging & Observability

| Concern | Current state | Gap |
|---|---|---|
| Structured logs | None | `Microsoft.Extensions.Logging` not referenced |
| Log levels | None | `--verbose` / `--quiet` not implemented |
| Log destinations | `Console.Out` / `Console.Error` only | No file, no JSON |
| Telemetry | None | Defensible for a small tool; document the decision |
| Correlation IDs | N/A | No multi-call workflow yet, but Codex processor would benefit |

The architectural-eval doc from 2025 already flagged this; nothing has
shipped.

### 4.2 Configuration

- No config file support (e.g., `~/.synthea-cli/config.json` for default
  Java path, default output dir, GitHub token).
- All configuration is command-line. For repeat users, this is friction.
- Environment variables are not consulted (`SYNTHEA_JAR_PATH`,
  `SYNTHEA_JAVA`, `GITHUB_TOKEN`, etc.).

### 4.3 Error Handling

- Validators give clear, single-sentence error messages — good.
- Runtime errors (network, IO, process spawn) propagate as exceptions and
  surface as stack traces. The handler should wrap critical failures in a
  top-level `try/catch` that maps known categories to friendly messages
  and exit codes.
- No exit-code convention documented (e.g., 0 = success, 1 = validation,
  2 = environment, 3 = upstream, etc.).

### 4.4 Cancellation

- `ctx.GetCancellationToken()` is forwarded to `EnsureJarAsyncFunc` but
  **not** to the child process. Ctrl+C kills the .NET wrapper but leaves
  Java running.
- Should call `proc.Kill(entireProcessTree: true)` on cancellation.

### 4.5 Concurrency

- No internal concurrency to speak of. Stdout/stderr pumps run in
  parallel via `Task.Run` — fine.
- Static mutable state in `Program` and `JarManager` would not survive
  concurrent invocations within the same process. Not relevant for the
  CLI usage pattern, but blocks reuse as a library.

### 4.6 Security & Supply Chain

- SHA-256 verification when upstream provides it.
- No verification when upstream omits the checksum (silent fallback).
- No signature verification (cosign, sigstore) — upstream Synthea does not
  publish signatures either, so the gap is shared.
- `System.CommandLine 2.0.0-beta4.22272.1` — production dependency on a
  beta package. Risk: breaking changes when 2.0 GA lands.
- No Dependabot config, no CodeQL, no `dotnet list package --vulnerable`
  step in CI.
- No SBOM generation.

### 4.7 Performance

- Patient generation is dominated by Java; the wrapper's overhead is
  negligible.
- JAR download: 80 KB buffer, single TCP stream, no resume. Fine.
- `Console.Write` in the progress callback is unthrottled — on a fast
  link the reporter may fire hundreds of times per second.

### 4.8 Internationalization / Localization

- All messages are English literals embedded in source. No `.resx`, no
  localization. Acceptable for a developer tool but worth noting.

---

## 5. Build & Pipeline

### 5.1 Solution Structure

```
SyntheaCli.sln
├── src/Synthea.Cli/                       (production)
├── tests/Synthea.Cli.UnitTests/           (xUnit)
├── tests/Synthea.Cli.IntegrationTests/    (xUnit, Trait Category=Integration)
├── scripts/  (solution-folder pointer)
├── run/      (solution-folder pointer)
└── Solution Items/README.md
```

- Standard `src/` + `tests/` shape — good.
- `Directory.Build.props` centralizes `BaseOutputPath`, `BaseIntermediateOutputPath`, `TreatWarningsAsErrors`, and `Nullable`. Good.
- Multiple platform configurations (`Debug|x86`, `Debug|x64`) are defined
  but all map to `Any CPU` — harmless clutter.

### 5.2 CI Workflow (`.github/workflows/ci.yml`)

**Issues**

- **Branch filter `[main]`.** The current default branch is `master`
  (confirmed by `git status` and `master` in CLAUDE.md). Pushes to `master`
  do **not** trigger CI. This is almost certainly a real bug.
- **`runs-on: ubuntu-latest` only.** Windows is a first-class target (it is
  the primary dev environment per `setup-test-environment.ps1`,
  `fix-java-detection.ps1`). Cross-platform behavior is essentially
  untested in CI.
- **`--warnaserror` on `dotnet build`** but `TreatWarningsAsErrors=true` is
  already in props — redundant but not wrong.
- **No code coverage threshold.** Tests collect coverage via
  `coverlet.collector` but CI never reads or enforces it.
- **No package-vulnerability check.**
- **No `dotnet format --verify-no-changes`** step.

### 5.3 NuGet Workflow (`.github/workflows/nuget.yml`)

- Triggered on `v*` tags — correct.
- Reads version from tag (`${GITHUB_REF_NAME#v}`) — correct.
- Pushes to nuget.org — correct.
- Uses `actions/setup-dotnet@v3` (others use v4) — minor inconsistency.
- No `--include-symbols`, no `.snupkg` push — debug experience for
  consumers is poor.
- No `nuget verify` or `dotnet nuget verify` step before push.

### 5.4 `Directory.Build.props`

- `BaseOutputPath` and `BaseIntermediateOutputPath` redirect builds to a
  centralized `artifacts/` tree. This conflicts with
  `Synthea.Cli.IntegrationTests` which scans **five candidate paths**
  trying to find the built DLL. A single, predictable path would simplify.
- `TreatWarningsAsErrors=true` is a good gate; `Nullable=enable` is
  enforced project-wide.

---

## 6. Testability & Test Suite

### 6.1 Coverage at a Glance

| Tier | Count | Notes |
|---|---|---|
| Unit | ~40 | xUnit, `CollectionBehavior(DisableTestParallelization = true)` |
| Integration | 4 | One smoke, three real-process variants with skip logic |
| Total | 44 | README claims "≥90% coverage"; no enforcement in CI |

### 6.2 Strengths

- Tests cover the pure argument builder (`ProgramRefactorTests`), the
  System.CommandLine wiring (`ProgramHandlerTests`, `CliTests`), the
  JAR download path (`JarManagerTests`), and the standalone task processor
  (`CodexTaskProcessorTests`).
- Mocks are minimal (no Moq/NSubstitute) — fake classes are written
  inline and stay readable.
- `JarManagerTests` uses a stub `HttpMessageHandler`, a clean approach.
- Integration tests have a graceful skip mechanism
  (`SkipTestException`) when Java or the built DLL is missing.

### 6.3 Weaknesses

- **Parallelism disabled globally.** Static-state seams force serial
  execution. As the suite grows this becomes a wall-clock problem.
- **`CliTests.InvokeMain` uses reflection** to call `Program.Main` when
  `InternalsVisibleTo` already exposes the type. Unnecessary indirection.
- **`SkippableFactAttribute` is declared but never applied** in
  `SyntheaCliWrapperRunTests.cs`. The "skip" path uses `Assert.Fail("SKIPPED:
  ...")`, which marks the test as failed in dashboards. xUnit v2 has no
  native runtime skip; xunit-skippable would fix this.
- **Integration tests hardcode five DLL paths.** Brittle.
- **No tests for**: cancellation/Ctrl+C, GitHub rate limit / 403, network
  timeout, corrupted JAR, `--refresh` against existing cache, Windows
  path semantics, UTF-8 console output.
- **`tests/Synthea.Cli.IntegrationTests/SyntheaRunTests.cs`** uses
  `Program.Runner = new FakeRunner(...)` and `Program.EnsureJarAsyncFunc =
  ...`. This is an "integration test" in name only — it's effectively a
  unit test of the handler that happens to write a stub file to disk.
- **No coverage threshold enforced.** "44 tests" is reported but the actual
  line/branch coverage number is not visible.

---

## 7. Documentation

### 7.1 Top-level README.md

- **Broken merge near lines 3–8.** The file opens with what appears to be a
  failed three-way merge: the "Scripts" heading is followed by a fragment of
  the feature table, then the "Scripts" body. Renders as garbled markdown.
  This is in production-published docs and probably appears on the NuGet
  package page.
- **Java version drift:** features table says "Java 17+", prerequisites say
  "Java ≥ 11", `setup-test-environment.ps1` and CHANGELOG say "Java 11". The
  README is internally inconsistent.
- **Cache location drift:** features table claims
  `$XDG_CACHE_HOME/Synthea.Cli` on Linux/Mac. Code uses
  `LocalApplicationData` → `~/.local/share/Synthea.Cli`. README is wrong.
- **"Docker image" feature claimed** but the README itself says "Docker
  build scripts are not currently available in this version." Two parts of
  the same README disagree.

### 7.2 `docs/deliverables/Architecture.md`

- 27 lines, one mermaid diagram, two paragraphs. For a 1.0 release this is
  thin. Should describe component boundaries, data flow, failure modes,
  caching strategy, security model.
- `architectural-eval.md` is more substantive — but is filed as a
  "deliverable" not a permanent architecture record. No ADR directory.

### 7.3 `docs/deliverables/health-check.md`

- **Zero bytes / empty file.** Should be deleted or populated.

### 7.4 `AGENTS.md`

- Explicitly labelled "Stub" with a TODO. Acceptable as long as it stays
  current.

### 7.5 Process docs

- `docs/deliverables/` contains ~18 files, many of them clearly LLM-generated
  artifacts (action-plan-with-timelines, on-going-scorecard-metrics,
  prompt-generator, ...). They are not curated for end users and clutter
  the docs experience. Archive policy needed.

### 7.6 Missing artifacts

- No `ARCHITECTURE.md` at repo root (only the `docs/deliverables/` copy).
- No ADRs.
- No threat model.
- No supply-chain document.

---

## 8. Findings Register

Severity legend: **H** = High (fix before next release), **M** = Medium
(plan into next iteration), **L** = Low (track / cleanup), **I** =
Informational.

| ID | Area | Finding | Sev | Effort |
|----|------|---------|-----|--------|
| A-1 | CI | Workflow filters on `main`, default branch is `master`; pushes are not building | **H** | 5 min |
| A-2 | CI | No Windows runner despite Windows being a primary target | **H** | 15 min |
| A-3 | README | Garbled merge in top "Scripts" section renders broken on NuGet | **H** | 15 min |
| A-4 | Docs | Java-version inconsistency across README (11 vs 17) | **H** | 15 min |
| A-5 | JAR | No `GITHUB_TOKEN` / proxy / `--jar` override → CI rate-limit risk | **M** | 1 day |
| A-6 | RunCommand | No cancel-on-Ctrl+C — child Java process orphaned | **M** | 2 hours |
| A-7 | Assembly | `CodexTaskProcessor` is dead code in the published tool | **M** | 4 hours (extract or wire) |
| A-8 | RunCommand | `--format` semantics override defaults silently | **M** | 1 day (design + impl + docs) |
| A-9 | RunCommand | Passthru tokens placed before positional state/city/zip | **M** | 1 day |
| A-10 | Visibility | `Program`, `JarManager`, `IProcessRunner`, `IProcess`, `CodexTaskProcessor` are `public`; should be `internal` | **M** | 1 hour |
| A-11 | Logging | No `Microsoft.Extensions.Logging`; no `--verbose`/`--quiet` | **M** | 1–2 days |
| A-12 | Testability | Mutable static seams force `DisableTestParallelization` | **M** | 1 week (DI refactor) |
| A-13 | Errors | No top-level try/catch; exceptions surface as stack traces | **M** | 4 hours |
| A-14 | JAR | No cache eviction / `--clear-cache` | **M** | 4 hours |
| A-15 | Dependency | Production dependency on `System.CommandLine` beta | **M** | track until GA |
| A-16 | Docs | `Architecture.md` is thin; no ADRs | **M** | 1 day |
| A-17 | Docs | `health-check.md` is empty (zero bytes) | **L** | 5 min |
| A-18 | RunCommand | Hardcoded 50-state allowlist excludes DC/PR/etc | **L** | 1 hour |
| A-19 | RunCommand | UTF-8 not forced on console; mojibake on Windows | **L** | 5 min |
| A-20 | RunCommand | Progress uses `\r` even when output is redirected | **L** | 15 min |
| A-21 | RunCommand | 473-line file; option factories could share a helper | **L** | 2 hours |
| A-22 | Tests | `CliTests` uses reflection unnecessarily | **L** | 5 min |
| A-23 | Tests | `SkippableFactAttribute` declared, never used; skips show as failures | **L** | 30 min |
| A-24 | Tests | Integration tests scan five DLL paths — fragile | **L** | 30 min |
| A-25 | JarManager | `JsonDocument.GetProperty` throws raw on shape change | **L** | 30 min |
| A-26 | JarManager | `catch { }` around temp-file cleanup hides everything | **L** | 5 min |
| A-27 | JarManager | `Path.GetTempFileName()` has 65k limit, creates 0-byte file | **L** | 5 min |
| A-28 | RunOptions | 19-positional record; consider splitting Hosting vs Synthea | **L** | 2 hours |
| A-29 | CI | NuGet workflow uses `setup-dotnet@v3` while CI uses `@v4` | **L** | 1 min |
| A-30 | CI | No coverage threshold; no `dotnet format` check; no vulnerable-package scan | **L** | 1 hour |
| A-31 | Docs | `XDG_CACHE_HOME` claim in README does not match code | **L** | 5 min |
| A-32 | Docs | Docker described as a "feature" but not present | **L** | 15 min |
| A-33 | Sln | Duplicate Debug/Release platform configurations all aliasing Any CPU | **I** | 15 min |
| A-34 | Sln | Solution folder names (`scripts`, `run`) point to paths that have moved to `tools/` | **L** | 10 min |
| A-35 | NuGet | No `.snupkg` / symbols pushed | **L** | 10 min |
| A-36 | JAR | If upstream stops publishing `.sha256`, integrity check silently disappears | **M** | 1 hour |
| A-37 | RunCommand | No `--dry-run` / `--print-args` for debugging | **L** | 1 hour |
| A-38 | RunCommand | No way to discover which Synthea JAR version is loaded | **L** | 30 min |
| A-39 | Sec | No Dependabot, no CodeQL, no SBOM | **L** | 1 hour |
| A-40 | Config | No environment-variable / config-file support | **L** | 1 day |

---

## 9. Architectural Recommendations

Grouped by horizon. Numbers in parentheses reference findings above.

### 9.1 Immediate (next merge — under 1 day total)

1. **Fix the CI branch filter** (A-1). Either rename the branch to `main`
   or update `ci.yml` to also include `master`.
2. **Add a Windows job** to `ci.yml` (A-2). At minimum:
   `runs-on: [ubuntu-latest, windows-latest]` in a strategy matrix.
3. **Repair README** (A-3, A-4, A-31, A-32). One pass to remove the merge
   artifact, reconcile Java versions, fix the cache-path claim, and remove
   the contradictory Docker feature row.
4. **Delete `docs/deliverables/health-check.md`** (A-17). It is a 0-byte
   file.
5. **Bump `actions/setup-dotnet`** in `nuget.yml` to v4 (A-29).

### 9.2 Short term (1–2 weeks)

6. **Decide the fate of `CodexTaskProcessor`** (A-7). Recommend extracting
   to a separate `Synthea.Cli.Codex` assembly so the global tool's surface
   shrinks. If kept, surface a sub-command and document it.
7. **Tighten visibility** (A-10). Mark `Program`, `JarManager`,
   `IProcessRunner`, `IProcess`, `DefaultProcessRunner`, and (if retained)
   `CodexTaskProcessor` `internal`. The unit-test project already has
   `InternalsVisibleTo`.
8. **Top-level exception handler** (A-13). Wrap `RunCommand`'s handler in a
   try/catch that maps `HttpRequestException`, `IOException`, and
   `InvalidOperationException` (checksum) to friendly messages and distinct
   exit codes. Document the exit-code convention in the README.
9. **Cancel-on-Ctrl+C** (A-6). Register the cancellation token to call
   `proc.Kill(entireProcessTree: true)`. Tested via a fake runner that
   observes the cancel call.
10. **Air-gap / proxy / token support** (A-5, A-36).
    - Add `SYNTHEA_CLI_JAR_PATH` env var: if set, skip download entirely.
    - Add `--jar <path>` flag for the same.
    - Add `GITHUB_TOKEN` env var: if set, attach `Authorization: Bearer
      <token>` to GitHub API calls.
    - Add an `--insist-checksum` flag that fails the run if the upstream
      did not provide a `.sha256` (default off for backward compatibility,
      but recommended in docs for production CI).
11. **Add `--print-args` and `--version-info`** (A-37, A-38). The former
    prints the would-be Java command line and exits. The latter prints
    the .NET tool version and the resolved Synthea JAR filename.
12. **Force UTF-8 on Console** (A-19). At the top of `Main`:
    `Console.OutputEncoding = System.Text.Encoding.UTF8;`
13. **Suppress `\r` progress when stdout is redirected** (A-20).
14. **Switch to `Microsoft.Extensions.Logging`** (A-11), keeping
    `Console.WriteLine` for the progress reporter. Add `--verbose` /
    `--quiet`. Default level INFO.

### 9.3 Medium term (next minor release, ~1 quarter)

15. **Refactor `Program.Runner` / `Program.EnsureJarAsyncFunc` into a DI
    container** (A-12). Use `Microsoft.Extensions.DependencyInjection`,
    wire `IProcessRunner`, `IJarSource`, `ILogger`, etc. Re-enable test
    parallelization. This pays back across every future command.
16. **Split `RunOptions`** (A-28) into `HostingOptions` (Refresh, JavaPath,
    JarPath, GitHubToken) and `SyntheaArgs` (everything domain-specific).
17. **Reduce `RunCommand` to ~250 lines** (A-21) by introducing a
    `OptionBuilders` helper that hosts the repeated factory pattern.
18. **Cache hygiene** (A-14). Add `synthea cache clear`, `synthea cache
    list`, optional max-age eviction. Document the cache location.
19. **`--format` redesign** (A-8). Either:
    - introduce `--add-format` (additive) and `--only-format` (exclusive),
      with `--format` aliasing one of them, or
    - make `--format` always additive and provide `--disable-format` for
      the rare opt-out.
20. **Passthru ordering** (A-9). Reposition state/city/zip *after* the
    passthru tokens, or require passthru to come *after* `--` separator
    only and validate accordingly.
21. **Coverage threshold** (A-30). In CI, run
    `dotnet test --collect:"XPlat Code Coverage"` and fail under 85% line
    coverage using `reportgenerator`.
22. **Document the exit-code convention** and the new env-var contract.
23. **Add an ADR directory** (`docs/adr/`), and seed it with retroactive
    ADRs for: the static-seam test pattern, the JAR caching strategy,
    System.CommandLine choice, and the `--format` semantics.

### 9.4 Long term (1.x → 2.0)

24. **Wait for `System.CommandLine` GA** (A-15) and migrate. Plan an issue
    that tracks the upgrade so the beta exposure doesn't drift.
25. **Sub-command architecture**. As the CLI grows beyond `run`, the
    monolithic `Program.cs` + `RunCommand.cs` should become:
    ```
    src/Synthea.Cli/
      Program.cs                (composition root, DI wiring)
      Commands/
        RunCommand/
          RunCommand.cs
          RunOptions.cs
          ArgumentBuilder.cs    (the pure function, isolated)
        CacheCommand/
          CacheCommand.cs       (list / clear)
        VersionCommand.cs
      Services/
        IJarSource.cs / JarManager.cs
        IProcessRunner.cs / DefaultProcessRunner.cs
    ```
26. **Supply chain**. Enable Dependabot, CodeQL, and a `dotnet list package
    --vulnerable` step. Sign the NuGet package with a code-signing cert
    once available.
27. **Pre-built container image** with the Synthea JAR baked in for
    air-gapped environments. Matches the recommendation in
    `architectural-eval.md`.
28. **Architecture documentation overhaul** (A-16). Replace the 27-line
    Architecture.md with a real document: context, container, component
    diagrams (C4 model), data flow, failure modes, deployment topology,
    security posture.

---

## 10. Scorecard

Updated from the existing `scorecard.json`. Range 1–5 (higher is better).

| Category | Current `scorecard.json` | This review | Rationale |
|---|:---:|:---:|---|
| Structure | 4 | 4 | Clean `src/tests` shape and centralized props. Loses 1 for `CodexTaskProcessor` mixed into the assembly. |
| Design qualities | 3 | 3 | Pure argument builder and immutable record are strong; static seams and 473-line file pull it down. |
| Cross-cutting | 3 | 2 | No structured logging, no telemetry, no cancellation propagation, no config abstraction. |
| Dependencies | 2 | 2 | Beta `System.CommandLine`, unauthenticated GitHub, no `Dependabot`, no SBOM. |
| Pipeline | 4 | 3 | Branch filter likely-broken; Linux-only; no coverage gate; no vuln scan. |
| Documentation | 4 | 3 | Garbled README sections; thin Architecture.md; empty health-check.md; docs/deliverables clutter. |
| Testability | (not scored) | 3 | Good seams but mutable statics and `DisableTestParallelization` are a ceiling. |
| Security & supply chain | (not scored) | 2 | Optional checksum, no token/proxy, no Dependabot/CodeQL. |
| **Overall** | **B** | **B−** | Net regression once cross-cutting and pipeline are weighed alongside structure. |

---

## 11. Things Done Well (Worth Preserving)

Even with the criticisms above, this codebase has habits that should be
preserved as it grows:

1. **Pure `BuildArgumentList`** isolates the most-tested logic.
2. **Immutable `RunOptions` record** keeps state from leaking between
   layers.
3. **`ProcessStartInfo.ArgumentList`** (not string concat) for safety.
4. **Atomic JAR install** via temp-file + `File.Move`.
5. **SHA-256 verification when available**.
6. **Validators per option** with single-sentence error messages.
7. **`Directory.Build.props` + `TreatWarningsAsErrors`** as a baseline
   quality gate.
8. **`InternalsVisibleTo`** for tests — the right alternative to making
   everything `public`.
9. **Integration-test category split** (`Trait("Category", "Integration")`)
   with a graceful skip path when the environment is incomplete.

---

## 12. Open Questions for the Maintainer

Before acting on the recommendations above, the following decisions should
be made consciously:

1. **Is `CodexTaskProcessor` still in scope** for this assembly? If yes,
   how is it expected to be invoked? If no, where does it go?
2. **What is the intended default branch** (`main` or `master`)? The CI
   workflow and the actual branch disagree; one of them is wrong.
3. **Should the wrapper be usable air-gapped**? If yes, the JAR-path
   override and proxy support move from "Long term" to "Immediate".
4. **What is the API contract of the tool's assembly?** If only the `synthea`
   CLI surface is supported, tighten visibility. If library reuse is
   intentional, then the static seams should be redesigned.
5. **Does `--format` "merge with defaults" or "override defaults"?** Pick
   one and document it loudly; the current behavior surprises users either
   way.
6. **What exit-code convention** should the tool publish?
7. **What is the threat model?** Without one, the SHA-256 fallback and the
   GitHub-API dependency are hard to reason about.

---

## 13. Review Method

- Read all production sources: `Program.cs`, `RunCommand.cs`, `RunOptions.cs`,
  `JarManager.cs`, `ProcessHelpers.cs`, `CodexTaskProcessor.cs`,
  `Synthea.Cli.csproj`.
- Read all test sources: `CliTests`, `ProgramHandlerTests`,
  `ProgramRefactorTests`, `JarManagerTests`, `CodexTaskProcessorTests`,
  `AssemblyInfo`, `ScaffoldingSmokeTest`, `SyntheaRunTests`,
  `SyntheaCliWrapperRunTests`, `SkipTestException`, both test `.csproj`
  files.
- Read build/CI: `Directory.Build.props`, `.github/workflows/ci.yml`,
  `.github/workflows/nuget.yml`, `Synthea.Cli.sln`, `.gitignore`.
- Read docs: `README.md`, `CHANGELOG.md`, `CONTRIBUTING.md`, `AGENTS.md`,
  `docs/deliverables/Architecture.md`,
  `docs/deliverables/architectural-eval.md`,
  `docs/deliverables/synthea-feature-parity-analysis.md`,
  `docs/deliverables/roadmap.md`,
  `docs/deliverables/codex-automation.md`,
  `docs/deliverables/project-structure-recommendations.md`,
  `docs/deliverables/health-check.md` (empty), `scorecard.json`.
- Cross-referenced findings against the existing
  `docs/deliverables/architectural-eval.md` to avoid duplicating work and to
  identify which 2025 recommendations remain unresolved.

This document is a **review**, not an implementation plan. The
Recommendations section above proposes ordering and effort, but a separate
strategy/plan note (e.g., `SyntheaCli-strategy-2026.md`) should be authored
to commit to specific work items.

---

*End of review.*
