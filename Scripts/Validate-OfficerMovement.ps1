param(
 [string]$Python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 [string]$Godot="$PSScriptRoot/../.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe"
)
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 & $Python Scripts/prepare_officer_movement.py
 if($LASTEXITCODE -ne 0){throw 'Right native package gate rejected'}
 dotnet build Game/HakutaiKagami.Game.csproj --no-incremental 2>&1 | Tee-Object Art/Officer/MovementV13/godot-build.txt
 if($LASTEXITCODE -ne 0){throw 'Godot build failed'}
 & $Godot --headless --path Game --editor --import --quit 2>&1 | Tee-Object Art/Officer/MovementV13/godot-import.txt
 if($LASTEXITCODE -ne 0){throw 'Godot import failed'}
 & $Godot --path Game res://Scenes/OfficerMovementTrial.tscn --resolution 1280x800 --rendering-method gl_compatibility -- --officer-movement-smoke-test 2>&1 | Tee-Object Art/Officer/MovementV13/godot-runtime.txt
 if($LASTEXITCODE -ne 0){throw 'Godot real rendering failed'}
} finally {Pop-Location}
