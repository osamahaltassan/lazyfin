"""Writes the RTL fix verification sample and builds the test library from it.

Usage, from the root of the main checkout, after `scripts/dev.sh setup`:

    python3 -I <this branch>/research/rtl-fix-verification/make_library.py [--library DIR]

It writes the sample files into `sample/` next to this script, then builds the library in
`.temp/rtl-check/library` (or `--library`), one folder per movie, on the short test video in
`.temp/media/Movies/Lazyfin Test (2026)/`, with the test servers' ffmpeg from `.temp/ffmpeg`:

- `Lazyfin RTL External (2026)`: the MP4, `<name>.ar.srt`, and `<name>.en.srt`.
- `Lazyfin RTL Embedded (2026)`: an MKV with the Arabic SRT and the Arabic ASS muxed in, both
  tagged `ara`.
- `Lazyfin RTL Uploaded (2026)`: the MP4 alone. The Arabic SRT is uploaded through Jellyfin Web.
- `Lazyfin RTL Windows-1256 (2026)`: the MP4 and `<name>.ar.srt` saved in Windows-1256, for burn-in.

`CUES` is the one place the Arabic text is written. Every SRT is UTF-8 with no byte order mark and
LF line endings, except `arabic-windows-1256.srt`. A library that already exists is left alone,
so the files a check compares never change between runs. Delete the folder to build it again.
Only the standard library is used.
"""

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SAMPLE = HERE / "sample"
VIDEO = Path(".temp/media/Movies/Lazyfin Test (2026)/Lazyfin Test (2026).mp4")

# (start, end, lines) per cue, the same two cues for every Arabic SRT.
CUES = [
    ("00:00:01,000", "00:00:04,500", [
        "وصلنا إلى المدينة في الصباح.",
        'شاهدنا فيلم "The Matrix" أمس!',
        "The Arabic word مرحبا means hello.",
        "<i>كان ذلك مجرد حلم.</i>",
    ]),
    ("00:00:05,500", "00:00:09,000", [
        "{\\an8}هذه الجملة في أعلى الشاشة.",
    ]),
]

ENGLISH = [
    ("00:00:01,000", "00:00:04,500", ["We reached the city in the morning."]),
    ("00:00:05,500", "00:00:09,000", ["This line sits at the bottom."]),
]

# The VSFilter rule lays every ASS line out left to right, so ASS authors put the end punctuation
# at the start of an Arabic line. The RTL fix leaves ASS as authored, so it must look the same in
# both runs.
ASS = """[Script Info]
ScriptType: v4.00+
PlayResX: 1280
PlayResY: 720
WrapStyle: 0

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Arial,48,&H00FFFFFF,&H000000FF,&H00000000,&H80000000,0,0,0,0,100,100,0,0,1,2,1,2,40,40,40,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:01.00,0:00:04.50,Default,,0,0,0,,.وصلنا إلى المدينة في الصباح\\N!هذا رائع جدًا
Dialogue: 0,0:00:05.50,0:00:09.00,Default,,0,0,0,,{\\an8}.هذه الجملة في أعلى الشاشة
"""


def srt(cues):
    return "\n".join(f"{n}\n{start} --> {end}\n" + "\n".join(lines) + "\n"
                     for n, (start, end, lines) in enumerate(cues, start=1))


def write_sample():
    SAMPLE.mkdir(exist_ok=True)
    arabic = srt(CUES)
    (SAMPLE / "arabic.srt").write_bytes(arabic.encode("utf-8"))
    (SAMPLE / "arabic-windows-1256.srt").write_bytes(arabic.encode("cp1256"))
    (SAMPLE / "english.srt").write_bytes(srt(ENGLISH).encode("utf-8"))
    (SAMPLE / "arabic.ass").write_bytes(ASS.encode("utf-8"))


def ffmpeg():
    found = sorted(Path(".temp/ffmpeg").glob("*/ffmpeg"))
    if not found:
        sys.exit("No jellyfin-ffmpeg under .temp/ffmpeg. Run scripts/dev.sh setup.")
    return found[-1]


def build(library):
    if library.exists():
        print(f"{library} already exists; left alone.")
        return
    if not VIDEO.exists():
        sys.exit(f"Missing {VIDEO}. Run this from the main checkout after scripts/dev.sh setup.")
    work = library.with_name(library.name + ".partial")
    shutil.rmtree(work, ignore_errors=True)

    def movie(kind):
        name = f"Lazyfin RTL {kind} (2026)"
        folder = work / name
        folder.mkdir(parents=True)
        return folder / name

    base = movie("External")
    shutil.copyfile(VIDEO, f"{base}.mp4")
    shutil.copyfile(SAMPLE / "arabic.srt", f"{base}.ar.srt")
    shutil.copyfile(SAMPLE / "english.srt", f"{base}.en.srt")

    base = movie("Embedded")
    subprocess.run(
        [str(ffmpeg()), "-hide_banner", "-nostdin", "-loglevel", "error",
         "-i", str(VIDEO), "-i", str(SAMPLE / "arabic.srt"), "-i", str(SAMPLE / "arabic.ass"),
         "-map", "0:v", "-map", "0:a", "-map", "1", "-map", "2",
         "-c:v", "copy", "-c:a", "copy", "-c:s:0", "srt", "-c:s:1", "ass",
         "-metadata:s:s:0", "language=ara", "-metadata:s:s:0", "title=Arabic SRT",
         "-metadata:s:s:1", "language=ara", "-metadata:s:s:1", "title=Arabic ASS",
         "-fflags", "+bitexact", "-y", f"{base}.mkv"],
        check=True)

    base = movie("Uploaded")
    shutil.copyfile(VIDEO, f"{base}.mp4")

    base = movie("Windows-1256")
    shutil.copyfile(VIDEO, f"{base}.mp4")
    shutil.copyfile(SAMPLE / "arabic-windows-1256.srt", f"{base}.ar.srt")

    work.rename(library)
    print(f"Built {library}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--library", type=Path, default=Path(".temp/rtl-check/library"))
    args = parser.parse_args()
    write_sample()
    build(args.library)


if __name__ == "__main__":
    main()
