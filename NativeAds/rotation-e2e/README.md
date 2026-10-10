# Ad rotation end-to-end test

Runs the real SDK and native players on an Android emulator against a real Soil server, and
checks that interstitial and rewarded ads rotate between ad groups the way a game sees them.
Unlike `unity-e2e` (local media, no server), this goes through ad group selection, staging,
switching and file cleanup.

```bash
# 1. A Soil dev server with several interstitial and rewarded ad groups for the demo app
#    (c425a46d-…), e.g. from the Soil repo:
python manage.py runserver 0.0.0.0:8000

# 2. An emulator on adb (default serial emulator-5554), then:
NativeAds/rotation-e2e/run.sh                                  # build + "rotation" scenario
SCENARIO=interstitial NativeAds/rotation-e2e/run.sh --no-build # 8 interstitials back to back
SCENARIO=outage       NativeAds/rotation-e2e/run.sh --no-build # ad server fails in rounds 3-4
```

Close the Unity Editor on this project first. `run.sh` copies `SoilRotationE2E/` into
`Assets/`, points the SDK's API URL at `http://127.0.0.1:8001/api`, builds in batch mode and puts
every file back. `proxy.py` serves that port in front of the dev server and adds the
`CF-IPCountry` header Cloudflare adds in production (`COUNTRY`, default `IR`, the country the
test campaigns are set up for); the emulator reaches it through `adb reverse`. Asset URLs are whatever the
server returns, so media may come from the production CDN.

`drive.py` launches the player with the scenario, closes each fullscreen ad with the back key once
it unlocks (the ad ignores back while locked; no uiautomator, whose UI dumps slow a small
emulator's system_server down), and exits non-zero unless every check passes:

- every round loads, shows and closes, with no exception logged;
- every visible part of each shown ad comes from the ad group the cache holds for its format:
  the title, description and call to action of the ad whose video (or image) is on screen, that
  group's image and logo, and its click link (no new video under an old ad's texts or link);
- both formats move on to other ad groups: at most a third of the shows repeat the previous app
  (a download slower than the ad on screen), never three in a row;
- no video fell back to its image (the player could not decode a video it was given, e.g.
  because it was prepared while another video played);
- a rewarded show is rewarded, and every show raises Shown then Closed;
- the cache never holds more than two ad groups' files per format;
- outage: ads keep showing from the cache while the ad server answers 503.

Logs (`device.log`, `build.log`, `proxy.log`) land in `out/`, with two screenshots of each ad:
`<format>_<round>.png` as it opens and `<format>_<round>_end.png` as it closes (for a rewarded ad,
the end card shown after the video). The run
uses its own adb server (`ANDROID_ADB_SERVER_PORT`, default 5039), because Unity 2022 bundles an
older adb that kills the shared server on 5037 whenever a build polls devices; the driver also
reopens logcat from the last line it read if its server restarts anyway.
