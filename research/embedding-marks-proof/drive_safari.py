"""Serves the Safari page of the embedding marks proof and drives Safari through it.

Usage, from the repo root, after `scripts/dev.sh setup` has unpacked the test servers' ffmpeg:

    python3 -I research/embedding-marks-proof/drive_safari.py serve [--ffmpeg PATH]
    python3 -I research/embedding-marks-proof/drive_safari.py shoot [--ffmpeg PATH]

Both make the page's video with jellyfin-ffmpeg once, a blank 640x360 H.264 file in
`.temp/embedding-marks-proof/page/video.mp4`, and serve this folder on http://127.0.0.1:8765
with Python's built-in HTTP server, plus `/video.mp4` with the byte ranges Safari asks for.

- `serve` keeps serving until Ctrl-C, so any browser can open http://127.0.0.1:8765/page/.
- `shoot` starts `safaridriver`, opens the page in Safari through the W3C WebDriver API, and for
  each path and case calls `proof.show(path, slug)` and saves a screenshot of the page to
  `research/embedding-marks-proof/safari-page/<path>/<slug>.png`. Safari's version lands in
  `.temp/embedding-marks-proof/page/versions.txt`.

`shoot` needs Safari's remote automation, which the maintainer turns on once with
`safaridriver --enable`. The script never changes Safari or system settings itself.
Only the standard library is used.

Without remote automation, run `serve`, open http://127.0.0.1:8765/page/?step in Safari, and take
one window screenshot per step, pressing the right arrow key between them. LAZ-25 ran that way,
because the maintainer kept remote automation off, so `shoot` has never run against Safari.
"""

import argparse
import base64
import json
import re
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

PROOF = Path(__file__).resolve().parent
RAW = Path(".temp/embedding-marks-proof/page")
VIDEO = RAW / "video.mp4"
PAGE_PORT = 8765
DRIVER_PORT = 4444
PATHS = ("custom", "native")
ELEMENT = "element-6066-11e4-a52e-4f735466cecf"


def default_ffmpeg():
    found = sorted(Path(".temp/ffmpeg").glob("*/ffmpeg"))
    if not found:
        sys.exit("No jellyfin-ffmpeg under .temp/ffmpeg. Run scripts/dev.sh setup, or pass --ffmpeg.")
    return found[-1]


def make_video(ffmpeg):
    if VIDEO.exists():
        return
    RAW.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [str(ffmpeg), "-hide_banner", "-nostdin", "-loglevel", "error",
         "-f", "lavfi", "-i", "color=c=0x303030:s=640x360:r=25:d=22",
         "-c:v", "libx264", "-pix_fmt", "yuv420p", "-g", "25", "-movflags", "+faststart",
         "-y", str(VIDEO)],
        check=True)


class Handler(SimpleHTTPRequestHandler):
    def log_message(self, format, *args):
        pass

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_GET(self):
        if self.path.split("?")[0] == "/video.mp4":
            self.send_video(body=True)
        else:
            super().do_GET()

    def do_HEAD(self):
        if self.path.split("?")[0] == "/video.mp4":
            self.send_video(body=False)
        else:
            super().do_HEAD()

    def send_video(self, body):
        data = VIDEO.read_bytes()
        size = len(data)
        start, end = 0, size - 1
        match = re.fullmatch(r"bytes=(\d*)-(\d*)", self.headers.get("Range", ""))
        if match and (match[1] or match[2]):
            if match[1]:
                start = int(match[1])
                end = min(int(match[2]), size - 1) if match[2] else size - 1
            else:
                start = max(size - int(match[2]), 0)
            self.send_response(206)
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        else:
            self.send_response(200)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Length", str(end - start + 1))
        self.end_headers()
        if body:
            self.wfile.write(data[start:end + 1])


def start_server():
    server = ThreadingHTTPServer(("127.0.0.1", PAGE_PORT), partial(Handler, directory=str(PROOF)))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server


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


def wait_for_driver(driver):
    for _ in range(50):
        try:
            if driver.call("GET", "/status").get("ready"):
                return
        except (urllib.error.URLError, ConnectionError, RuntimeError):
            pass
        time.sleep(0.2)
    sys.exit("safaridriver did not start.")


def shoot(out):
    driver_process = subprocess.Popen(["safaridriver", "--port", str(DRIVER_PORT)])
    driver = WebDriver(DRIVER_PORT)
    try:
        wait_for_driver(driver)
        try:
            capabilities = driver.start()
        except RuntimeError as error:
            sys.exit(f"{error}\nIf remote automation is off, the maintainer runs `safaridriver --enable` once.")
        versions = (f"Safari {capabilities.get('browserVersion')}, "
                    f"platform {capabilities.get('platformName')}\n")
        (RAW / "versions.txt").write_text(versions)
        print(versions, end="")

        driver.call("POST", "/window/rect", {"x": 0, "y": 0, "width": 760, "height": 1000})
        driver.call("POST", "/timeouts", {"script": 60000, "pageLoad": 60000})
        driver.call("POST", "/url", {"url": f"http://127.0.0.1:{PAGE_PORT}/page/"})
        cases = driver.call("POST", "/execute/async", {
            "script": "const done = arguments[0]; proof.ready.then(p => done(p.cases), e => done('ERROR ' + e));",
            "args": []})
        if isinstance(cases, str):
            sys.exit(cases)
        print("user agent:", driver.call("POST", "/execute/sync", {"script": "return navigator.userAgent;", "args": []}))

        main = driver.call("POST", "/element", {"using": "css selector", "value": "main"})[ELEMENT]
        for path in PATHS:
            (out / path).mkdir(parents=True, exist_ok=True)
            for slug in cases:
                result = driver.call("POST", "/execute/async", {
                    "script": ("const [path, slug, done] = arguments;"
                               "proof.show(path, slug).then(done, e => done('ERROR ' + e));"),
                    "args": [path, slug]})
                if result.startswith("ERROR"):
                    sys.exit(f"{path} {slug}: {result}")
                png = base64.b64decode(driver.call("GET", f"/element/{main}/screenshot"))
                (out / path / f"{slug}.png").write_bytes(png)
                print(out / path / f"{slug}.png")
    finally:
        try:
            driver.quit()
        finally:
            driver_process.terminate()


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("mode", choices=("serve", "shoot"))
    parser.add_argument("--ffmpeg", type=Path, help="jellyfin-ffmpeg; defaults to the newest under .temp/ffmpeg")
    parser.add_argument("--out", type=Path, default=PROOF / "safari-page", help="where `shoot` saves screenshots")
    args = parser.parse_args()

    subprocess.run([sys.executable, "-I", str(PROOF / "make_sample.py")], check=True, stdout=subprocess.DEVNULL)
    make_video(args.ffmpeg or default_ffmpeg())
    server = start_server()
    try:
        if args.mode == "serve":
            print(f"Serving http://127.0.0.1:{PAGE_PORT}/page/ until Ctrl-C.")
            threading.Event().wait()
        else:
            shoot(args.out)
    except KeyboardInterrupt:
        pass
    finally:
        server.shutdown()


if __name__ == "__main__":
    main()
