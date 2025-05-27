<#
.SYNOPSIS
    Traverse a directory tree and output a Markdown document
    that represents its structure.

.EXAMPLE
    # List only folders
    .\Write-DirectoryTreeMarkdown.ps1 -Path 'C:\_Template\Projects\_code\synthea-cli' -OutFile '.\Tree.md'

.EXAMPLE
    # Include files as well
    .\Write-DirectoryTreeMarkdown.ps1 -Path 'C:\_Template\Projects\_code\synthea-cli' -OutFile 'C:\_Template\Projects\_code\synthea-cli\docs\deliverables\project-structure.md' -IncludeFiles
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$Path,

    [Parameter()]
    [string]$OutFile = (Join-Path $PWD 'directory-tree.md'),

    [switch]$IncludeFiles         # add this switch to list files too
)

# ---- prepare ----------------------------------------------------------------
$root = Resolve-Path -LiteralPath $Path
$md   = [System.Collections.Generic.List[string]]::new()
$md.Add("# Directory tree for ``$($root.Path)``")
$md.Add("")


# Adds a line in tree format (├─, └─, │) with optional comment
function Add-Line {
    param(
        [int]   $Depth,
        [string]$Name,
        [bool]  $IsDir,
        [bool]  $IsLast = $false
    )
    $prefix = ''
    if ($Depth -gt 0) {
        $prefix = ('│   ' * ($Depth-1))
        $prefix += if ($IsLast) { '└─ ' } else { '├─ ' }
    }
    $line = $prefix + $Name
    $md.Add($line)
}

# ---- recursive walker -------------------------------------------------------

# Recursively walk the directory and build the tree
function Walk {
    param(
        [string]$Current,
        [int]   $Depth
    )
    $dirs = @(Get-ChildItem -LiteralPath $Current -Directory | Where-Object { $_.Name -ne 'bin' -and $_.Name -ne 'obj' } | Sort-Object Name)
    $files = $IncludeFiles ? @(Get-ChildItem -LiteralPath $Current -File | Sort-Object Name) : @()
    $entries = @($dirs) + @($files)
    for ($i = 0; $i -lt $entries.Count; $i++) {
        $entry = $entries[$i]
        $isDir = $entry.PSIsContainer -eq $true
        $isLast = ($i -eq $entries.Count - 1)
        Add-Line -Depth $Depth -Name ($isDir ? ($entry.Name + '/') : $entry.Name) -IsDir:$isDir -IsLast:$isLast
        if ($isDir) {
            Walk -Current $entry.FullName -Depth ($Depth + 1)
        }
    }
}


# Print the root folder name
$md.Add((Split-Path -Path $root.Path -Leaf) + '/')
Walk -Current $root.Path -Depth 0

# ---- write markdown ---------------------------------------------------------
$md | Set-Content -LiteralPath $OutFile -Encoding UTF8
