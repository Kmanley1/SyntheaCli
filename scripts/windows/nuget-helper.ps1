.\nuget-helper.ps1           # packs, installs fresh build
.\nuget-helper.ps1 -Install  # uninstall & pull latest from NuGet.org
.\nuget-helper.ps1 -Version 0.2.0 -Install  # install specific published version


<#
.SYNOPSIS
    Helper for packing & installing the synthea-cli dotnet-tool.

.PARAMETER Version
    Specific version to (re)install from NuGet.org. If omitted, installs
    the version produced by dotnet pack (local) or the latest upstream.

.PARAMETER Install
    Switch: uninstall any existing global tool and install the requested
    version (or latest) from NuGet.org.

.EXAMPLE
    # pack local, then install that build
    .\nuget-helper.ps1

.EXAMPLE
    # uninstall & install latest published package
    .\nuget-helper.ps1 -Install

.EXAMPLE
    # install a particular published version
    .\nuget-helper.ps1 -Install -Version 0.2.0
#>

param (
    [string] $Version  = "",
    [switch] $Install
)

# ─── variables ──────────────────────────────────────────────────────────────
$ProjectPath   = ".\Synthea.Cli\Synthea.Cli.csproj"
$OutDir        = ".\nupkgs"
$PackageId     = "synthea-cli"

# ─── pack & local install ───────────────────────────────────────────────────
if (-not $Install) {
    Write-Host "`n➡️  Pack project → $OutDir" -ForegroundColor Cyan
    dotnet pack $ProjectPath -c Release -o $OutDir -p:ContinuousIntegrationBuild=true

    Write-Host "`n➡️  Install freshly built tool (global)" -ForegroundColor Cyan
    dotnet tool install --global $PackageId --add-source $OutDir --prerelease --version "*" --ignore-failed-sources

    Write-Host "`n✅ Done. Try:" -ForegroundColor Green
    Write-Host "   synthea --help"
    return
}

# ─── uninstall current tool ─────────────────────────────────────────────────
Write-Host "`n➡️  Uninstall existing $PackageId (if any)" -ForegroundColor Cyan
dotnet tool uninstall --global $PackageId 2>$null

# ─── install from NuGet.org ─────────────────────────────────────────────────
$installCmd = "dotnet tool install --global $PackageId"
if ($Version) { $installCmd += " --version $Version" }
Write-Host "`n➡️  $installCmd" -ForegroundColor Cyan
Invoke-Expression $installCmd

Write-Host "`n✅ Done. Verify with:" -ForegroundColor Green
Write-Host "   synthea --help"
