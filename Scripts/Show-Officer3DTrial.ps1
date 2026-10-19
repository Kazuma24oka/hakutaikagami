param([switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$pythonExe = 'C:\Users\hizen\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if (!(Test-Path -LiteralPath $pythonExe)) { throw '検証用Pythonがありません。使用するPythonを明示して同じ検証スクリプトを実行してください。' }
$arguments = @((Join-Path $PSScriptRoot 'validate_officer3d_trial.py'), $repoRoot)
if (!$ValidateOnly) { $arguments += '--show' }
& $pythonExe @arguments
if ($LASTEXITCODE -ne 0) { throw '3D試作の固定入力検査または実行検査が失敗しました。' }
