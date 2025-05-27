# Synthea-CLI

[![NuGet](https://img.shields.io/nuget/v/synthea-cli.svg)](https://www.nuget.org/packages/synthea-cli/)

`synthea-cli` is a cross-platform **.NET 8** command-line wrapper around **[Synthea™](https://github.com/synthetichealth/synthea)** — MITRE’s open-source synthetic health-record generator.  
The tool downloads the latest Synthea JAR on first use, caches it locally, and gives you a simple, typed interface plus first-class scripting and container support.

---

## Table of Contents

- [Synthea-CLI](#synthea-cli)
  - [Table of Contents](#table-of-contents)
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
    - [Pre/Post Context Tasks](#prepost-context-tasks)
  - [Project Layout](#project-layout)
  - [Contributing](#contributing)
  - [License \& Credits](#license--credits)
  - [Architecture](#architecture)

---

## Features

| Feature | Details |
|---------|---------|
| **Zero-install JAR** | Automatically fetches the newest `synthea-with-dependencies.jar` from GitHub releases and verifies its SHA-256 checksum. |
| **Configurable cache** | Stores the JAR under `%LOCALAPPDATA%\synthea-cli` / `$XDG_CACHE_HOME/synthea-cli`; refresh anytime with `--refresh`. |
| **Friendly flags** | `--state OH`, `--city "Columbus"`, `--output ./data`, `--seed 42`, `--initial-snapshot snap.json`, `--format FHIR` map directly to Synthea flags. |
| **Portable** | Runs on Windows, macOS, Linux, containers—wherever .NET 8 + Java 17+ are available. |
| **Docker image** | Multi-stage Dockerfile builds a slim runtime with OpenJDK 17 and publishes the tool. |
| **Unit-tested** | ≥ 90 % line coverage via xUnit and Coverlet; network & process calls are fully mocked. |

---

## Quick Start

### Prerequisites

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download>  
- **Java ≥ 17** in your `PATH` (`java -version`) – *only required at runtime*.

### Install as a global tool

```bash
dotnet tool install --global synthea-cli --version 0.2.0
```

### Run

```bash
# Generate 10 synthetic patients from Ohio into ./output using a fixed seed
synthea run --output ./output --population 10 --state OH --seed 12345

# Same, but force-refresh the cached JAR
synthea --refresh run -o ./output --population 10 --state TX --city Austin

# Advance 30 days from an initial snapshot and limit to CSV output only
synthea run -o ./output --initial-snapshot snap.json --days-forward 30 --format CSV
```

> **Short alias:** `syn` works everywhere `synthea` does.

---

## Developer Guide

### Clone & Build

```bash
git clone https://github.com/Kmanley1/synthea-cli.git
cd synthea-cli
dotnet restore
dotnet build
dotnet run -- run -o ./out --population 5 --state AK
```

### Unit Tests

```bash
dotnet test --collect:"XPlat Code Coverage"
# Coverage report in ./TestResults/**/coverage.cobertura.xml
```

### Docker

```bash
./build.sh                 # builds image synthea-cli:latest
./run.sh                   # runs CLI, mounts ./output as /data
```

Manual example:

```bash
docker build -t synthea-cli .
docker run --rm -v "$PWD/out":/data synthea-cli            -- --state CA --population 100            # args after --
```

### `setup.sh` for CI / Codex

`setup.sh` (with a wrapper at `run/setup.sh`) does:

1. Installs OpenJDK 17 and .NET 8 on Ubuntu runners  
2. Restores & publishes `Synthea.Cli` to `/workspace/synthea-cli/bin`

#### GitHub Actions example


```yaml
steps:
  - uses: actions/checkout@v4
  - run: ./setup.sh
  - run: dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll -- --help
```

### Pre/Post Context Tasks

Task automation uses a `tasks/context` folder for reusable setup snippets. Pre-task files
in `tasks/context/pre/` run before each normal task, while post-task files in
`tasks/context/post/` run afterward. These context files stay in place and are
never moved. Only non-context tasks are moved to `tasks/implemented` after
successful completion.

See [docs/deliverables/codex-automation.md](docs/deliverables/codex-automation.md) for more details.

### Running Integration Tests

Execute all integration tests with:

```bash
dotnet test --filter Category=Integration
```

This builds the CLI and runs cross-platform tests including `SyntheaRunTests`
and `SyntheaCliWrapperRunTests`.

---

## Project Layout

```text
synthea-cli/
├─ .gitattributes                # enforce LF for shell scripts
├─ .gitignore
├─ README.md
├─ synthea-cli.code-workspace    # VS Code workspace file
├─ setup.sh                      # CI / Codex build script
├─ docs/
│   └─ Architecture.md           # CLI flow diagrams & overview
├─ scripts/
│   ├─ synthea-cli-create.ps1    # helper to scaffold new CLI repo
│   └─ windows/
│       └─ install-vscode-extensions.ps1
├─ run/
│   └─ setup.sh                  # thin wrapper for Codex harness
├─ Synthea.Cli.sln               # Visual Studio solution
├─ src/Synthea.Cli/              # main CLI project
│   ├─ Program.cs                # System.CommandLine entry point
│   ├─ JarManager.cs             # JAR download & cache helper
│   └─ Synthea.Cli.csproj
├─ Synthea.Cli.UnitTests/            # unit tests (xUnit)
│   ├─ CliTests.cs
│   ├─ JarManagerTests.cs
│   ├─ ProgramHandlerTests.cs
│   └─ Synthea.Cli.UnitTests.csproj
└─ synthea-output/               # default data output (git-ignored)
```

---

## Contributing

1. Fork → feature branch → PR.  
2. Ensure `dotnet test` passes and coverage ≥ 90 %.  
3. If you add new dependencies, update both `.csproj` files and `setup.sh`.  
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

See [docs/Architecture.md](docs/Architecture.md) for diagrams of the CLI flow and a high-level overview of the Synthea JAR.
