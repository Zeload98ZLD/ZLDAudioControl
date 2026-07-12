$ErrorActionPreference = "Stop"

Write-Host "ZLD Audio Control wird als einzelne EXE veröffentlicht..." -ForegroundColor Cyan

dotnet restore
dotnet publish .\ZLDAudioControl.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

$publish = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"
Write-Host ""
Write-Host "Fertig:" -ForegroundColor Green
Write-Host (Join-Path $publish "ZLDAudioControl.exe")
Start-Process explorer.exe $publish
