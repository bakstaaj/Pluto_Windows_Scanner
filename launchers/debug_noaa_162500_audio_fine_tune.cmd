@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_fine_tune_162500"
set "LOG=%OUTDIR%\noaa_162500_audio_fine_tune_log.txt"
set "CSV=%OUTDIR%\audio_fine_tune_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 Audio Fine Tune > "%LOG%"
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

REM These are centered around the best results from Phase 3H:
REM 03 = RX1 normal, channel LP 12k, audio LP 4.5k, HP 300
REM 04 = RX1 invert-q, channel LP 12k, audio LP 4.5k, HP 300
REM Now refine channel width, audio cutoff, highpass, and offset.

call :run 01_norm_ch10k_lp3500_hp200 162500000 normal 10000 3500 200 0.35
call :run 02_norm_ch10k_lp4500_hp200 162500000 normal 10000 4500 200 0.35
call :run 03_norm_ch12k_lp3500_hp200 162500000 normal 12000 3500 200 0.35
call :run 04_norm_ch12k_lp4500_hp200 162500000 normal 12000 4500 200 0.35
call :run 05_norm_ch14k_lp3500_hp200 162500000 normal 14000 3500 200 0.30
call :run 06_norm_ch08k_lp3000_hp150 162500000 normal 8000 3000 150 0.45
call :run 07_invq_ch10k_lp3500_hp200 162500000 invert-q 10000 3500 200 0.35
call :run 08_invq_ch12k_lp3500_hp200 162500000 invert-q 12000 3500 200 0.35
call :run 09_invq_ch12k_lp4500_hp200 162500000 invert-q 12000 4500 200 0.35
call :run 10_invq_ch08k_lp3000_hp150 162500000 invert-q 8000 3000 150 0.45
call :run 11_norm_m1k_ch12k_lp3500_hp200 162499000 normal 12000 3500 200 0.35
call :run 12_norm_p1k_ch12k_lp3500_hp200 162501000 normal 12000 3500 200 0.35
call :run 13_invq_m1k_ch12k_lp3500_hp200 162499000 invert-q 12000 3500 200 0.35
call :run 14_invq_p1k_ch12k_lp3500_hp200 162501000 invert-q 12000 3500 200 0.35

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
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %ALP% --audio-highpass-hz %AHP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%"
"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %ALP% --audio-highpass-hz %AHP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
