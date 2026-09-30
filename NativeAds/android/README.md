# Android native ad player tests

Test harness for `Assets/FlyingAcorn/Soil/Advertisement/Plugins/Android/SoilAds.androidlib`. It compiles
the shipped sources in place (see `build.gradle`); nothing in this folder goes into a game build.

Needs JDK 17 and the Android SDK (`local.properties` with `sdk.dir=...`, or `ANDROID_HOME`).

```sh
export JAVA_HOME=/Library/Java/JavaVirtualMachines/zulu-17.jdk/Contents/Home

# JVM unit tests: lock policy, parsing, event JSON, slot/show state machine
./gradlew --offline testDebugUnitTest

# Device tests on a running emulator/device (real Activity, MediaPlayer, banner, back, clicks)
./run-device-tests.sh                       # all
./run-device-tests.sh SoilAdsDeviceTest#muteToggles   # one
```

`run-device-tests.sh` installs the test APK and runs it with `adb shell am instrument`, because
`connectedDebugAndroidTest` needs extra artifacts from Google's Maven repository. Drop `--offline`
from either command when that repository is reachable.

Test media in `src/androidTest/assets` was made with ffmpeg:

```sh
ffmpeg -f lavfi -i testsrc=size=320x240:rate=15:duration=3 -f lavfi -i sine=frequency=440:duration=3 \
  -c:v libx264 -profile:v baseline -pix_fmt yuv420p -b:v 60k -c:a aac -b:a 32k -shortest video_3s.mp4
ffmpeg -f lavfi -i sine=frequency=440:duration=2 -c:a aac audio_only.mp4
ffmpeg -f lavfi -i testsrc=size=320x480:rate=1 -frames:v 1 image.png
ffmpeg -f lavfi -i color=c=orange:size=128x128 -frames:v 1 logo.png
ffmpeg -f lavfi -i color=c=navy:size=6000x4000 -frames:v 1 -q:v 10 large.jpg
```
