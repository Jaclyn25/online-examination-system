$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
if (Test-Path -LiteralPath $path) { Write-Host 'Existing .env preserved.'; exit 0 }
$bytes = New-Object byte[] 24
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$secret = ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
$rng.GetBytes($bytes)
$demo = 'A7!' + ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
$rng.Dispose()
[IO.File]::WriteAllText($path, "POSTGRES_PASSWORD=$secret`nDEMO_INSTRUCTOR_PASSWORD=$demo`n")
Write-Host 'Generated secrets in ignored .env.'
