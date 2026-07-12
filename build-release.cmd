@echo off
title ZLD Audio Control 1.0 - Release Build
cd /d "%~dp0"

echo.
echo ==========================================
echo   ZLD Audio Control 1.0 - EXE erstellen
echo ==========================================
echo.

dotnet clean .\ZLDAudioControl.csproj -c Release
if errorlevel 1 goto :error

dotnet restore .\ZLDAudioControl.csproj
if errorlevel 1 goto :error

dotnet publish .\ZLDAudioControl.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false

if errorlevel 1 goto :error

echo.
echo Fertig. Die EXE liegt hier:
echo bin\Release\net8.0-windows\win-x64\publish\ZLDAudioControl.exe
echo.
start "" "%~dp0bin\Release\net8.0-windows\win-x64\publish"
pause
exit /b 0

:error
echo.
echo Der Build ist fehlgeschlagen. Die Fehlermeldung steht oben.
pause
exit /b 1
