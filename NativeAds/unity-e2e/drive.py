#!/usr/bin/env python3
"""Installs, launches and drives the end-to-end test player built by run.sh.

Reads the player's "SOILADS-E2E ..." log lines, saves screenshots when asked, taps the native
close button of each fullscreen ad once it unlocks (Android; on iOS the player closes its own
ads), and exits non-zero unless every scenario passed.

    python3 drive.py android|ios <out dir>
"""
import glob
import json
import os
import re
import subprocess
import sys
import threading
import time

APP = "com.flyingacorn.soilads.e2e"
TAG = "SOILADS-E2E "
TIMEOUT_SECONDS = 600


def sh(*args, **kwargs):
    return subprocess.run(args, capture_output=True, text=True, **kwargs)


class AndroidRun:
    def __init__(self, out):
        self.out = out
        self.adb = os.environ.get("ADB", os.path.expanduser("~/Library/Android/sdk/platform-tools/adb"))
        self.proc = None

    def start(self):
        sh(self.adb, "wait-for-device")
        result = sh(self.adb, "install", "-r", "-g", os.path.join(self.out, "SoilAdsE2E.apk"))
        if result.returncode != 0:
            sys.exit(f"install failed: {result.stdout}{result.stderr}")
        sh(self.adb, "shell", "am", "force-stop", APP)
        sh(self.adb, "logcat", "-c")
        self.proc = subprocess.Popen([self.adb, "logcat", "-v", "raw", "-s", "Unity:I", "SoilAds:*"],
                                     stdout=subprocess.PIPE, text=True, errors="replace")
        sh(self.adb, "shell", "monkey", "-p", APP, "-c", "android.intent.category.LAUNCHER", "1")

    def lines(self):
        with open(os.path.join(self.out, "device.log"), "w") as log:
            for line in self.proc.stdout:
                log.write(line)
                log.flush()
                yield line

    def screenshot(self, name):
        with open(os.path.join(self.out, name + ".png"), "wb") as f:
            subprocess.run([self.adb, "exec-out", "screencap", "-p"], stdout=f)

    def close_button(self):
        """Returns (text, center) of the close button, or None while it is not on screen."""
        dump = sh(self.adb, "exec-out", "uiautomator", "dump", "/dev/tty").stdout
        for node in re.findall(r"<node [^>]*>", dump):
            if 'content-desc="soil_ad_close"' not in node:
                continue
            text = re.search(r' text="([^"]*)"', node).group(1)
            x1, y1, x2, y2 = map(int, re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', node).groups())
            return text, ((x1 + x2) // 2, (y1 + y2) // 2)
        return None

    def close_fullscreen_when_unlocked(self, format_name):
        def run():
            deadline = time.time() + 90
            captured = False
            while time.time() < deadline:
                found = self.close_button()
                if found:
                    text, (x, y) = found
                    if not captured:
                        self.screenshot(f"fullscreen_{format_name}")
                        captured = True
                    if not text.strip().isdigit():
                        print(f"  tapping close of {format_name} ({text!r}) at {x},{y}", flush=True)
                        sh(self.adb, "shell", "input", "tap", str(x), str(y))
                        return
                time.sleep(0.5)
            print(f"  close button of {format_name} never unlocked", flush=True)

        threading.Thread(target=run, daemon=True).start()

    def stop(self):
        if self.proc:
            self.proc.terminate()


class IosRun:
    def __init__(self, out):
        self.out = out
        self.proc = None
        self.device = None

    def find_device(self, name):
        listing = json.loads(sh("xcrun", "simctl", "list", "devices", "available", "-j").stdout)
        for devices in listing["devices"].values():
            for device in devices:
                if device["name"] == name:
                    return device["udid"]
        sys.exit(f"no simulator named {name}")

    def start(self):
        self.device = self.find_device(os.environ.get("SIMULATOR", "iPhone 16 Pro"))
        sh("xcrun", "simctl", "boot", self.device)
        sh("xcrun", "simctl", "bootstatus", self.device, "-b")
        apps = glob.glob(os.path.join(self.out, "derived/Build/Products/*-iphonesimulator/*.app"))
        if not apps:
            sys.exit("no simulator app was built")
        # Installing a debug IL2CPP app can take minutes; E2E_SKIP_INSTALL=1 reuses the last one.
        result = sh("xcrun", "simctl", "install", self.device, apps[0]) \
            if os.environ.get("E2E_SKIP_INSTALL") != "1" else subprocess.CompletedProcess([], 0, "", "")
        if result.returncode != 0:
            sys.exit(f"install failed: {result.stderr}")
        self.proc = subprocess.Popen(
            ["xcrun", "simctl", "launch", "--console-pty", "--terminate-running-process", self.device, APP],
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, errors="replace")

    def lines(self):
        with open(os.path.join(self.out, "device.log"), "w") as log:
            for line in self.proc.stdout:
                log.write(line)
                log.flush()
                yield line

    def screenshot(self, name):
        sh("xcrun", "simctl", "io", self.device, "screenshot", os.path.join(self.out, name + ".png"))

    def close_fullscreen_when_unlocked(self, format_name):
        # The player closes its own fullscreen ads on iOS; just capture one on screen.
        threading.Timer(4, lambda: self.screenshot(f"fullscreen_{format_name}")).start()

    def stop(self):
        if self.proc:
            self.proc.terminate()
        sh("xcrun", "simctl", "terminate", self.device, APP)


def main():
    platform, out = sys.argv[1], sys.argv[2]
    run = AndroidRun(out) if platform == "android" else IosRun(out)
    run.start()

    timer = threading.Timer(TIMEOUT_SECONDS, lambda: (print("TIMEOUT", flush=True), run.stop()))
    timer.start()
    summary = None
    try:
        for line in run.lines():
            index = line.find(TAG)
            if index < 0:
                continue
            message = line[index + len(TAG):].strip()
            print(message, flush=True)
            if message.startswith("EXPECT_TAP "):
                run.close_fullscreen_when_unlocked(message.split()[1])
            elif message.startswith("SCREENSHOT "):
                run.screenshot(message.split()[1])
            elif message.startswith("DONE"):
                summary = message
                break
    finally:
        timer.cancel()
        run.stop()

    if not summary:
        sys.exit("the player never finished")
    passed, failed = map(int, re.search(r"passed=(\d+) failed=(\d+)", summary).groups())
    print(f"{passed} passed, {failed} failed; screenshots and device.log in {out}")
    sys.exit(1 if failed or not passed else 0)


if __name__ == "__main__":
    main()
