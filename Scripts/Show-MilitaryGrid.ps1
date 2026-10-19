param([switch]$ValidateOnly)
$ErrorActionPreference="Stop"
$repoRoot=Split-Path $PSScriptRoot -Parent
$pythonPath="C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
if($ValidateOnly){& $pythonPath "$PSScriptRoot\validate_military_grid.py" $repoRoot}
else{& $pythonPath "$PSScriptRoot\validate_military_grid.py" $repoRoot --show}
if($LASTEXITCODE -ne 0){throw "Military grid validation failed"}
