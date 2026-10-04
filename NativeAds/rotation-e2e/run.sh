#!/usr/bin/env bash
# Device end-to-end test of fullscreen ad rotation against a local Soil server.
#
#   NativeAds/rotation-e2e/run.sh            # build, then install and drive on the emulator
#   NativeAds/rotation-e2e/run.sh --no-build # drive the last build again
#   NativeAds/rotation-e2e/run.sh --build-only # build without touching the emulator
#
# Needs: a Soil dev server with several interstitial/rewarded ad groups on SOIL_UPSTREAM
# (default http://127.0.0.1:8000, e.g. `python manage.py runserver`), and an emulator on adb
# ($ANDROID_SERIAL, default emulator-5554). proxy.py fronts the server on $PROXY_PORT and adds
# the CF-IPCountry header ($COUNTRY, default IR) Cloudflare adds in production; the device
# reaches it through `adb reverse`, so the build talks to http://127.0.0.1:$PROXY_PORT/api.
#
# The build copies the harness into Assets/SoilRotationE2E, points the SDK's API URL at the
# proxy, builds in batch mode, then puts every file back. Close the Unity Editor on this
# project first. Output lands in NativeAds/rotation-e2e/out/.
set -euo pipefail

ROOT=$(cd "$(dirname "$0")/../.." && pwd)
HERE="$ROOT/NativeAds/rotation-e2e"
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}
ADB=${ADB:-$HOME/Library/Android/sdk/platform-tools/adb}
export ANDROID_SERIAL=${ANDROID_SERIAL:-emulator-5554}
# A private adb server: Unity 2022 bundles an older adb, and any tool that kills the shared
# server on 5037 (version mismatch, kill-server) would otherwise cut the run's installs and logs.
export ANDROID_ADB_SERVER_PORT=${ANDROID_ADB_SERVER_PORT:-5039}
export PROXY_PORT=${PROXY_PORT:-8001}
SOIL_UPSTREAM=${SOIL_UPSTREAM:-http://127.0.0.1:8000}
COUNTRY=${COUNTRY:-IR}
OUT=${OUT:-$HERE/out}
MODE_FILE="$OUT/proxy_mode"
mkdir -p "$OUT"

if [ "${1:-}" != "--no-build" ]; then
  CONSTANTS="Assets/FlyingAcorn/Soil/Core/Data/Constants.cs"
  SETTINGS=("ProjectSettings/ProjectSettings.asset" "ProjectSettings/EditorBuildSettings.asset" "Assets/Resources/FA_Build_Settings.asset"
    "ProjectSettings/AndroidResolverDependencies.xml" "Assets/Plugins/Android/AndroidManifest.xml" "Assets/Plugins/Android/mainTemplate.gradle" "$CONSTANTS")
  BACKUP=$(mktemp -d)
  for f in "${SETTINGS[@]}"; do [ -f "$ROOT/$f" ] && mkdir -p "$BACKUP/$(dirname "$f")" && cp "$ROOT/$f" "$BACKUP/$f"; done
  remove_harness() { rm -rf "$ROOT/Assets/SoilRotationE2E" "$ROOT/Assets/SoilRotationE2E.meta"; }
  restore() { remove_harness; for f in "${SETTINGS[@]}"; do [ -f "$BACKUP/$f" ] && cp "$BACKUP/$f" "$ROOT/$f"; done; }
  trap restore EXIT
  remove_harness
  cp -R "$HERE/SoilRotationE2E" "$ROOT/Assets/SoilRotationE2E"

  # Every request, regional ones included, goes to the proxy.
  sed -i '' -e "s#FallBackApiUrl = \"[^\"]*\"#FallBackApiUrl = \"http://127.0.0.1:$PROXY_PORT/api\"#" \
            -e "s#IsRegionalApiAvailable = true#IsRegionalApiAvailable = false#" "$ROOT/$CONSTANTS"
  grep -q "127.0.0.1:$PROXY_PORT" "$ROOT/$CONSTANTS" || { echo "could not point the SDK at the proxy"; exit 1; }

  "$UNITY" -batchmode -quit -projectPath "$ROOT" -buildTarget Android \
    -executeMethod SoilRotationE2EBuild.BuildAndroid -e2eOutput "$OUT/SoilRotationE2E.apk" -logFile "$OUT/build.log" \
    || { grep -E "error|SOILROT-BUILD" "$OUT/build.log" | tail -40; exit 1; }
  restore
  trap - EXIT
  [ "${1:-}" = "--build-only" ] && exit 0
fi

echo pass > "$MODE_FILE"
if ! curl -s -o /dev/null "http://127.0.0.1:$PROXY_PORT/api/"; then
  python3 "$HERE/proxy.py" "$PROXY_PORT" "$MODE_FILE" "$SOIL_UPSTREAM" "$COUNTRY" > "$OUT/proxy.log" 2>&1 &
  PROXY_PID=$!
  trap 'kill $PROXY_PID 2>/dev/null || true' EXIT
  sleep 1
fi
"$ADB" reverse "tcp:$PROXY_PORT" "tcp:$PROXY_PORT" > /dev/null
python3 "$HERE/drive.py" "$OUT" "$MODE_FILE" "${SCENARIO:-rotation}"
