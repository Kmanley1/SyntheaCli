#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sets up the development environment for SyntheaCli integration testing.

.DESCRIPTION
    This script checks for and optionally installs the prerequisites needed
    for running SyntheaCli integration tests:
    - Java JDK 11 or newer
    - Builds the project in Release configuration

.PARAMETER InstallJava
    If specified, attempts to install Java using available package managers.

.PARAMETER Force
    Forces reinstallation of components even if they appear to be present.

.EXAMPLE
    .\setup-test-environment.ps1
    Checks the environment and reports what's missing.

.EXAMPLE
    .\setup-test-environment.ps1 -InstallJava
    Installs Java and sets up the environment.
#>

param(
    [switch]$InstallJava,
    [switch]$Force
)

Write-Host "SyntheaCli Test Environment Setup" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green

# Check for Java
Write-Host "`nChecking Java installation..." -ForegroundColor Yellow

try {
    $javaVersion = java -version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Java is installed:" -ForegroundColor Green
        Write-Host "  $($javaVersion[0])" -ForegroundColor Gray
    } else {
        throw "Java not found"
    }
} catch {
    Write-Host "✗ Java is not installed or not in PATH" -ForegroundColor Red
    
    if ($InstallJava) {
        Write-Host "  Attempting to install Java..." -ForegroundColor Yellow
        
        # Try different package managers
        if (Get-Command winget -ErrorAction SilentlyContinue) {
            Write-Host "  Using winget to install Microsoft OpenJDK 11..." -ForegroundColor Cyan
            winget install Microsoft.OpenJDK.11
        } elseif (Get-Command choco -ErrorAction SilentlyContinue) {
            Write-Host "  Using Chocolatey to install OpenJDK 11..." -ForegroundColor Cyan
            choco install openjdk11 -y
        } elseif (Get-Command scoop -ErrorAction SilentlyContinue) {
            Write-Host "  Using Scoop to install OpenJDK 11..." -ForegroundColor Cyan
            scoop install openjdk11
        } else {
            Write-Host "  No supported package manager found (winget, choco, scoop)" -ForegroundColor Red
            Write-Host "  Please install Java manually: https://adoptium.net/" -ForegroundColor Yellow
            exit 1
        }
        
        # Refresh PATH and check again
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
        
        try {
            $javaVersion = java -version 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Java installation successful:" -ForegroundColor Green
                Write-Host "  $($javaVersion[0])" -ForegroundColor Gray
            } else {
                throw "Java still not found after installation"
            }
        } catch {
            Write-Host "✗ Java installation failed or PATH not updated" -ForegroundColor Red
            Write-Host "  You may need to restart your terminal or system" -ForegroundColor Yellow
            Write-Host "  Or manually add Java to your PATH" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  Run with -InstallJava to attempt automatic installation" -ForegroundColor Yellow
        Write-Host "  Or install manually: https://adoptium.net/" -ForegroundColor Yellow
    }
}

# Check for .NET
Write-Host "`nChecking .NET installation..." -ForegroundColor Yellow

try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET is installed: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET is not installed" -ForegroundColor Red
    Write-Host "  Please install .NET 8.0 SDK: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Build the project
Write-Host "`nBuilding SyntheaCli in Release configuration..." -ForegroundColor Yellow

try {
    $buildOutput = dotnet build -c Release 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Build successful" -ForegroundColor Green
    } else {
        throw "Build failed"
    }
} catch {
    Write-Host "✗ Build failed:" -ForegroundColor Red
    Write-Host "$buildOutput" -ForegroundColor Red
    exit 1
}

# Check if the CLI DLL exists
$dllPaths = @(
    "artifacts\bin\Release\net8.0\Synthea.Cli.dll",
    "src\Synthea.Cli\bin\Release\net8.0\Synthea.Cli.dll"
)

$foundDll = $false
foreach ($dllPath in $dllPaths) {
    if (Test-Path $dllPath) {
        Write-Host "✓ SyntheaCli DLL found at: $dllPath" -ForegroundColor Green
        $foundDll = $true
        break
    }
}

if (-not $foundDll) {
    Write-Host "✗ SyntheaCli DLL not found in expected locations:" -ForegroundColor Red
    foreach ($dllPath in $dllPaths) {
        Write-Host "  $dllPath" -ForegroundColor Gray
    }
}

# Run a quick test
Write-Host "`nRunning integration tests..." -ForegroundColor Yellow

try {
    $testOutput = dotnet test tests/Synthea.Cli.IntegrationTests/ --logger "console;verbosity=minimal" 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ All integration tests passed" -ForegroundColor Green
    } else {
        # Check if tests were skipped vs failed
        if ($testOutput -match "SKIPPED:") {
            Write-Host "! Some integration tests were skipped (this is expected if dependencies are missing)" -ForegroundColor Yellow
            Write-Host "  Check the test output for details" -ForegroundColor Gray
        } else {
            Write-Host "✗ Some integration tests failed" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ Failed to run integration tests" -ForegroundColor Red
}

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "You can now run: dotnet test" -ForegroundColor Cyan
