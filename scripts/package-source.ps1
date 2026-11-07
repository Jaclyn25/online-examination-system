param([string]$OutputDirectory = 'artifacts')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$revision = (& git -C $root rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'A committed Git checkout is required.' }
$artifactDirectory = if ([IO.Path]::IsPathRooted($OutputDirectory)) { [IO.Path]::GetFullPath($OutputDirectory) } else { [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory)) }
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$archivePath = Join-Path $artifactDirectory "aperture-exams-source-$revision.zip"
& git -C $root archive --format=zip "--output=$archivePath" HEAD
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '(^|/)(node_modules|bin|obj|dist|\.git|\.env|playwright-report|test-results)(/|$)') { throw "Unexpected excluded path in archive: $($entry.FullName)" }
    }
    $entryCount = $archive.Entries.Count
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$archivePath.sha256", "$hash  $([IO.Path]::GetFileName($archivePath))`n")
Write-Output "Source archive: $archivePath"
Write-Output "Entries: $entryCount"
Write-Output "SHA-256: $hash"
