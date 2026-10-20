param([switch]$Verify)
$ErrorActionPreference='Stop'
$repoPath=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoPath
try {
    $runtimePython='C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if($Verify) { & $runtimePython -X utf8 Scripts/verify_regional_route.py; if($LASTEXITCODE -ne 0){throw 'Route verification failed'}; return }
    & $runtimePython -X utf8 Scripts/check_regional_binding.py
    if($LASTEXITCODE -ne 0){throw 'Source or evidence changed. Run verification and review the actual images first.'}
    $godotPath=Join-Path $repoPath '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    Start-Process -FilePath $godotPath -WorkingDirectory $repoPath -WindowStyle Hidden -ArgumentList @('--path',('"'+(Join-Path $repoPath 'Game')+'"'),'--resolution','1400x1000','res://Scenes/RegionalRouteTrial.tscn')
} finally {Pop-Location}
