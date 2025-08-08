# Making Synthea CLI Wrapper Available for Tests

This document explains how to make the Synthea CLI wrapper available for integration testing.

## Prerequisites

The integration tests require two main components:

1. **The Synthea CLI Wrapper** - The .NET CLI application we built
2. **Java Runtime Environment** - Required to run the underlying Synthea JAR

## Test Environment Setup Options

### Option 1: Local Development Setup (Recommended)

#### Step 1: Install Java
The Synthea CLI requires Java 11 or newer. Install one of these:

**Windows:**
```powershell
# Using Chocolatey
choco install openjdk11

# Using Scoop
scoop install openjdk11

# Using winget
winget install Microsoft.OpenJDK.11
```

**macOS:**
```bash
# Using Homebrew
brew install openjdk@11

# Using SDKMAN
sdk install java 11.0.2-open
```

**Linux:**
```bash
# Ubuntu/Debian
sudo apt update
sudo apt install openjdk-11-jdk

# CentOS/RHEL/Fedora
sudo dnf install java-11-openjdk-devel

# Arch Linux
sudo pacman -S jdk11-openjdk
```

#### Step 2: Verify Java Installation
```bash
java -version
```

#### Step 3: Build the CLI in Release Mode
```bash
dotnet build -c Release
```

#### Step 4: Run Integration Tests
```bash
dotnet test tests/Synthea.Cli.IntegrationTests/
```

### Option 2: Using Development Build (Debug Mode)

If you want to test without building in Release mode, you can modify the integration test to also look for Debug builds:

1. The test has been updated to search multiple paths:
   - `artifacts/bin/Release/net8.0/Synthea.Cli.dll` (Release build)
   - `src/Synthea.Cli/bin/Release/net8.0/Synthea.Cli.dll` (Traditional Release path)
   - `Synthea.Cli/bin/Release/net8.0/Synthea.Cli.dll` (Alternative Release path)

2. To add Debug support, you could further modify the test to include:
   - `artifacts/bin/Debug/net8.0/Synthea.Cli.dll`

### Option 3: Global Installation (Advanced)

You can package and install the CLI globally:

#### Step 1: Create a NuGet Package
```bash
dotnet pack src/Synthea.Cli/Synthea.Cli.csproj -c Release -o ./nupkg
```

#### Step 2: Install as Global Tool
```bash
dotnet tool install --global --add-source ./nupkg Synthea.Cli
```

#### Step 3: Verify Installation
```bash
synthea --help
```

### Option 4: CI/CD Pipeline Setup

For automated testing in CI/CD pipelines:

#### GitHub Actions Example:
```yaml
name: CI
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Setup Java
      uses: actions/setup-java@v3
      with:
        distribution: 'temurin'
        java-version: '11'
    
    - name: Build
      run: dotnet build -c Release
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

#### Azure DevOps Example:
```yaml
steps:
- task: UseDotNet@2
  inputs:
    version: '8.0.x'
    
- task: JavaToolInstaller@0
  inputs:
    versionSpec: '11'
    jdkArchitectureOption: 'x64'
    jdkSourceOption: 'PreInstalled'
    
- script: dotnet build -c Release
  displayName: 'Build solution'
  
- script: dotnet test --no-build
  displayName: 'Run tests'
```

## Troubleshooting

### Issue 1: "Synthea CLI wrapper not found"
**Solution:** Build the project in Release mode:
```bash
dotnet build -c Release
```

### Issue 2: "Java not found"
**Solution:** Install Java and ensure it's in your PATH:
```bash
java -version  # Should show Java version
```

### Issue 3: Integration tests still skipping
**Diagnosis:** Check what paths the test is searching:
The test now provides detailed output showing which paths it searched.

### Issue 4: Tests fail with "JAR not found"
**Solution:** The CLI automatically downloads the Synthea JAR on first run. Ensure internet connectivity or pre-download the JAR.

## Understanding the Integration Tests

The integration tests work by:

1. **Finding the CLI:** Looking for the built DLL or global installation
2. **Checking Java:** Verifying Java is available in PATH
3. **Running Commands:** Executing CLI commands and verifying output
4. **Validating Results:** Checking that patient files are generated correctly

The tests are designed to be **environment-aware** and will skip gracefully if prerequisites aren't met, rather than failing hard.

## Current Test Status

After applying the fixes:
- ✅ **CLI Discovery:** Fixed to find DLL in artifacts directory
- ❌ **Java Requirement:** Still needs Java installation
- ✅ **Path Resolution:** Multiple search paths implemented
- ✅ **Error Reporting:** Clear messages about missing dependencies

## Next Steps

1. **Install Java** to enable full integration testing
2. **Consider adding Debug build support** for development workflow
3. **Add Docker-based testing** for consistent environments
4. **Create test fixtures** with pre-built JAR for faster testing

## Development Workflow Recommendations

### For Local Development:
1. Install Java once
2. Use `dotnet build -c Release` before running integration tests
3. Run `dotnet test` to verify all tests pass

### For CI/CD:
1. Use the pipeline examples above
2. Cache the Synthea JAR between runs
3. Consider using a test matrix for different Java versions

### For Contributors:
1. Document Java requirement in README
2. Add setup scripts for common development environments
3. Consider providing a dev container configuration
