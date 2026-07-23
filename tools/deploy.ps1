# Builds the mod and installs it into the Bannerlord Modules folder.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
# The local deploy wears its own identity - Id "TrainingBattles.Dev", shown as
# "Training Battles (dev)" - so it can one day sit beside a Steam Workshop copy in the
# launcher and be picked deliberately. Enable only ONE of the two at a time.
$moduleDir = Join-Path $GameFolder "Modules\TrainingBattles.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

dotnet build (Join-Path $repoRoot "src\TrainingBattles.Module\TrainingBattles.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml") -Raw
$manifest = $manifest -replace '<Id value="TrainingBattles" />', '<Id value="TrainingBattles.Dev" />'
$manifest = $manifest -replace '<Name value="Training Battles" />', '<Name value="Training Battles (dev)" />'
Set-Content (Join-Path $moduleDir "SubModule.xml") $manifest -Encoding utf8

$outDir = Join-Path $repoRoot "src\TrainingBattles.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "TrainingBattles.dll") $binDir -Force
Copy-Item (Join-Path $outDir "TrainingBattles.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue

Write-Host "Deployed to $moduleDir as 'Training Battles (dev)' - enable it in the launcher."
