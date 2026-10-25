"""Print the burn-in filter of every transcode log a test server wrote after a marker file.

The burn-in check plays each track with Burn subtitles set to All, then runs this to read the
`subtitles=f='...'` filter ffmpeg got. When the filter points into Lazyfin's `lazyfin/rtl-fix`
cache folder, it also opens that copy and prints whether it is strict UTF-8, whether it has a
byte order mark, its RLE and PDF counts, and whether a `:charenc=` option follows the filter.

Run it from the main checkout root, with Python's isolated mode:

    touch <marker>
    # play one track in Jellyfin Web, burned in
    python3 -I <this branch>/research/rtl-fix-verification/burnin_filters.py .temp/servers/12.2 <marker>

The last line printed for a play is the transcode the screenshots came from. Seeking in a
10-second video stays inside the first transcode, so one play gives one log.
"""
import glob
import os
import re
import sys

server, marker = sys.argv[1], sys.argv[2]
since = os.path.getmtime(marker) if os.path.exists(marker) else 0
for log in sorted(glob.glob(os.path.join(server, "log", "FFmpeg.Transcode-*.log")), key=os.path.getmtime):
    if os.path.getmtime(log) <= since:
        continue
    command = next((line for line in open(log, encoding="utf-8") if "subtitles=" in line), "")
    match = re.search(r"subtitles=f='.*?(?=[\",]|$)", command)
    print(os.path.basename(log), match.group(0) if match else "NO SUBTITLES FILTER")
    if match and "/lazyfin/rtl-fix/" in match.group(0):
        path = re.match(r"subtitles=f='([^']*)'", match.group(0)).group(1)
        data = open(path, "rb").read()
        text = data.decode("utf-8")
        print(
            "   copy", os.path.basename(path),
            "UTF-8,", "byte order mark" if data.startswith(b"\xef\xbb\xbf") else "no byte order mark,",
            "RLE", text.count("‫"), "PDF", text.count("‬"),
            "charenc after the filter:", ":charenc=" in match.group(0))
