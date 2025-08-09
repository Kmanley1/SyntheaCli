# Synthea CLI Setup for ChatGPT Codex

This document explains how to set up the Synthea CLI environment for development and testing with ChatGPT Codex.

## Quick Setup

### Linux/Ubuntu (ChatGPT Codex Default)
```bash
# Run the enhanced setup script
./tools/setup.sh
```

### Windows
```powershell
# Option 1: Use the comprehensive setup script
.\setup-test-environment.ps1 -InstallJava

# Option 2: Use the new tools setup script
.\tools\setup.ps1 -InstallJava
```

## What the Setup Scripts Do

### 1. System Dependencies
- **Java**: Installs Java 17+ (required for running Synthea JAR)
- **.NET**: Verifies .NET 8.0 SDK is available
- **Package Management**: Uses appropriate package managers (apt, winget, choco, scoop)

### 2. Build Configuration
- Restores NuGet dependencies
- Builds both **Debug** and **Release** configurations
- Creates artifacts in `artifacts/bin/` directory
- Ensures integration tests can find the CLI binaries

### 3. Test Environment
- Verifies all build outputs are in expected locations
- Runs the complete test suite to validate setup
- Tests both unit tests (40) and integration tests (4)

### 4. Integration Test Requirements
The integration tests expect to find the CLI at these paths:
```
artifacts/bin/Release/net8.0/Synthea.Cli.dll  (preferred)
artifacts/bin/Debug/net8.0/Synthea.Cli.dll    (fallback)
```

## Manual Setup (if scripts fail)

### 1. Install Prerequisites
```bash
# Ubuntu/Debian
sudo apt-get update
sudo apt-get install openjdk-17-jre-headless dotnet-sdk-8.0

# Windows (with winget)
winget install EclipseAdoptium.Temurin.21.JDK
winget install Microsoft.DotNet.SDK.8
```

### 2. Build the Project
```bash
dotnet restore
dotnet build -c Release
dotnet build -c Debug    # Needed for integration tests
```

### 3. Verify Setup
```bash
dotnet test              # Run all tests
```

## Usage Examples

### Basic Usage
```bash
# Generate 10 patients for Ohio
dotnet artifacts/bin/Release/net8.0/Synthea.Cli.dll run -o ./output --state OH -p 10

# Show help
dotnet artifacts/bin/Release/net8.0/Synthea.Cli.dll run --help
```

### Development/Testing
```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/Synthea.Cli.UnitTests/

# Run only integration tests
dotnet test tests/Synthea.Cli.IntegrationTests/
```

## Troubleshooting

### Java Not Found
If you see "Java not found" errors:
```bash
# Check Java installation
java -version

# On Windows, refresh PATH
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")
```

### Integration Tests Failing
If integration tests can't find the CLI:
```bash
# Ensure both configurations are built
dotnet build -c Debug
dotnet build -c Release

# Check if files exist
ls -la artifacts/bin/Debug/net8.0/Synthea.Cli.dll
ls -la artifacts/bin/Release/net8.0/Synthea.Cli.dll
```

### Build Errors
If you encounter build errors:
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## ChatGPT Codex Specific Notes

- The setup script is optimized for Ubuntu 22.04 (Codex default environment)
- All dependencies are automatically installed without user interaction
- Build outputs are placed in standardized locations for integration tests
- The environment is verified by running the full test suite
- Both CLI configurations (Debug/Release) are available for different use cases

## Support

If you encounter issues:
1. Run the setup script with verbose output
2. Check that Java 11+ and .NET 8.0 are properly installed
3. Verify build outputs exist in `artifacts/bin/` directory
4. Run tests individually to isolate issues
