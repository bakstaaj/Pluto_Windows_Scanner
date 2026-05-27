@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_fm_channel_filter_162500"
set "LOG=%OUTDIR%\noaa_162500_audio_fm_channel_filter_log.txt"
set "CSV=%OUTDIR%\audio_fm_channel_filter_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 FM Channel Filter Audio Debug > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo Checking backend options... >> "%LOG%"
"%AUDIO%" --help | findstr /I "fm-channel rx-channel iq-mode" >> "%LOG%" 2>&1
echo. >> "%LOG%"

call :run 01_rx1_normal_chlp18k 162500000 normal 18000 5000 300 0.20
call :run 02_rx1_invertq_chlp18k 162500000 invert-q 18000 5000 300 0.20
call :run 03_rx1_normal_chlp12k 162500000 normal 12000 4500 300 0.25
call :run 04_rx1_invertq_chlp12k 162500000 invert-q 12000 4500 300 0.25
call :run 05_rx1_normal_chlp25k 162500000 normal 25000 5000 300 0.15
call :run 06_rx1_invertq_chlp25k 162500000 invert-q 25000 5000 300 0.15
call :run 07_rx1_plus2500_invertq_chlp18k 162502500 invert-q 18000 5000 300 0.20
call :run 08_rx1_minus2500_invertq_chlp18k 162497500 invert-q 18000 5000 300 0.20

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "FREQ=%~2"
set "IQMODE=%~3"
set "CHLP=%~4"
set "LP=%~5"
set "HP=%~6"
set "VOL=%~7"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %LP% --audio-highpass-hz %HP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%"

"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --fm-channel-lowpass-hz %CHLP% --audio-lowpass-hz %LP% --audio-highpass-hz %HP% --seconds 8 --squelch-off --volume %VOL% --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
