#!/usr/bin/env bash
# Renders the burn-in half of the embedding marks proof.
#
# Usage, from the repo root, after `scripts/dev.sh setup` has unpacked the test servers' ffmpeg:
#
#   research/embedding-marks-proof/render_burn_in.sh [path to jellyfin-ffmpeg]
#
# It regenerates the sample, then draws each case of `sample/plain.srt` and `sample/marked.srt`
# onto a blank 1280x720 video through the `subtitles` filter, with no `charenc` and no font
# option, the way the burn-in hook will hand ffmpeg its UTF-8 copy. Each case's two full frames
# go to `.temp/embedding-marks-proof/burn-in/`, and one comparison PNG per case, plain on top and
# marked below, goes to `research/embedding-marks-proof/burn-in/`. libass's font choices land in
# `.temp/embedding-marks-proof/burn-in/fontselect.txt`.
set -euo pipefail

proof=research/embedding-marks-proof
ffmpeg=${1:-$(ls -d .temp/ffmpeg/*/ffmpeg | sort -V | tail -1)}
raw=.temp/embedding-marks-proof/burn-in
out=$proof/burn-in
label_font=/System/Library/Fonts/Supplemental/Arial.ttf

mkdir -p "$raw" "$out"
: >"$raw/fontselect.txt"
"$ffmpeg" -hide_banner -version | head -1 >"$raw/ffmpeg-version.txt"

python3 -I "$proof/make_sample.py" | while read -r slug at; do
  for copy in plain marked; do
    "$ffmpeg" -hide_banner -nostdin -loglevel info \
      -f lavfi -i "color=c=0x303030:s=1280x720:r=25:d=24" \
      -vf "subtitles=f='$proof/sample/$copy.srt'" \
      -ss "$at" -frames:v 1 -y "$raw/$slug-$copy.png" 2>&1 |
      grep -o 'fontselect: .*' | sed "s/^/$slug $copy /" >>"$raw/fontselect.txt" || true
  done
  "$ffmpeg" -hide_banner -nostdin -loglevel error \
    -i "$raw/$slug-plain.png" -i "$raw/$slug-marked.png" \
    -filter_complex "[0]drawtext=fontfile=$label_font:text='PLAIN':x=24:y=24:fontsize=40:fontcolor=yellow[p];[1]drawtext=fontfile=$label_font:text='MARKED':x=24:y=24:fontsize=40:fontcolor=yellow[m];[p][m]vstack,drawbox=x=0:y=718:w=1280:h=4:color=yellow:t=fill" \
    -frames:v 1 -y "$out/$slug.png"
  echo "$out/$slug.png"
done
