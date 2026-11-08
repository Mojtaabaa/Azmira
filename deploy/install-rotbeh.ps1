<#
.SYNOPSIS
  Update an IIS/install folder from a publish package while preserving sandbox settings.

.EXAMPLE
  .\deploy\install-rotbeh.ps1 -Source .\publish -Target C:\inetpub\Rotbeh
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string] $Source,

  [Parameter(Mandatory = $true)]
  [string] $Target,

  [switch] $SkipAppPool,
  [string] $AppPoolName = ''
)

$ErrorActionPreference = 'Stop'

$Source = (Resolve-Path $Source).Path
if (-not (Test-Path (Join-Path $Source 'KonkoorHelp.Api.dll'))) {
  throw "Source does not look like a Rotbeh publish folder (missing KonkoorHelp.Api.dll): $Source"
}

New-Item -ItemType Directory -Force -Path $Target | Out-Null
$Target = (Resolve-Path $Target).Path

$manifestPath = Join-Path $Source 'sandbox\PRESERVE.manifest.json'
$preserveRelative = @(
  'sandbox\appsettings.json',
  'sandbox\local',
  'sandbox\secrets'
)
if (Test-Path $manifestPath) {
  try {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.preserve) {
      $preserveRelative = @($manifest.preserve | ForEach-Object { $_ -replace '/', '\' })
    }
  } catch {
    Write-Warning "Could not parse PRESERVE.manifest.json; using built-in preserve list."
  }
}

$backupRoot = Join-Path $env:TEMP ("rotbeh-sandbox-backup-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null

function Backup-Preserved {
  param([string] $Rel)
  $clean = $Rel.TrimEnd('\', '/', '*')
  $from = Join-Path $Target $clean
  if (-not (Test-Path $from)) { return }
  $to = Join-Path $backupRoot $clean
  New-Item -ItemType Directory -Force -Path (Split-Path $to -Parent) | Out-Null
  Write-Host "Preserve backup: $clean"
  Copy-Item -Path $from -Destination $to -Recurse -Force
}

function Restore-Preserved {
  param([string] $Rel)
  $clean = $Rel.TrimEnd('\', '/', '*')
  $from = Join-Path $backupRoot $clean
  if (-not (Test-Path $from)) { return }
  $to = Join-Path $Target $clean
  New-Item -ItemType Directory -Force -Path (Split-Path $to -Parent) | Out-Null
  Write-Host "Restore preserved: $clean"
  Copy-Item -Path $from -Destination $to -Recurse -Force
}

foreach ($rel in $preserveRelative) { Backup-Preserved $rel }

if (-not $SkipAppPool -and $AppPoolName) {
  Import-Module WebAdministration -ErrorAction SilentlyContinue
  if (Get-Command Stop-WebAppPool -ErrorAction SilentlyContinue) {
    Write-Host "Stopping app pool $AppPoolName"
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
  }
}

Write-Host "Syncing $Source -> $Target (sandbox live settings restored after copy)"
# Exclude live secrets during mirror; templates still copy.
& robocopy $Source $Target /MIR /R:2 /W:2 /NFL /NDL /NJH /NJS `
  /XD (Join-Path $Source 'sandbox\local') (Join-Path $Source 'sandbox\secrets') `
  /XF appsettings.json
$rc = $LASTEXITCODE
if ($rc -ge 8) { throw "robocopy failed with exit code $rc" }

# Ensure sandbox templates exist, then restore operator files on top.
New-Item -ItemType Directory -Force -Path (Join-Path $Target 'sandbox') | Out-Null
foreach ($rel in $preserveRelative) { Restore-Preserved $rel }

# First install: seed live settings from example if missing.
$live = Join-Path $Target 'sandbox\appsettings.json'
$example = Join-Path $Target 'sandbox\appsettings.json.example'
$defaults = Join-Path $Target 'sandbox\appsettings.defaults.json'
if (-not (Test-Path $live)) {
  if (Test-Path $example) {
    Copy-Item $example $live
    Write-Host "Created sandbox\appsettings.json from example — fill secrets."
  } elseif (Test-Path $defaults) {
    Copy-Item $defaults $live
    Write-Host "Created sandbox\appsettings.json from defaults — fill secrets."
  }
}

if (-not $SkipAppPool -and $AppPoolName) {
  if (Get-Command Start-WebAppPool -ErrorAction SilentlyContinue) {
    Write-Host "Starting app pool $AppPoolName"
    Start-WebAppPool -Name $AppPoolName
  }
}

Remove-Item -Recurse -Force $backupRoot -ErrorAction SilentlyContinue
Write-Host "Install complete: $Target"
Write-Host "On next start, the app merges any new keys from sandbox\appsettings.defaults.json into sandbox\appsettings.json."
