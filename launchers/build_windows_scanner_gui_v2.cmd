@echo off
setlocal

REM Build/publish the Windows Pluto SDR Scanner GUI v2.0.

set "PROJECT_DIR=%~dp0.."
set "GUI_DIR=%PROJECT_DIR%\gui\PlutoWindowsScannerGui_v2"

if not exist "%GUI_DIR%\PlutoWindowsScannerGui.csproj" (
    echo ERROR: GUI project not found:
    echo   %GUI_DIR%\PlutoWindowsScannerGui.csproj
    pause
    exit /b 1
)

cd /d "%GUI_DIR%"

echo Building GUI...
dotnet build -c Release
if errorlevel 1 (
    echo ERROR: dotnet build failed.
    pause
    exit /b 1
)

echo.
echo Publishing GUI for win-x64, framework-dependent...
dotnet publish -c Release -r win-x64 --self-contained false
if errorlevel 1 (
    echo ERROR: dotnet publish failed.
    pause
    exit /b 1
)

echo.
echo GUI publish complete:
echo   %GUI_DIR%\bin\Release\net8.0-windows\win-x64\publish
pause
