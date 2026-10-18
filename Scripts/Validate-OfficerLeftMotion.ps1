param(
 [string]$Python='C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
 [string]$Godot="$PSScriptRoot/../.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe"
)
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
 & $Python Scripts/prepare_officer_left_motion.py
 if($LASTEXITCODE -ne 0){throw 'Right native package gate rejected'}
 dotnet build Game/HakutaiKagami.Game.csproj --no-incremental 2>&1 | Tee-Object Art/Officer/LeftMotionV12/godot-build.txt
 if($LASTEXITCODE -ne 0){throw 'Godot build failed'}
 & $Godot --headless --path Game --editor --import --quit 2>&1 | Tee-Object Art/Officer/LeftMotionV12/godot-import.txt
 if($LASTEXITCODE -ne 0){throw 'Godot import failed'}
 & $Godot --path Game res://Scenes/OfficerLeftMotionTrial.tscn --resolution 1100x720 --rendering-method gl_compatibility -- --officer-left-smoke-test 2>&1 | Tee-Object Art/Officer/LeftMotionV12/godot-runtime.txt
 if($LASTEXITCODE -ne 0){throw 'Godot real rendering failed'}
} finally {Pop-Location}
