#!/usr/bin/env bash
# Device end-to-end test of the native ad players through the real Unity bridge.
#
#   NativeAds/unity-e2e/run.sh android   # emulator or device visible to adb
#   NativeAds/unity-e2e/run.sh ios       # iOS simulator $SIMULATOR (default "iPhone 16 Pro"), booted if needed
#
# Copies the harness into Assets/SoilAdsE2E, builds the test player with Unity in batch mode,
# removes the harness again, then installs, launches and drives the player (drive.py), which
# taps the native close buttons, saves screenshots and prints PASS/FAIL per scenario.
# Close the Unity Editor on this project first; batch mode cannot open a project twice.
set -euo pipefail

PLATFORM=${1:?usage: run.sh android|ios}
ROOT=$(cd "$(dirname "$0")/../.." && pwd)
HERE="$ROOT/NativeAds/unity-e2e"
UNITY=${UNITY:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}
OUT=${OUT:-$HERE/out/$PLATFORM}
mkdir -p "$OUT"

# The build switches platform and store; put the project's settings files back afterwards.
SETTINGS=("ProjectSettings/ProjectSettings.asset" "ProjectSettings/EditorBuildSettings.asset" "Assets/Resources/FA_Build_Settings.asset")
BACKUP=$(mktemp -d)
for f in "${SETTINGS[@]}"; do [ -f "$ROOT/$f" ] && mkdir -p "$BACKUP/$(dirname "$f")" && cp "$ROOT/$f" "$BACKUP/$f"; done
restore_settings() { for f in "${SETTINGS[@]}"; do [ -f "$BACKUP/$f" ] && cp "$BACKUP/$f" "$ROOT/$f"; done; }

remove_harness() { rm -rf "$ROOT/Assets/SoilAdsE2E" "$ROOT/Assets/SoilAdsE2E.meta"; }
trap 'remove_harness; restore_settings' EXIT
remove_harness
cp -R "$HERE/SoilAdsE2E" "$ROOT/Assets/SoilAdsE2E"

case "$PLATFORM" in
  android)
    "$UNITY" -batchmode -quit -projectPath "$ROOT" -buildTarget Android \
      -executeMethod SoilAdsE2EBuild.BuildAndroid -e2eOutput "$OUT/SoilAdsE2E.apk" -logFile "$OUT/build.log" \
      || { grep -E "error|SOILADS-E2E-BUILD" "$OUT/build.log" | tail -40; exit 1; }
    ;;
  ios)
    rm -rf "$OUT/xcode"
    "$UNITY" -batchmode -quit -projectPath "$ROOT" -buildTarget iOS \
      -executeMethod SoilAdsE2EBuild.BuildIosSimulator -e2eOutput "$OUT/xcode" -logFile "$OUT/build.log" \
      || { grep -E "error|SOILADS-E2E-BUILD" "$OUT/build.log" | tail -40; exit 1; }
    remove_harness
    xcodebuild -project "$OUT/xcode/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Debug \
      -sdk iphonesimulator -derivedDataPath "$OUT/derived" CODE_SIGNING_ALLOWED=NO build \
      > "$OUT/xcodebuild.log" 2>&1 || { grep -E "error:" "$OUT/xcodebuild.log" | head -40; exit 1; }
    ;;
  *) echo "unknown platform $PLATFORM"; exit 2 ;;
esac

remove_harness
python3 "$HERE/drive.py" "$PLATFORM" "$OUT"
