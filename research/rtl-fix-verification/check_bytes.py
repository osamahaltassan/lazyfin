"""Fetches every subtitle form the test servers hand out, and judges the fix run against the control run.

Usage, from the root of the main checkout, with both test servers running:

    python3 -I <this branch>/research/rtl-fix-verification/check_bytes.py snapshot <name>
    python3 -I <this branch>/research/rtl-fix-verification/check_bytes.py fetch <control|fix>
    python3 -I <this branch>/research/rtl-fix-verification/check_bytes.py judge

- `snapshot <name>` records the SHA-256 of every subtitle file in the library, the MKV included, in
  `.temp/rtl-check/library-<name>.json`. Take `before-control` before the control run,
  `after-upload` after the upload, and `after-fix` after the fix run.
- `fetch <run>` signs in to each server as the test admin and saves, for every text subtitle track of
  every movie in the library, `Stream.js`, `Stream.vtt`, and `Stream.srt`, plus `Stream.ass` for an
  ASS track, and the first segment of the track's HLS subtitle playlist, taken from a master playlist
  with `SubtitleMethod=Hls`. Files land in `.temp/rtl-check/bytes/<run>/<server>/<movie>/`.
- `judge` prints one PASS or FAIL line per server, track, and form, then the library check, and exits
  1 if anything failed:
  - An Arabic SRT track passes when, in the fix run, each RTL line is wrapped in one RLE and one PDF
    after its leading markup and before its trailing markup, every other line has none, and the fix
    output with every RLE and PDF removed equals the control output. JSON is compared after parsing.
  - The English and ASS tracks pass when every form is byte for byte the same in both runs.
  - The Windows-1256 item is for burn-in only, so its lines say INFO and never fail the check.
  - The library passes when every subtitle file is byte for byte the same after the fix run as before
    the control run, and the uploaded one the same as right after the upload.

Only the standard library is used.
"""

import hashlib
import json
import re
import sys
import unicodedata
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

SERVERS = {"12.2": 8122, "13.0": 8130}
WORK = Path(".temp/rtl-check")
LIBRARY = WORK / "library"
BYTES = WORK / "bytes"
AUTH = 'MediaBrowser Client="lazyfin-rtl-check", Device="rtl-check", DeviceId="lazyfin-rtl-check", Version="0.0.0"'
RLE, PDF = "‫", "‬"
SUBTITLE_FILES = {".srt", ".ass", ".ssa", ".vtt", ".mkv"}
MARKUP = re.compile(r"(?:<[^>]*>|\{[^}]*\})*")
TIMING = re.compile(r"^\S+ --> \S+")


class Server:
    def __init__(self, port):
        self.base = f"http://127.0.0.1:{port}"
        self.token = None
        self.token = json.loads(self.call("POST", "/Users/AuthenticateByName",
                                          {"Username": "lazyfin", "Pw": "lazyfin"}))["AccessToken"]

    def call(self, method, path, body=None):
        url = path if path.startswith("http") else self.base + path
        headers = {"Authorization": AUTH + (f', Token="{self.token}"' if self.token else ""),
                   "Content-Type": "application/json", "Cache-Control": "no-cache"}
        data = None if body is None else json.dumps(body).encode()
        with urllib.request.urlopen(urllib.request.Request(url, data, headers, method=method), timeout=120) as r:
            return r.read()

    def json(self, path):
        return json.loads(self.call("GET", path))


def kind_of(name):
    return name.removeprefix("Lazyfin RTL ").removesuffix(" (2026)")


def snapshot(name):
    files = {str(p.relative_to(LIBRARY)): hashlib.sha256(p.read_bytes()).hexdigest()
             for p in sorted(LIBRARY.rglob("*")) if p.is_file() and p.suffix.lower() in SUBTITLE_FILES}
    out = WORK / f"library-{name}.json"
    out.write_text(json.dumps(files, indent=2, ensure_ascii=False) + "\n")
    for path, digest in files.items():
        print(digest[:16], path)
    print(f"Wrote {out}")


def first_hls_segment(server, item, source, index):
    session = uuid.uuid4().hex
    query = urllib.parse.urlencode({
        "MediaSourceId": source, "SubtitleStreamIndex": index, "SubtitleMethod": "Hls",
        "VideoCodec": "h264", "AudioCodec": "aac", "SegmentContainer": "ts",
        "PlaySessionId": session, "DeviceId": "lazyfin-rtl-check", "ApiKey": server.token,
    })
    master_url = f"{server.base}/Videos/{item}/master.m3u8?{query}"
    try:
        master = server.call("GET", master_url).decode()
        uri = next((m for m in re.findall(r'#EXT-X-MEDIA:TYPE=SUBTITLES.*?URI="([^"]+)"', master)
                    if f"/Subtitles/{index}/" in m), None)
        if uri is None:
            return None
        playlist_url = urllib.parse.urljoin(master_url, uri)
        playlist = server.call("GET", playlist_url).decode()
        segment = next(line for line in playlist.splitlines() if line and not line.startswith("#"))
        return server.call("GET", urllib.parse.urljoin(playlist_url, segment))
    finally:
        server.call("DELETE", f"/Videos/ActiveEncodings?deviceId=lazyfin-rtl-check&playSessionId={session}")


def fetch(run):
    for label, port in SERVERS.items():
        server = Server(port)
        movies = server.json("/Items?Recursive=true&IncludeItemTypes=Movie&SearchTerm=Lazyfin%20RTL")["Items"]
        for movie in movies:
            item = movie["Id"]
            kind = kind_of(movie["Name"])
            info = server.json(f"/Items/{item}/PlaybackInfo?UserId=")
            for source in info["MediaSources"]:
                for stream in source["MediaStreams"]:
                    if stream["Type"] != "Subtitle" or not stream.get("IsTextSubtitleStream"):
                        continue
                    index, codec = stream["Index"], stream["Codec"].lower()
                    folder = BYTES / run / label / kind
                    folder.mkdir(parents=True, exist_ok=True)
                    stem = f"{index}-{stream.get('Language') or 'und'}-{codec}"
                    forms = ["js", "vtt", "srt"] + (["ass"] if codec in ("ass", "ssa") else [])
                    for form in forms:
                        data = server.call("GET", f"/Videos/{item}/{source['Id']}/Subtitles/{index}/Stream.{form}")
                        (folder / f"{stem}.{form}").write_bytes(data)
                        print(run, label, kind, stem, form, len(data))
                    segment = first_hls_segment(server, item, source["Id"], index)
                    if segment is None:
                        print(run, label, kind, stem, "hls", "NOT IN MASTER PLAYLIST")
                    else:
                        (folder / f"{stem}.hls.vtt").write_bytes(segment)
                        print(run, label, kind, stem, "hls", len(segment))


def strip_marks(value):
    if isinstance(value, str):
        return value.replace(RLE, "").replace(PDF, "")
    if isinstance(value, list):
        return [strip_marks(v) for v in value]
    if isinstance(value, dict):
        return {k: strip_marks(v) for k, v in value.items()}
    return value


def is_rtl(line):
    for char in MARKUP.sub("", line):
        direction = unicodedata.bidirectional(char)
        if direction == "L":
            return False
        if direction in ("R", "AL"):
            return True
    return False


def cue_lines(form, data):
    """Every line of cue text in one fetched form, the way a renderer reads it."""
    if form == "js":
        return [line for event in json.loads(data)["TrackEvents"] for line in re.split(r"\r\n|\n|\r", event["Text"])]
    lines, in_cue = [], False
    for line in re.split(r"\r\n|\n|\r", data.decode("utf-8-sig")):
        if TIMING.match(line):
            in_cue = True
        elif not line.strip():
            in_cue = False
        elif in_cue:
            lines.append(line)
    return lines


def judge_line(line):
    """None when the line is as the fix should leave it, else what is wrong."""
    rle, pdf = line.count(RLE), line.count(PDF)
    if not is_rtl(strip_marks(line)):
        return None if rle == pdf == 0 else f"left-to-right line carries marks: {line!r}"
    if rle != 1 or pdf != 1:
        return f"RTL line has {rle} RLE and {pdf} PDF: {line!r}"
    lead, rest = line.split(RLE)
    inner, trail = rest.split(PDF)
    if MARKUP.fullmatch(lead) is None or MARKUP.fullmatch(trail) is None:
        return f"marks are not just inside the markup: {line!r}"
    if MARKUP.match(inner).group(0) or re.search(r"(?:<[^>]*>|\{[^}]*\})$", inner):
        return f"marks are outside some of the markup: {line!r}"
    return None


def same_without_marks(form, control, fix):
    if form == "js":
        return strip_marks(json.loads(fix)) == json.loads(control)
    return fix.decode("utf-8").replace(RLE, "").replace(PDF, "").encode("utf-8") == control


def judge_arabic(form, control, fix):
    problems = []
    if RLE in control.decode("utf-8", "replace") or PDF in control.decode("utf-8", "replace") or (
            form == "js" and RLE in json.dumps(json.loads(control), ensure_ascii=False)):
        problems.append("control run already carries marks")
    lines = cue_lines(form, fix)
    wrapped = sum(1 for line in lines if RLE in line)
    rtl = sum(1 for line in lines if is_rtl(strip_marks(line)))
    problems += [p for p in map(judge_line, lines) if p]
    if not same_without_marks(form, control, fix):
        problems.append("fix output without marks differs from control")
    detail = f"{wrapped} of {rtl} RTL lines wrapped, {len(lines) - rtl} other lines unmarked"
    return problems, detail


def judge():
    failed = False
    for label in SERVERS:
        control_root, fix_root = BYTES / "control" / label, BYTES / "fix" / label
        for control_file in sorted(control_root.rglob("*")):
            if not control_file.is_file():
                continue
            relative = control_file.relative_to(control_root)
            kind = relative.parts[0]
            stem, form = control_file.name.split(".", 1)
            form = "hls" if form == "hls.vtt" else form
            rule_form = "vtt" if form == "hls" else form
            fix_file = fix_root / relative
            name = f"{label:5} {kind:13} {stem:14} {form:4}"
            if not fix_file.exists():
                print(f"FAIL {name} missing in the fix run")
                failed = True
                continue
            control, fix = control_file.read_bytes(), fix_file.read_bytes()
            arabic_srt = stem.endswith("-ara-subrip") or stem.endswith("-ar-subrip")
            if arabic_srt:
                try:
                    problems, detail = judge_arabic(rule_form, control, fix)
                except (UnicodeDecodeError, ValueError, KeyError) as error:
                    problems, detail = [f"could not read: {error}"], ""
                verdict = "INFO" if kind == "Windows-1256" else ("FAIL" if problems else "PASS")
                failed |= verdict == "FAIL"
                print(f"{verdict} {name} {detail}" + "".join(f"\n       - {p}" for p in problems))
            else:
                same = control == fix
                failed |= not same
                print(f"{'PASS' if same else 'FAIL'} {name} "
                      + ("byte for byte the same as control" if same else "differs from control"))

    before = json.loads((WORK / "library-before-control.json").read_text())
    upload = json.loads((WORK / "library-after-upload.json").read_text())
    after = json.loads((WORK / "library-after-fix.json").read_text())
    expected = {**before, **{p: d for p, d in upload.items() if p not in before}}
    for path in sorted(set(expected) | set(after)):
        same = expected.get(path) == after.get(path)
        failed |= not same
        origin = "before the control run" if path in before else "right after the upload"
        print(f"{'PASS' if same else 'FAIL'} library {path}: "
              + (f"same as {origin}" if same else f"changed or missing since {origin}"))
    return failed


def main():
    if len(sys.argv) < 2 or sys.argv[1] not in ("snapshot", "fetch", "judge"):
        sys.exit(__doc__)
    if sys.argv[1] == "snapshot":
        snapshot(sys.argv[2])
    elif sys.argv[1] == "fetch":
        fetch(sys.argv[2])
    else:
        sys.exit(1 if judge() else 0)


if __name__ == "__main__":
    main()
