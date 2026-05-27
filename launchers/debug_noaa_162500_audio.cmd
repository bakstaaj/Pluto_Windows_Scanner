@echo off
setlocal EnableExtensions EnableDelayedExpansion

REM Phase 3B NOAA 162.500 MHz audio diagnostics for Pluto Windows Scanner GUI v2.
REM Fixed version: run_test no longer tries to execute the test label as a command.

set "PROJECT_DIR=%~dp0.."
cd /d "%PROJECT_DIR%"

set "MSYS2_ROOT=C:\msys64"
set "PATH=%MSYS2_ROOT%\ucrt64\bin;%MSYS2_ROOT%\usr\bin;%PROJECT_DIR%\bin;%PATH%"

set "AUDIO_EXE=%PROJECT_DIR%\bin\pluto_audio_monitor.exe"
set "SESSION_DIR=%PROJECT_DIR%\sessions"
set "LOG=%SESSION_DIR%\noaa_162500_audio_debug_log.txt"

if not exist "%AUDIO_EXE%" (
  echo ERROR: pluto_audio_monitor.exe was not found.
  echo Expected: %AUDIO_EXE%
  pause
  exit /b 1
)

mkdir "%SESSION_DIR%" 2>nul

echo Pluto Windows Scanner GUI v2 - NOAA 162.500 Audio Debug > "%LOG%"
echo Project: %PROJECT_DIR% >> "%LOG%"
echo Audio EXE: %AUDIO_EXE% >> "%LOG%"
echo Date: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo ======================================================================
echo NOAA 162.500 MHz audio debug - fixed launcher
echo ======================================================================
echo This will create several short WAV files in:
echo   %SESSION_DIR%
echo.
echo Close the GUI/live spectrum/audio capture first so only one process owns the Pluto.
echo.
echo Best first comparison:
echo   1_noaa5_preset_squelch_off.wav    known preset path
echo   2_freq_hz_squelch_off.wav         GUI-style Hz frequency path
echo   3_freq_mhz_squelch_off.wav        tests whether --freq expects MHz
echo   4_freq_hz_bw_25k_squelch_off.wav  NFM-style 25 kHz RF bandwidth
echo   5_freq_hz_bw_12k_squelch_off.wav  tighter NFM 12.5 kHz RF bandwidth
echo   6_freq_hz_squelch_db_minus80.wav  squelch threshold path
echo.
pause

call :run_test "0_help" --help
call :run_test "1_noaa5_preset_squelch_off" --uri ip:192.168.2.1 --mode nfm --preset noaa5 --rate 1000000 --seconds 12 --squelch-off --wav "%SESSION_DIR%\1_noaa5_preset_squelch_off.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"
call :run_test "2_freq_hz_squelch_off" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --seconds 12 --squelch-off --wav "%SESSION_DIR%\2_freq_hz_squelch_off.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"
call :run_test "3_freq_mhz_squelch_off" --uri ip:192.168.2.1 --mode nfm --freq 162.500 --rate 1000000 --seconds 12 --squelch-off --wav "%SESSION_DIR%\3_freq_mhz_squelch_off.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"
call :run_test "4_freq_hz_bw_25k_squelch_off" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --bw 25000 --seconds 12 --squelch-off --wav "%SESSION_DIR%\4_freq_hz_bw_25k_squelch_off.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"
call :run_test "5_freq_hz_bw_12k_squelch_off" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --bw 12500 --seconds 12 --squelch-off --wav "%SESSION_DIR%\5_freq_hz_bw_12k_squelch_off.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"
call :run_test "6_freq_hz_squelch_db_minus80" --uri ip:192.168.2.1 --mode nfm --freq 162500000 --rate 1000000 --seconds 12 --squelch-db -80 --wav "%SESSION_DIR%\6_freq_hz_squelch_db_minus80.wav" --csv "%SESSION_DIR%\audio_debug_log.csv"

echo.
echo ======================================================================
echo Done.
echo Log:
echo   %LOG%
echo.
echo WAV files created, if each backend test succeeded:
echo   %SESSION_DIR%\1_noaa5_preset_squelch_off.wav
echo   %SESSION_DIR%\2_freq_hz_squelch_off.wav
echo   %SESSION_DIR%\3_freq_mhz_squelch_off.wav
echo   %SESSION_DIR%\4_freq_hz_bw_25k_squelch_off.wav
echo   %SESSION_DIR%\5_freq_hz_bw_12k_squelch_off.wav
echo   %SESSION_DIR%\6_freq_hz_squelch_db_minus80.wav
echo.
echo Please listen to each WAV and send:
echo   1. which file is clear,
echo   2. which file is static,
echo   3. the updated noaa_162500_audio_debug_log.txt.
echo ======================================================================
explorer.exe "%SESSION_DIR%"
pause
exit /b 0

:run_test
set "TEST_NAME=%~1"
echo.
echo ----------------------------------------------------------------------
echo Running !TEST_NAME!
echo ----------------------------------------------------------------------
echo [!TEST_NAME!] >> "%LOG%"

REM Shift the label off the argument list and rebuild the remaining arguments.
shift /1
set "ARGS="
:collect_args
if "%~1"=="" goto have_args
set "ARGS=!ARGS! "%~1""
shift /1
goto collect_args

:have_args
echo Command: "%AUDIO_EXE%" !ARGS! >> "%LOG%"
"%AUDIO_EXE%" !ARGS! >> "%LOG%" 2>&1
set "RC=!ERRORLEVEL!"
echo ExitCode: !RC! >> "%LOG%"

echo Output files after !TEST_NAME!: >> "%LOG%"
if exist "%SESSION_DIR%\*.wav" (
  for %%F in ("%SESSION_DIR%\*.wav") do echo   %%~nxF %%~zF bytes >> "%LOG%"
) else (
  echo   No WAV files present yet. >> "%LOG%"
)
echo. >> "%LOG%"
echo !TEST_NAME! exit code: !RC!
exit /b 0
