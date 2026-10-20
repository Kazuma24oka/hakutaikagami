param([switch]$Verify)
$ErrorActionPreference='Stop'
$repoPath=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repoPath
try{
 $runtimePython='C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
 if($Verify){& $runtimePython -X utf8 Scripts/verify_historical_roads.py;if($LASTEXITCODE -ne 0){throw 'Verification failed'};return}
 & $runtimePython -X utf8 Scripts/check_historical_binding.py
 if($LASTEXITCODE -ne 0){throw 'Reverify and inspect the actual video/images first.'}
 $godotPath=Join-Path $repoPath '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
 Start-Process -FilePath $godotPath -WorkingDirectory $repoPath -WindowStyle Hidden -ArgumentList @('--path',('"'+(Join-Path $repoPath 'Game')+'"'),'--resolution','1400x1100','res://Scenes/HistoricalRoadTrial.tscn')
}finally{Pop-Location}
