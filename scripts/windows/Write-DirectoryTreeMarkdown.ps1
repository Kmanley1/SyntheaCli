<#
.SYNOPSIS
    Traverse a directory tree and output a Markdown document
    that represents its structure.

.EXAMPLE
    # List only folders
    .\Write-DirectoryTreeMarkdown.ps1 -Path 'C:\Projects' -OutFile '.\Tree.md'

.EXAMPLE
    # Include files as well
    .\Write-DirectoryTreeMarkdown.ps1 -Path 'C:\Projects' -OutFile '.\Tree-WithFiles.md' -IncludeFiles
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
        [bool]  $IsLast = $false,
        [string]$Comment = ''
    )
    $prefix = ''
    if ($Depth -gt 0) {
        $prefix = ('│   ' * ($Depth-1))
        $prefix += if ($IsLast) { '└─ ' } else { '├─ ' }
    }
    $line = $prefix + $Name
    if ($Comment) { $line += '    # ' + $Comment }
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
        $comment = ''
        # Optionally, add comments for known files/folders (customize as needed)
        switch -Wildcard ($entry.Name) {
            '.gitattributes' { $comment = 'enforce LF for shell scripts' }
            'synthea-cli.code-workspace' { $comment = 'VS Code workspace file' }
            'setup.sh' { $comment = 'CI / Codex build script' }
            'Architecture.md' { $comment = 'CLI flow diagrams & overview' }
            'synthea-cli-create.ps1' { $comment = 'helper to scaffold new CLI repo' }
            'install-vscode-extensions.ps1' { $comment = '' }
            'Synthea.Cli.sln' { $comment = 'Visual Studio solution' }
            'Program.cs' { $comment = 'System.CommandLine entry point' }
            'JarManager.cs' { $comment = 'JAR download & cache helper' }
            'Synthea.Cli.csproj' { $comment = '' }
            'CliTests.cs' { $comment = '' }
            'JarManagerTests.cs' { $comment = '' }
            'ProgramHandlerTests.cs' { $comment = '' }
            'Synthea.Cli.UnitTests.csproj' { $comment = '' }
            'setup.sh' { $comment = 'thin wrapper for Codex harness' }
            'synthea-output' { $comment = 'default data output (git-ignored)' }
            default { $comment = '' }
        }
        Add-Line -Depth $Depth -Name ($isDir ? ($entry.Name + '/') : $entry.Name) -IsDir:$isDir -IsLast:$isLast -Comment:$comment
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
