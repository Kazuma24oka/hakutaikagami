param([Parameter(Mandatory=$true)][string]$Godot, [switch]$WithRendering)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    New-Item -ItemType Directory -Force TestResults | Out-Null
    dotnet build Game/HakutaiKagami.Game.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
    dotnet test Tests/HakutaiKagami.Tests.csproj --logger 'trx;LogFileName=foundation.trx' --results-directory TestResults
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    & $Godot --headless --path Game --editor --import --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot import failed.' }
    & $Godot --headless --path Game -- --smoke-test
    if ($LASTEXITCODE -ne 0) { throw 'Headless scene test failed.' }
    if ($WithRendering) {
        & $Godot --path Game -- --smoke-test
        if ($LASTEXITCODE -ne 0) { throw 'Rendered scene test failed.' }
    }
} finally { Pop-Location }
