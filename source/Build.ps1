param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '.NET 8 SDKをインストールしてから再実行してください。' }
if (-not (Test-Path 'tessdata/jpn.traineddata') -or -not (Test-Path 'tessdata/eng.traineddata')) { throw 'OCRモデルが見つかりません。配布フォルダーをすべて展開してください。' }
dotnet run --project tests/GeometryTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw '自動テストが失敗しました。' }
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish LiveTextMarker.csproj -c Release -r win-x64 --self-contained $selfContained -p:Platform=x64 -o out/LiveTextMarker
if ($LASTEXITCODE -ne 0) { throw 'ビルドが失敗しました。' }
Write-Host '出力: out/LiveTextMarker/LiveTextMarker.exe。関連ファイルを同じフォルダーに置いて起動してください。'
