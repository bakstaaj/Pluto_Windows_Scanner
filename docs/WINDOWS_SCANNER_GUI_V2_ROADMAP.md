# Windows Pluto SDR Scanner GUI v2.0 Roadmap

## Folder Strategy

Use a new GUI-focused folder for v2.0 work:

```text
~/sdrdev/pluto_windows_scanner
```

Use the existing v1.x repo as the native/backend reference and build workspace:

```text
~/sdrdev/pluto_native_test
```

This keeps GUI iteration clean and avoids mixing v2 UI work with v1.x scanner packaging history.

## Phase 1 — GUI Shell Around Validated Backends

Status: starter package created.

Goals:

- WPF GUI launches from `launchers/start_windows_scanner_gui_v2.cmd`.
- GUI reads standard bands from `configs/bands.csv`.
- GUI calls `bin/pluto_dual_rx_power_scan.exe` for detection.
- GUI calls `bin/pluto_audio_monitor.exe` for listen/record.
- GUI writes scan/audio outputs under `sessions/`.
- GUI exports active channels as CHIRP Generic CSV.

## Phase 2 — Better Band/Profile Management

Planned:

- Expand `bands.csv` into a richer profile model.
- Add selectable presets for NOAA, 2m, 70cm, airband, FM broadcast, Colorado DTRS control-frequency checks, HF ranges, and user-defined ranges.
- Add per-band defaults for mode, tuning step, bandwidth, sample rate, threshold, dwell time, and CHIRP export mode.

## Phase 3 — Continuous Live Spectrum/Waterfall Backend

Planned:

- Add a GUI-friendly streaming backend, likely one of:
  - `pluto_gui_spectrum_stream.exe`
  - an updated `pluto_spectrum_stream.exe` mode
- Stream FFT frames to the GUI through stdout, a local TCP socket, named pipe, or CSV-frame file.
- Add live peak detection and click-to-tune from the waterfall.

## Phase 4 — Integrated Active Channel Monitor

Planned:

- Continue scanning while maintaining an active-channel table.
- Add hit counts, first seen, last seen, strongest level, and user notes.
- Allow quick lock/listen on any active row.
- Add scan pause/resume and hold-on-active behavior.

## Phase 5 — Packaged v2 Release

Planned:

- Package `pluto_windows_scanner` as a clean Windows release.
- Include GUI, backend executables/DLLs, configs, launchers, docs, and empty `sessions/` folder.
- Keep generated release ZIPs out of Git.
