# 0001. Static seams and their removal

## Status
Accepted — implemented in `2eb5f5e` (PR #60).

## Context
Early iterations of `Synthea.Cli` used mutable-static seams to make
production code testable without a DI container:

- `Program.Runner` (settable `IProcessRunner` static)
- `Program.EnsureJarAsyncFunc` (settable `Func<...>` static)
- `JarManager.Http` (settable `HttpClient` static)
- `JarManager.CacheRootOverride` (settable cache path static)

Tests reassigned these fields per `[Fact]`. The pattern worked for
single-threaded test execution but forced
`[CollectionBehavior(DisableTestParallelization = true)]` in
`AssemblyInfo.cs`. As the test suite grew, that ceiling started to
hurt: every static mutation became a synchronization point, and any
test that forgot to restore a field could poison the next one.

## Decision
Adopt `Microsoft.Extensions.DependencyInjection`. Wire:

- `IProcessRunner` → `DefaultProcessRunner` (singleton)
- `IJarSource` → `JarManager` (singleton; instance fields for the
  `HttpClient`, cache root, and `ILogger<JarManager>`)

`Program.Main` builds a default `ServiceProvider`; tests build their
own and pass it to `Program.RunAsync(args, services)`. The four static
seams disappear. `DisableTestParallelization` is removed; xunit runs
the suite in parallel.

## Consequences
- Tests run in parallel; the unit suite stays under one second.
- No global mutable state to forget to reset.
- One new package (`Microsoft.Extensions.DependencyInjection`); ~2 KB
  per published binary.
- New parallelism exposed latent races that the serial suite hid
  (process-wide `Environment.*` mutation in `CliConfigTests` — fixed
  by `envGetter`-delegate overloads in PR #63).
