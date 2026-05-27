# Windows Scanner GUI v2 Phase 3A — NOAA Audio Debug

This phase adds a diagnostic launcher only. It does not change the GUI source or backend binaries.

## Problem being isolated

A strong NOAA line is visible in the live waterfall at 162.500 MHz, but the GUI listen path produces static.

That means the RF/waterfall side is likely functioning. The likely fault is in one of these places:

- audio monitor `--freq` unit expectation: Hz vs MHz
- audio monitor preset path vs explicit-frequency path
- NFM mode selection
- squelch behavior
- `--bw` behavior in the GUI listen command
- sample-rate propagation

## Run

From MSYS2 UCRT64:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_audio.cmd
```

## Test files created

```text
sessions/1_noaa5_preset_squelch_off.wav
sessions/2_freq_hz_squelch_off.wav
sessions/3_freq_mhz_squelch_off.wav
sessions/4_freq_hz_with_bw_squelch_off.wav
sessions/5_freq_hz_squelch_db_minus80.wav
sessions/noaa_162500_audio_debug_log.txt
sessions/audio_debug_log.csv
```

## Interpretation

- If `1_noaa5_preset_squelch_off.wav` is clear but `2_freq_hz_squelch_off.wav` is static, the GUI should use NOAA preset mapping or the backend `--freq` parser needs correction.
- If `3_freq_mhz_squelch_off.wav` is clear but `2_freq_hz_squelch_off.wav` is static, `pluto_audio_monitor.exe --freq` expects MHz, not Hz.
- If `2_freq_hz_squelch_off.wav` is clear but `4_freq_hz_with_bw_squelch_off.wav` is static, the GUI should stop passing `--bw` to NFM audio or use a narrower mode-specific bandwidth.
- If only the squelch-off files are clear, the GUI needs a listen-path squelch-off option or a lower default listen squelch.
- If all files are static, the issue is likely inside the backend NFM audio demodulator or its gain/audio-filter defaults.
