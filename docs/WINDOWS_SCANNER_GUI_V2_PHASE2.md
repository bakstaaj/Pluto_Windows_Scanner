# Windows Pluto SDR Scanner GUI v2.0 — Phase 2 Live Spectrum

Phase 2 adds live spectrum and waterfall rendering to the Windows GUI while preserving the Phase 1 scanner workflow.

## What changed

The Scanner tab now has a live spectrum control row:

- Live Center Hz
- FFT size
- Averaging count
- Frame interval in milliseconds
- Gain mode
- Optional manual gain dB
- Use Selected
- Start Live Spectrum
- Stop Live

The GUI launches:

```text
bin/pluto_spectrum_stream.exe
```

with arguments similar to:

```text
--uri ip:192.168.2.1 --freq 162550000 --rate 1000000 --bw 1000000 --fft 512 --avg 2 --interval-ms 500 --frames 0 --gain-mode slow_attack
```

The backend emits `SPECTRUM,...` CSV-style rows. The GUI parses those rows, updates the spectrum line chart, and appends rows to the waterfall display.

## Recommended first test

Use a known active signal or NOAA weather:

```text
URI:            ip:192.168.2.1
Live Center Hz: 162550000
Rate:           1000000
RF BW:          1000000
FFT:            512
Avg:            2
Interval ms:    500
Gain Mode:      slow_attack
Gain dB:         blank
```

Click **Start Live Spectrum**. The status line should report live frames, center frequency, span, and bin count.

## Notes

- The live spectrum/waterfall uses the existing `pluto_spectrum_stream.exe` backend from the v1.6 Windows toolset.
- The active-channel table is still populated by scans, not by the live spectrum stream.
- The live stream uses the current squelch/threshold value to draw orange markers in the spectrum view.
- Use **Use Selected** to set the live center frequency from the selected active-channel row.
- Stop the live spectrum before closing the GUI during early testing.

## Next recommended Phase 3 work

Phase 3 should connect live spectrum peaks to the active-channel workflow:

1. Detect peaks from live spectrum frames.
2. Add live-detected peaks to the active-channel table.
3. Add click-to-tune from the spectrum chart.
4. Let a live-selected peak start audio monitoring directly.


## Phase 2A stability fix

This update throttles live spectrum rendering, keeps only one pending WPF render queued, disables dense active-bin marker drawing for live FFT frames, and draws the waterfall as a bitmap instead of thousands of Rectangle controls. Start with FFT 512 and Interval 500 ms. Increase only after Stop Live remains responsive.
