param([switch]$ValidateOnly)
$ErrorActionPreference="Stop"
$repoRoot=Split-Path $PSScriptRoot -Parent
$pythonPath="C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
if($ValidateOnly){& $pythonPath "$PSScriptRoot\validate_officer3d_v2.py" $repoRoot}
else{& $pythonPath "$PSScriptRoot\validate_officer3d_v2.py" $repoRoot --show}
if($LASTEXITCODE -ne 0){throw "3D validation failed"}
