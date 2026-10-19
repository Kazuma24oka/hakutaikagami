param([Parameter(Mandatory=$true)][string]$Godot, [switch]$WithRendering)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $resultFolder = Join-Path 'TestResults' ('vertical-' + [Guid]::NewGuid().ToString('N'))
    dotnet build Game/HakutaiKagami.Game.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    dotnet test Tests/HakutaiKagami.Tests.csproj --logger 'trx;LogFileName=tests.trx' --results-directory $resultFolder
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $Godot --headless --path Game -- --smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Foundation regression failed.' }
    & $Godot --headless --path Game -- --tax-smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Tax scene failed.' }
    if ($WithRendering) {
        & $Godot --path Game -- --tax-smoke-test
        if ($LASTEXITCODE -ne 0) { throw 'Rendered tax scene failed.' }
    }
} finally { Pop-Location }
