#!/usr/bin/env bash
set -euo pipefail

# Install Windows Pluto SDR Scanner GUI v2.0 into a separate development folder.
# Default target:
#   ~/sdrdev/pluto_windows_scanner
# Run from MSYS2 UCRT64 after extracting this package to:
#   C:\Users\jim\Downloads\PlutoWindowsScannerGuiV2_NewFolder
#
# Example:
#   /c/Users/jim/Downloads/PlutoWindowsScannerGuiV2_NewFolder/tools/install_windows_scanner_gui_v2_new_folder.sh
#
# Optional:
#   ./tools/install_windows_scanner_gui_v2_new_folder.sh --target ~/sdrdev/pluto_windows_scanner

TARGET="${HOME}/sdrdev/pluto_windows_scanner"
FORCE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --target)
      TARGET="${2:-}"
      shift 2
      ;;
    --force)
      FORCE=1
      shift
      ;;
    -h|--help)
      sed -n '1,35p' "$0"
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

if [[ -e "$TARGET" && $FORCE -eq 1 ]]; then
  echo "Removing existing target due to --force: $TARGET"
  rm -rf "$TARGET"
fi

mkdir -p "$TARGET" "$TARGET/bin" "$TARGET/configs" "$TARGET/docs" "$TARGET/launchers" "$TARGET/tools" "$TARGET/gui" "$TARGET/sessions" "$TARGET/releases"

# Marker file helps the GUI discover the project root even without native source/build folders.
touch "$TARGET/.pluto_windows_scanner_root"

copy_dir_replace() {
  local src="$1"
  local dst="$2"
  rm -rf "$dst"
  mkdir -p "$(dirname "$dst")"
  cp -a "$src" "$dst"
}

copy_file() {
  local src="$1"
  local dst="$2"
  mkdir -p "$(dirname "$dst")"
  cp -a "$src" "$dst"
}

echo "Installing GUI source into: $TARGET"
copy_dir_replace "$PKG_ROOT/gui/PlutoWindowsScannerGui_v2" "$TARGET/gui/PlutoWindowsScannerGui_v2"

echo "Installing v2 configs/docs/launchers/tools..."
cp -a "$PKG_ROOT/configs/." "$TARGET/configs/"
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

if [[ ! -f "$TARGET/configs/gui_v2_settings.json" ]]; then
  cp -a "$PKG_ROOT/configs/gui_v2_settings.json.sample" "$TARGET/configs/gui_v2_settings.json"
  echo "  Created configs/gui_v2_settings.json from sample."
else
  echo "  Leaving existing configs/gui_v2_settings.json in place."
fi

# Seed the new folder with the validated v1.6 Windows backend runtime when present.
if [[ -d "$PKG_ROOT/runtime_baseline/bin" ]]; then
  echo "Installing bundled v1.6 backend executables/DLLs into bin/..."
  cp -a "$PKG_ROOT/runtime_baseline/bin/." "$TARGET/bin/"
fi
if [[ -d "$PKG_ROOT/runtime_baseline/configs_v1_6" ]]; then
  echo "Installing bundled v1.6 backend config samples..."
  mkdir -p "$TARGET/configs/v1_6"
  cp -a "$PKG_ROOT/runtime_baseline/configs_v1_6/." "$TARGET/configs/v1_6/"
fi
if [[ -d "$PKG_ROOT/runtime_baseline/docs_v1_6" ]]; then
  echo "Installing bundled v1.6 backend docs under docs/v1_6/..."
  mkdir -p "$TARGET/docs/v1_6"
  cp -a "$PKG_ROOT/runtime_baseline/docs_v1_6/." "$TARGET/docs/v1_6/"
fi
if [[ -d "$PKG_ROOT/runtime_baseline/launchers_v1_6" ]]; then
  echo "Installing bundled v1.6 backend launchers under launchers/v1_6/..."
  mkdir -p "$TARGET/launchers/v1_6"
  cp -a "$PKG_ROOT/runtime_baseline/launchers_v1_6/." "$TARGET/launchers/v1_6/"
fi
if [[ -f "$PKG_ROOT/runtime_baseline/MANIFEST_v1_6.txt" ]]; then
  cp -a "$PKG_ROOT/runtime_baseline/MANIFEST_v1_6.txt" "$TARGET/MANIFEST_v1_6.txt"
fi

cat > "$TARGET/.gitignore" <<'GITIGNORE'
# Runtime/test output
/sessions/
*.csv
*.wav
*.html

# Build output
**/bin/
**/obj/
!bin/
!bin/*.exe
!bin/*.dll

# Generated release packages
/releases/*.zip
/releases/*/

# Local scratch/backup files
*.bak
*.bak_*
*.tmp
GITIGNORE

cat <<DONE

Installed Windows Pluto SDR Scanner GUI v2.0 into:
  $TARGET

Next commands from MSYS2 UCRT64:
  cd ~/sdrdev/pluto_windows_scanner
  cmd.exe /c launchers\\build_windows_scanner_gui_v2.cmd
  cmd.exe /c launchers\\start_windows_scanner_gui_v2.cmd

Notes:
  - This folder is independent of ~/sdrdev/pluto_native_test for GUI work.
  - The bundled bin/ folder contains the validated v1.6 Windows scanner/audio backends.
  - Keep ~/sdrdev/pluto_native_test as the native C backend reference/build repo.
DONE
