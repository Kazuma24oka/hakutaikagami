param([Parameter(Mandatory=$true)][string]$Godot, [switch]$WithRendering)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $resultFolder = Join-Path 'TestResults' ('late-' + [Guid]::NewGuid().ToString('N'))
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    dotnet test Tests/HakutaiKagami.Tests.csproj --logger 'trx;LogFileName=tests.trx' --results-directory $resultFolder
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    foreach ($scenario in @('--smoke-test','--tax-smoke-test','--delivery-smoke-test','--late-smoke-test')) {
        & $Godot --headless --path Game -- $scenario
        if ($LASTEXITCODE -ne 0) { throw "Scene failed: $scenario" }
    }
    if ($WithRendering) {
        & $Godot --path Game -- --late-smoke-test
        if ($LASTEXITCODE -ne 0) { throw 'Rendered late duplicate GUI failed.' }
    }
} finally { Pop-Location }
