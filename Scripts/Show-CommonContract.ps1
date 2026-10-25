param([switch]$HeadlessCheck)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$godot = Join-Path $repoRoot '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
$pythonPath = 'C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $pythonPath -X utf8 (Join-Path $PSScriptRoot 'verify_common_contract_inputs.py')
if ($LASTEXITCODE -ne 0) { throw 'Common contract inputs changed; regenerate relevant evidence before showing' }
& dotnet build (Join-Path $repoRoot 'Game/HakutaiKagami.Game.csproj') --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'Game build failed' }
& $pythonPath -X utf8 (Join-Path $PSScriptRoot 'verify_common_contract_inputs.py')
if ($LASTEXITCODE -ne 0) { throw 'Inputs changed during build' }
if ($HeadlessCheck) {
    & $godot --headless --path (Join-Path $repoRoot 'Game') 'res://Scenes/CommonContractTrial.tscn' -- --common-contract-check
    if ($LASTEXITCODE -ne 0) { throw 'Common contract UI validation failed' }
} else {
    Start-Process -FilePath $godot -ArgumentList @('--path',('"' + (Join-Path $repoRoot 'Game') + '"'),'res://Scenes/CommonContractTrial.tscn') -WindowStyle Hidden -WorkingDirectory $repoRoot
}
