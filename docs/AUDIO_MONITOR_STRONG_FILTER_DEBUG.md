# Phase 3F Audio Monitor Strong Filter Debug

This update patches `pluto_audio_monitor.c` to test whether the NOAA/NFM static problem is caused by the old audio path's weak single-pole low-pass filter and unscaled FM discriminator.

Changes:

- Keeps Phase 3E `--rx-channel` and `--iq-mode` options.
- Adds `--audio-highpass-hz`.
- Adds `--fm-deviation-hz`.
- Changes the FM discriminator to normalize by expected deviation.
- Replaces the old single-pole low-pass with cascaded biquad high-pass/low-pass filtering before resampling to WAV.
- Keeps the known-good `1000000` sample-rate default.

Run:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_audio_strong_filter.cmd
```

Listen to the WAV files under:

```text
sessions/audio_strong_filter_162500
```

If one file is clear, wire those settings into the GUI Listen path. If all are still static, the next step is raw IQ capture plus offline demod inspection.
