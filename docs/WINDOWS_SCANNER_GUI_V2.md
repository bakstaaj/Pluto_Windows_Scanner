# Windows Pluto SDR Scanner GUI v2.0

This v2.0 work should live in a new standalone Windows GUI folder:

```text
~/sdrdev/pluto_windows_scanner
C:\msys64\home\jim\sdrdev\pluto_windows_scanner
```

Keep the older native/backend repo as the v1.x reference and C build workspace:

```text
~/sdrdev/pluto_native_test
C:\msys64\home\jim\sdrdev\pluto_native_test
```

## Install

Extract the package to:

```text
C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_NewFolder
```

Install from MSYS2 UCRT64:

```bash
/c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_NewFolder/tools/install_windows_scanner_gui_v2_new_folder.sh
```

Then run:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe /c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe /c launchers\\start_windows_scanner_gui_v2.cmd
```

If MSYS2 cannot find the .NET SDK, add it to PATH first:

```bash
export PATH="$PATH:/c/Program Files/dotnet"
```

## Architecture

The v2 GUI folder is a Windows-first application shell around the validated v1.6 backend tools.

```text
pluto_windows_scanner/
  bin/        validated scanner/audio backend .exe and DLL files
  gui/        WPF GUI source
  configs/    bands.csv and GUI settings
  docs/       v2 docs plus v1.6 backend reference docs
  launchers/  Windows launchers
  sessions/   scan/audio/report outputs
  tools/      install, sync, and package scripts
```

The GUI currently calls these backend tools from `bin/`:

```text
pluto_dual_rx_power_scan.exe
pluto_audio_monitor.exe
```

The v1.6 backend is still developed and rebuilt in:

```text
~/sdrdev/pluto_native_test
```

After rebuilding the native tools there, refresh this GUI folder with:

```bash
cd ~/sdrdev/pluto_windows_scanner
./tools/sync_backend_from_v1_repo.sh
```

## Current v2.0 GUI Capabilities

- Scan a single frequency.
- Scan a frequency range.
- Scan a standard band from `configs/bands.csv`.
- Show spectrum and waterfall-style views from scan result levels.
- Show active channels in a table.
- Select an active channel and record/listen using the audio monitor backend.
- Configure Pluto URI, sample rate, bandwidth, squelch, RX mode, RX combine behavior, sessions folder, and bands file.
- Export detected active channels to an 18-column CHIRP Generic CSV.

## Defaults

```text
URI: ip:192.168.2.1
RX mode: auto
RX combine: max
Sample rate: 1000000
Bandwidth: 1000000
Squelch/threshold: -65 dBFS
```

The RX defaults match the validated dual-RX v1.6 work:

```text
--rx-mode auto
--rx-combine max
```

## Important Limitation

The v2.0 starter GUI waterfall is not yet a continuous live IQ waterfall. It is built from scan result rows after each scan. The next backend milestone is a GUI-friendly live spectrum/waterfall streamer.

## Phase 1 Update

The Phase 1 GUI update adds repeat scanning, active-channel accumulation, hit counts, last/peak dBFS tracking, last-seen timestamps, a Clear List button, and an Open Last CSV button. See `docs/WINDOWS_SCANNER_GUI_V2_PHASE1.md` for install and test steps.

## Phase 2 Live Spectrum Update

Phase 2 adds a live spectrum/waterfall workflow backed by `bin/pluto_spectrum_stream.exe`.

New Scanner-tab controls:

```text
Live Center Hz
FFT
Avg
Interval ms
Gain Mode
Gain dB
Use Selected
Start Live Spectrum
Stop Live
```

Recommended first live test:

```text
URI: ip:192.168.2.1
Live Center Hz: 162550000
Rate: 1000000
RF BW: 1000000
FFT: 512
Avg: 2
Interval ms: 500
Gain Mode: slow_attack
Gain dB: blank
```

The live stream updates the top spectrum chart and waterfall continuously. The Phase 1 scan loop still controls the detected-active-channel table and CHIRP export.
