param(
    [string]$Python = 'C:/Users/hizen/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe',
    [string]$Godot = "$PSScriptRoot/../.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe"
)
# Stable entry delegates to the active, gated front-walk profile.
& "$PSScriptRoot/Validate-OfficerFrontWalk.ps1" -Python $Python -Godot $Godot
