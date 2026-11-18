# 検査を流して、ターミナルに進み具合を出す（2026-11-18 ユーザー「ターミナルでわかるイメージ」）。
# 使い方: .\Scripts\run-tests.ps1 <置き場の名前> [dotnet test に渡す引数...]
#   例: .\Scripts\run-tests.ps1 2026-11-18_全件
#   例: .\Scripts\run-tests.ps1 和睦だけ --filter FullyQualifiedName~NationalTruceRoundTests
# 1分以上かかった検査・失敗・長引いている検査（1分ごと、いつもの時間つき）・30秒ごとの合計が流れる。
param([Parameter(Mandatory=$true)][string]$Name, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Rest)
chcp 65001 > $null
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$env:PYTHONIOENCODING = 'utf-8'
$dir = Join-Path 'D:\02_プロジェクト別\百代鏡\検証記録' $Name
python (Join-Path $PSScriptRoot 'test_watch.py') run $dir @Rest
