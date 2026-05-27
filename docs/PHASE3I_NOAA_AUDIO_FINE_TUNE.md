# Phase 3I - NOAA/NFM Audio Fine Tune

Phase 3H proved that the NOAA voice is present after adding complex FM channel filtering. The clearest early results were:

- RX1 normal, 12 kHz complex channel LP, 4.5 kHz audio LP, 300 Hz HP
- RX1 invert-q, 12 kHz complex channel LP, 4.5 kHz audio LP, 300 Hz HP

Phase 3I runs a focused sweep around those values:

- Complex FM channel low-pass: 8 kHz, 10 kHz, 12 kHz, 14 kHz
- Voice low-pass: 3.0 kHz, 3.5 kHz, 4.5 kHz
- Voice high-pass: 150 Hz, 200 Hz
- I/Q mode: normal and invert-q
- Small tuning offsets: -1 kHz and +1 kHz

Run with the GUI closed:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_audio_fine_tune.cmd
```

Listen to WAVs under:

```text
sessions/audio_fine_tune_162500
```

Pick the clearest file by number. That setting should become the default NOAA/NFM Listen profile in the GUI.
