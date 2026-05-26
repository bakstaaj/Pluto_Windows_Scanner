#!/usr/bin/env bash
set -euo pipefail

# Package the separate Windows Pluto SDR Scanner GUI v2.0 folder.
# Run from:
#   cd ~/sdrdev/pluto_windows_scanner
#   ./tools/package_windows_scanner_gui_v2.sh

RELEASE_NAME="pluto-windows-scanner-v2.0-gui"
RELEASE_ROOT="releases/${RELEASE_NAME}"
ZIP_PATH="releases/${RELEASE_NAME}.zip"
GUI_DIR="gui/PlutoWindowsScannerGui_v2"
GUI_PUBLISH="${GUI_DIR}/bin/Release/net8.0-windows/win-x64/publish"

if [[ ! -d configs || ! -d launchers || ! -d tools || ! -f .pluto_windows_scanner_root ]]; then
  echo "ERROR: Run this script from ~/sdrdev/pluto_windows_scanner." >&2
  exit 1
fi

if [[ ! -f "${GUI_DIR}/PlutoWindowsScannerGui.csproj" ]]; then
  echo "ERROR: GUI project not found at ${GUI_DIR}." >&2
  exit 1
fi

if [[ ! -f bin/pluto_dual_rx_power_scan.exe ]]; then
  echo "ERROR: bin/pluto_dual_rx_power_scan.exe not found." >&2
  echo "Run ./tools/sync_backend_from_v1_repo.sh first, or reinstall from the new-folder package." >&2
  exit 1
fi

echo "=== Building/publishing GUI ==="
cmd.exe /c launchers\\build_windows_scanner_gui_v2.cmd

mkdir -p releases
rm -rf "${RELEASE_ROOT}" "${ZIP_PATH}"
mkdir -p "${RELEASE_ROOT}/bin" "${RELEASE_ROOT}/configs" "${RELEASE_ROOT}/docs" "${RELEASE_ROOT}/launchers" "${RELEASE_ROOT}/gui/PlutoWindowsScannerGui_v2" "${RELEASE_ROOT}/sessions" "${RELEASE_ROOT}/tools"

echo "=== Copying backend executables/DLLs ==="
cp -a bin/*.exe "${RELEASE_ROOT}/bin/" 2>/dev/null || true
cp -a bin/*.dll "${RELEASE_ROOT}/bin/" 2>/dev/null || true

echo "=== Copying configs/docs/launchers/tools ==="
cp -a configs/. "${RELEASE_ROOT}/configs/"
cp -a docs/. "${RELEASE_ROOT}/docs/"
cp -a launchers/. "${RELEASE_ROOT}/launchers/"
cp -a tools/. "${RELEASE_ROOT}/tools/"

if [[ -d "${GUI_PUBLISH}" && -f "${GUI_PUBLISH}/PlutoWindowsScannerGui.exe" ]]; then
  echo "=== Copying published GUI ==="
  cp -a "${GUI_PUBLISH}/." "${RELEASE_ROOT}/gui/PlutoWindowsScannerGui_v2/"
else
  echo "WARN: Published GUI exe not found; copying source project instead."
  cp -a "${GUI_DIR}/." "${RELEASE_ROOT}/gui/PlutoWindowsScannerGui_v2/"
fi

touch "${RELEASE_ROOT}/.pluto_windows_scanner_root"

cat > "${RELEASE_ROOT}/README.txt" <<'README'
Pluto Windows Scanner v2.0 GUI
==============================

Run:
  launchers\start_windows_scanner_gui_v2.cmd

This package is the separate-folder Windows GUI scanner layout. It includes:
  bin\       validated v1.6 scanner/audio backend tools
  gui\       Windows GUI
  configs\   standard bands and GUI settings
  sessions\  scan/audio outputs

Default Pluto URI:
  ip:192.168.2.1
README

(
  cd releases
  zip -qr "${RELEASE_NAME}.zip" "${RELEASE_NAME}"
)

cat <<DONE

Created release:
  ${ZIP_PATH}

DONE
