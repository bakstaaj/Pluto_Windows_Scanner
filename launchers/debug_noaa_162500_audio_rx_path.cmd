@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
set "AUDIO=%ROOT%\bin\pluto_audio_monitor.exe"
set "OUTDIR=%ROOT%\sessions\audio_rx_path_162500"
set "LOG=%OUTDIR%\noaa_162500_audio_rx_path_log.txt"
set "CSV=%OUTDIR%\audio_rx_path_log.csv"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 RX Path Audio Debug > "%LOG%"
echo Project: %ROOT% >> "%LOG%"
echo Audio EXE: %AUDIO% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

if not exist "%AUDIO%" (
  echo ERROR: Missing audio backend: %AUDIO% >> "%LOG%"
  type "%LOG%"
  exit /b 1
)

echo Checking updated backend options... >> "%LOG%"
"%AUDIO%" --help | findstr /C:"rx-channel" /C:"iq-mode" >> "%LOG%" 2>&1

echo Running RX1/RX2 diagnostic captures. Close the GUI/live spectrum first. >> "%LOG%"
echo. >> "%LOG%"

call :run 01_rx1_center_normal 1 162500000 normal
call :run 02_rx2_center_normal 2 162500000 normal
call :run 03_rx1_plus2500_normal 1 162502500 normal
call :run 04_rx2_plus2500_normal 2 162502500 normal
call :run 05_rx2_center_invert_q 2 162500000 invert-q
call :run 06_rx2_center_invert_i 2 162500000 invert-i
call :run 07_rx2_center_swap 2 162500000 swap

echo. >> "%LOG%"
echo Done. WAV files are in: %OUTDIR% >> "%LOG%"
echo Listen first to 01 and 02. If 02 is clear and 01 is static, your antenna/signal path is RX2 and the GUI needs an RX channel control. >> "%LOG%"
type "%LOG%"
exit /b 0

:run
set "LABEL=%~1"
set "RX=%~2"
set "FREQ=%~3"
set "IQ=%~4"
set "WAV=%OUTDIR%\%LABEL%_%FREQ%.wav"

echo [%LABEL%] >> "%LOG%"
echo RX channel: %RX% >> "%LOG%"
echo Frequency: %FREQ% Hz >> "%LOG%"
echo I/Q mode: %IQ% >> "%LOG%"
echo WAV: %WAV% >> "%LOG%"
echo Command: "%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel %RX% --iq-mode %IQ% --seconds 8 --squelch-off --volume 0.05 --wav "%WAV%" --csv "%CSV%" >> "%LOG%"

"%AUDIO%" --uri ip:192.168.2.1 --mode nfm --freq %FREQ% --rate 1000000 --rx-channel %RX% --iq-mode %IQ% --seconds 8 --squelch-off --volume 0.05 --wav "%WAV%" --csv "%CSV%" >> "%LOG%" 2>&1

echo ExitCode: %ERRORLEVEL% >> "%LOG%"
if exist "%WAV%" (
  for %%F in ("%WAV%") do echo WAV_SIZE: %%~zF bytes >> "%LOG%"
) else (
  echo WAV_MISSING >> "%LOG%"
)
echo. >> "%LOG%"
exit /b 0
