#!/usr/bin/env pwsh

Write-Host "Testing Java Detection Methods" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green

# Test 1: Direct java -version
Write-Host "`n1. Testing direct 'java -version':" -ForegroundColor Yellow
try {
    $javaVersion = java -version 2>&1
    Write-Host "✓ Success: $($javaVersion[0])" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 2: where java
Write-Host "`n2. Testing 'where java':" -ForegroundColor Yellow
try {
    $whereResult = where java 2>&1
    Write-Host "✓ Success: $whereResult" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: cmd.exe /c where java (exact test method)
Write-Host "`n3. Testing 'cmd.exe /c where java':" -ForegroundColor Yellow
try {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "cmd.exe"
    $psi.Arguments = "/c where java"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    
    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    
    Write-Host "Exit Code: $($proc.ExitCode)"
    Write-Host "StdOut: $stdout" -ForegroundColor Cyan
    if ($stderr) { Write-Host "StdErr: $stderr" -ForegroundColor Red }
    
    if ($proc.ExitCode -eq 0) {
        Write-Host "✓ Command succeeded" -ForegroundColor Green
    } else {
        Write-Host "✗ Command failed" -ForegroundColor Red
    }
} catch {
    Write-Host "✗ Exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: PATH search
Write-Host "`n4. Testing PATH environment search:" -ForegroundColor Yellow
$paths = ($env:PATH -split ';' | Where-Object { $_ -ne "" })
$javaFound = $false
foreach ($path in $paths) {
    $javaPath = Join-Path $path "java.exe"
    if (Test-Path $javaPath) {
        Write-Host "✓ Found java.exe at: $javaPath" -ForegroundColor Green
        $javaFound = $true
    }
}
if (-not $javaFound) {
    Write-Host "✗ java.exe not found in PATH" -ForegroundColor Red
}

# Test 5: Environment variables
Write-Host "`n5. Java-related environment variables:" -ForegroundColor Yellow
$javaVars = Get-ChildItem env: | Where-Object { $_.Name -like "*JAVA*" -or $_.Name -like "*JDK*" -or $_.Name -like "*JRE*" }
if ($javaVars) {
    foreach ($var in $javaVars) {
        Write-Host "$($var.Name) = $($var.Value)" -ForegroundColor Cyan
    }
} else {
    Write-Host "No Java-related environment variables found" -ForegroundColor Yellow
}
