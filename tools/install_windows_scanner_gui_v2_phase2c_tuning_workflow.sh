#!/usr/bin/env bash
set -euo pipefail

# Install/update Windows Pluto SDR Scanner GUI v2.0 Phase 2C Tuning Workflow into
# the separate GUI folder.
# Default target:
#   ~/sdrdev/pluto_windows_scanner
#
# Run from MSYS2 UCRT64 after extracting this package to:
#   C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_Phase2C_TuningWorkflow
#
# Example:
#   /c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase2C_TuningWorkflow/tools/install_windows_scanner_gui_v2_phase2c_tuning_workflow.sh

TARGET="${HOME}/sdrdev/pluto_windows_scanner"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      TARGET="${2:-}"
      shift 2
      ;;
    -h|--help)
      sed -n '1,28p' "$0"
      exit 0
      ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PKG_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
TARGET="$(mkdir -p "$(dirname "$TARGET")" && cd "$(dirname "$TARGET")" && pwd)/$(basename "$TARGET")"

mkdir -p "$TARGET" "$TARGET/configs" "$TARGET/docs" "$TARGET/launchers" "$TARGET/tools" "$TARGET/gui" "$TARGET/sessions" "$TARGET/releases"
touch "$TARGET/.pluto_windows_scanner_root"

copy_dir_replace() {
  local src="$1"
  local dst="$2"
  rm -rf "$dst"
  mkdir -p "$(dirname "$dst")"
  cp -a "$src" "$dst"
}

echo "Updating GUI source in: $TARGET"
copy_dir_replace "$PKG_ROOT/gui/PlutoWindowsScannerGui_v2" "$TARGET/gui/PlutoWindowsScannerGui_v2"

echo "Updating docs, launchers, and tools..."
cp -a "$PKG_ROOT/docs/." "$TARGET/docs/"
cp -a "$PKG_ROOT/launchers/." "$TARGET/launchers/"
cp -a "$PKG_ROOT/tools/." "$TARGET/tools/"
chmod +x "$TARGET/tools"/*.sh 2>/dev/null || true

if [[ ! -f "$TARGET/configs/bands.csv" ]]; then
  cp -a "$PKG_ROOT/configs/bands_v2_default.csv" "$TARGET/configs/bands.csv"
  echo "  Created configs/bands.csv from v2 defaults."
else
  echo "  Leaving existing configs/bands.csv in place."
fi

cp -a "$PKG_ROOT/configs/bands_v2_default.csv" "$TARGET/configs/bands_v2_default.csv"

if [[ ! -f "$TARGET/configs/gui_v2_settings.json" ]]; then
  cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json"
  echo "  Created configs/gui_v2_settings.json from sample."
else
  echo "  Leaving existing configs/gui_v2_settings.json in place. The GUI will add new Phase 2 settings when saved."
fi
cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json.sample"

# Phase 2C safety defaults. Existing configs from the first Phase 2 package may
# still have 1024 FFT / 200 ms live rendering, which can overwhelm WPF. Keep a
# backup and switch to safer initial values; the user can raise these later.
if [[ -f "$TARGET/configs/gui_v2_settings.json" ]]; then
  cp -a "$TARGET/configs/gui_v2_settings.json" "$TARGET/configs/gui_v2_settings.json.bak_phase2c"
  sed -i -E 's/"LiveFftSize"[[:space:]]*:[[:space:]]*[0-9]+/"LiveFftSize": 512/' "$TARGET/configs/gui_v2_settings.json" || true
  sed -i -E 's/"LiveIntervalMs"[[:space:]]*:[[:space:]]*[0-9]+/"LiveIntervalMs": 500/' "$TARGET/configs/gui_v2_settings.json" || true
  sed -i -E 's/"RateHz"[[:space:]]*:[[:space:]]*960000/"RateHz": 1000000/' "$TARGET/configs/gui_v2_settings.json" || true
  if ! grep -q '"TuneFrequencyHz"' "$TARGET/configs/gui_v2_settings.json"; then
    sed -i -E 's/("LiveCenterHz"[[:space:]]*:[[:space:]]*[0-9]+,?)/
  "TuneFrequencyHz": 162550000,/' "$TARGET/configs/gui_v2_settings.json" || true
    echo "  Added TuneFrequencyHz to existing GUI config."
  fi
  echo "  Set Pluto-safe sample-rate default: RateHz=1000000."
fi

# Replace old 960000 band/config values from earlier v2 packages. Prior testing
# showed 960000 can fail on this Pluto+ setup with ret=-22, while 1000000 is
# the new GUI default. Preserve a backup for review.
if [[ -f "$TARGET/configs/bands.csv" ]]; then
  cp -a "$TARGET/configs/bands.csv" "$TARGET/configs/bands.csv.bak_phase2c"
  sed -i 's/,960000,/,1000000,/g' "$TARGET/configs/bands.csv" || true
  echo "  Updated configs/bands.csv sample-rate entries from 960000 to 1000000 where present."
fi

if [[ ! -f "$TARGET/bin/pluto_spectrum_stream.exe" ]]; then
  echo "WARN: $TARGET/bin/pluto_spectrum_stream.exe was not found."
  echo "      Copy/sync the v1.6 backend tools before starting live spectrum."
else
  echo "Live spectrum backend found: bin/pluto_spectrum_stream.exe"
fi

if [[ ! -f "$TARGET/bin/pluto_dual_rx_power_scan.exe" ]]; then
  echo "WARN: $TARGET/bin/pluto_dual_rx_power_scan.exe was not found."
  echo "      Copy/sync the v1.6 backend tools before scanning."
else
  echo "Scanner backend found: bin/pluto_dual_rx_power_scan.exe"
fi

cat <<DONE

Installed Windows Pluto SDR Scanner GUI v2.0 Phase 2C Tuning Workflow into:
  $TARGET

Next commands from MSYS2 UCRT64:
  cd ~/sdrdev/pluto_windows_scanner
  cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
  cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd

Phase 2C adds spectrum-click and active-row tuning workflow.
Click the spectrum or double-click an active row, then center live spectrum, tune single scan, or listen tuned.
DONE
