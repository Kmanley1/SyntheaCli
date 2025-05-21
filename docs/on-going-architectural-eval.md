# Repository Overview: synthea-cli

## Purpose

`synthea-cli` is a cross-platform **.NET 8** command-line wrapper around [MITRE Synthea](https://github.com/synthetichealth/synthea). The tool automatically downloads the latest `synthea-with-dependencies.jar`, caches it locally, and runs Java with user-provided options. It simplifies running Synthea across Windows, macOS, Linux, and containers.

## Key Components

### Program.cs
- Uses `System.CommandLine` to define the CLI structure: a root command with global flags (`--refresh`, `--java-path`) and a `run` subcommand.
- The `run` command accepts options like `--output`, `--population`, `--state`, `--city`, and forwards additional arguments directly to the JAR.
- Before launching Java, it calls `JarManager.EnsureJarAsync` to retrieve or download the JAR. Output from the Java process is streamed back to the console.

### JarManager.cs
- Manages the cache directory (under `%LOCALAPPDATA%/synthea-cli` or `$XDG_CACHE_HOME/synthea-cli`).
- Queries GitHub releases for the latest JAR. If the release provides a `.sha256` file, the download's checksum is verified.
- Supports progress reporting and cleans up temporary files after download.

### Tests
- `Synthea.Cli.Tests` includes xUnit tests for CLI behavior and jar download logic.
- Network calls and process execution are mocked to allow fast, isolated tests (`JarManagerTests.cs`, `ProgramHandlerTests.cs`).
- A simple smoke test ensures running `synthea` with no arguments defaults to help.

### CI and Packaging
- The repository contains a `setup.sh` script and GitHub Actions workflow to build and publish a NuGet package.
- Dockerfile and helper scripts (`build.sh`, `run.sh`) support container-based usage.

## Business Perspective

By automating JAR retrieval and providing a typed CLI, `synthea-cli` streamlines synthetic health data generation in enterprise environments. The MIT license allows integration with minimal restrictions, and cross-platform support enables use in CI pipelines, local development, or containers. Extensive unit tests and a clear separation from the official Synthea project enhance maintainability and reliability.
