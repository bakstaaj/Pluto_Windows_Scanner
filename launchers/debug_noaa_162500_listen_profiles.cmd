@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\listen_profiles_noaa_162500"
set "LOG=%OUTDIR%\listen_profiles_noaa_162500_log.txt"
set "CSV=%OUTDIR%\listen_profiles_noaa_162500_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - Phase 3K Listen Profile NOAA Check > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

if not exist "%AUDIO%" (
  echo ERROR: Missing audio backend: %AUDIO% >> "%LOG%"
  type "%LOG%"
  exit /b 1
)

call :run 01_noaa_clean_normal normal
call :run 02_noaa_clean_invertq invert-q

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "IQMODE=%~2"
set "WAV=%OUTDIR%\%LABEL%_162500000.wav"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz 8000 --audio-lowpass-hz 3000 --audio-highpass-hz 150 --seconds 10 --squelch-off --volume 0.45 --wav "%WAV%" --csv "%CSV%" >> "%LOG%"
"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz 8000 --audio-lowpass-hz 3000 --audio-highpass-hz 150 --seconds 10 --squelch-off --volume 0.45 --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
