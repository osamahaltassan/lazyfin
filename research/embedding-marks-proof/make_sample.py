"""Writes the embedding marks proof sample in two copies, plain and marked.

Usage, from the repo root:

    python3 -I research/embedding-marks-proof/make_sample.py

It writes `sample/plain.srt` and `sample/marked.srt` next to this script, UTF-8 with no byte
order mark and LF line endings, `sample/cues.json`, the cue text the Safari page reads, and one
local HLS stream per copy in `hls/<copy>/`, for Safari's own HLS player. The
plain copy is each line as a subtitle author writes it. The marked copy wraps every RTL line in RLE (U+202B) and PDF (U+202C), right after the line's
leading markup and right before its trailing markup, the way the RTL fix will. Every other line
is the same bytes in both copies.

`CASES` is the one place the sample is written. Each case is one cue, shown for 1.5 seconds,
starting every 2 seconds. `render_burn_in.sh` grabs each cue's frame at its `FRAME_AT` time.

`sample/cues.json` lists every case but those in `BURN_IN_ONLY`, each with its `slug`, `title`,
`start` and `end` in seconds, and its `plain` and `marked` text, lines joined by LF, the way a
Jellyfin `TrackEvent.Text` reaches Jellyfin Web. The `{\\an8}` case is burn-in only, because
Jellyfin Web strips `{\\...}` blocks and WebVTT has cue settings instead.

Each `hls/<copy>/` holds `master.m3u8`, which names `subtitles.m3u8` as a WebVTT subtitle
rendition with `DEFAULT=YES` and `AUTOSELECT=YES` in the shape Jellyfin's `DynamicHlsHelper`
writes it, and `subtitles.vtt`, the one WebVTT segment. That segment carries `TIMESTAMP_MAP` after
the `WEBVTT` line, as Jellyfin's 12.x HLS subtitle segments do, and the same cases as
`sample/cues.json`. No left-to-right mark is added: Jellyfin Web never touches HLS subtitles.
The master points at `../video/main.m3u8`, the MPEG-TS video `drive_safari.py` makes, whose first
PTS is 900000, so each cue's local time is its playback time.
"""

import json
from pathlib import Path

RLE = "‫"
PDF = "‬"

TIMESTAMP_MAP = "X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000"
SUBTITLE_SEGMENT = 22.0

CUE_STEP = 2.0
CUE_LENGTH = 1.5
FRAME_AT = 0.75


def rtl(text, lead="", trail=""):
    """An RTL line: the marked copy wraps `text` in RLE and PDF, inside `lead` and `trail`."""
    return (lead, text, trail, True)


def ltr(text):
    """A line the RTL fix leaves alone, the same in both copies."""
    return ("", text, "", False)


# (slug, what the case checks, lines of its cue)
CASES = [
    ("01-period", "Arabic line ending in a period", [rtl("وصلنا إلى المدينة في الصباح.")]),
    ("02-exclamation", "Arabic line ending in an exclamation mark", [rtl("هذا رائع جدًا!")]),
    ("03-closing-quote", "Arabic line ending in a closing quote", [rtl('قالت لي: "لا تتأخر"')]),
    ("04-english-name", "Arabic line quoting an English name, ending in a period", [rtl('شاهدنا فيلم "The Matrix" أمس.')]),
    ("05-leading-digits", "Arabic line starting with digits", [rtl("20 دقيقة فقط حتى نهاية الفيلم")]),
    ("06-english-with-arabic", "English line with an Arabic word, unmarked in both copies", [ltr("The Arabic word مرحبا means hello.")]),
    ("07-two-lines", "Two-line cue: one Arabic line, one English line", [rtl("المفتاح على الطاولة."), ltr("The key is on the table.")]),
    ("08-italic", "Italic Arabic line", [rtl("كان ذلك مجرد حلم.", "<i>", "</i>")]),
    ("09-hebrew", "Hebrew line ending in a period", [rtl("שלום לכולם.")]),
    ("10-an8", "Arabic line with {\\an8} at the top of the screen", [rtl("هذه الجملة في أعلى الشاشة.", "{\\an8}")]),
]

BURN_IN_ONLY = {"10-an8"}


def timestamp(seconds, separator=","):
    millis = round(seconds * 1000)
    hours, millis = divmod(millis, 3_600_000)
    minutes, millis = divmod(millis, 60_000)
    secs, millis = divmod(millis, 1000)
    return f"{hours:02}:{minutes:02}:{secs:02}{separator}{millis:03}"


def render_line(line, marked):
    lead, text, trail, is_rtl = line
    if marked and is_rtl:
        return lead + RLE + text + PDF + trail
    return lead + text + trail


def srt(marked):
    blocks = []
    for number, (_, _, lines) in enumerate(CASES, start=1):
        start = number * CUE_STEP
        body = "\n".join(render_line(line, marked) for line in lines)
        blocks.append(f"{number}\n{timestamp(start)} --> {timestamp(start + CUE_LENGTH)}\n{body}\n")
    return "\n".join(blocks)


def cues():
    entries = []
    for number, (slug, title, lines) in enumerate(CASES, start=1):
        if slug in BURN_IN_ONLY:
            continue
        start = number * CUE_STEP
        entries.append({
            "slug": slug,
            "title": title,
            "start": start,
            "end": start + CUE_LENGTH,
            "plain": "\n".join(render_line(line, False) for line in lines),
            "marked": "\n".join(render_line(line, True) for line in lines),
        })
    return json.dumps(entries, ensure_ascii=False, indent=2) + "\n"


def vtt(marked):
    blocks = ["WEBVTT\n" + TIMESTAMP_MAP + "\n"]
    for number, (slug, _, lines) in enumerate(CASES, start=1):
        if slug in BURN_IN_ONLY:
            continue
        start = number * CUE_STEP
        body = "\n".join(render_line(line, marked) for line in lines)
        blocks.append(f"{slug}\n{timestamp(start, '.')} --> {timestamp(start + CUE_LENGTH, '.')}\n{body}\n")
    return "\n".join(blocks)


def master_playlist():
    return (
        "#EXTM3U\n"
        '#EXT-X-MEDIA:TYPE=SUBTITLES,GROUP-ID="subs",NAME="Arabic - SUBRIP",DEFAULT=YES,FORCED=NO,'
        'AUTOSELECT=YES,URI="subtitles.m3u8",LANGUAGE="ara"\n'
        '#EXT-X-STREAM-INF:BANDWIDTH=200000,CODECS="avc1.64001e",RESOLUTION=640x360,SUBTITLES="subs"\n'
        "../video/main.m3u8\n")


def subtitle_playlist():
    return (
        "#EXTM3U\n"
        f"#EXT-X-TARGETDURATION:{SUBTITLE_SEGMENT:.0f}\n"
        "#EXT-X-VERSION:3\n"
        "#EXT-X-MEDIA-SEQUENCE:0\n"
        "#EXT-X-PLAYLIST-TYPE:VOD\n"
        f"#EXTINF:{SUBTITLE_SEGMENT:.6f},\n"
        "subtitles.vtt\n"
        "#EXT-X-ENDLIST\n")


def main():
    out = Path(__file__).resolve().parent / "sample"
    out.mkdir(exist_ok=True)
    for name, marked in (("plain", False), ("marked", True)):
        (out / f"{name}.srt").write_bytes(srt(marked).encode("utf-8"))
    (out / "cues.json").write_bytes(cues().encode("utf-8"))
    for name, marked in (("plain", False), ("marked", True)):
        stream = out.parent / "hls" / name
        stream.mkdir(parents=True, exist_ok=True)
        (stream / "master.m3u8").write_bytes(master_playlist().encode("utf-8"))
        (stream / "subtitles.m3u8").write_bytes(subtitle_playlist().encode("utf-8"))
        (stream / "subtitles.vtt").write_bytes(vtt(marked).encode("utf-8"))
    for number, (slug, _, _) in enumerate(CASES, start=1):
        print(f"{slug} {number * CUE_STEP + FRAME_AT:.2f}")


if __name__ == "__main__":
    main()
