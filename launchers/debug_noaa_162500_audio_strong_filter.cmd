@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_strong_filter_162500"
set "LOG=%OUTDIR%\noaa_162500_audio_strong_filter_log.txt"
set "CSV=%OUTDIR%\audio_strong_filter_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 Strong Filter Audio Debug > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

if not exist "%AUDIO%" (
  echo ERROR: Missing audio backend: %AUDIO% >> "%LOG%"
  type "%LOG%"
  exit /b 1
)

echo Checking backend options... >> "%LOG%"
"%AUDIO%" --help | findstr /C:"audio-highpass" /C:"fm-deviation" /C:"rx-channel" /C:"iq-mode" >> "%LOG%" 2>&1

echo Running RX1 strong-filter diagnostic captures. Close GUI/live spectrum first. >> "%LOG%"
echo. >> "%LOG%"

call :run 01_rx1_nfm_dev5000_lp5000_hp300 162500000 1 normal 5000 5000 300 0.70
call :run 02_rx1_nfm_dev2500_lp3000_hp200 162500000 1 normal 2500 3000 200 0.90
call :run 03_rx1_nfm_dev5000_lp3000_hp200 162500000 1 normal 5000 3000 200 0.90
call :run 04_rx1_plus2500_dev5000_lp5000_hp300 162502500 1 normal 5000 5000 300 0.70
call :run 05_rx1_minus2500_dev5000_lp5000_hp300 162497500 1 normal 5000 5000 300 0.70
call :run 06_rx1_invertq_dev5000_lp5000_hp300 162500000 1 invert-q 5000 5000 300 0.70
call :run 07_rx1_swap_dev5000_lp5000_hp300 162500000 1 swap 5000 5000 300 0.70

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "FREQ=%~2"
set "RX=%~3"
set "IQ=%~4"
set "DEV=%~5"
set "LP=%~6"
set "HP=%~7"
set "VOL=%~8"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel %RX% --iq-mode %IQ% --fm-deviation-hz %DEV% --audio-lowpass-hz %LP% --audio-highpass-hz %HP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%"
"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel %RX% --iq-mode %IQ% --fm-deviation-hz %DEV% --audio-lowpass-hz %LP% --audio-highpass-hz %HP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
