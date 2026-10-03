#!/usr/bin/env bash
# Builds the instrumented test APK and runs it on the connected device/emulator with adb.
# (Used instead of connectedAndroidTest, which needs extra artifacts from Google's Maven repository.)
set -euo pipefail
cd "$(dirname "$0")"
SDK="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
ADB="$SDK/platform-tools/adb"
AAPT2="$(ls -d "$SDK"/build-tools/*/aapt2 | sort -V | tail -1)"

./gradlew --offline -Pandroid.aapt2FromMavenOverride="$AAPT2" assembleDebugAndroidTest
# Keep system UI out of the way: the one-time "full screen" cling and ANR dialogs of a slow
# emulator would otherwise take the key focus from the ad (back-button tests).
"$ADB" shell settings put secure immersive_mode_confirmations confirmed
"$ADB" shell settings put global hide_error_dialogs 1
"$ADB" install -r -t build/outputs/apk/androidTest/debug/SoilAdsAndroidTests-debug-androidTest.apk
"$ADB" shell am instrument -w -r ${1:+-e class "com.flyingacorn.soil.ads.$1"} \
    com.flyingacorn.soil.ads.test/androidx.test.runner.AndroidJUnitRunner | tee build/device-tests.txt
grep -q "^OK (" build/device-tests.txt
