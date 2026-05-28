# Pluto Scan

**Pluto Scan** is a Windows host application for scanning, visualizing, and recording RF activity with an Analog Devices Pluto/Pluto+ SDR.

The project is maintained by **bakStaaJ Designs**.

## Overview

Pluto Scan provides a Windows GUI around native Pluto SDR command-line tools. It is designed for practical radio scanning workflows such as NOAA weather radio, VHF airband, amateur radio, FM broadcast checks, and local trunked/control-channel discovery.

The application supports:

- Frequency range, single-frequency, and standard-band scanning
- Configurable band definitions from `configs/bands.csv`
- Narrow-channel scan detection to reduce false active-channel hits
- Spectrum and waterfall display
- Detected active-channel list
- Listen/record workflow with configurable listen profiles
- WAV recording with countdown popup and graceful stop
- CHIRP-compatible CSV export
- Configurable scan speed and progress timing
- Per-band scanner detector defaults

## Current Name

The user-facing application name is:

```text
Pluto Scan
```

Some internal folders and project files may still use earlier names such as `PlutoWindowsScannerGui_v2`. Those names are implementation details and may be renamed later.

## Repository Layout

Typical project layout:

```text
pluto_windows_scanner/
├── bin/
│   ├── pluto_dual_rx_power_scan.exe
│   ├── pluto_audio_monitor.exe
│   └── other native helper tools
├── configs/
│   ├── bands.csv
│   ├── gui_v2_settings.json
│   └── listen_profiles.json
├── gui/
│   └── PlutoWindowsScannerGui_v2/
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── RecordingCountdownWindow.cs
│       └── RunLogWindow.cs
├── launchers/
│   ├── build_windows_scanner_gui_v2.cmd
│   └── start_windows_scanner_gui_v2.cmd
└── sessions/
    ├── scan CSV output
    ├── WAV recordings
    └── troubleshooting logs
```

Native backend source is developed separately in the companion native tool repository and copied into `bin/` when updated.

## Requirements

- Windows
- MSYS2 UCRT64 environment
- .NET 8 Windows Desktop SDK
- Pluto/Pluto+ SDR reachable over IIO, usually:

```text
ip:192.168.2.1
```

- Native backend executables in `bin/`

## Build and Run

From MSYS2 UCRT64:

```bash
cd ~/sdrdev/pluto_windows_scanner

cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd
```

The GUI is configured to open maximized.

## Typical Workflow

1. Select a standard band or enter a frequency range.
2. Confirm scan settings:
   - Rate
   - RF bandwidth
   - threshold dBFS
   - scanner channel low-pass Hz
   - scanner samples
   - scanner settle ms
3. Start scan from the `Scan` menu.
4. Review detected active channels.
5. Double-click an active row or use `Listen / Record`.
6. Use the recording countdown popup to stop early if needed.
7. Export active channels to CHIRP CSV when desired.

## Important Scanner Detection Notes

The scanner uses a narrow-channel detector in `pluto_dual_rx_power_scan.exe`.

This was added because wideband power detection could incorrectly mark nearby channels as active. For example, a strong NOAA signal on `162.500 MHz` could previously make adjacent scan steps appear active. The narrow-channel detector measures power around the tuned center and reduces these false positives.

Recommended detector widths:

```text
NOAA / NFM voice:     8000-12000 Hz
Ham FM / 2m / 70cm:   10000-15000 Hz
Airband AM:           12000-15000 Hz
P25 / DTRS detect:    12000-15000 Hz
Broadcast WBFM:       120000+ Hz or disable with 0
```

## Configuration Files

### `configs/bands.csv`

Defines standard scan bands. Current columns include:

```csv
id,name,start_hz,stop_hz,step_hz,mode,rate_hz,bw_hz,squelch_db,notes,scan_channel_lowpass_hz,scan_samples,scan_settle_ms,active_min_snr_db,progress_overhead_ms
```

Per-band scan defaults can automatically populate detector settings when a band is selected.

### `configs/listen_profiles.json`

Defines listen/record profiles. Profiles can adjust demod mode, RX channel, IQ mode, demod rate, low-pass/high-pass filters, volume, and squelch behavior.

The GUI includes menu actions to reload profiles and open the JSON file.

### `configs/gui_v2_settings.json`

Stores GUI defaults and scanner tuning settings, including:

- repo root
- sessions directory
- bands CSV path
- default CHIRP mode
- listen duration
- active-channel minimum SNR dB
- scanner channel low-pass Hz
- scanner samples
- scanner settle ms
- progress overhead ms

## Scan Progress

The GUI provides estimated progress feedback during long scans. This is useful for large airband scans where backend output may not provide reliable per-frequency UI updates.

The progress estimate uses:

```text
samples / sample_rate + settle_ms + progress_overhead_ms
```

If progress reaches the estimate before the backend exits, the UI shows a finalizing state until the scan completes.

## Recording

The listen/record workflow creates WAV files in `sessions/`.

Recording features include:

- countdown popup
- in-popup Stop Recording button
- graceful stop-file signaling to avoid corrupting WAV files
- Open Last WAV
- Delete Last WAV
- Open Sessions Folder

## Menus

Main actions are organized into menus:

```text
Scan
Listen / Record
Profiles
Live Spectrum
View
Config
```

The main screen focuses on inputs, spectrum/waterfall, and detected active channels.

## Current Known-Good Test

NOAA weather scan near Cripple Creek, CO:

```text
Range: 162400000 to 162550000
Step: 25000
Rate: 1000000
RF BW: 1000000
Threshold: -65
Scanner channel LP Hz: 12000
```

Expected active signal in testing:

```text
162.500 MHz
```

## Development Workflow

Preferred workflow:

```text
direct repo patch -> build -> test -> git commit
```

Avoid zip patch packages unless explicitly requested.

Recommended command pattern:

```bash
cd ~/sdrdev/pluto_windows_scanner

git status --short

# edit or patch files

cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd

git add <changed files>
git commit -m "Descriptive commit message"
git push
```

## Repository

Target repository:

```text
https://github.com/bakstaaj/Pluto_Windows_Scanner
```

## License

No license has been selected yet. Add a `LICENSE` file before publishing broader release packages or accepting external contributions.
