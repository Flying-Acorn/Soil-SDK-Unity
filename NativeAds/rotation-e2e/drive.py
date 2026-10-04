#!/usr/bin/env python3
"""Installs, launches and drives the rotation test player built by run.sh, then judges it.

Reads the player's "SOILROT ..." log lines, closes every fullscreen ad with the back key once it
unlocks, and exits non-zero unless:
  - every round loaded, showed and closed (DONE shown=12), with no exception logged;
  - every shown ad belongs to the ad group the cache holds for its format (no mixed ads);
  - interstitials and rewarded ads both moved on to other ad groups;
  - a rewarded show was rewarded, and each show raised Shown then Closed;
  - the cache never held more than two ad groups' files per format (old files are deleted).

    python3 drive.py <out dir> <proxy mode file> [scenario]

Scenarios:
  rotation (default)  rewarded and interstitial ads alternate for 6 rounds;
  interstitial        8 interstitials back to back;
  outage              as rotation, but the ad server fails during rounds 3-4: ads must keep
                      showing from the cache, then rotate again once it is back.
No video may fall back to its image: the player logs "Video unusable" when it could not decode a
video it was given (e.g. while another video held the decoder).
"""
import os
import re
import subprocess
import sys
import threading
import time
from collections import defaultdict

APP = "com.flyingacorn.soilads.rotation"
TAG = "SOILROT "
TIMEOUT_SECONDS = 3000
MAX_FILES_PER_FORMAT = 6
ADB = os.environ.get("ADB", os.path.expanduser("~/Library/Android/sdk/platform-tools/adb"))
PROXY_PORT = os.environ.get("PROXY_PORT", "8001")


def sh(*args):
    return subprocess.run(args, capture_output=True, text=True)


def ad_on_screen():
    """Whether the player's fullscreen ad Activity has the focus (a light query, unlike a UI dump)."""
    focus = sh(ADB, "shell", "dumpsys window | grep mCurrentFocus").stdout
    return "SoilAdActivity" in focus


class Closer(threading.Thread):
    """Closes the fullscreen ad the log announced, the way a player would with the back key: the
    ad ignores back while its close button is locked and closes once it unlocks. Takes one
    screenshot of each ad. Works only while an ad is expected (between SHOWING and CLOSED), and
    never uses uiautomator: every UI dump goes through system_server, and on a small emulator
    dumps that fail pile up there until the whole system slows down."""

    def __init__(self, out):
        super().__init__(daemon=True)
        self.out = out
        self.pending_shot = None
        self.label = None
        self.active = False
        self.stopped = False

    def expect(self, label):
        self.pending_shot = label
        self.label = label
        self.active = True

    def run(self):
        while not self.stopped:
            if self.active and ad_on_screen():
                label, self.pending_shot = self.pending_shot, None
                if label:
                    time.sleep(1)  # let the ad draw before the screenshot
                    self.screenshot(f"{label}.png")
                # Overwritten before every back press: the last one is the screen the ad closed
                # from - for a rewarded ad its end card (image and call to action after the video).
                self.screenshot(f"{self.label}_end.png")
                sh(ADB, "shell", "input", "keyevent", "4")
            time.sleep(2)

    def screenshot(self, name):
        with open(os.path.join(self.out, name), "wb") as f:
            subprocess.run([ADB, "exec-out", "screencap", "-p"], stdout=f)


def device_lines():
    """The player's log messages. Another tool's adb (Unity 2022 bundles an older one) kills the
    adb server now and then; logcat is then reopened from the last timestamp seen, and the
    lines already read are skipped, so nothing is lost or read twice."""
    last_stamp, seen_at_stamp = None, set()
    while True:
        args = [ADB, "logcat", "-v", "threadtime", "-s", "Unity:*", "SoilAds:*"]
        if last_stamp:
            args[2:2] = ["-T", last_stamp]
            print(f"  logcat reopened from {last_stamp}", flush=True)
        proc = subprocess.Popen(args, stdout=subprocess.PIPE, text=True, errors="replace")
        for line in proc.stdout:
            m = re.match(r"(\d\d-\d\d \d\d:\d\d:\d\d\.\d{3})\s+\S+\s+\S+\s+\S\s+\S+\s*: ?(.*)", line)
            if not m:
                continue
            stamp, message = m.groups()
            if stamp != last_stamp:
                last_stamp, seen_at_stamp = stamp, set()
            if line in seen_at_stamp:
                continue
            seen_at_stamp.add(line)
            yield message + "\n"
        proc.wait()
        time.sleep(2)
        sh(ADB, "wait-for-device")
        # A new adb server forgets `adb reverse`: without it the player loses the ad server.
        sh(ADB, "reverse", f"tcp:{PROXY_PORT}", f"tcp:{PROXY_PORT}")


def retry(step, *args, attempts=6):
    for attempt in range(attempts):
        result = sh(*args)
        if result.returncode == 0:
            return result
        print(f"  {step} failed ({(result.stdout + result.stderr).strip()[:120]}), retrying", flush=True)
        sh(ADB, "wait-for-device")
        time.sleep(3)
    sys.exit(f"{step} failed: {result.stdout}{result.stderr}")


def install(apk):
    """Installs in short steps that each survive another tool killing adb: the APK goes over in
    8 MB chunks (each pushed again if cut), then the device joins and installs it itself."""
    remote = "/data/local/tmp/soilrot"
    retry("clean", ADB, "shell", f"rm -rf {remote} && mkdir -p {remote}")
    with open(apk, "rb") as f:
        index = 0
        while True:
            chunk = f.read(8 * 1024 * 1024)
            if not chunk:
                break
            local = os.path.join(os.path.dirname(apk), f".chunk{index:03d}")
            with open(local, "wb") as c:
                c.write(chunk)
            retry(f"push chunk {index}", ADB, "push", local, f"{remote}/part{index:03d}")
            os.remove(local)
            index += 1
    result = retry("install", ADB, "shell",
                   f"cat {remote}/part* > {remote}.apk && pm install -r -g {remote}.apk; rm -rf {remote} {remote}.apk")
    if "Success" not in result.stdout:
        sys.exit(f"install failed: {result.stdout}{result.stderr}")


def main():
    out, mode_file = sys.argv[1], sys.argv[2]
    scenario = sys.argv[3] if len(sys.argv) > 3 else "rotation"

    def set_mode(value):
        with open(mode_file, "w") as f:
            f.write(value)
        print(f"  ad server: {value}", flush=True)

    set_mode("pass")
    install(os.path.join(out, "SoilRotationE2E.apk"))
    sh(ADB, "shell", "am", "force-stop", APP)
    sh(ADB, "shell", "pm", "clear", APP)  # a first launch: no player, no cache, no history
    sh(ADB, "logcat", "-c")
    lines = device_lines()
    activity = sh(ADB, "shell", "cmd", "package", "resolve-activity", "--brief", APP).stdout.strip().splitlines()[-1]
    sh(ADB, "shell", "am", "start", "-n", activity, "--es", "scenario", scenario)
    closer = Closer(out)
    closer.start()

    shows = defaultdict(list)  # format -> [(round, group, name, match, media)]
    media = {}  # format -> what the player last prepared: video, image or text
    unusable_videos = []
    closes = defaultdict(list)  # format -> [events]
    files = defaultdict(list)
    failures, exceptions = [], []
    done = None
    started = time.time()

    with open(os.path.join(out, "device.log"), "w") as log:
        for line in lines:
            log.write(line)
            log.flush()
            if time.time() - started > TIMEOUT_SECONDS:
                failures.append("timeout")
                break
            if "Video unusable" in line:
                unusable_videos.append(line.strip()[-70:])
            loaded = re.search(r"Player: (interstitial|rewarded) Loaded (\w+)", line)
            if loaded:
                media[loaded.group(1)] = loaded.group(2)
            if TAG not in line:
                continue
            message = line.split(TAG, 1)[1].strip()
            print(message, flush=True)
            kind, _, rest = message.partition(" ")
            if kind == "SHOWING":
                fields = dict(re.findall(r"(\w+)=(\S*)", rest))
                fmt = rest.split()[0]
                rnd = int(fields["round"])
                shows[fmt].append((rnd, fields["group"], fields.get("name"), fields["match"] == "True", media.get(fmt),
                                   fields.get("why", "-")))
                closer.expect(f"{fmt}_{rnd}")
                if scenario == "outage" and fmt == "interstitial":
                    if rnd == 2:
                        set_mode("fail")
                    elif rnd == 4:
                        set_mode("pass")
            elif kind == "CLOSED":
                closer.active = False
                fmt = rest.split()[0]
                closes[fmt].append(re.search(r"events=(\S*)", rest).group(1).split(","))
            elif kind == "FILES":
                fmt, count = rest.split()
                files[fmt].append(int(count))
            elif kind == "EXCEPTION":
                exceptions.append(rest)
            elif kind == "FAIL":
                failures.append(rest)
                break
            elif kind == "DONE":
                done = int(re.search(r"shown=(\d+)", rest).group(1))
                break
    closer.stopped = True
    set_mode("pass")

    verdicts = []

    def check(name, ok, detail=""):
        verdicts.append(ok)
        print(f"{'PASS' if ok else 'FAIL'} {name} {detail}", flush=True)

    check("no failure", not failures, "; ".join(failures))
    expected = 8 if scenario == "interstitial" else 12
    check("all rounds shown", done == expected, f"shown={done}")
    check("no exception", not exceptions, "; ".join(exceptions[:3]))
    check("no video fell back to its image", not unusable_videos, "; ".join(unusable_videos[:3]))
    formats = ("interstitial",) if scenario == "interstitial" else ("interstitial", "rewarded")
    for fmt in formats:
        seq = shows[fmt]
        names = [s[2] for s in seq]
        # Texts, call to action, link, image and logo all from the ad group the cache holds.
        check(f"{fmt} every part of each ad from its own ad group", all(s[3] for s in seq),
              str([(s[0], s[2], s[5]) for s in seq if not s[3]]))
        # The next ad group downloads after a show; a download slower than the gap before the next
        # show leaves one repeat, so at most a third of the shows may repeat the previous app.
        # During an outage (rounds 3-4, and round 5, the first show after it) repeats are expected.
        excused = {3, 4, 5} if scenario == "outage" else set()
        repeats = [i + 1 for i in range(1, len(names)) if names[i] == names[i - 1] and i + 1 not in excused]
        check(f"{fmt} rotated", len(repeats) <= len(names) // 3, f"{' -> '.join(names)} (repeats at rounds {repeats})")
        triples = [i + 1 for i in range(2, len(names))
                   if names[i] == names[i - 1] == names[i - 2] and i + 1 not in excused]
        check(f"{fmt} never the same app three times running", not triples, str(triples))
        check(f"{fmt} shown then closed", all("Shown" in e and e[-1] == "Closed" for e in closes[fmt]), str(closes[fmt]))
        check(f"{fmt} old files deleted", all(c <= MAX_FILES_PER_FORMAT for c in files[fmt]), str(files[fmt]))
    if "rewarded" in formats:
        check("rewarded rewarded", all("Rewarded" in e for e in closes["rewarded"]), str(closes["rewarded"]))
    if scenario == "outage":
        during = [s[2] for s in shows["interstitial"] if s[0] in (3, 4)]
        check("outage: ads kept showing from the cache", len(during) == 2 and all(during), " -> ".join(map(str, during)))

    passed = sum(verdicts)
    print(f"{passed}/{len(verdicts)} checks passed; logs and screenshots in {out}", flush=True)
    sys.exit(0 if passed == len(verdicts) else 1)


if __name__ == "__main__":
    main()
