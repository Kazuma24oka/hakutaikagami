$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
Push-Location $repo
try {
    & $python (Join-Path $PSScriptRoot 'check_play_guide_binding.py')
    if($LASTEXITCODE -ne 0){throw '現在の候補と検証記録が一致しません。再検証してから開いてください。'}
    $godot=Join-Path $repo '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64.exe'
    $project=Join-Path $repo 'Game'
    # The user explicitly requested the interactive candidate window. No pointer/input settings are touched.
    $process=Start-Process -FilePath $godot -ArgumentList @('--path',('"'+$project+'"'),'--resolution','1280x900') -WindowStyle Normal -PassThru
    Write-Output ('通常の新規ゲーム入口を起動しました。ProcessId='+$process.Id)
} finally {Pop-Location}
