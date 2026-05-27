# Audio Monitor RX Channel Debug — Phase 3E

This patch updates `pluto_audio_monitor.exe` to support selecting which Pluto+ RX path is used for demodulated audio.

Why this matters:

- The v2 GUI live display/scanners can show a strong signal when either RX path sees it.
- The old audio monitor always used RX1 only: `cf-ad9361-lpc voltage0/voltage1`.
- On the Pluto+ AD9361/2R2T setup, RX2 is exposed as `cf-ad9361-lpc voltage2/voltage3`.
- If the antenna or stronger signal is on RX2, the waterfall can show a strong line while Listen records static from RX1.

New options:

```text
--rx-channel <1|2>      Choose RX1 or RX2 for audio demodulation
--rx <1|2>              Short alias
--iq-mode <mode>        normal, invert-i, invert-q, swap, swap-invert-i, swap-invert-q
```

Recommended NOAA test:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_audio_rx_path.cmd
```

Listen first to:

```text
sessions/audio_rx_path_162500/01_rx1_center_normal_162500000.wav
sessions/audio_rx_path_162500/02_rx2_center_normal_162500000.wav
```

Interpretation:

- If RX2 is clear and RX1 is static, add an RX channel selector to the GUI Listen controls and default it to RX2 for the user's setup.
- If both RX1 and RX2 are static, continue debugging the demodulator or IQ path.
- If an I/Q mode variant is clearer, preserve that setting as an advanced audio option.

This patch also changes legacy 960000 Hz audio defaults to 1000000 Hz because 960000 produced `-22` sample-rate errors on this Pluto+ setup.
