param([string]$Python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 & $Python Scripts/prepare_officer_left_motion.py
 if($LASTEXITCODE -ne 0){throw 'Right native package gate rejected'}
 $engine=(Resolve-Path '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64.exe').Path
 Start-Process -FilePath $engine -ArgumentList '--path','Game','res://Scenes/OfficerLeftMotionTrial.tscn','--resolution','1100x720','--rendering-method','gl_compatibility' -WorkingDirectory (Get-Location).Path -WindowStyle Normal
} finally {Pop-Location}
