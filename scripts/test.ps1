$ErrorActionPreference = 'Stop'
Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) '.env') | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
}
$env:EXAMS_TEST_CONNECTION = "Host=127.0.0.1;Port=54331;Database=aperture;Username=aperture;Password=$env:POSTGRES_PASSWORD"
dotnet test -c Release --logger 'trx;LogFileName=backend.trx' --results-directory artifacts/TestResults
exit $LASTEXITCODE
