# setup.ps1 - Setup Synthea CLI environment for ChatGPT Codex (Windows)
# Enhanced setup script with full environment verification and integration test support

param(
    [switch]$InstallJava,
    [switch]$SkipTests,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Setting up Synthea CLI environment for ChatGPT Codex..." -ForegroundColor Cyan
Write-Host ""

# Function to check if a command exists
function Test-CommandExists {
    param($Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to get Java version
function Get-JavaVersion {
    try {
        $javaOutput = java -version 2>&1 | Select-String "version" | Select-Object -First 1
        if ($javaOutput -match '"(\d+)\.?.*"' -or $javaOutput -match '"(\d+)"') {
            return [int]$matches[1]
        }
        return 0
    }
    catch {
        return 0
    }
}

# 1) Check system dependencies
Write-Host "📦 Checking system dependencies..." -ForegroundColor Yellow

# Check .NET
if (Test-CommandExists "dotnet") {
    $dotnetVersion = dotnet --version
    Write-Host "  ✅ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  ❌ .NET SDK not found!" -ForegroundColor Red
    Write-Host "     Please install .NET 8 SDK from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Check Java
if (Test-CommandExists "java") {
    $javaVersion = Get-JavaVersion
    if ($javaVersion -ge 11) {
        Write-Host "  ✅ Java $javaVersion found (compatible with Synthea)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Java $javaVersion found but may not be fully compatible (requires Java 11+)" -ForegroundColor Yellow
    }
} else {
    if ($InstallJava) {
        Write-Host "  📥 Installing Java via winget..." -ForegroundColor Yellow
        try {
            winget install EclipseAdoptium.Temurin.21.JDK --silent
            # Refresh environment variables
            $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH","User")
            Write-Host "  ✅ Java installed successfully" -ForegroundColor Green
        }
        catch {
            Write-Host "  ❌ Failed to install Java via winget" -ForegroundColor Red
            Write-Host "     Please install Java manually from: https://adoptium.net/" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "  ❌ Java not found!" -ForegroundColor Red
        Write-Host "     Install with: .\setup.ps1 -InstallJava" -ForegroundColor Yellow
        Write-Host "     Or manually from: https://adoptium.net/" -ForegroundColor Yellow
        exit 1
    }
}

# 2) Restore dependencies
Write-Host ""
Write-Host "📥 Restoring .NET dependencies..." -ForegroundColor Yellow
dotnet restore --nologo
Write-Host "  ✅ Dependencies restored" -ForegroundColor Green

# 3) Build the solution
Write-Host ""
Write-Host "🔨 Building Synthea CLI..." -ForegroundColor Yellow

# Build Release configuration
Write-Host "  - Building Release configuration..." -ForegroundColor White
dotnet build --no-restore -c Release --nologo
if ($LASTEXITCODE -eq 0) {
    Write-Host "    ✅ Release build completed" -ForegroundColor Green
} else {
    Write-Host "    ❌ Release build failed" -ForegroundColor Red
    exit 1
}

# Build Debug configuration (for integration tests)
Write-Host "  - Building Debug configuration..." -ForegroundColor White
dotnet build --no-restore -c Debug --nologo
if ($LASTEXITCODE -eq 0) {
    Write-Host "    ✅ Debug build completed" -ForegroundColor Green
} else {
    Write-Host "    ❌ Debug build failed" -ForegroundColor Red
    exit 1
}

# 4) Verify build outputs
Write-Host ""
Write-Host "🧪 Verifying integration test setup..." -ForegroundColor Yellow

$debugDll = "artifacts\bin\Debug\net8.0\Synthea.Cli.dll"
$releaseDll = "artifacts\bin\Release\net8.0\Synthea.Cli.dll"

if (Test-Path $debugDll) {
    Write-Host "  ✅ Debug CLI available at: $debugDll" -ForegroundColor Green
} else {
    Write-Host "  ❌ Debug CLI not found at: $debugDll" -ForegroundColor Red
}

if (Test-Path $releaseDll) {
    Write-Host "  ✅ Release CLI available at: $releaseDll" -ForegroundColor Green
} else {
    Write-Host "  ❌ Release CLI not found at: $releaseDll" -ForegroundColor Red
}

# 5) Run tests to verify everything works
if (-not $SkipTests) {
    Write-Host ""
    Write-Host "🧪 Running tests to verify setup..." -ForegroundColor Yellow
    
    if ($Verbose) {
        dotnet test --no-build --verbosity normal
    } else {
        dotnet test --no-build --verbosity minimal
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✅ All tests passed successfully" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Some tests failed - check output above for details" -ForegroundColor Yellow
    }
}

# 6) Setup complete
Write-Host ""
Write-Host "🎉 Synthea CLI setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Available commands:" -ForegroundColor Cyan
Write-Host "  • Run CLI (Release):  dotnet artifacts\bin\Release\net8.0\Synthea.Cli.dll run --help"
Write-Host "  • Run CLI (Debug):    dotnet artifacts\bin\Debug\net8.0\Synthea.Cli.dll run --help"
Write-Host "  • Run all tests:      dotnet test"
Write-Host "  • Build solution:     dotnet build"
Write-Host ""
Write-Host "🔧 Environment ready for ChatGPT Codex development!" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Example usage:" -ForegroundColor Cyan
Write-Host "  dotnet artifacts\bin\Release\net8.0\Synthea.Cli.dll run -o .\output --state OH -p 10"
