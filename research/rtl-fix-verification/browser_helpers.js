/**
 * Page helpers that drove Jellyfin Web's player in the Claude desktop app's built-in browser.
 *
 * The built-in browser has no WebDriver, so the agent pasted this file into the page with the
 * browser's JavaScript tool, once per page load, and called the helpers one tool call at a time.
 * Each call returns within the tool's 45-second limit. Per server and run:
 *
 * 1. Emulate a 960x540 viewport, so the 16:9 video fills the page and a screenshot of the page is
 *    a screenshot of the video picture. The pane's screenshot then comes back as an 800x450 JPEG.
 * 2. For each track in TRACKS: `await lazOpen(item, index)`, then `await lazWaitPlay()`, which
 *    pauses as soon as the video plays.
 * 3. For each cue in CUES: `await lazShot(slug, seconds)`, then one throwaway screenshot and one
 *    kept screenshot. The throwaway one makes the pane paint a fresh frame; the first screenshot
 *    after a seek sometimes showed the frame from before the controls were hidden.
 * 4. Before judging a fix-run screenshot of an Arabic SRT track, read the player's own
 *    `Stream.js` response with the browser's network tool and check it holds U+202B and U+202C.
 *
 * `lazShot` returns the renderer Jellyfin Web used (`native-cue`, `custom-element`, or
 * `ass-canvas`), the cue text on screen, and its RLE and PDF counts.
 *
 * On a fresh page load the details page can render twice. If `lazWaitPlay` reports `not yet` on
 * the details page with the track reset, call `lazOpen` again.
 */

const TRACKS = [
    ['external-arabic-srt', '40aff9e604c4fe4c065886dd18890b5a', 0],
    ['embedded-arabic-srt', '6f5ac2d01781a16941d3669b8dc36c77', 2],
    ['uploaded-arabic-srt', '72822f2b3b4b37e7f7fbb4204be91069', 0],
    ['external-english-srt', '40aff9e604c4fe4c065886dd18890b5a', 1],
    ['embedded-arabic-ass', '6f5ac2d01781a16941d3669b8dc36c77', 3]
];

const CUES = [['cue1', 2.5], ['cue2', 7.0]];

const sleep = ms => new Promise(r => setTimeout(r, ms));

window.lazProbe = () => {
    const video = document.querySelector('video.htmlvideoplayer');
    const count = (s, c) => s.split(c).length - 1;
    const custom = document.querySelector('.videoSubtitlesInner');
    const assCanvas = document.querySelector('.libassjs-canvas-parent canvas, canvas.JASSUB, .jassub canvas');
    const showing = Array.from(video.textTracks).filter(t => t.mode === 'showing');
    const nativeText = showing.flatMap(t => Array.from(t.activeCues || []).map(c => c.text));
    let renderer = 'none';
    let text = '';
    if (assCanvas) {
        renderer = 'ass-canvas';
    } else if (custom && custom.innerHTML.trim()) {
        renderer = 'custom-element';
        text = custom.innerHTML;
    } else if (nativeText.length) {
        renderer = 'native-cue';
        text = nativeText.join('\n');
    }
    return {
        renderer,
        text,
        rle: count(text, '‫'),
        pdf: count(text, '‬'),
        currentTime: video.currentTime,
        paused: video.paused,
        videoSrc: video.currentSrc.split('?')[0],
        viewport: [innerWidth, innerHeight]
    };
};

window.lazSeek = async (seconds) => {
    if (!document.getElementById('lazyfin-hide-osd')) {
        const style = document.createElement('style');
        style.id = 'lazyfin-hide-osd';
        style.textContent = '.videoOsdBottom, .skinHeader, .osdHeader, .upNextContainer { visibility: hidden !important; }';
        document.head.appendChild(style);
    }
    const v = document.querySelector('video.htmlvideoplayer');
    v.pause();
    v.currentTime = seconds;
    for (let i = 0; i < 80; i++) {
        if (v.paused && !v.seeking && v.readyState >= 2) break;
        await sleep(250);
    }
    await sleep(2500);
    return window.lazProbe();
};

window.lazOpen = async (item, index) => {
    if (!location.hash.includes(item) || !location.hash.startsWith('#/details')) location.hash = '#/details?id=' + item;
    let select = null;
    for (let i = 0; i < 80; i++) {
        await sleep(250);
        const page = document.querySelector('.page:not(.hide)');
        if (location.hash.startsWith('#/details') && page
            && page.querySelector('.selectSubtitles option[value="' + index + '"]')
            && page.querySelector('.mainDetailButtons .btnPlay:not(.hide)')) {
            select = page.querySelector('.selectSubtitles');
            break;
        }
    }
    if (!select) return 'no details page';
    await sleep(500);
    select.value = String(index);
    select.dispatchEvent(new Event('change', { bubbles: true }));
    await sleep(500);
    document.querySelector('.page:not(.hide) .mainDetailButtons .btnPlay:not(.hide)').click();
    return 'clicked ' + select.value;
};

window.lazWaitPlay = async () => {
    for (let i = 0; i < 120; i++) {
        const v = document.querySelector('video.htmlvideoplayer');
        if (location.hash.startsWith('#/video') && v && !v.paused && v.currentTime > 0.3) {
            v.pause();
            return 'playing ' + v.currentTime;
        }
        await sleep(250);
    }
    const v = document.querySelector('video.htmlvideoplayer');
    return 'not yet: ' + location.hash + ' ' + (v && [v.currentTime, v.paused, v.readyState]);
};

window.lazShot = async (slug, seconds) => ({ slug, ...(await window.lazSeek(seconds)) });
