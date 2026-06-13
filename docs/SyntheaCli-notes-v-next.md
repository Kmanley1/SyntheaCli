---
title: SyntheaCli — v-next / v1.0.0 scope
status: notes
last-updated: 2026-06-13
---

# SyntheaCli — v-next (the road to v1.0.0)

## Where we are (verified 2026-06-13)

- Shipped version: **v0.5.0** (git tag == NuGet `synthea-cli` 0.5.0 == csproj `PackageVersion`).
- Health: build clean (0 warnings, `--warnaserror`), **312 unit tests green, 91.14% line / 84.51%
  branch** coverage. Working tree clean, master in sync with origin.
- Target framework **.NET 10 (LTS)**; **Java 17+** floor enforced (C2, #68).
- `System.CommandLine` is on **2.0.x GA** — the largest "don't call it 1.0 yet" risk from the
  design review is resolved. The remaining gate to 1.0.0 is **positioning/scope, not technical debt.**

## The v1.0.0 thesis

> 1.0.0 is an **air-gapped / enterprise-distribution** milestone, not a Java feature-parity
> milestone.

The wrapper's value is *convenience* — "no Java setup, no JAR hunting, one command." The highest-
leverage way to compound that value is to make the tool trivially droppable into CI and locked-down
enterprise/clinical networks. Most of the hard prerequisites already exist: `--jar` /
`SYNTHEA_CLI_JAR_PATH` override, checksum verification, `GITHUB_TOKEN` + `HTTPS_PROXY` support, and a
`doctor` preflight. The missing piece is a **container image with a pinned Synthea JAR baked in** so
the tool runs with zero network access.

## MoSCoW for v1.0.0

### MUST
- **Docker image (air-gapped).** Self-contained CLI + pinned Synthea JAR, published to GHCR on `v*`
  tags. *Status: DONE 2026-06-13 — built + smoke-tested locally and on GHCR; `docker.yml` pins
  Synthea v4.0.0 for tag builds, smoke-tests the image (doctor + `run -p 1`) before publishing, and
  stamps the CLI + Synthea versions into the image.*
- **Truthful release record.** CHANGELOG backfilled for 0.3.1/0.4.0/0.5.0 and the phantom `[1.0.0]`
  removed. *Status: done 2026-06-13.*
- **A 1.0.0 release pass**: bump `PackageVersion`, tag `v1.0.0`, let the NuGet + release-notes +
  docker workflows fire, smoke-test the published artifacts.

### SHOULD
- ~~**Pin the baked Synthea JAR**~~ *DONE 2026-06-13* — pinned by release **tag** (v4.0.0), recorded
  in the `io.synthea.jar.version` image label + `synthea --version`. (Digest/content-pinning remains
  a narrower future option.)
- ~~**Image smoke test in CI**~~ *DONE 2026-06-13* — `docker.yml` runs `doctor` + `run -p 1` and
  asserts FHIR output before pushing.
- ~~Decide the **SemVer support contract**~~ *DONE 2026-06-13* — documented in the README "Stability
  & versioning" section (flag surface + exit codes + config keys are the 1.x contract).

### COULD
- `ingest` / `validate` / `evolve` subcommands (carried from earlier planning; only if a concrete
  consumer need shows up).
- Homebrew / winget distribution alongside the dotnet tool + container.
- `--module-dir` ergonomics and richer `modules describe` output.

### WON'T (v1.0.0 non-goals — stated so they stop anchoring the roadmap)
- **Physiology simulation (SBML/ODE solvers).** The old `roadmap.md` billed this "CRITICAL #1" at
  6–12 months. Re-implementing Synthea internals in a *wrapper* is a category error — physiology
  already runs inside the JAR; expose it via `--property` passthrough if a need arises.
- **Flexporter JS execution engine.** Only the mapping-file passthrough (`--flexporter-mapping`,
  #75) belongs in the wrapper; running the JS engine is the JAR's job.
- **GraphViz / attributes / concepts analysis tools.** Out of scope for a generation wrapper.

## De-facto backlog (reconstructed from git, since the original plan doc was never committed)

Work items live as `A–F` IDs in commit messages. Shipped in v0.5.0: A1–A3, A5, A6, A8–A10, B4, B6,
C1–C7, D1, D2, D4, D8, F3–F5. **Absent from history** (no commit, no surviving plan): B1–B3, B5,
D3, D5–D7, the entire E-series, F1–F2. These IDs are orphaned references — if any represent real
intended work, reconstruct them here before treating them as a backlog. Best guess from the gaps:
DI/test polish, additional subcommands, and docs/CI hardening — none obviously v1.0.0-blocking.

## Open questions
- ~~Pin strategy for the baked JAR~~ *RESOLVED 2026-06-13* — tag builds pin `SYNTHEA_VERSION=v4.0.0`;
  `workflow_dispatch` may use "latest" and does not publish. Implemented in `docker.yml`.
- ~~Config-file schema freeze~~ *RESOLVED* — the README "Stability & versioning" section names the
  `config.json` keys as part of the 1.x contract.
- ~~GHCR visibility~~ *CONFIRMED public* 2026-06-13 (anonymous pull works).
- **Remaining for the release pass:** bump `<Version>` to 1.0.0, promote CHANGELOG `[Unreleased]` →
  `[1.0.0]`, tag `v1.0.0` (which republishes `:latest` from the smoke-tested, pinned image).

## Sources
- This session's grounding workflow (5-dimension verified assessment), 2026-06-13.
- `SyntheaCli-notes-design-review.md` (A-1..A-40 register; rec #27 = pre-built container image).
- Stale inputs deliberately *not* trusted: `docs/research/roadmap.md`,
  `docs/research/feature-parity-analysis.md` (both dated Jan 2025, pre-v0.5.0).
- Git tag + commit history v0.3.0..v0.5.0.
