@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_best_current_162500"
set "LOG=%OUTDIR%\noaa_162500_best_audio_current_log.txt"
set "CSV=%OUTDIR%\audio_best_current_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 Current Best Audio Settings > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo Checking backend options... >> "%LOG%"
"%AUDIO%" --help | findstr /C:"fm-channel-lowpass" /C:"audio-highpass" /C:"rx-channel" /C:"iq-mode" >> "%LOG%" 2>&1
echo. >> "%LOG%"

if not exist "%AUDIO%" (
  echo ERROR: Missing audio backend: %AUDIO% >> "%LOG%"
  type "%LOG%"
  exit /b 1
)

call :run 01_best_norm_ch08k_lp3000_hp150 162500000 normal 8000 3000 150 0.45
call :run 02_alt_invq_ch08k_lp3000_hp150 162500000 invert-q 8000 3000 150 0.45
call :run 03_best_norm_longer_20s 162500000 normal 8000 3000 150 0.45 20
call :run 04_alt_invq_longer_20s 162500000 invert-q 8000 3000 150 0.45 20

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "FREQ=%~2"
set "IQMODE=%~3"
set "CHLP=%~4"
set "ALP=%~5"
set "AHP=%~6"
set "VOL=%~7"
set "SECS=%~8"
if "%SECS%"=="" set "SECS=10"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %ALP% --audio-highpass-hz %AHP% --seconds %SECS% --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%"
"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %ALP% --audio-highpass-hz %AHP% --seconds %SECS% --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
