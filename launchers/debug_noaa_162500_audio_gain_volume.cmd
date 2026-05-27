@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0debug_noaa_162500_audio_gain_volume.ps1"
exit /b %ERRORLEVEL%
