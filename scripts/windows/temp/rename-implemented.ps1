# rename-implemented.ps1
# Prepends file creation date/time (UTC) in sortable format to each file in tasks\implemented

$rootPath      = "C:\_Template\Projects\_code\synthea-cli\docs\tasks\implemented"   # adjust if you run from elsewhere
$dateFmt       = "yyyy-MM-dd_HH-mm-ss"
$dash          = "-"

Get-ChildItem -Path $rootPath -File -Recurse | ForEach-Object {
    $timestamp = ($_.CreationTimeUtc).ToString($dateFmt)
    $newName   = "${timestamp}${dash}$($_.Name)"

    if ($_.Name -notmatch '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}-') {   # skip if already renamed
        Rename-Item -LiteralPath $_.FullName -NewName $newName
        Write-Host "Renamed $($_.Name) -> $newName"
    } else {
        Write-Host "Skipped (already prefixed): $($_.Name)"
    }
}
