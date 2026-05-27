# Windows Scanner GUI v2 Phase 3B - Audio Debug Launcher Fix

This package fixes the NOAA 162.500 MHz audio diagnostic launcher.

## Problem fixed

The Phase 3A launcher accidentally passed the test label as the executable command.
For example, it tried to run:

```text
"1_noaa5_preset_squelch_off" "...\pluto_audio_monitor.exe" ...
```

Windows therefore returned:

```text
'"1_noaa5_preset_squelch_off"' is not recognized as an internal or external command
ExitCode: 9009
```

No WAV files were created because `pluto_audio_monitor.exe` never ran.

## Installation

From MSYS2 UCRT64:

```bash
/c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase3B_AudioDebugFix/tools/install_windows_scanner_gui_v2_phase3b_audio_debug_fix.sh
```

## Run

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_audio.cmd
```

Close the GUI/live spectrum before running this test so only one process owns the Pluto.

## Output

Expected output files are written to `sessions/`:

```text
1_noaa5_preset_squelch_off.wav
2_freq_hz_squelch_off.wav
3_freq_mhz_squelch_off.wav
4_freq_hz_bw_25k_squelch_off.wav
5_freq_hz_bw_12k_squelch_off.wav
6_freq_hz_squelch_db_minus80.wav
noaa_162500_audio_debug_log.txt
```

Report which WAV is clear and which are static.
