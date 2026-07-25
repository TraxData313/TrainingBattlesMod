# Builds the mod and assembles a CLEAN, reproducible module layout for the Steam Workshop
# (or any manual distribution) under dist\TrainingBattles — the same files deploy.ps1 puts
# into the game but under the REAL module identity (Id "TrainingBattles", name "Training
# Battles", no .Dev rename) and from scratch every time, so no stale file from an old build
# can ride along. Also drops a versioned zip beside it, reading the version from
# module\SubModule.xml.
# Usage: powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot "TrainingBattles"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# A clean slate is the whole point of packaging.
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }

dotnet build (Join-Path $repoRoot "src\TrainingBattles.Module\TrainingBattles.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\TrainingBattles.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "TrainingBattles.dll") $binDir -Force
Copy-Item (Join-Path $outDir "TrainingBattles.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force

# GUI assets (the custom windows' prefab XMLs). The dist dir is freshly made, plain copy is safe.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) { Copy-Item $guiSource (Join-Path $moduleDir "GUI") -Recurse }

# The version stamp comes from the manifest, so the zip name always tells the truth.
$version = "unversioned"
try {
    [xml]$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml")
    $v = $manifest.Module.Version.value
    if ($v) { $version = $v -replace '[^\w\.\-]', '' }
} catch { }

$zipPath = Join-Path $distRoot "TrainingBattles_$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $moduleDir -DestinationPath $zipPath

Write-Host "Packaged $version to $moduleDir"
Write-Host "Zip: $zipPath"
Write-Host "Workshop upload: point the uploader at the dist\TrainingBattles folder."
