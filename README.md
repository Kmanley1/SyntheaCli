# Synthea.Cli

[![NuGet](https://img.shields.io/nuget/v/Synthea.Cli.svg)](https://www.nuget.org/packages/Synthea.Cli/)

`Synthea.Cli` is a cross-platform **.NET 8** command-line wrapper around **[Synthea™](https://github.com/synthetichealth/synthea)** — MITRE’s open-source synthetic health-record generator.  
The tool downloads the latest Synthea JAR on first use, caches it locally, and gives you a simple, typed interface plus first-class scripting and container support.

---

## Table of Contents

- [Features](#features)
- [Quick Start](#quick-start)
  - [Prerequisites](#prerequisites)
  - [Install as a global tool](#install-as-a-global-tool)
  - [Run](#run)
- [Developer Guide](#developer-guide)
  - [Clone \& Build](#clone--build)
  - [Unit Tests](#unit-tests)
  - [Docker](#docker)
  - [`setup.sh` for CI / Codex](#setupsh-for-ci--codex)
    - [GitHub Actions example](#github-actions-example)
  - [Running Integration Tests](#running-integration-tests)
- [Project Layout](#project-layout)
- [Contributing](#contributing)
- [License \& Credits](#license--credits)
- [Architecture](#architecture)

---

## Features

| Feature | Details |
|---------|---------|
| **Zero-install JAR** | Automatically fetches the newest `synthea-with-dependencies.jar` from GitHub releases and verifies its SHA-256 checksum when the upstream publishes one. |
| **Configurable cache** | Stores the JAR under `%LOCALAPPDATA%\Synthea.Cli` on Windows and `~/.local/share/Synthea.Cli` on Linux/macOS; refresh anytime with `--refresh`. |
| **Friendly flags** | `--state OH`, `--city "Columbus"`, `--output ./data`, `--seed 42`, `--initial-snapshot snap.json`, `--format FHIR` map directly to Synthea flags. |
| **Additive formats** | `--format` is exclusive (enables only the named formats, disables the rest). Use `--add-format` (repeatable) to enable a format additively without overriding Synthea defaults. |
| **Verbosity control** | `--verbose` enables debug logs; `--quiet` suppresses info logs (warnings and errors still print). Default is info-level via `Microsoft.Extensions.Logging`. |
| **Portable** | Runs on Windows, macOS, Linux—wherever .NET 8 + Java 11+ are available. |
| **Unit-tested** | Complete test coverage via xUnit and Coverlet; network & process calls are fully mocked. |

---

## Quick Start

### Prerequisites

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download>  
- **Java ≥ 11** in your `PATH` (`java -version`) – *only required at runtime*.

### Install as a global tool

```bash
dotnet tool install --global Synthea.Cli --version 1.0.0
```

### Run

```bash
# Generate 10 synthetic patients from Ohio into ./output using a fixed seed
synthea run --output ./output --population 10 --state OH --seed 12345

# Same, but force-refresh the cached JAR
synthea --refresh run -o ./output --population 10 --state TX --city Austin

# Advance 30 days from an initial snapshot and limit to CSV output only
synthea run -o ./output --initial-snapshot snap.json --days-forward 30 --format CSV

# Keep Synthea's default exporters AND also turn on CCDA (additive)
synthea run -o ./output --population 10 --state OH --add-format CCDA

# Print the java invocation that would be run, without running it
synthea run -o ./output --population 5 --state OH --print-args

# Debug-level logs (download URLs, cache hits, every internal step)
synthea --verbose run -o ./output --population 5 --state OH

# Suppress info logs; only warnings and errors print
synthea --quiet run -o ./output --population 5 --state OH
```

> **Short alias:** `syn` works everywhere `synthea` does.

### Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success (Synthea's own exit code is propagated when the JAR runs) |
| `1` | Argument validation error (invalid state, ZIP, gender, age range, …) |
| `2` | Filesystem / I/O error |
| `3` | External-dependency error (GitHub unreachable, checksum mismatch, missing release asset) |
| `4` | Unexpected error (catch-all) |
| `130` | Cancelled by user (Ctrl+C); the child Java process is terminated along with the CLI |

---

## Developer Guide

### Clone & Build

```bash
git clone https://github.com/Kmanley1/Synthea.Cli.git
cd Synthea.Cli
dotnet restore
dotnet build
dotnet run -- run -o ./out --population 5 --state AK
```

### Unit Tests

```bash
dotnet test --collect:"XPlat Code Coverage"
# Coverage report in ./TestResults/**/coverage.cobertura.xml
```

The project includes **37 comprehensive tests**:
- **33 unit tests** covering all CLI functionality, validation logic, and edge cases
- **4 integration tests** with actual Java/Synthea execution and file generation validation

All tests use xUnit framework with full mocking for network and process calls.

#### Auto-Fix for Package Corruption

The test infrastructure includes automatic detection and repair of NuGet package corruption:

```bash
# Enhanced test setup with auto-fix capability
.\setup-test-environment.ps1

# VS Code tasks with auto-fix (Ctrl+Shift+P -> Tasks: Run Task)
test-with-autofix              # Run tests with automatic corruption fix
setup-test-environment         # Full environment setup

# Manual corruption fix if needed
.\fix-java-detection.ps1       # Repair corrupted NuGet packages
```

**Auto-fix triggers on these errors:**
- `The type or namespace name 'Xunit' could not be found`
- `Package [name], version [version] was not found`  
- `NuGet restore might have only partially completed`

The system automatically clears caches, restores packages, rebuilds, and re-runs tests.

### Docker

> **Status:** Planned. A Dockerfile is not yet included in this version.
> The example below is illustrative of the intended usage once one is added.

```bash
# Illustrative only — requires a Dockerfile to be added first.
docker build -t synthea.cli .
docker run --rm -v "$PWD/out":/data synthea.cli run --output /data --state CA --population 100
```


### `setup.sh` for CI / Codex

The setup script has moved to `tools/setup.sh` for better project organization.

`tools/setup.sh` does:

1. Installs OpenJDK 11 and .NET 8 on Ubuntu runners  
2. Restores & publishes `Synthea.Cli` to `/workspace/synthea-cli/bin`

#### GitHub Actions example


```yaml
steps:
  - uses: actions/checkout@v4
  - run: ./tools/setup.sh
  - run: dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll -- --help
```

### Running Integration Tests

Execute all integration tests with:

```bash
dotnet test --filter Category=Integration
```

This builds the CLI and runs cross-platform tests including `ScaffoldingSmokeTest`, `SyntheaRunTests`,
and `SyntheaCliWrapperRunTests`.

---

## Project Layout

```text
SyntheaCli/
├─ .gitattributes                # enforce LF for shell scripts
├─ .gitignore
├─ README.md
├─ Synthea.Cli.code-workspace    # VS Code workspace file
├─ Directory.Build.props         # Shared MSBuild settings (TreatWarningsAsErrors, Nullable)
├─ Synthea.Cli.sln               # Visual Studio solution
├─ tools/
│   ├─ setup.sh                  # CI / Linux build script
│   ├─ setup.ps1                 # CI / Windows build script  
│   └─ windows/
│       ├─ install-vscode-extensions.ps1
│       ├─ synthea-cli-create.ps1 # helper to scaffold new CLI repo
│       └─ nuget-helper.ps1
├─ setup-test-environment.ps1    # Test environment setup for Windows
├─ docs/
│   ├─ deliverables/             # Project documentation and analysis
│   ├─ prompts/                  # AI automation templates
│   ├─ reference/                # External documentation  
│   └─ research/                 # Research materials
├─ src/Synthea.Cli/              # main CLI project
│   ├─ Program.cs                # System.CommandLine entry point
│   ├─ RunCommand.cs             # Run command implementation
│   ├─ RunOptions.cs             # Command options definitions
│   ├─ JarManager.cs             # JAR download & cache helper
│   ├─ ProcessHelpers.cs         # Process execution utilities
│   └─ Synthea.Cli.csproj
├─ tests/
│   ├─ Synthea.Cli.UnitTests/           # unit tests (xUnit)
│   │   ├─ CliTests.cs
│   │   ├─ JarManagerTests.cs
│   │   ├─ ProgramHandlerTests.cs
│   │   ├─ ProgramRefactorTests.cs
│   │   └─ Synthea.Cli.UnitTests.csproj
│   └─ Synthea.Cli.IntegrationTests/     # integration tests (xUnit)
│       ├─ ScaffoldingSmokeTest.cs
│       ├─ SyntheaRunTests.cs
│       ├─ SyntheaCliWrapperRunTests.cs
│       ├─ SkipTestException.cs
│       └─ Synthea.Cli.IntegrationTests.csproj
└─ third-party/                  # External dependencies and libraries
    └─ MPXJ.Net/
```

---

## Contributing

1. Fork → feature branch → PR.  
2. Ensure `dotnet test` passes and coverage ≥ 90 %.  
3. If you add new dependencies, update both `.csproj` files and `tools/setup.sh`.  
4. Follow `dotnet format` / `.editorconfig` (4-space indents, C# latest).

Issues and feature requests welcome!

---

## License & Credits

- **Tool code** © 2025 Ken Manley — MIT License (`LICENSE`).  

- **Synthea™** is © MITRE, Apache-2.0. This CLI downloads the official Synthea JAR but is **not** an official MITRE project.  
- Uses **System.CommandLine** (`2.0.0-beta4`) under MIT.

---

Happy generating! 🎉

## Architecture

See [docs/deliverables/Architecture.md](docs/deliverables/Architecture.md) for detailed architectural information and design decisions.
