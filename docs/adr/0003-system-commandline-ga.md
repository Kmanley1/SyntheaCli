# 0003. System.CommandLine GA migration

## Status
Accepted — migrated in `0965880` (PR #53).

## Context
`Synthea.Cli` shipped with `System.CommandLine` 2.0.0-beta4 — a beta
package with a 9-figure download count but no GA backing. Dependabot
opened a PR weekly to bump it to a newer beta or release candidate,
each of which broke the API in non-trivial ways:

- `InvocationContext` was removed in favour of `ParseResult`.
- `SetHandler` overloads were reshaped.
- `GetValueForOption` moved to `ParseResult.GetValue`.
- `IsRequired = true` became `Required = true` on `Option<T>`.
- The validator delegate changed from a named
  `ValidateSymbolResult<OptionResult>` type to `Action<OptionResult>`.

The validator delegate change was particularly nasty: a relapse to the
beta-style `ValidateSymbolResult<OptionResult>` signature is a compile
error in GA but the migration churn between betas made the error
message ambiguous.

## Decision
Migrate straight to the first GA release (2.0.x) on a clean tree
before any other work. Pin the version. Drop the `System.CommandLine`
ignore-entry from `.github/dependabot.yml`. Add a regression test
(`RunCommand_Validators_UseActionOptionResultDelegate`) that asserts
each validator's delegate type, with a sanity check that the named
beta-era `ValidateSymbolResult<T>` type is not loadable.

## Consequences
- No more weekly Dependabot reopens on the same package.
- API surface is stable; future bumps are point releases.
- Future contributors looking at the parser see GA documentation, not
  beta blog posts.
- One-time cost: rewrote every option definition and the handler
  signature.
