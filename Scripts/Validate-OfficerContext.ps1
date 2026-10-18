param([Parameter(Mandatory=$true)][string]$Godot, [Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Python Scripts/build_officer_context_trial.py
    if ($LASTEXITCODE -ne 0) { throw 'Context export failed.' }
    $before = Get-FileHash Game/Assets/Trials/OfficerContextV1/*.png
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Import failed.' }
    & $Godot --headless --path Game res://Scenes/OfficerContextTrial.tscn -- --context-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Headless context scene failed.' }
    & $Godot --path Game res://Scenes/OfficerContextTrial.tscn -- --context-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Rendered context scene failed.' }
    & $Python Scripts/build_officer_context_trial.py
    if ($LASTEXITCODE -ne 0) { throw 'Context rebuild failed.' }
    $after = Get-FileHash Game/Assets/Trials/OfficerContextV1/*.png
    if (Compare-Object $before.Hash $after.Hash) { throw 'Context rebuild changed PNGs.' }
    Write-Output 'OFFICER_CONTEXT_REBUILD_PASS png_hashes_identical=4'
} finally { Pop-Location }
