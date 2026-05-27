@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\iq_debug_162500"
set "LOG=%OUTDIR%\noaa_162500_iq_debug_log.txt"
set "CSV=%OUTDIR%\audio_iq_debug_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 IQ Debug > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo Checking backend options... >> "%LOG%"
"%AUDIO%" --help | findstr /i "iq-debug rx-channel iq-mode" >> "%LOG%" 2>&1
echo. >> "%LOG%"

call :run rx1_center 162500000 normal
call :run rx1_plus2500 162502500 normal
call :run rx1_invertq 162500000 invert-q

echo. >> "%LOG%"
echo Done. Upload the *_iq_debug.csv files and this log if audio is still static. >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "FREQ=%~2"
set "IQMODE=%~3"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"
set "IQCSV=%OUTDIR%\%LABEL%_%FREQ%_iq_debug.csv"

echo [%LABEL%] >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --seconds 4 --squelch-off --volume 0.05 --audio-lowpass-hz 5000 --audio-highpass-hz 0 --iq-debug-csv "%IQCSV%" --iq-debug-max 100000 --wav "%WAV%" --csv "%CSV%" >> "%LOG%"
"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel 1 --iq-mode %IQMODE% --seconds 4 --squelch-off --volume 0.05 --audio-lowpass-hz 5000 --audio-highpass-hz 0 --iq-debug-csv "%IQCSV%" --iq-debug-max 100000 --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
if exist "%IQCSV%" for %%F in ("%IQCSV%") do echo IQCSV_SIZE: %%~zF bytes >> "%LOG%"
echo. >> "%LOG%"
exit /b 0
