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
  tags. *Status: Dockerfile + `.github/workflows/docker.yml` landed 2026-06-13 (this session);
  needs a real build + smoke test in CI before relying on it.*
- **Truthful release record.** CHANGELOG backfilled for 0.3.1/0.4.0/0.5.0 and the phantom `[1.0.0]`
  removed. *Status: done 2026-06-13.*
- **A 1.0.0 release pass**: bump `PackageVersion`, tag `v1.0.0`, let the NuGet + release-notes +
  docker workflows fire, smoke-test the published artifacts.

### SHOULD
- **Pin the baked Synthea JAR by digest** (not just "latest at build time") so the image is fully
  reproducible, and label the image with the resolved Synthea version.
- **Image smoke test in CI**: `docker run … doctor` + a tiny `run -p 1` to prove the baked JAR works
  before push.
- Decide the **SemVer support contract** for 1.0.0 (which CLI surface is "stable"; the golden-file
  `--help` tests already guard drift).

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
- Pin strategy for the baked JAR: resolve "latest" → concrete tag at build (simple) vs. require an
  explicit `SYNTHEA_VERSION` for releases (reproducible). *Leaning: require explicit pin for tagged
  releases; allow "latest" only for `workflow_dispatch`.*
- Does 1.0.0 imply a frozen config-file schema? If so, document the compatibility promise.
- GHCR namespace is `ghcr.io/kmanley1/...` (lowercased). Confirm package visibility (public) after
  first push.

## Sources
- This session's grounding workflow (5-dimension verified assessment), 2026-06-13.
- `SyntheaCli-notes-design-review.md` (A-1..A-40 register; rec #27 = pre-built container image).
- Stale inputs deliberately *not* trusted: `docs/research/roadmap.md`,
  `docs/research/feature-parity-analysis.md` (both dated Jan 2025, pre-v0.5.0).
- Git tag + commit history v0.3.0..v0.5.0.
