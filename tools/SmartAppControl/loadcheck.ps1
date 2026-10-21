# Loads the INSTALLED dev DLLs the way the game does: its .NET Framework runtime maps each DLL
# from its file, so LoadFile in a fresh Windows PowerShell 5.1 process gets the same Smart App
# Control (Code Integrity) verdict the launcher would. Exit 0 = all three load.
# Usage: powershell -ExecutionPolicy Bypass -File tools\SmartAppControl\loadcheck.ps1 [-GameFolder <path>]
param(
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$bin = Join-Path $GameFolder "Modules\TrainingBattles.Dev\bin\Win64_Shipping_Client"
$ok = $true
foreach ($f in @("TrainingBattles.Core.dll", "TrainingBattles.dll", "TrainingBattles.Naval.dll")) {
    try {
        $a = [Reflection.Assembly]::LoadFile((Join-Path $bin $f))
        $info = ($a.GetCustomAttributes([Reflection.AssemblyInformationalVersionAttribute], $false) | Select-Object -First 1).InformationalVersion
        Write-Host "LOADED $f - $info"
    }
    catch { $ok = $false; Write-Host "REFUSED $f - $($_.Exception.InnerException.Message) $($_.Exception.Message)" }
}
if ($ok) { exit 0 } else { exit 1 }
