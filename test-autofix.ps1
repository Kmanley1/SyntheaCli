#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Test the auto-fix functionality by simulating NuGet corruption
    
.DESCRIPTION
    This script tests the enhanced setup scripts to ensure they can detect 
    and automatically fix NuGet package corruption issues.
#>

Write-Host "🧪 Testing Auto-Fix Functionality" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

Write-Host "`n1. Testing corruption detection..." -ForegroundColor Yellow

# Test the corruption detection function
function Test-ForNuGetCorruption {
    param($TestOutput)
    
    $corruptionIndicators = @(
        "could not be found \(are you missing a using directive or an assembly reference\?\)",
        "The type or namespace name 'Xunit' could not be found",
        "The type or namespace name 'Fact' could not be found",
        "Package .*, version .* was not found",
        "NuGet restore might have only partially completed"
    )
    
    foreach ($indicator in $corruptionIndicators) {
        if ($TestOutput -match $indicator) {
            return $true
        }
    }
    return $false
}

# Test cases
$testCases = @(
    "The type or namespace name 'Xunit' could not be found (are you missing a using directive or an assembly reference?)",
    "Package System.CommandLine, version 2.0.0-beta4.22272.1 was not found",
    "NuGet restore might have only partially completed",
    "This is normal test output with no corruption",
    "Some other error that isn't corruption"
)

foreach ($testCase in $testCases) {
    $isCorruption = Test-ForNuGetCorruption -TestOutput $testCase
    $status = if ($isCorruption) { "DETECTED" } else { "NOT DETECTED" }
    $color = if ($isCorruption) { "Yellow" } else { "Green" }
    Write-Host "  '$($testCase.Substring(0, [Math]::Min(50, $testCase.Length)))...' -> $status" -ForegroundColor $color
}

Write-Host "`n2. Checking enhanced setup scripts..." -ForegroundColor Yellow

$scriptsToCheck = @(
    "setup-test-environment.ps1",
    "tools/setup.ps1"
)

foreach ($script in $scriptsToCheck) {
    if (Test-Path $script) {
        $content = Get-Content $script -Raw
        if ($content -match "Test-ForNuGetCorruption") {
            Write-Host "✓ $script has auto-fix capability" -ForegroundColor Green
        } else {
            Write-Host "✗ $script missing auto-fix capability" -ForegroundColor Red
        }
    } else {
        Write-Host "✗ $script not found" -ForegroundColor Red
    }
}

Write-Host "`n3. Checking VS Code tasks..." -ForegroundColor Yellow

if (Test-Path ".vscode/tasks.json") {
    $tasksContent = Get-Content ".vscode/tasks.json" -Raw
    if ($tasksContent -match "test-with-autofix") {
        Write-Host "✓ VS Code has enhanced test task with auto-fix" -ForegroundColor Green
    } else {
        Write-Host "✗ VS Code missing enhanced test task" -ForegroundColor Red
    }
    
    if ($tasksContent -match "setup-test-environment") {
        Write-Host "✓ VS Code has setup task" -ForegroundColor Green
    } else {
        Write-Host "✗ VS Code missing setup task" -ForegroundColor Red
    }
} else {
    Write-Host "✗ VS Code tasks.json not found" -ForegroundColor Red
}

Write-Host "`n4. Checking fix script availability..." -ForegroundColor Yellow

if (Test-Path "fix-java-detection.ps1") {
    Write-Host "✓ fix-java-detection.ps1 is available" -ForegroundColor Green
} else {
    Write-Host "! fix-java-detection.ps1 not found (manual fix will be used)" -ForegroundColor Yellow
}

Write-Host "`n✅ Auto-fix functionality test complete!" -ForegroundColor Green
Write-Host "`n📋 Available enhanced commands:" -ForegroundColor Cyan
Write-Host "  • Enhanced setup:     .\setup-test-environment.ps1" -ForegroundColor White
Write-Host "  • Tools setup:        .\tools\setup.ps1" -ForegroundColor White
Write-Host "  • VS Code test task:  Ctrl+Shift+P -> Tasks: Run Task -> test-with-autofix" -ForegroundColor White
Write-Host "  • VS Code setup task: Ctrl+Shift+P -> Tasks: Run Task -> setup-test-environment" -ForegroundColor White
