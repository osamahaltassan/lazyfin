# Verifying the RTL fix on the 12.2 and 13.0 test servers

Evidence for the check that the RTL fix works end to end on the local 12.2 and 13.0 test servers from `scripts/dev.sh`, with a control run without Lazyfin beside a fix run with it. This branch is never merged.

This file holds the subtitle bytes and the web player in Safari. The web player check in the built-in browser and the burn-in check add their rows to each server's section later.

## Versions

| What | Version |
| --- | --- |
| macOS | 26.7.1 (25G241), Apple silicon |
| Safari | 27.0 (21625.1.29.18.28), driven through `safaridriver` and the W3C WebDriver API |
| Built-in browser | The Claude desktop app's browser pane, Chromium `152.0.7977.167` |
| Server 12.2 | Jellyfin 12.2.0, `jellyfin_12.2-arm64.tar.xz` from the stable macOS builds |
| Server 13.0 | Jellyfin 13.0.0, unstable build `2026101212`, `jellyfin_2026101212-arm64.tar.xz` |
| jellyfin-ffmpeg | 8.1.3-1, `portable_macarm64-gpl`, the build `scripts/dev.sh` pins (`ffmpeg version 8.1.3-Jellyfin`), libass API `0x1705000`, commit `4a05d812`, CoreText font provider |
| Lazyfin | `main` at `eefc2b2` ("Fix RTL lines in burned-in video"), installed as 0.0.1.0 by `scripts/dev.sh release` through a local plugin repository |
| Burn-in font | Arial, `/System/Library/Fonts/Supplemental/Arial.ttf` (`ArialMT`), and `Arial Italic.ttf` for the italic line, picked by libass for the server's default style. |
| Fallback font | `Arial.ttf`, a copy of the same Arial file, in `.temp/rtl-check/fonts`, set as each server's fallback font folder from the Safari check on. Jellyfin Web hands it to SubtitlesOctopus, its ASS renderer. |

## The test library

[`make_library.py`](rtl-fix-verification/make_library.py) writes the [sample](rtl-fix-verification/sample/) and builds the library in `.temp/rtl-check/library`, on the 10-second `testsrc` test video from the ffmpeg ticket. Both servers have it as their one `Movies` library and read the same files.

| Movie | Subtitles |
| --- | --- |
| Lazyfin RTL External (2026) | MP4, with [`arabic.srt`](rtl-fix-verification/sample/arabic.srt) next to it as `.ar.srt` and [`english.srt`](rtl-fix-verification/sample/english.srt) as `.en.srt` |
| Lazyfin RTL Embedded (2026) | MKV with `arabic.srt` (stream 2) and [`arabic.ass`](rtl-fix-verification/sample/arabic.ass) (stream 3) muxed in, both tagged `ara` |
| Lazyfin RTL Uploaded (2026) | MP4 alone. `arabic.srt` was uploaded through Jellyfin Web's subtitle upload dialog in the control run. |
| Lazyfin RTL Windows-1256 (2026) | MP4, with [`arabic-windows-1256.srt`](rtl-fix-verification/sample/arabic-windows-1256.srt) next to it as `.ar.srt`, for burn-in |

Every Arabic SRT holds the same two cues:

- **Cue 1, 1.0 s to 4.5 s:** an Arabic line ending in a period, an Arabic line quoting "The Matrix" and ending in `!`, the English line `The Arabic word مرحبا means hello.`, and an italic Arabic line ending in a period.
- **Cue 2, 5.5 s to 9.0 s:** `{\an8}` and an Arabic line ending in a period.

The English SRT has two English cues at the same times. The ASS track has two `Dialogue` lines at the same times, with the period at the start of each Arabic line, where ASS authors put it for the VSFilter rule. Every SRT but the Windows-1256 one is UTF-8 with no byte order mark and LF line endings.

The test video is 10 seconds, not the 15 the spec guessed. Both cues fit in it with a gap between them.

## Server settings

Both servers keep their defaults, apart from the library:

- One `Movies` library at `.temp/rtl-check/library`, with "Save subtitles with media" on, the default, so an upload lands next to the video.
- Fallback fonts were off for the bytes check. The burn-in proof needed no font for Arabic, and one burned-in control frame per server confirms Arabic draws as letters, not boxes. See [Burn-in font check](#burn-in-font-check).
- Fallback fonts are on from the Safari check on, with `.temp/rtl-check/fonts` holding only `Arial.ttf`. Without a fallback font, Jellyfin Web's ASS renderer, SubtitlesOctopus, has no Arabic font, and the ASS track drew every Arabic letter as a box in a trial run. A screenshot with boxes is never a result, so this was set through the API on both servers before the Safari control run, and stays on for the later checks.
- The test admin's subtitle settings stay at their defaults. The burn-in check sets Burn subtitles itself.

## The runs

**Control run.** Lazyfin 0.0.0.0 was uninstalled on each server through the Dashboard's plugin page, in the built-in browser, and both servers were restarted. `GET /Plugins` then listed AudioDB, ListenBrainz Similarity Provider, MusicBrainz, OMDb, Studio Images, and TMDb on both, and no Lazyfin.

**The upload.** In the bytes check, Safari would not start a WebDriver session. First it refused because remote automation was off. After the maintainer turned it on, Safari timed out creating the session. The maintainer turned remote automation off again and uploaded `arabic.srt` by hand on the 12.2 server: Jellyfin Web's subtitle editor, language Arabic, file picked in the dialog. The server saved it as `Lazyfin RTL Uploaded (2026).ara.srt`, byte for byte the same as `arabic.srt`.

The 13.0 server reads the same folder, so it got the same file through a metadata refresh of the item, not a second upload. A second upload would have saved a second file, `….ara.0.srt`, because Jellyfin's `SubtitleManager` never overwrites, and both servers would then have listed two Arabic subtitles. After the refresh, both servers list one Arabic external subtitle on the Uploaded movie.

**Fix run.** `scripts/dev.sh release` built the release zip and manifest the way CI does and installed Lazyfin through a local plugin repository. `GET /Plugins` then listed `Lazyfin 0.0.1.0 Active` on both servers.

## Subtitle bytes

[`check_bytes.py`](rtl-fix-verification/check_bytes.py) fetched, in each run and with the test admin's token, every text subtitle track as `Stream.js`, `Stream.vtt`, and `Stream.srt`, the ASS track as `Stream.ass` too, and the first segment of each track's HLS subtitle playlist, from a master playlist with `SubtitleMethod=Hls`. It judged them by the spec's rules:

- **Arabic SRT tracks:** in the fix run, each RTL line is wrapped in one RLE and one PDF, after its leading markup and before its trailing markup, and every other line has none. With every RLE and PDF removed, the fix output equals the control output. JSON is parsed before it is compared.
- **English and ASS tracks:** every form is byte for byte the same in both runs, the ASS track's HLS WebVTT segment included.
- **The Windows-1256 item** is for burn-in only, so its lines say Info and cannot fail. Both servers convert it to UTF-8 before they hand it out, so it comes out wrapped like the others.

Before trusting the judge, it was fed bad input on purpose: the control bytes as if they were the fix run, RLE moved before `<i>` or `{\an8}`, PDF moved after `</i>`, one Arabic line left unwrapped, the English line wrapped, and a changed letter. It failed each one.

What the fix run's HLS segment for the MKV's Arabic SRT track looks like, with the marks shown as `[RLE]` and `[PDF]`:

```text
WEBVTT
X-TIMESTAMP-MAP=MPEGTS:900000,LOCAL:00:00:00.000

00:00:01.023 --> 00:00:04.523
[RLE]وصلنا إلى المدينة في الصباح.[PDF]
[RLE]شاهدنا فيلم "The Matrix" أمس![PDF]
The Arabic word مرحبا means hello.
<i>[RLE]كان ذلك مجرد حلم.[PDF]</i>

00:00:05.523 --> 00:00:09.023 line:20%
[RLE]هذه الجملة في أعلى الشاشة.[PDF]
```

The 12.2 and 13.0 servers returned identical bytes for every file, in the control run and in the fix run.

## The web player in Safari

[`drive_safari.py`](rtl-fix-verification/drive_safari.py) drove Jellyfin Web in Safari through `safaridriver`. For each server and run, it opened one fresh WebDriver session, which starts with an empty cache and no sign-in, and signed in as the test admin through the login form. For each of the five tracks it opened the movie's details page, picked the track in its Subtitles menu, clicked Play, paused, and seeked to 2.5 s for cue 1 and 7.0 s for cue 2. It then hid the player's controls with a style rule, so they never cover a subtitle, and saved a screenshot cropped to the video picture, 960 pixels wide. Burn subtitles stayed at Auto, so text subtitles were delivered, not burned in.

Beside each screenshot the driver read what the page showed: the renderer, the subtitle text, and the RLE and PDF marks in it. That shows the fix run's screenshots came from the marked subtitles, not from a cache.

- **Arabic and English SRT tracks:** Jellyfin Web draws them with its custom subtitle element, from `Stream.js`. The MP4s play directly, and the MKV plays as an HLS remux. In the fix run the element held 3 RLE and 3 PDF on cue 1 and 1 of each on cue 2, on every Arabic track. In the control run it held none, and the English track held none in either run.
- **Arabic ASS track:** SubtitlesOctopus draws it on a canvas, with the fallback font.

**Judging.** The English and ASS screenshots are pixel for pixel the same in both runs on both servers: ffmpeg's PSNR between control and fix is infinite. The External and Uploaded screenshots are pixel for pixel the same as each other, and the Embedded ones differ from them only in video decoding noise, at a PSNR above 52 dB. 12.2 and 13.0 gave pixel-identical screenshots for every track.

- **The control shows the bug** on every Arabic SRT track. The period sits at the right end of each Arabic line, where an Arabic reader starts. The "The Matrix" line reads in left-to-right order, with `!` at the right end. The italic line has its period at the right end.
- **The fix run** puts the period, the `!`, and the italic line's period at the left end of each Arabic line. The "The Matrix" line starts at the right with its first word, and the English name is whole and in order. The English line with مرحبا looks the same as in the control. Nothing shows where the marks sit: no box and no gap.
- **The `{\an8}` cue** shows at the bottom in both runs. Jellyfin Web's custom element ignores the position tag, with or without Lazyfin. Its period moves from the right end to the left end.
- **The ASS track** shows its period at the left end in both runs, where the ASS author put it.

How to read the tables below: in each pair, the control is on the left and the fix on the right. Click a picture for the full size.

## 12.2

### Subtitle bytes

| Check | JSON | WebVTT | SRT | ASS | HLS segment |
| --- | --- | --- | --- | --- | --- |
| Bytes, External, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, External, English SRT | Pass | Pass | Pass | | Pass |
| Bytes, Embedded, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, Embedded, Arabic ASS | Pass | Pass | Pass | Pass | Pass |
| Bytes, Uploaded, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, Windows-1256, Arabic SRT | Info | Info | Info | | Info |

Script output:

```text
PASS 12.2  Embedded      2-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Embedded      2-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Embedded      2-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Embedded      2-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Embedded      3-ara-ass      ass  byte for byte the same as control
PASS 12.2  Embedded      3-ara-ass      hls  byte for byte the same as control
PASS 12.2  Embedded      3-ara-ass      js   byte for byte the same as control
PASS 12.2  Embedded      3-ara-ass      srt  byte for byte the same as control
PASS 12.2  Embedded      3-ara-ass      vtt  byte for byte the same as control
PASS 12.2  External      0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  External      0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  External      0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  External      0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  External      1-eng-subrip   hls  byte for byte the same as control
PASS 12.2  External      1-eng-subrip   js   byte for byte the same as control
PASS 12.2  External      1-eng-subrip   srt  byte for byte the same as control
PASS 12.2  External      1-eng-subrip   vtt  byte for byte the same as control
PASS 12.2  Uploaded      0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Uploaded      0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Uploaded      0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 12.2  Uploaded      0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 12.2  Windows-1256  0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 12.2  Windows-1256  0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 12.2  Windows-1256  0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 12.2  Windows-1256  0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
```

### Safari web player

| Check | Renderer | Result | Control, cue 1 | Fix, cue 1 | Control, cue 2 | Fix, cue 2 |
| --- | --- | --- | --- | --- | --- | --- |
| Safari, External, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/12.2/external-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/12.2/external-arabic-srt-cue1.jpg" width="200" alt="12.2 control External, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/12.2/external-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/12.2/external-arabic-srt-cue1.jpg" width="200" alt="12.2 fix External, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/12.2/external-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/12.2/external-arabic-srt-cue2.jpg" width="200" alt="12.2 control External, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/12.2/external-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/12.2/external-arabic-srt-cue2.jpg" width="200" alt="12.2 fix External, Arabic SRT cue2"></a> |
| Safari, Embedded, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/12.2/embedded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/12.2/embedded-arabic-srt-cue1.jpg" width="200" alt="12.2 control Embedded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/12.2/embedded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/12.2/embedded-arabic-srt-cue1.jpg" width="200" alt="12.2 fix Embedded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/12.2/embedded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/12.2/embedded-arabic-srt-cue2.jpg" width="200" alt="12.2 control Embedded, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/12.2/embedded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/12.2/embedded-arabic-srt-cue2.jpg" width="200" alt="12.2 fix Embedded, Arabic SRT cue2"></a> |
| Safari, Uploaded, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/12.2/uploaded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/12.2/uploaded-arabic-srt-cue1.jpg" width="200" alt="12.2 control Uploaded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/12.2/uploaded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/12.2/uploaded-arabic-srt-cue1.jpg" width="200" alt="12.2 fix Uploaded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/12.2/uploaded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/12.2/uploaded-arabic-srt-cue2.jpg" width="200" alt="12.2 control Uploaded, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/12.2/uploaded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/12.2/uploaded-arabic-srt-cue2.jpg" width="200" alt="12.2 fix Uploaded, Arabic SRT cue2"></a> |
| Safari, External, English SRT | Custom element | **Pass**. Pixel for pixel the same as the control. | <a href="rtl-fix-verification/safari/control/12.2/external-english-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/12.2/external-english-srt-cue1.jpg" width="200" alt="12.2 control External, English SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/12.2/external-english-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/12.2/external-english-srt-cue1.jpg" width="200" alt="12.2 fix External, English SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/12.2/external-english-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/12.2/external-english-srt-cue2.jpg" width="200" alt="12.2 control External, English SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/12.2/external-english-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/12.2/external-english-srt-cue2.jpg" width="200" alt="12.2 fix External, English SRT cue2"></a> |
| Safari, Embedded, Arabic ASS | SubtitlesOctopus canvas | **Pass**. Pixel for pixel the same as the control. | <a href="rtl-fix-verification/safari/control/12.2/embedded-arabic-ass-cue1.jpg"><img src="rtl-fix-verification/safari/control/12.2/embedded-arabic-ass-cue1.jpg" width="200" alt="12.2 control Embedded, Arabic ASS cue1"></a> | <a href="rtl-fix-verification/safari/fix/12.2/embedded-arabic-ass-cue1.jpg"><img src="rtl-fix-verification/safari/fix/12.2/embedded-arabic-ass-cue1.jpg" width="200" alt="12.2 fix Embedded, Arabic ASS cue1"></a> | <a href="rtl-fix-verification/safari/control/12.2/embedded-arabic-ass-cue2.jpg"><img src="rtl-fix-verification/safari/control/12.2/embedded-arabic-ass-cue2.jpg" width="200" alt="12.2 control Embedded, Arabic ASS cue2"></a> | <a href="rtl-fix-verification/safari/fix/12.2/embedded-arabic-ass-cue2.jpg"><img src="rtl-fix-verification/safari/fix/12.2/embedded-arabic-ass-cue2.jpg" width="200" alt="12.2 fix Embedded, Arabic ASS cue2"></a> |

## 13.0

### Subtitle bytes

| Check | JSON | WebVTT | SRT | ASS | HLS segment |
| --- | --- | --- | --- | --- | --- |
| Bytes, External, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, External, English SRT | Pass | Pass | Pass | | Pass |
| Bytes, Embedded, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, Embedded, Arabic ASS | Pass | Pass | Pass | Pass | Pass |
| Bytes, Uploaded, Arabic SRT | Pass | Pass | Pass | | Pass |
| Bytes, Windows-1256, Arabic SRT | Info | Info | Info | | Info |

Script output:

```text
PASS 13.0  Embedded      2-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Embedded      2-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Embedded      2-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Embedded      2-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Embedded      3-ara-ass      ass  byte for byte the same as control
PASS 13.0  Embedded      3-ara-ass      hls  byte for byte the same as control
PASS 13.0  Embedded      3-ara-ass      js   byte for byte the same as control
PASS 13.0  Embedded      3-ara-ass      srt  byte for byte the same as control
PASS 13.0  Embedded      3-ara-ass      vtt  byte for byte the same as control
PASS 13.0  External      0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  External      0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  External      0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  External      0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  External      1-eng-subrip   hls  byte for byte the same as control
PASS 13.0  External      1-eng-subrip   js   byte for byte the same as control
PASS 13.0  External      1-eng-subrip   srt  byte for byte the same as control
PASS 13.0  External      1-eng-subrip   vtt  byte for byte the same as control
PASS 13.0  Uploaded      0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Uploaded      0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Uploaded      0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
PASS 13.0  Uploaded      0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 13.0  Windows-1256  0-ara-subrip   hls  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 13.0  Windows-1256  0-ara-subrip   js   4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 13.0  Windows-1256  0-ara-subrip   srt  4 of 4 RTL lines wrapped, 1 other lines unmarked
INFO 13.0  Windows-1256  0-ara-subrip   vtt  4 of 4 RTL lines wrapped, 1 other lines unmarked
```

### Safari web player

| Check | Renderer | Result | Control, cue 1 | Fix, cue 1 | Control, cue 2 | Fix, cue 2 |
| --- | --- | --- | --- | --- | --- | --- |
| Safari, External, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/13.0/external-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/13.0/external-arabic-srt-cue1.jpg" width="200" alt="13.0 control External, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/13.0/external-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/13.0/external-arabic-srt-cue1.jpg" width="200" alt="13.0 fix External, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/13.0/external-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/13.0/external-arabic-srt-cue2.jpg" width="200" alt="13.0 control External, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/13.0/external-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/13.0/external-arabic-srt-cue2.jpg" width="200" alt="13.0 fix External, Arabic SRT cue2"></a> |
| Safari, Embedded, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/13.0/embedded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/13.0/embedded-arabic-srt-cue1.jpg" width="200" alt="13.0 control Embedded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/13.0/embedded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/13.0/embedded-arabic-srt-cue1.jpg" width="200" alt="13.0 fix Embedded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/13.0/embedded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/13.0/embedded-arabic-srt-cue2.jpg" width="200" alt="13.0 control Embedded, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/13.0/embedded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/13.0/embedded-arabic-srt-cue2.jpg" width="200" alt="13.0 fix Embedded, Arabic SRT cue2"></a> |
| Safari, Uploaded, Arabic SRT | Custom element | **Pass**. Bug in the control, fixed in the fix run. | <a href="rtl-fix-verification/safari/control/13.0/uploaded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/13.0/uploaded-arabic-srt-cue1.jpg" width="200" alt="13.0 control Uploaded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/13.0/uploaded-arabic-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/13.0/uploaded-arabic-srt-cue1.jpg" width="200" alt="13.0 fix Uploaded, Arabic SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/13.0/uploaded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/13.0/uploaded-arabic-srt-cue2.jpg" width="200" alt="13.0 control Uploaded, Arabic SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/13.0/uploaded-arabic-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/13.0/uploaded-arabic-srt-cue2.jpg" width="200" alt="13.0 fix Uploaded, Arabic SRT cue2"></a> |
| Safari, External, English SRT | Custom element | **Pass**. Pixel for pixel the same as the control. | <a href="rtl-fix-verification/safari/control/13.0/external-english-srt-cue1.jpg"><img src="rtl-fix-verification/safari/control/13.0/external-english-srt-cue1.jpg" width="200" alt="13.0 control External, English SRT cue1"></a> | <a href="rtl-fix-verification/safari/fix/13.0/external-english-srt-cue1.jpg"><img src="rtl-fix-verification/safari/fix/13.0/external-english-srt-cue1.jpg" width="200" alt="13.0 fix External, English SRT cue1"></a> | <a href="rtl-fix-verification/safari/control/13.0/external-english-srt-cue2.jpg"><img src="rtl-fix-verification/safari/control/13.0/external-english-srt-cue2.jpg" width="200" alt="13.0 control External, English SRT cue2"></a> | <a href="rtl-fix-verification/safari/fix/13.0/external-english-srt-cue2.jpg"><img src="rtl-fix-verification/safari/fix/13.0/external-english-srt-cue2.jpg" width="200" alt="13.0 fix External, English SRT cue2"></a> |
| Safari, Embedded, Arabic ASS | SubtitlesOctopus canvas | **Pass**. Pixel for pixel the same as the control. | <a href="rtl-fix-verification/safari/control/13.0/embedded-arabic-ass-cue1.jpg"><img src="rtl-fix-verification/safari/control/13.0/embedded-arabic-ass-cue1.jpg" width="200" alt="13.0 control Embedded, Arabic ASS cue1"></a> | <a href="rtl-fix-verification/safari/fix/13.0/embedded-arabic-ass-cue1.jpg"><img src="rtl-fix-verification/safari/fix/13.0/embedded-arabic-ass-cue1.jpg" width="200" alt="13.0 fix Embedded, Arabic ASS cue1"></a> | <a href="rtl-fix-verification/safari/control/13.0/embedded-arabic-ass-cue2.jpg"><img src="rtl-fix-verification/safari/control/13.0/embedded-arabic-ass-cue2.jpg" width="200" alt="13.0 control Embedded, Arabic ASS cue2"></a> | <a href="rtl-fix-verification/safari/fix/13.0/embedded-arabic-ass-cue2.jpg"><img src="rtl-fix-verification/safari/fix/13.0/embedded-arabic-ass-cue2.jpg" width="200" alt="13.0 fix Embedded, Arabic ASS cue2"></a> |

## The library files

The script hashed every subtitle file in the library, the MKV included, before the control run, right after the upload, and after the fix run. Every file is byte for byte the same after the fix run, and Lazyfin wrote nothing into the library.

```text
PASS library Lazyfin RTL Embedded (2026)/Lazyfin RTL Embedded (2026).mkv: same as before the control run
PASS library Lazyfin RTL External (2026)/Lazyfin RTL External (2026).ar.srt: same as before the control run
PASS library Lazyfin RTL External (2026)/Lazyfin RTL External (2026).en.srt: same as before the control run
PASS library Lazyfin RTL Uploaded (2026)/Lazyfin RTL Uploaded (2026).ara.srt: same as right after the upload
PASS library Lazyfin RTL Windows-1256 (2026)/Lazyfin RTL Windows-1256 (2026).ar.srt: same as before the control run
```

## Burn-in font check

One burned-in control frame per server, with Lazyfin uninstalled: the External movie's Arabic SRT at 2 s, through an HLS transcode with `SubtitleMethod=Encode`, cropped to the subtitle. Arabic draws as real letters on both servers, so no fallback font is needed. The control shows the bug: the period and the `!` sit at the right end, where an Arabic reader starts. The transcode logs show `subtitles=f='…/Lazyfin RTL External (2026).ar.srt':charenc=utf-8`, the original file.

| 12.2 | 13.0 |
| --- | --- |
| ![12.2 control burn-in](rtl-fix-verification/burn-in-control/12.2-external-arabic.jpg) | ![13.0 control burn-in](rtl-fix-verification/burn-in-control/13.0-external-arabic.jpg) |

## Running it again

From the main checkout, with this branch checked out in a worktree at `<branch>`:

1. `python3 -I <branch>/research/rtl-fix-verification/make_library.py`, then point each server's `Movies` library at `.temp/rtl-check/library`.
2. Uninstall Lazyfin on each server through the Dashboard and restart both, then `check_bytes.py snapshot before-control`.
3. Upload `sample/arabic.srt` to the Uploaded movie once, refresh that movie on the other server, then `check_bytes.py snapshot after-upload` and `check_bytes.py fetch control`.
4. `scripts/dev.sh release`, then `check_bytes.py fetch fix`, `check_bytes.py snapshot after-fix`, and `check_bytes.py judge`.

For the web player in Safari, turn on the fallback font before the control run, then run `drive_safari.py control` after step 3 and `drive_safari.py fix` after step 4. It needs Safari's remote automation on. If `POST /session` times out, quit every Safari process first: an old Safari that had been open for days blocked every session until it was quit. Safari's page zoom here is 85%, which makes WebDriver's element click miss, so the driver clicks with mouse actions at points scaled by the zoom.

## Not checked here

- What a viewer sees in the built-in browser and in burn-in. Their checks add their own rows.
- The upload went through Jellyfin Web on 12.2 only. 13.0 read the same saved file.
- The upload was done by hand in the bytes check, so the upload dialog was not driven through WebDriver. The Safari check reused that one uploaded file.
- Safari's own native HLS player and the `VTTCue` path. Jellyfin Web in Safari drew every text track with its custom element. The embedding marks proof on the `research/embedding-marks-proof` branch proved both other paths with hand-made files.
