param(
 [string]$Python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 [string]$Godot="$PSScriptRoot/../.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe"
)
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 & $Python Scripts/prepare_officer_back_motion.py
 if($LASTEXITCODE -ne 0){throw 'Right native package gate rejected'}
 dotnet build Game/HakutaiKagami.Game.csproj --no-incremental 2>&1 | Tee-Object Art/Officer/BackMotionV10/godot-build.txt
 if($LASTEXITCODE -ne 0){throw 'Godot build failed'}
 & $Godot --headless --path Game --editor --import --quit 2>&1 | Tee-Object Art/Officer/BackMotionV10/godot-import.txt
 if($LASTEXITCODE -ne 0){throw 'Godot import failed'}
 & $Godot --path Game res://Scenes/OfficerBackMotionTrial.tscn --resolution 1100x720 --rendering-method gl_compatibility -- --officer-back-smoke-test 2>&1 | Tee-Object Art/Officer/BackMotionV10/godot-runtime.txt
 if($LASTEXITCODE -ne 0){throw 'Godot real rendering failed'}
} finally {Pop-Location}
