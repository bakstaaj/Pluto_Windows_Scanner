# Pluto Activity Monitor RX Options

This is the first Windows-side activity-monitor RX integration package.

It intentionally adds a new target first:

```text
pluto_activity_monitor_rx.exe
```

It does **not** overwrite the existing:

```text
native/src/pluto_activity_monitor.c
pluto_activity_monitor.exe
```

This keeps the known working activity monitor safe while we validate the dual-RX behavior as an activity-monitor style tool.

## Added options

```text
--rx-mode auto|single|dual
--rx-combine max|average|separate
```

Recommended defaults:

```text
--rx-mode auto
--rx-combine max
```

Meaning:

```text
auto     Use dual RX when voltage0/1/2/3 are available; otherwise use RX1 only.
single   Force RX1 only.
dual     Require RX1 and RX2; fail if RX2 is missing.
max      Mark active if either receiver hears the strongest signal.
average  Average RX1 and RX2 power.
separate Log RX1 and RX2 separately while using max for active decision.
```

## Install

Extract to:

```text
C:\Users\jim\Downloads\PlutoActivityMonitorRxOptions
```

Then in MSYS2 UCRT64:

```bash
cd ~/sdrdev/pluto_native_test
/c/Users/jim/Downloads/PlutoActivityMonitorRxOptions/tools/install_activity_monitor_rx_options.sh
./tools/build_native_ucrt64.sh
```

## Test

```bash
./tools/run_activity_monitor_rx_test_msys2.sh
```

Manual equivalent:

```bash
./build/native/pluto_activity_monitor_rx.exe \
  --uri ip:192.168.2.1 \
  --freq-file configs/activity_rx_test_freqs.csv \
  --rx-mode auto \
  --rx-combine max \
  --threshold-dbfs -55 \
  --csv activity_monitor_rx_host.csv \
  --verbose
```

Expected good signs:

```text
RX1 channels: voltage0/voltage1 OK
RX2 channels: voltage2/voltage3 OK
Effective RX mode: dual
Dual available: yes
```

## Commit after testing

Do not commit generated CSV files.

Suggested commit:

```bash
git add \
  native/src/pluto_activity_monitor_rx.c \
  native/CMakeLists.txt \
  configs/activity_rx_test_freqs.csv \
  launchers/run_activity_monitor_rx.cmd \
  tools/run_activity_monitor_rx_test_msys2.sh \
  docs/ACTIVITY_MONITOR_RX_OPTIONS.md

git commit -m "Add Windows activity monitor RX options test target"
git push
```

## Next integration step

After this target validates, fold the same RX handling into the original `native/src/pluto_activity_monitor.c` so the production `pluto_activity_monitor.exe` supports the same options.
