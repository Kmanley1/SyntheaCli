# Changelog

All notable changes to **synthea-cli** are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Released versions map 1:1 to `v*` git tags and to published
[`synthea-cli`](https://www.nuget.org/packages/synthea-cli) NuGet versions. From v0.5.0
onward, the per-release notes on the GitHub Releases page are generated automatically from
Conventional Commits (`tools/extract-release-notes.ps1`).

## [Unreleased]

## [1.0.2] - 2026-06-13

### Security
- The CLI now downloads a **pinned Synthea release (v4.0.0)** and **verifies it against a known SHA-256**
  before running it — closing the "downloads and executes an unverified JAR" gap on the `dotnet tool` path
  (the Docker image already pinned + verified). To run a different engine, pass `--jar`.

### Changed
- The default Synthea engine is now the pinned **v4.0.0** release instead of GitHub's mutable
  `releases/latest` rolling build, so runs are reproducible. Cached JARs are version-named
  (`synthea-v4.0.0-with-dependencies.jar`); upgrading re-downloads once.

### Fixed
- Integration tests now **skip cleanly** (instead of failing) when Java is absent, and CI provisions Java
  (`actions/setup-java`) so the real `java -jar` spawn path is actually exercised and gated.

## [1.0.1] - 2026-06-13

### Fixed
- Logs and wrapper status lines (download progress, "Using…") now write to **stderr**, keeping stdout a
  clean data channel for piping (e.g. `run --dry-run | sh`).
- Exit-code documentation reconciled with actual behavior — "Java not found" is `3`, "Java older than 17"
  is `1` (the README/Architecture tables were wrong).

### Changed
- The NuGet publish workflow now **gates on the unit tests passing** for the tagged commit and pushes with
  `--skip-duplicate`.

### Security
- The air-gapped Docker image now **verifies the pinned Synthea JAR against a known SHA-256** after download.

## [1.0.0] - 2026-06-13

First stable release. From here, the CLI flag surface, exit codes, and `config.json` keys form a
SemVer 1.x public contract — see the README "Stability & versioning" section.

### Added
- **Air-gapped Docker image** — a container bundling a self-contained build of the CLI plus a
  **pinned** Synthea release (v4.0.0), wired via `SYNTHEA_CLI_JAR_PATH`, published to GHCR on `v*`
  tags. The baked Synthea version is recorded in the `io.synthea.jar.version` image label and shown
  by `synthea --version`. Builds behind an SSL-inspecting proxy by trusting an optional corporate
  root CA from `certs/`. CI smoke-tests the image (generates a patient) before publishing.
- **Stability / SemVer policy** documented in the README — from 1.0.0 the flag surface, exit codes,
  and config keys are the public contract.

### Changed
- **Output now lands directly in the `-o <dir>` directory** (previously a nested `output/`
  subfolder). The CLI passes Synthea an absolute `exporter.baseDirectory`, so `--output` means what
  its help text says.
- Container images now stamp the real CLI version into the binary, so `docker run … --version`
  matches the image tag instead of reporting the source default.

### Fixed
- `synthea --version` now reports the real package version instead of the `1.0.0` assembly default.
- `synthea --version` and `synthea doctor` now recognize a `--jar` / `SYNTHEA_CLI_JAR_PATH` / config
  JAR — and the baked Synthea version — instead of reporting "no JAR cached" / "version unavailable".
- HTTP User-Agent now reflects the real CLI version (was a stale `Synthea.Cli/0.1`).

## [0.5.0] - 2026-05-17

Seventeen-plus feature PRs on top of v0.4.0.

### Added
- **Reproducibility controls** — `--reference-date`, `--end-date`, `--allow-future-end`,
  `--clinician-seed`, `--single-person-seed`, `--overflow` for byte-for-byte reproducible
  runs (#72).
- **Arbitrary property passthrough** — `--property KEY=VALUE` (repeatable) forwards any
  Synthea property the CLI doesn't model explicitly (#73).
- **FHIR exporter coverage** — `--us-core-version 3.1.1|4|5|6|7` and `--fhir-version R5`
  (#74); `--flexporter-mapping`, `--ig-dir`, `--bulk-data` for FHIR Bulk Data + custom IGs
  (#75).
- **`synthea doctor`** — environment check for Java, cache, config, network, and disk;
  exits non-zero on any FAIL (#76).
- **`synthea modules list` / `synthea modules describe`** — introspect the modules bundled
  in the cached JAR (#77).
- **`--progress`** — periodic status output during long runs (#81).
- **Help & UX** — `--version` now reports the resolved JAR, `--help` carries worked
  examples, and `--dry-run` is an alias for `--print-args` (#80).

### Changed
- **JVM heap auto-sizing** — `-Xmx` is sized automatically from `-p` (population),
  overridable with `--java-heap` (#78).
- **Large-run ergonomics** — module separator is OS-corrected automatically, and output is
  split into subfolders for big runs (#79).
- README rewritten use-case-first; stale platform/version facts refreshed (#67).

### Fixed
- Malformed config JSON now exits non-zero instead of being silently ignored (#69).
- Synthea stderr is surfaced with remediation hints on failure (#70).
- Unknown `--state` values are caught in preflight with a did-you-mean suggestion (#71).

### ⚠️ Breaking
- **Minimum Java raised to 17.** The CLI now fails fast with a clear message on Java < 17
  (#68). Earlier 0.x releases accepted Java 11+.

### CI / packaging
- Golden-file `--help` stability tests (#82); NuGet icon, project URL, and refined tags
  (#83); release notes auto-extracted from Conventional Commits on tag push (#84).

## [0.4.0] - 2026-05-16

### Changed
- **Target framework upgraded from .NET 8 to .NET 10 (LTS)** (#66). Requires the .NET 10
  runtime / SDK.

## [0.3.1] - 2026-05-16

### Fixed
- `--state` now accepts both USPS two-letter codes (`OH`) and full state names (`Ohio`), in
  both the validator and the value passed to Synthea (#65).

## [0.3.0] - 2026-05-16

First full .NET CLI implementation.

### Added
- `RunCommand` / `RunOptions` with full parameter validation and System.CommandLine
  integration.
- `JarManager` — automatic Synthea JAR download, caching, and optional checksum validation.
- `ProcessHelpers` for robust Java process execution and output capture.
- Parameter validation for state, ZIP, gender, age range, FHIR version, and file-path
  existence, with user-friendly error messages and a full help system.
- Cross-platform setup scripts (`setup.sh`, `setup.ps1`) and centralized build output via
  `Directory.Build.props`.
- Codex setup + troubleshooting documentation.

> **Correction (2026-06-13):** earlier drafts of this file carried a `[1.0.0] - 2025-08-09`
> entry and 2025-08 dates for 0.3.0. No `v1.0.0` was ever tagged or published, and the
> 0.3.x–0.5.0 line actually shipped 2026-05-16/17. Those entries were aspirational /
> auto-generated and have been removed so this changelog matches the real git tag and NuGet
> release history.

## [0.2.0] - 2025-05-23
- Numerous CLI enhancements over v0.1.0:
  - population size and random seed options
  - gender, age range, module, and ZIP filters
  - configuration file and FHIR version support
  - snapshot management (`--initial-snapshot`, `--updated-snapshot`, `--days-forward`)
  - output format selection (CSV, FHIR, etc.)
- Windows `nuget-helper.ps1` script and improved `setup.sh` logic
- Refactored `Program` and expanded unit tests
- README updates; package version bumped to 0.2.0.

## [0.1.0] - 2025-05-20
- First packaged release of the `synthea-cli` .NET global tool.
