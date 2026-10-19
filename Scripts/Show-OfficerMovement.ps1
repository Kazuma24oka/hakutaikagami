param([string]$Python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 & $Python Scripts/prepare_officer_movement.py
 if($LASTEXITCODE -ne 0){throw 'Right native package gate rejected'}
 $engine=(Resolve-Path '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64.exe').Path
 Start-Process -FilePath $engine -ArgumentList '--path','Game','res://Scenes/OfficerMovementTrial.tscn','--resolution','1280x800','--rendering-method','gl_compatibility' -WorkingDirectory (Get-Location).Path -WindowStyle Normal
} finally {Pop-Location}
