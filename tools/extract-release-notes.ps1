# Extract grouped Conventional-Commits release notes between two refs.
#
# Usage:
#   pwsh -File tools/extract-release-notes.ps1 v0.4.0 v0.5.0
#   pwsh -File tools/extract-release-notes.ps1 v0.4.0 HEAD
#   pwsh -File tools/extract-release-notes.ps1                 # auto: previous tag .. HEAD
#
# Output: markdown to stdout. The release-notes.yml workflow pipes this
# straight into `gh release edit --notes-file -`.

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$FromRef,

    [Parameter(Position = 1)]
    [string]$ToRef = 'HEAD'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($FromRef)) {
    # Most recent tag reachable from ToRef minus one — i.e. the previous
    # release. `git describe --tags --abbrev=0 ToRef^` excludes ToRef
    # itself when it is the newly pushed tag.
    $FromRef = (& git describe --tags --abbrev=0 "$ToRef^" 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($FromRef)) {
        Write-Error "Could not determine a previous tag from $ToRef. Pass FromRef explicitly."
    }
}

$range = "$FromRef..$ToRef"

# Each commit on its own line: "<short-sha>\t<subject>". We don't pull
# body or trailers — release notes derive from subjects only, which is
# the Conventional Commits contract.
$log = & git log --no-merges --pretty=format:'%h%x09%s' $range
if ($LASTEXITCODE -ne 0) {
    Write-Error "git log $range failed."
}
if ([string]::IsNullOrWhiteSpace($log)) {
    # Empty range — still emit a useful header so downstream `gh release
    # edit` doesn't fail with empty input.
    "## Changes since $FromRef"
    ""
    "_No commits between $FromRef and $ToRef._"
    return
}

# Conventional Commits headers we care about. Order = display order in
# the output; anything not matching a known prefix lands under "Other".
$buckets = [ordered]@{
    'feat'     = 'Features'
    'fix'      = 'Fixes'
    'perf'     = 'Performance'
    'refactor' = 'Refactors'
    'docs'     = 'Documentation'
    'test'     = 'Tests'
    'build'    = 'Build'
    'ci'       = 'CI'
    'chore'    = 'Chores'
}

# Map bucket key -> List<string> of formatted entries.
$grouped = [ordered]@{}
foreach ($key in $buckets.Keys) { $grouped[$key] = [System.Collections.Generic.List[string]]::new() }
$other = [System.Collections.Generic.List[string]]::new()

# `feat(scope): subject` -> capture type, scope (optional), subject.
$pattern = '^(?<type>[a-z]+)(?<bang>!)?(?:\((?<scope>[^)]+)\))?:\s*(?<rest>.+)$'

foreach ($line in ($log -split "`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line -split "`t", 2
    if ($parts.Length -lt 2) { continue }
    $sha = $parts[0].Trim()
    $subject = $parts[1].Trim()

    $m = [regex]::Match($subject, $pattern)
    if (-not $m.Success) {
        $other.Add("- $subject ($sha)")
        continue
    }
    $type = $m.Groups['type'].Value.ToLowerInvariant()
    $scope = $m.Groups['scope'].Value
    $rest = $m.Groups['rest'].Value
    $breaking = $m.Groups['bang'].Success
    $scopePart = if ($scope) { "**$scope**: " } else { '' }
    $breakingPart = if ($breaking) { ' ⚠ **BREAKING**' } else { '' }
    $entry = "- $scopePart$rest$breakingPart ($sha)"

    if ($grouped.Contains($type)) {
        $grouped[$type].Add($entry)
    }
    else {
        $other.Add($entry)
    }
}

# Render markdown — header per non-empty bucket, then "Other" if any.
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Changes since $FromRef")
[void]$sb.AppendLine()
$emitted = $false
foreach ($key in $buckets.Keys) {
    $entries = $grouped[$key]
    if ($entries.Count -eq 0) { continue }
    [void]$sb.AppendLine("### $($buckets[$key])")
    foreach ($e in $entries) { [void]$sb.AppendLine($e) }
    [void]$sb.AppendLine()
    $emitted = $true
}
if ($other.Count -gt 0) {
    [void]$sb.AppendLine('### Other')
    foreach ($e in $other) { [void]$sb.AppendLine($e) }
    [void]$sb.AppendLine()
    $emitted = $true
}
if (-not $emitted) {
    [void]$sb.AppendLine("_No matching commits between $FromRef and $ToRef._")
}

Write-Output $sb.ToString().TrimEnd()
