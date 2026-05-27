# Windows Pluto SDR Scanner GUI v2.0 — Phase 2C Tuning Workflow

Phase 2C adds an interactive tuning workflow on top of the Phase 2 live spectrum/waterfall view.

## What Changed

- Click the frequency spectrum trace to choose the nearest displayed FFT/bin frequency.
- Double-click an active-channel row to tune that detected frequency.
- New `Tuned Hz` field stores the current working frequency.
- `Use Active Row` copies the selected active table row into `Tuned Hz`.
- `Tune Single Scan` copies `Tuned Hz` into `Single Hz` and switches the scanner to Single Frequency mode.
- `Center Live` copies `Tuned Hz` into the live spectrum center-frequency field.
- `Listen Tuned` records/listens to the tuned frequency even if it is not in the active table.
- A green cursor line is drawn on the spectrum when the tuned frequency is inside the displayed span.
- Listen/record now uses the same safe sample-rate normalization as scan and live spectrum.

## Recommended Test

1. Start the GUI.
2. Start Live Spectrum on NOAA Weather with `Rate=1000000`, `FFT=512`, `Interval=500`.
3. Click a visible spectrum peak.
4. Confirm `Tuned Hz` updates and a green cursor appears.
5. Click `Center Live`; if live spectrum is already running, stop and restart live to retune the backend.
6. Click `Tune Single Scan`, then run a single-frequency scan.
7. Click `Listen Tuned` to record audio from the tuned frequency.

## Notes

The existing backend process `pluto_spectrum_stream.exe` does not currently retune while running. `Center Live` updates the GUI field; if live spectrum is already active, stop and restart live spectrum to retune the backend.

A future backend enhancement can add a persistent control channel so the GUI can retune live spectrum without restarting the process.
