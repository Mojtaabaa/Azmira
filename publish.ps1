$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Push-Location frontend
npm run build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Pop-Location

dotnet publish backend/KonkoorHelp.Api.csproj -c Release -o publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Ensure sandbox templates are present even if csproj links were skipped.
$sandboxOut = Join-Path $PSScriptRoot 'publish\sandbox'
New-Item -ItemType Directory -Force -Path $sandboxOut | Out-Null
Copy-Item (Join-Path $PSScriptRoot 'sandbox\appsettings.defaults.json') $sandboxOut -Force
Copy-Item (Join-Path $PSScriptRoot 'sandbox\appsettings.json.example') $sandboxOut -Force
Copy-Item (Join-Path $PSScriptRoot 'sandbox\PRESERVE.manifest.json') $sandboxOut -Force
Copy-Item (Join-Path $PSScriptRoot 'sandbox\README.md') $sandboxOut -Force
Copy-Item (Join-Path $PSScriptRoot 'deploy\install-rotbeh.ps1') (Join-Path $PSScriptRoot 'publish\install-rotbeh.ps1') -Force

Write-Host "Ready: $PSScriptRoot\publish"
Write-Host "Update site with: .\publish\install-rotbeh.ps1 -Source .\publish -Target C:\inetpub\Rotbeh"
