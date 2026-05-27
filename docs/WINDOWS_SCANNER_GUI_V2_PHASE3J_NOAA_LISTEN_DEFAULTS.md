# Phase 3J - NOAA/NFM Listen Defaults

Phase 3I established a usable NOAA/NFM audio baseline on 162.500 MHz.

Best field-test files:

- 06_norm_ch08k_lp3000_hp150_162500000.wav
- 10_invq_ch08k_lp3000_hp150_162500000.wav

Both used:

- Frequency: 162500000 Hz
- SDR sample rate: 1000000 Hz
- FM channel low-pass: 8000 Hz
- Audio low-pass: 3000 Hz
- Audio high-pass: 150 Hz
- Volume: 0.45
- RX channel: 1

Default GUI Listen behavior after this patch:

```text
--rx-channel 1
--iq-mode normal
--fm-channel-lowpass-hz 8000
--audio-lowpass-hz 3000
--audio-highpass-hz 150
--volume 0.45
```

The alternate diagnostic is:

```text
--iq-mode invert-q
```

Important implementation note: the GUI no longer passes the main tuner RF BW field to `pluto_audio_monitor.exe` for NFM Listen. The scan/live GUI RF bandwidth can be much wider than the demodulated voice channel and previously made the listen path harder to tune.

Run a direct verification with:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_best_audio_current.cmd
```

Then rebuild and launch the GUI:

```bash
cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd
```
