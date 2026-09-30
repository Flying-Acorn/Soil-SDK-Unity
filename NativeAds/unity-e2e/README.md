# Device end-to-end test

Runs the real native players on an emulator/simulator through the real Unity bridge, with no
ad server: the harness hands local test media straight to the players.

```bash
NativeAds/unity-e2e/run.sh android   # needs a running emulator or a device on adb
NativeAds/unity-e2e/run.sh ios       # boots the "iPhone 16 Pro" simulator ($SIMULATOR) if needed
```

Close the Unity Editor on this project first. The script copies `SoilAdsE2E/` into
`Assets/SoilAdsE2E`, builds the player in batch mode, removes it again, then runs `drive.py`.
The driver taps each fullscreen ad's close button once it unlocks (Android), saves screenshots,
and exits non-zero unless every scenario passes. Output lands in `NativeAds/unity-e2e/out/<platform>/`.

Scenarios: image and right-to-left text banners (show, move, hide), broken media, video falling
back to its image, show before load, interstitial image, rewarded video (event order
Shown → Rewarded → Closed), and a second rewarded after the first closed.
