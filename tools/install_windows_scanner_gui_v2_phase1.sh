#!/usr/bin/env bash
set -euo pipefail

# Install/update Windows Pluto SDR Scanner GUI v2.0 Phase 1 into the separate GUI folder.
# Default target:
#   ~/sdrdev/pluto_windows_scanner
#
# Run from MSYS2 UCRT64 after extracting this package to:
#   C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_Phase1
#
# Example:
#   /c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase1/tools/install_windows_scanner_gui_v2_phase1.sh

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
  echo "  Leaving existing configs/gui_v2_settings.json in place. The GUI will add new Phase 1 settings when saved."
fi
cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json.sample"

if [[ ! -f "$TARGET/bin/pluto_dual_rx_power_scan.exe" ]]; then
  echo "WARN: $TARGET/bin/pluto_dual_rx_power_scan.exe was not found."
  echo "      Copy/sync the v1.6 backend tools before scanning."
else
  echo "Backend tools found in bin/. Leaving them unchanged."
fi

cat <<DONE

Installed Windows Pluto SDR Scanner GUI v2.0 Phase 1 into:
  $TARGET

Next commands from MSYS2 UCRT64:
  cd ~/sdrdev/pluto_windows_scanner
  cmd.exe /c launchers\\build_windows_scanner_gui_v2.cmd
  cmd.exe /c launchers\\start_windows_scanner_gui_v2.cmd

Phase 1 adds repeat scanning, active-channel accumulation, hit counts, peak/last dBFS, and Open Last CSV.
DONE
