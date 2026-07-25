# Anton's Alt+F4 guard (2026.07.25): killing Bannerlord with Alt+F4 often leaves
# display_mode = 0 (windowed) behind in engine_config.txt, so the next start opens
# windowed. Run this before every launch to pin fullscreen (0 = windowed,
# 1 = borderless, 2 = fullscreen — change $mode if borderless is preferred).
#
# Wire it into Steam: Bannerlord → Properties → Launch Options:
#   cmd /c powershell -ExecutionPolicy Bypass -File "C:\Users\Trax\Documents\BannerlordMods\TrainingBattlesMod\tools\fix-fullscreen.ps1" & %command%
$mode = 2
$file = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Configs\engine_config.txt'
if (Test-Path $file) {
    $text = Get-Content $file
    $fixed = $text -replace '^\s*display_mode\s*=.*$', "display_mode = $mode"
    if (($text -join "`n") -ne ($fixed -join "`n")) {
        Set-Content -Path $file -Value $fixed -Encoding ASCII
    }
}
