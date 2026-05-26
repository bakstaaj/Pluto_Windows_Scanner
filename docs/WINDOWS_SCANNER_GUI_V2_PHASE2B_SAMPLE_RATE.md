# Windows Pluto SDR Scanner GUI v2.0 — Phase 2B Sample Rate Fix

Phase 2B changes the GUI default sample rate from `960000` Hz to `1000000` Hz.

Why:

- In v1.6 testing, `sampling_frequency=960000` produced `-22` on the Pluto+ in the dual-RX scanner workflow.
- The warning was non-fatal for the earlier scanner, but the GUI should not keep using a known-bad default.
- `1000000` Hz is now the default sample rate used by the GUI, standard band presets, sample config, and live spectrum startup values.

What the installer updates:

- `gui/PlutoWindowsScannerGui_v2/MainWindow.xaml`
- `gui/PlutoWindowsScannerGui_v2/MainWindow.xaml.cs`
- `configs/bands_v2_default.csv`
- `configs/gui_v2_settings.json.sample`
- Existing `configs/gui_v2_settings.json`, if present
- Existing `configs/bands.csv`, if it still contains `,960000,`

Backups created during install:

- `configs/gui_v2_settings.json.bak_phase2b`
- `configs/bands.csv.bak_phase2b`

Runtime behavior:

- If the GUI sees `960000` in the Rate field, it automatically changes it to `1000000` before launching scan or live spectrum commands.
- A run-log message is written so the correction is visible.

Recommended first retest:

```text
URI:            ip:192.168.2.1
Live Center Hz: 162550000
Rate:           1000000
RF BW:          1000000
FFT:            512
Avg:            2
Interval ms:    500
Gain Mode:      slow_attack
Gain dB:        blank
```
