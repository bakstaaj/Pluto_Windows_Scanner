# Windows Pluto SDR Scanner GUI v2.0 — Phase 1 Update

Date: 2026-05-26
Target folder: `~/sdrdev/pluto_windows_scanner`
Windows path: `C:\msys64\home\jim\sdrdev\pluto_windows_scanner`

This update keeps the v2.0 work in the separate Windows GUI folder and continues to use the validated v1.6 backend executables from `bin/`.

## What changed

Phase 1 turns the initial static GUI starter into a more useful scanner front-end:

- Added a repeat-scan loop.
- Added a repeat delay field in seconds.
- Added a persistent active-channel list across repeated scans.
- Active-channel rows now track:
  - last signal level
  - peak signal level
  - hit count
  - last-seen time
- Added a Clear List button.
- Added an Open Last CSV button.
- Kept CHIRP CSV export pointed at the accumulated active-channel list.
- Kept Listen / Record Selected using `pluto_audio_monitor.exe`.

## Install/update

Extract this package to:

```text
C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_Phase1
```

Then run from MSYS2 UCRT64:

```bash
/c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase1/tools/install_windows_scanner_gui_v2_phase1.sh

cd ~/sdrdev/pluto_windows_scanner
cmd.exe /c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe /c launchers\\start_windows_scanner_gui_v2.cmd
```

This installer updates the GUI, docs, launchers, configs, and tools. It does not delete or replace your existing `bin/` backend executables unless you later run the backend sync script.

## Suggested first test

Use NOAA Weather first because the range is small:

```text
Mode: Frequency Range
Start Hz: 162400000
Stop Hz: 162550000
Step Hz: 25000
RX Mode: auto
RX Combine: max
Squelch / Threshold: -55 to -65 dBFS
Repeat: checked
Delay sec: 2
```

Click **Start Scan**. The table should accumulate active frequencies. Click **Stop** to stop the repeat loop.

## Current limitation

The waterfall is still scan-pass based, not a raw-IQ real-time waterfall. The next development step should add a streaming spectrum backend using `pluto_spectrum_stream.exe` or a new GUI-specific native streaming tool.
