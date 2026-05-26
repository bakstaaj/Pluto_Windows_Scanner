@echo off
setlocal

REM Run from the repo root or from a release folder with build\native present.
set EXE=%~dp0..\build\native\pluto_activity_monitor_rx.exe
if not exist "%EXE%" set EXE=%~dp0..\native\pluto_activity_monitor_rx.exe
if not exist "%EXE%" set EXE=%~dp0..\pluto_activity_monitor_rx.exe

if not exist "%EXE%" (
  echo ERROR: pluto_activity_monitor_rx.exe not found.
  echo Build first with: tools\build_native_ucrt64.sh
  exit /b 1
)

"%EXE%" --uri ip:192.168.2.1 --freq-file configs\activity_rx_test_freqs.csv --rx-mode auto --rx-combine max --threshold-dbfs -55 --csv activity_monitor_rx_host.csv --verbose
endlocal
