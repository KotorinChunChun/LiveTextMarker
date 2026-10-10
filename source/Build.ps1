param([switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and run this script again.' }
if (-not (Test-Path 'tessdata/jpn.traineddata') -or -not (Test-Path 'tessdata/eng.traineddata')) { throw 'The bundled OCR models are missing. Extract the entire ZIP first.' }
dotnet run --project tests/GeometryTests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Geometry tests failed.' }
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish PointerUnderline.csproj -c Release -r win-x64 --self-contained $selfContained -p:Platform=x64 -o out/PointerUnderline
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Built: out/PointerUnderline/PointerUnderline.exe. Run the EXE with the entire folder beside it.'
