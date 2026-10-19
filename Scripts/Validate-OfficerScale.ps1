param([Parameter(Mandatory=$true)][string]$Godot, [Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Python Scripts/build_officer_scale_trial.py
    if ($LASTEXITCODE -ne 0) { throw 'Export failed.' }
    $before = Get-FileHash Game/Assets/Trials/OfficerScaleV1/*.png
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Import failed.' }
    & $Godot --headless --path Game res://Scenes/OfficerScaleTrial.tscn -- --scale-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Headless scale scene failed.' }
    & $Godot --path Game res://Scenes/OfficerScaleTrial.tscn -- --scale-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Rendered scale scene failed.' }
    & $Python Scripts/build_officer_scale_trial.py --verify-godot
    if ($LASTEXITCODE -ne 0) { throw 'GPU pixel verification failed.' }
    $after = Get-FileHash Game/Assets/Trials/OfficerScaleV1/*.png
    if (Compare-Object $before.Hash $after.Hash) { throw 'Rebuild changed PNGs.' }
    Write-Output 'OFFICER_SCALE_REBUILD_PASS png_hashes_identical=3'
    dotnet test Tests/HakutaiKagami.Tests.csproj --no-restore --logger 'trx;LogFileName=tests.trx' --results-directory (Join-Path TestResults ('officer-scale-' + [Guid]::NewGuid().ToString('N')))
    if ($LASTEXITCODE -ne 0) { throw 'Core regression failed.' }
} finally { Pop-Location }
