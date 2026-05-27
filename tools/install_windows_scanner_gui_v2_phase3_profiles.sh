#!/usr/bin/env bash
set -euo pipefail

# Install/update Windows Pluto SDR Scanner GUI v2.0 Phase 3 Profiles into
# the separate GUI folder.
# Default target:
#   ~/sdrdev/pluto_windows_scanner
#
# Run from MSYS2 UCRT64 after extracting this package to:
#   C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_Phase3_Profiles
#
# Example:
#   /c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_Phase3_Profiles/tools/install_windows_scanner_gui_v2_phase3_profiles.sh

TARGET="${HOME}/sdrdev/pluto_windows_scanner"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      TARGET="${2:-}"
      shift 2
      ;;
    -h|--help)
      sed -n '1,24p' "$0"
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

mkdir -p "$TARGET" "$TARGET/configs" "$TARGET/configs/profiles" "$TARGET/docs" "$TARGET/launchers" "$TARGET/tools" "$TARGET/gui" "$TARGET/sessions" "$TARGET/releases"
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
  cp -a "$TARGET/configs/bands.csv" "$TARGET/configs/bands.csv.bak_phase3"
  sed -i 's/,960000,/,1000000,/g' "$TARGET/configs/bands.csv" || true
  echo "  Left existing configs/bands.csv in place and normalized 960000 -> 1000000 if present."
fi
cp -a "$PKG_ROOT/configs/bands_v2_default.csv" "$TARGET/configs/bands_v2_default.csv"

if [[ ! -f "$TARGET/configs/gui_v2_settings.json" ]]; then
  cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json"
  echo "  Created configs/gui_v2_settings.json from sample."
else
  cp -a "$TARGET/configs/gui_v2_settings.json" "$TARGET/configs/gui_v2_settings.json.bak_phase3"
  sed -i -E 's/"RateHz"[[:space:]]*:[[:space:]]*960000/"RateHz": 1000000/' "$TARGET/configs/gui_v2_settings.json" || true
  sed -i -E 's/"LiveFftSize"[[:space:]]*:[[:space:]]*[0-9]+/"LiveFftSize": 512/' "$TARGET/configs/gui_v2_settings.json" || true
  sed -i -E 's/"LiveIntervalMs"[[:space:]]*:[[:space:]]*[0-9]+/"LiveIntervalMs": 500/' "$TARGET/configs/gui_v2_settings.json" || true
  echo "  Preserved existing configs/gui_v2_settings.json with Phase 3 safety defaults."
fi
cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json.sample"

# Seed profiles without overwriting user-saved versions.
mkdir -p "$TARGET/configs/profiles"
for profile in "$PKG_ROOT"/configs/profiles/*.json; do
  base="$(basename "$profile")"
  if [[ ! -f "$TARGET/configs/profiles/$base" ]]; then
    cp -a "$profile" "$TARGET/configs/profiles/$base"
    echo "  Added profile: configs/profiles/$base"
  else
    echo "  Keeping existing profile: configs/profiles/$base"
  fi
done

if [[ ! -f "$TARGET/bin/pluto_spectrum_stream.exe" ]]; then
  echo "WARN: $TARGET/bin/pluto_spectrum_stream.exe was not found. Live spectrum needs the v1.6 backend in bin/."
else
  echo "Live spectrum backend found: bin/pluto_spectrum_stream.exe"
fi

if [[ ! -f "$TARGET/bin/pluto_dual_rx_power_scan.exe" ]]; then
  echo "WARN: $TARGET/bin/pluto_dual_rx_power_scan.exe was not found. Scanning needs the v1.6 backend in bin/."
else
  echo "Scanner backend found: bin/pluto_dual_rx_power_scan.exe"
fi

cat <<DONE

Installed Windows Pluto SDR Scanner GUI v2.0 Phase 3 Profiles into:
  $TARGET

Next commands from MSYS2 UCRT64:
  cd ~/sdrdev/pluto_windows_scanner
  cmd.exe //c launchers\\build_windows_scanner_gui_v2.cmd
  cmd.exe //c launchers\\start_windows_scanner_gui_v2.cmd

Phase 3 adds quick presets and save/load scan profiles under configs/profiles.
DONE
