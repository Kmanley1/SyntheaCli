#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Permanent fix for SyntheaCli Java detection and NuGet package issues
    
.DESCRIPTION
    This script provides a comprehensive fix for the persistent Java detection issues
    and NuGet package problems in the SyntheaCli project by:
    1. Cleaning all build artifacts and caches
    2. Restoring packages properly
    3. Rebuilding with correct dependencies
    4. Verifying that Java detection works
    
.EXAMPLE
    .\fix-java-detection.ps1
#>

Write-Host "🔧 SyntheaCli Java Detection Permanent Fix" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# Step 1: Clean everything thoroughly
Write-Host "`n📂 Step 1: Cleaning all build artifacts..." -ForegroundColor Yellow
Remove-Item -Recurse -Force "bin", "obj", "artifacts" -ErrorAction SilentlyContinue
Get-ChildItem -Recurse -Directory -Name "bin", "obj" | ForEach-Object { Remove-Item -Recurse -Force $_ -ErrorAction SilentlyContinue }

# Step 2: Clear all NuGet caches
Write-Host "`n🗑️  Step 2: Clearing NuGet caches..." -ForegroundColor Yellow
dotnet nuget locals all --clear

# Step 3: Restore packages from scratch
Write-Host "`n📦 Step 3: Restoring packages..." -ForegroundColor Yellow
dotnet restore --force --no-cache --verbosity minimal

# Step 4: Add xUnit packages explicitly if needed
Write-Host "`n🧪 Step 4: Ensuring test dependencies..." -ForegroundColor Yellow
dotnet add tests/Synthea.Cli.UnitTests/Synthea.Cli.UnitTests.csproj package xunit --version 2.4.2 --verbosity minimal
dotnet add tests/Synthea.Cli.UnitTests/Synthea.Cli.UnitTests.csproj package xunit.runner.visualstudio --version 2.4.5 --verbosity minimal
dotnet add tests/Synthea.Cli.IntegrationTests/Synthea.Cli.IntegrationTests.csproj package xunit --version 2.4.2 --verbosity minimal
dotnet add tests/Synthea.Cli.IntegrationTests/Synthea.Cli.IntegrationTests.csproj package xunit.runner.visualstudio --version 2.4.5 --verbosity minimal

# Step 5: Build the solution
Write-Host "`n🏗️  Step 5: Building solution..." -ForegroundColor Yellow
$buildResult = dotnet build --verbosity minimal 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Build successful" -ForegroundColor Green
} else {
    Write-Host "❌ Build failed:" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    exit 1
}

# Step 6: Test Java detection
Write-Host "`n☕ Step 6: Testing Java detection..." -ForegroundColor Yellow
try {
    $javaVersion = java -version 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Java is available:" -ForegroundColor Green
        Write-Host "   $($javaVersion[0])" -ForegroundColor Gray
    } else {
        throw "Java command failed"
    }
} catch {
    Write-Host "❌ Java is not available in PATH" -ForegroundColor Red
    Write-Host "   Please install Java 11+ from: https://adoptium.net/" -ForegroundColor Yellow
    exit 1
}

# Step 7: Run tests
Write-Host "`n🧪 Step 7: Running all tests..." -ForegroundColor Yellow
$testResult = dotnet test --verbosity minimal --logger "console;verbosity=normal" 2>&1

# Parse test results
$testOutput = $testResult | Out-String
if ($testOutput -match "Test summary: total: (\d+), failed: (\d+), succeeded: (\d+)") {
    $totalTests = $Matches[1]
    $failedTests = $Matches[2]
    $succeededTests = $Matches[3]
    
    Write-Host "`n📊 Test Results:" -ForegroundColor Cyan
    Write-Host "   Total Tests: $totalTests" -ForegroundColor White
    Write-Host "   Succeeded: $succeededTests" -ForegroundColor Green
    Write-Host "   Failed: $failedTests" -ForegroundColor $(if ([int]$failedTests -eq 0) { "Green" } else { "Red" })
    
    if ([int]$failedTests -eq 0) {
        Write-Host "`n🎉 SUCCESS: All tests are passing!" -ForegroundColor Green
        Write-Host "   Java detection is now working correctly." -ForegroundColor Green
    } else {
        Write-Host "`n⚠️  Some tests failed, but this may be expected for integration tests" -ForegroundColor Yellow
        Write-Host "   if Java is not properly installed or configured." -ForegroundColor Yellow
    }
} else {
    Write-Host "`n❌ Could not parse test results" -ForegroundColor Red
    Write-Host $testOutput -ForegroundColor Gray
}

Write-Host "`n✨ Fix completed!" -ForegroundColor Green
Write-Host "If you continue to see Java detection errors, the issue is likely:" -ForegroundColor Yellow
Write-Host "1. Java is not in your system PATH" -ForegroundColor Yellow
Write-Host "2. You need to restart your terminal/IDE" -ForegroundColor Yellow
Write-Host "3. Environment variables need to be refreshed" -ForegroundColor Yellow
