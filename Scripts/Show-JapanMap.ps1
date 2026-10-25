param([switch]$Verify)
$ErrorActionPreference='Stop'
$repoPath=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoPath
try {
    $runtimePython='C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if($Verify){& $runtimePython -X utf8 Scripts/verify_japan_map.py;if($LASTEXITCODE -ne 0){throw 'Geography verification failed'};return}
    & $runtimePython -X utf8 Scripts/check_japan_binding.py
    if($LASTEXITCODE -ne 0){throw 'Inputs or evidence changed. Reverify and review actual images first.'}
    $godotPath=Join-Path $repoPath '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
    Start-Process -FilePath $godotPath -WorkingDirectory $repoPath -WindowStyle Hidden -ArgumentList @('--path',('"'+(Join-Path $repoPath 'Game')+'"'),'--resolution','1440x960','res://Scenes/JapanMapTrial.tscn')
}finally{Pop-Location}
