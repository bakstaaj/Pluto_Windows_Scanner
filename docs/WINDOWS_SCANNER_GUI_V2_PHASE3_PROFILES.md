# Windows Pluto SDR Scanner GUI v2.0 — Phase 3 Profiles

Phase 3 adds operator workflow features on top of the Phase 2C live spectrum and tuning workflow.

## New features

- Quick Preset drop-down:
  - NOAA Weather
  - 2m Amateur Activity
  - 70cm Amateur Activity
  - Airband AM
  - Colorado DTRS 800 Check
- Apply Preset button to load known-good scan/live/listen settings.
- Profile name field.
- Save Profile button.
- Load Profile button.
- Open Profiles button.
- Seed profile JSON files under `configs/profiles`.

## Why this matters

The GUI can now remember complete scanner setups instead of requiring the user to manually re-enter scan ranges, live spectrum center frequencies, squelch values, modulation mode, listen duration, FFT settings, and RX settings.

## Install

From MSYS2 UCRT64, after extracting to `C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_Phase3_Profiles`:

```bash
/c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase3_Profiles/tools/install_windows_scanner_gui_v2_phase3_profiles.sh

cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd
```

## Test flow

1. Open the GUI.
2. Choose `NOAA Weather` in Quick Preset.
3. Click `Apply Preset`.
4. Confirm the scan range is `162400000` to `162550000`, step `25000`.
5. Confirm rate is `1000000`, not `960000`.
6. Click `Start Live Spectrum`.
7. Stop live spectrum.
8. Run a scan.
9. Type a new profile name such as `my-noaa-test`.
10. Click `Save Profile`.
11. Click `Open Profiles` and confirm the JSON file exists.
12. Change a few fields.
13. Click `Load Profile` and select the saved profile.
14. Confirm the fields are restored.

## Notes

Phase 3 intentionally keeps the existing v1.6 backend tools in `bin/`. The installer updates GUI source, docs, launchers, and profile/config templates but does not overwrite the backend EXEs or DLLs.

The GUI continues to normalize `960000` sample-rate values to `1000000` because prior Pluto+ testing showed `960000` can fail with `ret=-22` on this setup.
