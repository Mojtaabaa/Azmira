$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Push-Location frontend
npm run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Pop-Location

dotnet publish backend/KonkoorHelp.Api.csproj -c Release -o publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Ready: $PSScriptRoot\publish"
