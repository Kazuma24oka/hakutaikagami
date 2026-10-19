param([Parameter(Mandatory=$true)][string]$Python,
      [Parameter(Mandatory=$true)][string]$Godot,
      [Parameter(Mandatory=$true)][string]$Node,
      [Parameter(Mandatory=$true)][string]$Playwright)
$ErrorActionPreference='Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $auditImages=Get-ChildItem Game/Assets/Trials/OfficerPartsV1 -Recurse -Filter *.png -ErrorAction SilentlyContinue | Get-FileHash
    & $Python Scripts/test_officer_art_pipeline.py 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-art-audit-tests.txt
    if($LASTEXITCODE -ne 0){throw 'Art regression failed'}
    & $Python Scripts/officer_art_pipeline.py --stage front_authoring
    if($LASTEXITCODE -ne 2){throw 'Unplanned production was not blocked'}
    & $Python Scripts/build_officer_parts_trial.py 2>&1
    if($LASTEXITCODE -eq 0){throw 'Rejected exporter was not blocked'}
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-art-audit-build.txt
    if($LASTEXITCODE -ne 0){throw 'Game build failed'}
    & $Godot --headless --path Game --editor --import --quit 2>&1 | Tee-Object Docs/Proposals/Evidence/officer-art-audit-godot.txt
    if($LASTEXITCODE -ne 0){throw 'Godot import failed'}
    & $Godot --headless --path Game res://Scenes/OfficerPartsTrial.tscn -- --parts-smoke-test 2>&1 | Tee-Object -Append Docs/Proposals/Evidence/officer-art-audit-godot.txt
    if($LASTEXITCODE -ne 2){throw 'Rejected scene was not blocked'}
    & $Node Scripts/verify_officer_art_browser.cjs $Playwright
    if($LASTEXITCODE -ne 0){throw 'Browser verification failed'}
    $auditAfter=Get-ChildItem Game/Assets/Trials/OfficerPartsV1 -Recurse -Filter *.png -ErrorAction SilentlyContinue | Get-FileHash
    if(Compare-Object @($auditImages|ForEach-Object { $_.Path+':'+$_.Hash }) @($auditAfter|ForEach-Object { $_.Path+':'+$_.Hash })){throw 'Rejected images changed during audit'}
    Write-Output 'OFFICER_ART_AUDIT_PASS technical_regression_only=1 artwork_approved=0 rejected_pngs_unchanged=1 native_input=0'
}finally{Pop-Location}
