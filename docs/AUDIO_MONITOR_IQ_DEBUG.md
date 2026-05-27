# Audio Monitor IQ Debug Backend

Phase 3G adds diagnostic output to `pluto_audio_monitor.exe`:

```text
--iq-debug-csv <file>
--iq-debug-max <n>
```

The CSV captures raw I/Q samples and intermediate FM discriminator values before they are written to WAV audio. This is intended to determine whether the NOAA voice modulation is present in the raw I/Q path or whether the issue is in the demod/audio path.

Run:

```bash
cd ~/sdrdev/pluto_windows_scanner
cmd.exe //c launchers\\debug_noaa_162500_iq_debug.cmd
```

If the WAVs are still static, upload the generated files from:

```text
sessions/iq_debug_162500
```

Most useful files:

```text
*_iq_debug.csv
noaa_162500_iq_debug_log.txt
```
