<#
.SYNOPSIS
Generates Markdown and JSON directory reports.

.DESCRIPTION
Generate-DirectoryReport.ps1 builds a Markdown tree and JSON representation
of a directory hierarchy. The outputs are written to the "deliverables"
folder in the repository root.

.PARAMETER RootPath
Root directory to analyze. Defaults to the current directory.

.PARAMETER IncludeHidden
Include hidden and system files in the report.

.EXAMPLE
.\Generate-DirectoryReport.ps1 -RootPath "C:\Projects\MyRepo"
#>

[CmdletBinding()]
param(
    [string]$RootPath = (Get-Location).Path,
    [switch]$IncludeHidden
)

Set-StrictMode -Version Latest

function Get-DirectoryTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [switch]$IncludeHidden
    )
    $items = Get-ChildItem -LiteralPath $Path -Force:$IncludeHidden -ErrorAction Stop
    if (-not $IncludeHidden) {
        $items = $items | Where-Object { -not ($_.Attributes -match 'Hidden|System') }
    }
    $items = $items | Sort-Object -Property @{ Expression = { -not $_.PSIsContainer } }, Name
    $nodes = @()
    foreach ($item in $items) {
        $node = [ordered]@{
            name = $item.Name
            type = if ($item.PSIsContainer) { 'folder' } else { 'file' }
        }
        if ($item.PSIsContainer) {
            $node.children = Get-DirectoryTree -Path $item.FullName -IncludeHidden:$IncludeHidden
        }
        $nodes += [PSCustomObject]$node
    }
    return ,$nodes
}

function Escape-Markdown {
    param([string]$Text)
    return ($Text -replace '([\\`*_\[\]{}()>#+\-|!])','``$1')
}

function ConvertTo-MarkdownTree {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object[]]$Nodes,
        [int]$Indent = 0
    )
    $sb = [System.Text.StringBuilder]::new()
    foreach ($n in $Nodes) {
        $indentStr = '  ' * $Indent
        $null = $sb.Append($indentStr + '- ' + (Escape-Markdown $n.name) + [Environment]::NewLine)
        if ($n.type -eq 'folder' -and $n.PSObject.Properties.Name -contains 'children') {
            $childText = ConvertTo-MarkdownTree -Nodes $n.children -Indent ($Indent + 1)
            $null = $sb.Append($childText)
        }
    }
    return $sb.ToString()
}

try {
    $resolvedRoot = Resolve-Path -LiteralPath $RootPath -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $resolvedRoot -PathType Container)) {
        throw "RootPath '$RootPath' must be an existing directory."
    }
} catch {
    throw "Invalid RootPath: $_"
}

$tree = Get-DirectoryTree -Path $resolvedRoot -IncludeHidden:$IncludeHidden

$deliverablesPath = $PSScriptRoot
if (-not (Test-Path -LiteralPath $deliverablesPath)) {
    New-Item -Path $deliverablesPath -ItemType Directory -ErrorAction Stop | Out-Null
}

$markdownPath = Join-Path -Path $deliverablesPath -ChildPath 'directory-structure.md'
$jsonPath = Join-Path -Path $deliverablesPath -ChildPath 'directory-structure.json'

try {
    ConvertTo-MarkdownTree -Nodes $tree | Out-File -LiteralPath $markdownPath -Encoding utf8 -Force
    $tree | ConvertTo-Json -Depth 100 | Out-File -LiteralPath $jsonPath -Encoding utf8 -Force
} catch {
    throw "Failed to write report files: $_"
}

[PSCustomObject]@{
    MarkdownPath = $markdownPath
    JsonPath     = $jsonPath
}
