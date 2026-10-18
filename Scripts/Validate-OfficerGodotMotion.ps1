param(
    [string]$Python = 'C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
    [string]$Godot = "$PSScriptRoot/../.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe"
)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Python Scripts/prepare_officer_godot_motion.py --root .
    if ($LASTEXITCODE -ne 0) { throw 'Bound Godot motion plan invalid' }
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-motion-godot-build.txt
    if ($LASTEXITCODE -ne 0) { throw 'Godot C# build failed' }
    & $Godot --headless --path Game --editor --import --quit 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-motion-godot-import.txt
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed' }
    & $Godot --path Game --resolution 1100x720 --rendering-method gl_compatibility -- --officer-motion-smoke-test 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-motion-godot-runtime.txt
    if ($LASTEXITCODE -ne 0) { throw 'Real Godot display test failed' }
} finally { Pop-Location }
