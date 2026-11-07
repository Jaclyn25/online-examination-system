$ErrorActionPreference = 'Stop'
Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) '.env') | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process') }
}
$apertureDbPort = if ($env:DB_PORT) { $env:DB_PORT } else { '54331' }
$env:ConnectionStrings__Exams = "Host=127.0.0.1;Port=$apertureDbPort;Database=aperture;Username=aperture;Password=$env:POSTGRES_PASSWORD"
$env:Database__Migrate = 'true'
$env:Database__Seed = 'true'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Demo__Enabled = 'true'
$env:Demo__InstructorPassword = $env:DEMO_INSTRUCTOR_PASSWORD
dotnet run --project backend/Aperture.Api --no-launch-profile --urls http://127.0.0.1:5082
