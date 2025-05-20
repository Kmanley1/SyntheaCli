# Synthea-CLI

`synthea-cli` is a cross-platform **.NET 8** command-line wrapper around **[Synthea™](https://github.com/synthetichealth/synthea)** — MITRE’s open-source synthetic health-record generator.  
The tool downloads the latest Synthea JAR on first use, caches it locally, and gives you a simple, typed interface plus first-class scripting and container support.

---

## Table of Contents

1. [Features](#features)  
2. [Quick Start](#quick-start)  
   • [Prerequisites](#prerequisites) · [Install as dotnet-tool](#install-as-a-global-tool) · [Run](#run)  
3. [Developer Guide](#developer-guide)  
   • [Clone & Build](#clone--build) · [Unit Tests](#unit-tests) · [Docker](#docker) · [setup.sh](#setupsh-for-ci--codex)  
4. [Project Layout](#project-layout)  
5. [Contributing](#contributing)  
6. [License & Credits](#license--credits)

---

## Features

| Feature | Details |
|---------|---------|
| **Zero-install JAR** | Automatically fetches the newest `synthea-with-dependencies.jar` from GitHub releases and verifies its SHA-256 checksum. |
| **Configurable cache** | Stores the JAR under `%LOCALAPPDATA%\synthea-cli` / `$XDG_CACHE_HOME/synthea-cli`; refresh anytime with `--refresh`. |
| **Friendly flags** | `--state OH`, `--city "Columbus"`, `--output ./data` map to Synthea’s positional arguments and working directory. |
| **Portable** | Runs on Windows, macOS, Linux, containers—wherever .NET 8 + Java 17+ are available. |
| **Docker image** | Multi-stage Dockerfile builds a slim runtime with OpenJDK 17 and publishes the tool. |
| **Unit-tested** | ≥ 90 % line coverage via xUnit and Coverlet; network & process calls are fully mocked. |

---

## Quick Start

### Prerequisites

* **.NET 8 SDK** — <https://dotnet.microsoft.com/download>  
* **Java ≥ 17** in your `PATH` (`java -version`) – *only required at runtime*.

### Install as a global tool

```bash
dotnet tool install --global synthea-cli     --version 0.1.0
```

### Run

```bash
# Generate 10 synthetic patients from Ohio into ./output
synthea run --output ./output -p 10 --state OH

# Same, but force-refresh the cached JAR
synthea --refresh run -o ./output -p 10 --state TX --city Austin
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
dotnet run -- run -o ./out -p 5 --state AK
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
docker run --rm -v "$PWD/out":/data synthea-cli            -- --state CA -p 100            # args after --
```

### `setup.sh` for CI / Codex

`setup.sh` (with a wrapper at `run/setup.sh`) does:

1. Installs OpenJDK 17 and .NET 8 on Ubuntu runners  
2. Restores & publishes `Synthea.Cli` to `/workspace/synthea-cli/bin`

**GitHub Actions example**

```yaml
steps:
  - uses: actions/checkout@v4
  - run: ./setup.sh
  - run: dotnet /workspace/synthea-cli/bin/Synthea.Cli.dll -- --help
```

---

## Project Layout

```
synthea-cli/
├─ .gitattributes          # *.sh => LF
├─ .gitignore
├─ Dockerfile              # multi-stage build
├─ setup.sh                # CI / Codex build script
├─ run/setup.sh            # thin wrapper for Codex harness
├─ Synthea.sln
├─ Synthea.Cli/
│   ├─ Program.cs          # System.CommandLine entry point
│   └─ JarManager.cs       # JAR download & cache helper
└─ Synthea.Cli.Tests/      # xUnit + FluentAssertions
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

* **Tool code** © 2025 Ken Manley — MIT License (`LICENSE`).  
* **Synthea™** is © MITRE, Apache-2.0. This CLI downloads the official Synthea JAR but is **not** an official MITRE project.  
* Uses **System.CommandLine** (`2.0.0-beta4`) under MIT.

---

Happy generating! 🎉
