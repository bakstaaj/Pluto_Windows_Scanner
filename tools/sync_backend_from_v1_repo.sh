#!/usr/bin/env bash
set -euo pipefail

# Refresh this GUI folder's bin/ backend from the v1.x native repo or release folder.
# Run from:
#   cd ~/sdrdev/pluto_windows_scanner
#
# Defaults look for:
#   ~/sdrdev/pluto_native_test/build/native
#   ~/sdrdev/pluto_native_test/releases/pluto-plus-sdr-toolkit-v1.6-dual-rx-windows/bin
#
# Optional:
#   ./tools/sync_backend_from_v1_repo.sh --source /path/to/bin-or-release-root

SOURCE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --source)
      SOURCE="${2:-}"
      shift 2
      ;;
    -h|--help)
      sed -n '1,26p' "$0"
      exit 0
      ;;
    *)
      echo "ERROR: Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

if [[ ! -d configs || ! -d launchers || ! -d gui ]]; then
  echo "ERROR: Run this from the pluto_windows_scanner root." >&2
  echo "  cd ~/sdrdev/pluto_windows_scanner" >&2
  exit 1
fi

candidates=()
if [[ -n "$SOURCE" ]]; then
  candidates+=("$SOURCE")
else
  candidates+=("${HOME}/sdrdev/pluto_native_test/build/native")
  candidates+=("${HOME}/sdrdev/pluto_native_test/releases/pluto-plus-sdr-toolkit-v1.6-dual-rx-windows/bin")
  candidates+=("${HOME}/sdrdev/pluto_native_test/releases/pluto-plus-sdr-toolkit-v1.6-dual-rx-windows")
  candidates+=("/c/Users/jim/Downloads/pluto-v1.6-test/pluto-plus-sdr-toolkit-v1.6-dual-rx-windows/bin")
  candidates+=("/c/Users/jim/Downloads/pluto-v1.6-test/pluto-plus-sdr-toolkit-v1.6-dual-rx-windows")
fi

SRC=""
for c in "${candidates[@]}"; do
  [[ -d "$c" ]] || continue
  if [[ -f "$c/pluto_dual_rx_power_scan.exe" ]]; then
    SRC="$c"
    break
  fi
  if [[ -f "$c/bin/pluto_dual_rx_power_scan.exe" ]]; then
    SRC="$c/bin"
    break
  fi
done

if [[ -z "$SRC" ]]; then
  echo "ERROR: Could not find a backend bin folder containing pluto_dual_rx_power_scan.exe." >&2
  echo "Tried:" >&2
  printf '  %s\n' "${candidates[@]}" >&2
  exit 1
fi

mkdir -p bin

echo "Syncing backend executables/DLLs from: $SRC"
cp -a "$SRC"/*.exe bin/ 2>/dev/null || true
cp -a "$SRC"/*.dll bin/ 2>/dev/null || true

if [[ ! -f bin/pluto_dual_rx_power_scan.exe ]]; then
  echo "ERROR: Sync finished but bin/pluto_dual_rx_power_scan.exe is still missing." >&2
  exit 1
fi

cat <<DONE

Backend sync complete.
Key files:
  bin/pluto_dual_rx_power_scan.exe
  bin/pluto_audio_monitor.exe

Run:
  cmd.exe /c launchers\\start_windows_scanner_gui_v2.cmd
DONE
