"""Drives Jellyfin Web's player in Safari and screenshots each subtitle track at each cue.

Usage, from the main checkout root, with the test servers running:

    python3 -I <branch>/research/rtl-fix-verification/drive_safari.py <run> [--server 12.2|13.0]

`<run>` is `control`, `fix`, or `try` for a trial, and only names the output folder. The script never installs or removes
Lazyfin. For each server it starts `safaridriver`, opens one fresh WebDriver session, which starts
with an empty cache and no sign-in, signs in to Jellyfin Web as the test admin through the login
form, and then, for each of the five tracks in TRACKS:

1. opens the movie's details page and picks the track in its Subtitles menu,
2. clicks Play, waits for playback, then pauses and seeks to each cue in CUES,
3. saves a screenshot of the player cropped to the video as
   `.temp/rtl-check/safari/<run>/<server>/<track>-<cue>.jpg`, 960 pixels wide, through macOS's `sips`,
4. saves what the page shows beside it as `<track>-<cue>.json`: the renderer Jellyfin Web picked,
   the subtitle text on screen with RLE and PDF counted, and the subtitle URLs Safari fetched.

The controls bar is hidden with a style rule before each screenshot, so it never covers the
subtitle. Burn subtitles stays at its default, Auto, so text subtitles are delivered, not burned in.

When a step times out, the window lands in `failure.png` in the same folder.

It needs Safari's remote automation, which the maintainer turns on. If `POST /session` times out,
quit every Safari process and try again. The script never changes
Safari or system settings. Only the standard library is used, plus `sips`.
"""

import argparse
import base64
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

SERVERS = {"12.2": 8122, "13.0": 8130}
USER = "lazyfin"
PASSWORD = "lazyfin"
DRIVER_PORT = 4444
OUT = Path(".temp/rtl-check/safari")
ELEMENT = "element-6066-11e4-a52e-4f735466cecf"

# slug, item id, subtitle stream index
TRACKS = (
    ("external-arabic-srt", "40aff9e604c4fe4c065886dd18890b5a", 0),
    ("embedded-arabic-srt", "6f5ac2d01781a16941d3669b8dc36c77", 2),
    ("uploaded-arabic-srt", "72822f2b3b4b37e7f7fbb4204be91069", 0),
    ("external-english-srt", "40aff9e604c4fe4c065886dd18890b5a", 1),
    ("embedded-arabic-ass", "6f5ac2d01781a16941d3669b8dc36c77", 3),
)

# slug, seconds into the 10-second video
CUES = (("cue1", 2.5), ("cue2", 7.0))

HIDE_CONTROLS = """
const style = document.getElementById('lazyfin-hide-osd') || document.createElement('style');
style.id = 'lazyfin-hide-osd';
style.textContent = '.videoOsdBottom, .skinHeader, .osdHeader, .upNextContainer'
    + ' { visibility: hidden !important; }';
document.head.appendChild(style);
"""

SHOW_CONTROLS = "document.getElementById('lazyfin-hide-osd')?.remove();"

# Returns what the player shows now: renderer, cue text, video rectangle, and subtitle requests.
PROBE = r"""
const video = document.querySelector('video.htmlvideoplayer');
const count = (s, c) => s.split(c).length - 1;
const custom = document.querySelector('.videoSubtitlesInner');
const assCanvas = document.querySelector('.libassjs-canvas-parent canvas, canvas.JASSUB, .jassub canvas');
const showing = video ? Array.from(video.textTracks).filter(t => t.mode === 'showing') : [];
const nativeText = showing.flatMap(t => Array.from(t.activeCues || []).map(c => c.text));
let renderer = 'none';
let text = '';
if (assCanvas) { renderer = 'ass-canvas'; }
else if (custom && custom.innerHTML.trim()) { renderer = 'custom-element'; text = custom.innerHTML; }
else if (nativeText.length) { renderer = 'native-cue'; text = nativeText.join('\n'); }
const rect = video ? video.getBoundingClientRect() : null;
return {
    renderer,
    text,
    rle: count(text, '‫'),
    pdf: count(text, '‬'),
    textTracks: showing.map(t => ({kind: t.kind, label: t.label, language: t.language})),
    currentTime: video ? video.currentTime : null,
    paused: video ? video.paused : null,
    videoSrc: video ? video.currentSrc : null,
    video: video ? {x: rect.x, y: rect.y, width: rect.width, height: rect.height,
                    videoWidth: video.videoWidth, videoHeight: video.videoHeight} : null,
    viewport: {width: window.innerWidth, height: window.innerHeight},
    subtitleRequests: performance.getEntriesByType('resource').map(e => e.name)
        .filter(n => /Subtitles|\.vtt|\.ass|\.ssa|subtitles\.m3u8/i.test(n))
        .map(n => n.replace(/([?&](api_key|ApiKey)=)[^&]+/gi, '$1<token>')),
};
"""


class WebDriver:
    """A tiny W3C WebDriver client for `safaridriver`."""

    def __init__(self, port):
        self.base = f"http://127.0.0.1:{port}"
        self.session = None

    def call(self, method, path, payload=None):
        url = self.base + (f"/session/{self.session}" if self.session else "") + path
        data = None if payload is None else json.dumps(payload).encode()
        request = urllib.request.Request(url, data=data, method=method,
                                         headers={"Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(request, timeout=120) as response:
                return json.load(response)["value"]
        except urllib.error.HTTPError as error:
            value = json.load(error).get("value", {})
            raise RuntimeError(f"{method} {path}: {value.get('error')}: {value.get('message')}") from None

    def start(self):
        value = self.call("POST", "/session", {"capabilities": {"alwaysMatch": {"browserName": "safari"}}})
        self.session = value["sessionId"]
        return value["capabilities"]

    def quit(self):
        if self.session:
            self.call("DELETE", "")
            self.session = None

    def run(self, script, *args):
        return self.call("POST", "/execute/sync", {"script": script, "args": list(args)})

    def find(self, css):
        return self.call("POST", "/element", {"using": "css selector", "value": css})[ELEMENT]

    def click(self, css):
        """Clicks the middle of the element with a real mouse event.

        WebDriver's element click misses when Safari's page zoom is not 100%, so the point is
        scaled by the zoom, the window's width over the page's.
        """
        x, y = self.run(
            "const r = document.querySelector(arguments[0]).getBoundingClientRect();"
            "const zoom = window.outerWidth / window.innerWidth;"
            "return [(r.x + r.width / 2) * zoom, (r.y + r.height / 2) * zoom];", css)
        self.call("POST", "/actions", {"actions": [{
            "type": "pointer", "id": "mouse", "parameters": {"pointerType": "mouse"},
            "actions": [{"type": "pointerMove", "duration": 0, "x": int(x), "y": int(y), "origin": "viewport"},
                        {"type": "pointerDown", "button": 0}, {"type": "pointerUp", "button": 0}]}]})
        self.call("DELETE", "/actions")

    def type(self, css, text):
        self.call("POST", f"/element/{self.find(css)}/value", {"text": text})

    def wait(self, script, what, timeout=30, *args):
        deadline = time.time() + timeout
        while time.time() < deadline:
            try:
                value = self.run(script, *args)
                if value:
                    return value
            except RuntimeError:
                pass
            time.sleep(0.25)
        raise RuntimeError(f"timed out waiting for {what}")


def wait_for_driver(driver):
    for _ in range(50):
        try:
            if driver.call("GET", "/status").get("ready"):
                return
        except (urllib.error.URLError, ConnectionError, RuntimeError):
            pass
        time.sleep(0.2)
    sys.exit("safaridriver did not start.")


def sign_in(driver, base):
    driver.call("POST", "/url", {"url": f"{base}/web/#/login"})
    driver.wait("return !!document.querySelector('#txtManualName, .btnManual');", "the login page")
    if driver.run("return document.querySelector('.manualLoginForm').classList.contains('hide');"):
        driver.click(".btnManual")
    driver.type("#txtManualName", USER)
    driver.type("#txtManualPassword", PASSWORD)
    driver.click(".manualLoginForm .button-submit")
    driver.wait("return location.hash.startsWith('#/home');", "the home page")


def open_with_track(driver, base, item, index, tries=2):
    """Opens the movie's details page, picks the subtitle track, clicks Play, and pauses once it plays."""
    page = ".page:not(.hide)"
    for attempt in range(tries):
        driver.call("POST", "/url", {"url": f"{base}/web/#/details?id={item}"})
        driver.wait(f"return !!document.querySelector('{page} .selectSubtitles option[value=\"{index}\"]');",
                    "the details page's Subtitles menu")
        driver.run(
            f"const s = document.querySelector('{page} .selectSubtitles');"
            "s.value = String(arguments[0]); s.dispatchEvent(new Event('change', {bubbles: true}));",
            index)
        time.sleep(0.5)
        driver.click(f"{page} .mainDetailButtons .btnPlay:not(.hide)")
        try:
            driver.wait("const v = document.querySelector('video.htmlvideoplayer');"
                        "if (v && !v.paused && v.currentTime > 0.3) { v.pause(); return true; }",
                        "playback", timeout=60)
            return
        except RuntimeError:
            if attempt == tries - 1:
                raise


def shoot_cue(driver, folder, slug, seconds):
    driver.run(SHOW_CONTROLS)
    driver.run("const v = document.querySelector('video.htmlvideoplayer'); v.pause(); v.currentTime = arguments[0];",
               seconds)
    driver.wait("const v = document.querySelector('video.htmlvideoplayer');"
                "return v.paused && !v.seeking && v.readyState >= 2;", "the seek")
    time.sleep(2)
    driver.run(HIDE_CONTROLS)
    time.sleep(0.5)
    probe = driver.run(PROBE)
    png = folder / f"{slug}.png"
    png.write_bytes(base64.b64decode(driver.call("GET", "/screenshot")))
    crop(png, probe, folder / f"{slug}.jpg")
    png.unlink()
    (folder / f"{slug}.json").write_text(json.dumps(probe, ensure_ascii=False, indent=2) + "\n")
    print(f"{folder / slug}: {probe['renderer']}, RLE {probe['rle']}, PDF {probe['pdf']}")


def crop(png, probe, jpg):
    """Crops the viewport screenshot to the video picture, leaving out the letterbox, and saves a small JPEG."""
    size = subprocess.run(["sips", "-g", "pixelWidth", str(png)], capture_output=True, text=True, check=True)
    scale = int(size.stdout.split()[-1]) / probe["viewport"]["width"]
    v = probe["video"]
    aspect = v["videoWidth"] / v["videoHeight"]
    if v["width"] / v["height"] < aspect:
        pic_w, pic_h = v["width"], v["width"] / aspect
    else:
        pic_w, pic_h = v["height"] * aspect, v["height"]
    left = (v["x"] + (v["width"] - pic_w) / 2) * scale
    top = (v["y"] + (v["height"] - pic_h) / 2) * scale
    subprocess.run(["sips", "-c", str(int(pic_h * scale)), str(int(pic_w * scale)),
                    "--cropOffset", str(int(top)), str(int(left)), str(png), "--out", str(png)],
                   capture_output=True, check=True)
    subprocess.run(["sips", "--resampleWidth", "960", "-s", "format", "jpeg", "-s", "formatOptions", "70",
                    str(png), "--out", str(jpg)], capture_output=True, check=True)


def shoot(run, server, port):
    base = f"http://127.0.0.1:{port}"
    folder = OUT / run / server
    folder.mkdir(parents=True, exist_ok=True)
    driver_process = subprocess.Popen(["safaridriver", "--port", str(DRIVER_PORT)])
    driver = WebDriver(DRIVER_PORT)
    try:
        wait_for_driver(driver)
        capabilities = driver.start()
        version = (f"Safari {capabilities.get('browserVersion')} on macOS "
                   f"{capabilities.get('safari:platformVersion')} ({capabilities.get('safari:platformBuildVersion')})")
        (folder / "versions.txt").write_text(version + "\n")
        print(server, run, version)
        driver.call("POST", "/window/rect", {"x": 0, "y": 0, "width": 1280, "height": 800})
        try:
            sign_in(driver, base)
            for slug, item, index in TRACKS:
                open_with_track(driver, base, item, index)
                for cue, seconds in CUES:
                    shoot_cue(driver, folder, f"{slug}-{cue}", seconds)
        except RuntimeError:
            (folder / "failure.png").write_bytes(base64.b64decode(driver.call("GET", "/screenshot")))
            print("Saved", folder / "failure.png", "at", driver.run("return location.href;"))
            raise
    finally:
        try:
            driver.quit()
        finally:
            driver_process.terminate()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("run", choices=("control", "fix", "try"))
    parser.add_argument("--server", choices=tuple(SERVERS), action="append",
                        help="a server to check; defaults to both")
    args = parser.parse_args()
    for server in args.server or SERVERS:
        shoot(args.run, server, SERVERS[server])


if __name__ == "__main__":
    main()
