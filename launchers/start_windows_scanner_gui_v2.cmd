@echo off
setlocal

REM Repo-level/release launcher for Windows Pluto SDR Scanner GUI v2.0.
REM Expected location:
REM   launchers\start_windows_scanner_gui_v2.cmd

set "PROJECT_DIR=%~dp0.."
set "GUI_DIR=%PROJECT_DIR%\gui\PlutoWindowsScannerGui_v2"
set "PACKAGED_EXE=%GUI_DIR%\PlutoWindowsScannerGui.exe"
set "PUBLISHED_EXE=%GUI_DIR%\bin\Release\net8.0-windows\win-x64\publish\PlutoWindowsScannerGui.exe"

set "MSYS2_ROOT=C:\msys64"
set "PATH=%MSYS2_ROOT%\ucrt64\bin;%MSYS2_ROOT%\usr\bin;%PATH%"

if exist "%PACKAGED_EXE%" (
    cd /d "%PROJECT_DIR%"
    echo Starting packaged Windows Pluto SDR Scanner GUI v2.0...
    "%PACKAGED_EXE%"
    exit /b %ERRORLEVEL%
)

if exist "%PUBLISHED_EXE%" (
    cd /d "%PROJECT_DIR%"
    echo Starting published Windows Pluto SDR Scanner GUI v2.0...
    "%PUBLISHED_EXE%"
    exit /b %ERRORLEVEL%
)

if not exist "%GUI_DIR%\PlutoWindowsScannerGui.csproj" (
    echo ERROR: GUI project not found:
    echo   %GUI_DIR%\PlutoWindowsScannerGui.csproj
    echo.
    echo If this is a release folder, expected packaged executable:
    echo   %PACKAGED_EXE%
    pause
    exit /b 1
)

cd /d "%GUI_DIR%"
echo Starting Windows Pluto SDR Scanner GUI v2.0 using dotnet run...
echo Project:
echo   %GUI_DIR%
echo.

dotnet run
if errorlevel 1 (
    echo.
    echo ERROR: GUI failed to start. Make sure the .NET Desktop SDK is installed.
    pause
    exit /b 1
)
