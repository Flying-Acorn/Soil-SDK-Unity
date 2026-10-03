# SoilAds iOS player: test harness

Not shipped. Builds the plugin sources from
`Assets/FlyingAcorn/Soil/Advertisement/Plugins/iOS/SoilAds/` (referenced in place) into a tiny host
app together with `HostApp/UnityStubs.m`, which stands in for the Unity trampoline functions the
bridge links against (`UnitySendMessage`, `UnityGetGLViewController`, `UnityPause`,
`UnityIsPaused`, `UnityUpdateMuteState`). The hosted XCTest bundle then drives:

- `SoilAdsLockPolicyTests` – the lock formula from `NativeAds/PROTOCOL.md`, table by table.
- `SoilAdsCreativeTests` – creative / show options JSON parsing, format names, text direction.
- `SoilAdsManagerTests` – load / show / hide / destroy against a fake host and a real `UIWindow`,
  with real media in `Tests/Resources`.
- `SoilAdsBridgeTests` – the `SoilAds_*` C functions end to end through the stubs.

The host app target builds the plugin like Unity's generated Xcode project does (strict `-std=c11`,
ARC, modules) plus `-Wall -Wextra -Wpedantic` with warnings as errors, so GNU-only syntax such as
`typeof` fails here before it fails in a game build.

## Run

```sh
cd NativeAds/ios
xcodebuild test -project SoilAdsHarness.xcodeproj -scheme SoilAdsHarness \
  -destination 'platform=iOS Simulator,name=iPhone 16,OS=18.6'
```

Any iOS 13+ simulator works; pick one from `xcrun simctl list devices available` (on this machine
`iPhone 16` only exists with iOS 18.6, hence `OS=18.6`). Takes ~40 s once the simulator is booted.

## Maintenance

- Added or removed a source/test/resource file: `python3 gen_project.py` regenerates the project.
- Test media: `./make_test_media.sh` (needs `ffmpeg`) regenerates `Tests/Resources`.
