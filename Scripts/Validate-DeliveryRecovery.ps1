param([Parameter(Mandatory=$true)][string]$Godot, [switch]$WithRendering)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $resultFolder = Join-Path 'TestResults' ('delivery-' + [Guid]::NewGuid().ToString('N'))
    dotnet build Game/HakutaiKagami.Game.csproj --no-incremental
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    dotnet test Tests/HakutaiKagami.Tests.csproj --logger 'trx;LogFileName=tests.trx' --results-directory $resultFolder
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $Godot --headless --path Game -- --smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Foundation regression failed.' }
    & $Godot --headless --path Game -- --tax-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Tax regression failed.' }
    & $Godot --headless --path Game -- --delivery-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Delivery GUI recovery failed.' }
    if ($WithRendering) {
        & $Godot --path Game -- --delivery-smoke-test
        if ($LASTEXITCODE -ne 0) { throw 'Rendered delivery GUI recovery failed.' }
    }
} finally { Pop-Location }
