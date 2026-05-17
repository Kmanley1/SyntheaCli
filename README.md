# synthea-cli

[![NuGet](https://img.shields.io/nuget/v/synthea-cli.svg)](https://www.nuget.org/packages/synthea-cli/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Generate realistic synthetic patient data on Windows, macOS, or Linux with a single command.** No HIPAA exposure, no Java setup, no JAR hunting — `synthea-cli` wraps MITRE's [Synthea™](https://github.com/synthetichealth/synthea) (the gold-standard synthetic health-record generator) as a `dotnet tool` you install once and forget about.

```bash
dotnet tool install --global synthea-cli
synthea run -o ./output -p 100 --state OH --add-format CSV
# 100 Ohio patients with realistic clinical histories in FHIR + CSV, 60 seconds.
```

---

## Use it for

- **Testing FHIR integrations** — generate patient bundles to POST against HAPI FHIR, Azure Health Data Services, Google Healthcare API, AWS HealthLake, or any FHIR R4/STU3 endpoint. Bulk-data NDJSON output supports `$import` workflows.
- **Validating EMR / HL7 / CCDA pipelines** — generate realistic encounters, conditions, medications, immunizations, and procedures with SNOMED/LOINC/RxNorm codes. CCDA documents come out as XML; CSV exports map cleanly to OMOP CDM via [OHDSI ETL-Synthea](https://github.com/OHDSI/ETL-Synthea).
- **Loading test data into clinical data warehouses** — CSV exporter splits patients/encounters/conditions/medications/etc. into separate files, with referential integrity. Use as the source data for dbt models, dimensional warehouses, and analytics pipelines.
- **Stress-testing claims pipelines** — CPCDS exporter produces CARIN BB-shaped claims; BFD RIF exporter produces Medicare-shaped claims. Both with realistic adjudication patterns.
- **Reproducible test fixtures** — `--seed` plus `--clinician-seed` plus `--single-person-seed` make any generation byte-for-byte reproducible across machines and CI runs.

If you're a healthcare data engineer, integration tester, or anyone who needs realistic-but-fake patient data, this saves you the install dance.

---

## Table of contents

- [Quick start](#quick-start)
- [Common scenarios](#common-scenarios)
- [Cache management](#cache-management)
- [Configuration](#configuration)
- [Exit codes](#exit-codes)
- [Architecture](#architecture)
- [Developer guide](#developer-guide)
- [Contributing](#contributing)
- [License & credits](#license--credits)

---

## Quick start

### Prerequisites

- **.NET 10 SDK** — <https://dotnet.microsoft.com/download> (LTS, ends Nov 2028)
- **Java ≥ 17** in your `PATH` (verify with `java -version`) — required at runtime only. Synthea v4.0 dropped JDK 11 support; OpenJDK 17 LTS (Eclipse Temurin recommended) or newer is required.

### Install as a global tool

```bash
dotnet tool install --global synthea-cli
# To upgrade later:
dotnet tool update --global synthea-cli
```

> **Short alias:** `syn` works everywhere `synthea` does.

### First run

```bash
# 10 synthetic Ohio patients in FHIR R4 (Synthea's default exporter)
synthea run -o ./output -p 10 --state OH

# Inspect what landed:
#   ./output/fhir/*.json    <- one FHIR Bundle per patient
#   ./output/metadata/      <- runtime parameters captured
```

First invocation downloads the Synthea JAR (~180 MB) from GitHub releases. Subsequent runs hit the local cache.

---

## Common scenarios

### Multi-format export (FHIR + CSV + CCDA in one run)

```bash
synthea run -o ./output -p 50 --state OH `
    --add-format CSV `
    --add-format CCDA
# Outputs:
#   ./output/fhir/*.json      (default; one Bundle per patient)
#   ./output/csv/*.csv        (patients, encounters, conditions, medications, ...)
#   ./output/ccda/*.xml       (one CCDA document per patient)
```

`--add-format` is **additive** — adds the named format alongside Synthea's default FHIR output. Use `--format` instead if you want **exclusive** behavior (only the named format, all others disabled). Valid format names: `FHIR`, `CSV`, `CCDA`, `BULKFHIR`, `CPCDS`.

### Reproducible fixtures (same seed → same patients)

```bash
synthea run -o ./output -p 25 --state OH --seed 42
# Identical command on any machine produces identical patients.
```

### City and ZIP filters

```bash
# All patients live in Cleveland
synthea run -o ./output -p 25 --state OH --city Cleveland

# All patients in a specific ZIP
synthea run -o ./output -p 25 --state OH --zip 44101
```

### Demographic filters

```bash
# Only female patients, ages 30-50
synthea run -o ./output -p 25 --state OH --gender F --age-range 30-50
```

### Air-gapped use (pre-downloaded JAR)

```bash
# Skip the GitHub fetch entirely — point at a JAR you staged yourself
synthea run -o ./output -p 25 --state OH --jar /opt/synthea-with-dependencies.jar
```

### Behind a corporate proxy + with a GitHub token

```bash
# Token avoids the 60-req/hour anonymous GitHub API limit (matters in CI)
$env:GITHUB_TOKEN = "ghp_..."
$env:HTTPS_PROXY = "http://corp-proxy:8080"
synthea run -o ./output -p 25 --state OH
```

Or set both permanently in `~/.synthea-cli/config.json` (see [Configuration](#configuration)).

### Supply-chain hardening

```bash
# Fail the run if the upstream release does not ship a .sha256 asset
synthea run -o ./output -p 25 --state OH --insist-checksum
```

### Inspect what would be invoked (without running)

```bash
synthea run -o ./output -p 25 --state OH --print-args
# Prints the exact `java -jar ...` command, then exits.
# Useful for debugging or building CI shell scripts.
```

### Verbosity

```bash
# Debug-level logs (download URLs, cache hits, every internal step)
synthea --verbose run -o ./output -p 5 --state OH

# Warnings/errors only (good for CI logs)
synthea --quiet run -o ./output -p 5 --state OH
```

### Environment check

```bash
# Probe Java, cache dir, cached JAR age, config file, GitHub reachability, and free disk
synthea doctor
# Exits 0 on OK + WARN, exits 1 on any FAIL. Warnings are informational —
# `--jar` lets you bypass GitHub entirely if the reachability probe is unhappy.
```

---

## Cache management

The downloaded Synthea JAR is cached under `%LOCALAPPDATA%\Synthea.Cli` on Windows and `~/.local/share/Synthea.Cli` on Linux/macOS. Two sub-commands surface the cache without a file manager:

```bash
# List cached JARs with their size and last-modified date
synthea cache list

# Delete every cached JAR (with confirmation; pass --yes to skip)
synthea cache clear --yes

# Or force a fresh download on the next run
synthea --refresh run -o ./output -p 5 --state OH
```

---

## Configuration

Four sources can supply JAR-management settings, in precedence order (earlier wins):

1. **CLI flag** — `--jar <path>`, `--insist-checksum`
2. **Environment variable** — `SYNTHEA_CLI_JAR_PATH`, `GITHUB_TOKEN`, `HTTPS_PROXY` / `HTTP_PROXY`, `SYNTHEA_CLI_INSIST_CHECKSUM`
3. **Config file** — `~/.synthea-cli/config.json`:

   ```json
   {
     "jarPath": "/opt/synthea-with-dependencies.jar",
     "insistChecksum": true,
     "githubToken": "ghp_xxxxxxxxxxxxxxxxxxxx",
     "httpsProxy": "http://corp-proxy:8080"
   }
   ```

4. **Built-in default** — download the latest release JAR from GitHub.

**Notes:**
- `GITHUB_TOKEN` (when set) is attached as `Authorization: Bearer <token>` on GitHub API calls so anonymous CI runs don't hit the 60-req/hour rate limit.
- `HTTPS_PROXY` is wired into the HTTP client at startup.
- `--insist-checksum` fails the run if the upstream release does not publish a `.sha256` asset (default off for backward compatibility — recommended on in production CI).
- Token is per-request, not stored on the HTTP client default headers, so it doesn't leak to non-GitHub URLs.

---

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success (Synthea's own exit code is propagated when the JAR runs) |
| `1` | Argument validation error (invalid state, ZIP, gender, age range, …) |
| `2` | Filesystem / I/O error |
| `3` | External-dependency error (GitHub unreachable, checksum mismatch, missing release asset) |
| `4` | Unexpected error (catch-all) |
| `130` | Cancelled by user (Ctrl+C); the child Java process is terminated along with the CLI |

---

## Architecture

- **Container:** single .NET 10 process spawning `java -jar synthea-with-dependencies.jar`
- **JAR management:** GitHub API client + filesystem cache with optional SHA-256 verification
- **Configuration:** four-source precedence resolver (CLI > env > config file > default)
- **DI:** `Microsoft.Extensions.DependencyInjection` wires `IProcessRunner`, `IJarSource`, `ILogger<T>`
- **Validation:** option validators reject bad input before launching the JAR (state codes, ZIP shapes, gender, age range, format names, file existence, etc.)

See [docs/deliverables/Architecture.md](docs/deliverables/Architecture.md) for the C4-style architecture document and [docs/adr/](docs/adr/) for the five Architecture Decision Records covering: DI container, JAR caching, System.CommandLine GA migration, `--format` semantics, and passthru token ordering.

---

## Developer guide

### Clone & build

```bash
git clone https://github.com/Kmanley1/SyntheaCli.git
cd SyntheaCli
dotnet restore
dotnet build
dotnet run --project src/Synthea.Cli -- run -o ./out -p 5 --state OH
```

### Tests

```bash
# Unit tests (fast; default; ~104 tests)
dotnet test --filter "Category!=Integration"

# Integration tests (require Java on PATH; ~3 tests; ~30s)
dotnet test --filter Category=Integration

# With coverage
dotnet test tests/Synthea.Cli.UnitTests/Synthea.Cli.UnitTests.csproj `
    --collect:"XPlat Code Coverage"
# Coverage report in ./TestResults/**/coverage.cobertura.xml
```

Coverage gate (enforced in CI): **≥ 80% line, ≥ 75% branch**. Current baseline: 86.96% line / 82.84% branch.

### Cross-platform CI

GitHub Actions matrix runs on `ubuntu-latest` and `windows-latest` for every PR. CodeQL scans on every push. Dependabot keeps NuGet and GitHub Actions packages current.

### Releasing

Tag a version (`vX.Y.Z`) and push. `.github/workflows/nuget.yml` runs `dotnet pack` and `dotnet nuget push` automatically.

```bash
git tag -a v0.4.1 -m "..."
git push origin v0.4.1
```

### Useful project structure

```text
SyntheaCli/
├─ src/Synthea.Cli/                  # CLI source
│   ├─ Program.cs                    # DI composition + dispatch
│   ├─ RunCommand.cs                 # `run` subcommand + option definitions
│   ├─ CacheCommand.cs               # `cache list` / `cache clear`
│   ├─ JarManager.cs                 # GitHub download + cache + checksum
│   ├─ CliConfig.cs                  # 4-source precedence resolver
│   ├─ UsStates.cs                   # USPS code <-> full name lookup
│   ├─ ProcessHelpers.cs             # IProcessRunner / DefaultProcessRunner
│   └─ Synthea.Cli.csproj
├─ tests/
│   ├─ Synthea.Cli.UnitTests/        # 104 unit tests, full mocks
│   └─ Synthea.Cli.IntegrationTests/ # cross-platform end-to-end
├─ docs/
│   ├─ deliverables/Architecture.md  # C4-style architecture
│   └─ adr/                          # 5 Architecture Decision Records
├─ .github/workflows/                # ci.yml, nuget.yml, codeql.yml
├─ tools/                            # setup.sh / setup.ps1 (CI bootstrap)
└─ Synthea.Cli.sln
```

---

## Contributing

1. Fork → feature branch → PR
2. Ensure `dotnet test` passes; coverage gate (80% line / 75% branch) holds
3. Run `dotnet format --verify-no-changes --severity warn` (CI enforces it)
4. Follow Conventional Commits in your PR title (e.g. `feat(run): ...`, `fix(jar): ...`, `docs: ...`)

Issues and feature requests welcome — see [docs/adr/](docs/adr/) for context on prior design decisions before proposing changes that touch them.

---

## License & credits

- **Tool code** © 2026 Ken Manley — [MIT License](LICENSE)
- **Synthea™** is © MITRE Corporation, licensed [Apache-2.0](https://github.com/synthetichealth/synthea/blob/master/LICENSE). This CLI downloads the official Synthea JAR but is **not** an official MITRE project.
- Uses [System.CommandLine](https://github.com/dotnet/command-line-api) 2.0.x GA under MIT.
- Uses [Microsoft.Extensions.DependencyInjection](https://github.com/dotnet/runtime) and [Microsoft.Extensions.Logging](https://github.com/dotnet/runtime) under MIT.

Happy generating! 🎉
