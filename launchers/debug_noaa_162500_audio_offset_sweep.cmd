@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_offset_sweep_162500"
set "LOG=%OUTDIR%\noaa_162500_audio_offset_sweep_log.txt"
set "CSV=%OUTDIR%\audio_offset_sweep_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 Audio Offset Sweep > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

if not exist "%AUDIO%" (
  echo ERROR: Missing audio backend: %AUDIO% >> "%LOG%"
  type "%LOG%"
  exit /b 1
)

call :run 01_m15000 162485000
call :run 02_m12500 162487500
call :run 03_m10000 162490000
call :run 04_m07500 162492500
call :run 05_m05000 162495000
call :run 06_m02500 162497500
call :run 07_center 162500000
call :run 08_p02500 162502500
call :run 09_p05000 162505000
call :run 10_p07500 162507500
call :run 11_p10000 162510000
call :run 12_p12500 162512500
call :run 13_p15000 162515000

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "FREQ=%~2"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo Frequency: %FREQ% Hz >> "%LOG%"
echo WAV: %WAV% >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --seconds 8 --squelch-off --volume 0.05 --wav "%WAV%" --csv "%CSV%" >> "%LOG%"

"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --seconds 8 --squelch-off --volume 0.05 --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
