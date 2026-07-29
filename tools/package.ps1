# Builds the mod and assembles a CLEAN, reproducible module layout for the Steam Workshop
# (or any manual distribution) under dist\TrainingBattles — the same files deploy.ps1 puts
# into the game but under the REAL module identity (Id "TrainingBattles", name "Training
# Battles", no .Dev rename) and from scratch every time, so no stale file from an old build
# can ride along. Also drops a versioned zip beside it, reading the version from
# module\SubModule.xml.
# Usage: powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot "TrainingBattles"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# A clean slate is the whole point of packaging.
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }

# Building the NAVAL SATELLITE builds the module too (it references it) - see deploy.ps1.
dotnet build (Join-Path $repoRoot "src\TrainingBattles.Naval\TrainingBattles.Naval.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\TrainingBattles.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "TrainingBattles.dll") $binDir -Force
Copy-Item (Join-Path $outDir "TrainingBattles.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force

# The naval satellite: shipped, but loaded only when War Sails is present (NavalBridge).
Copy-Item (Join-Path $repoRoot "src\TrainingBattles.Naval\bin\$Configuration\TrainingBattles.Naval.dll") $binDir -Force

# GUI assets (the custom windows' prefab XMLs). The dist dir is freshly made, plain copy is safe.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) { Copy-Item $guiSource (Join-Path $moduleDir "GUI") -Recurse }

# THE SOFT-DEPENDENCY GATE. A developer who owns War Sails and MCM cannot playtest the install
# that has neither - this reads the built assembly's metadata and answers it (see the tool's own
# header). War Sails is a HARD gate: a naval type in the module's type surface is what made
# v1.3.0-v1.3.3 fail to start for every player without the DLC. MCM is advisory for now - the
# settings class has carried an MCM base type since v1.0.0 and needs the same satellite treatment
# before this can be promoted to a gate (TASKS_TODO).
$guard = Join-Path $repoRoot "tools\AssemblyGuard\AssemblyGuard.csproj"
$moduleDll = Join-Path $binDir "TrainingBattles.dll"
dotnet run --project $guard -c $Configuration -- $moduleDll NavalDLC
if ($LASTEXITCODE -ne 0) { throw "Soft-dependency gate failed: the module would not load without War Sails." }
dotnet run --project $guard -c $Configuration -- $moduleDll MCMv5
if ($LASTEXITCODE -ne 0) { Write-Warning "MCM is not isolated yet - players without MCM cannot load the mod (known, see TASKS_TODO)." }

# The version stamp comes from the manifest, so the zip name always tells the truth.
$version = "unversioned"
try {
    [xml]$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml")
    $v = $manifest.Module.Version.value
    if ($v) { $version = $v -replace '[^\w\.\-]', '' }
} catch { }

# Since 2026.07.28 the version bumps only on RELEASE day, so fixes pile up in main under the
# last released number - and packaging then would silently overwrite the zip of the release that
# is actually live. Refuse, unless the caller means it (-Force).
$zipPath = Join-Path $distRoot "TrainingBattles_$version.zip"
if ((Test-Path $zipPath) -and -not $Force) {
    throw "dist\TrainingBattles_$version.zip already exists - bump the version in module\SubModule.xml for the release, or pass -Force to overwrite."
}
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $moduleDir -DestinationPath $zipPath

Write-Host "Packaged $version to $moduleDir"
Write-Host "Zip: $zipPath"
Write-Host "Workshop upload: point the uploader at the dist\TrainingBattles folder."
