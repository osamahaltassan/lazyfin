# TV emulators in jw-dev for jellyfin-web pull request 8651

Can Samsung's Tizen TV emulator or LG's webOS TV emulator run inside the VM `jw-dev` and open Jellyfin Web? And do the emulator's `canPlayType` answers match a real TV's? Checked on 2026-11-03 for ticket LAZ-90. This branch is never merged. The file holds facts only, with no verdict on the review of [jellyfin-web pull request 8651](https://github.com/jellyfin/jellyfin-web/pull/8651).

## Answer

| Tool | Runs in jw-dev? | Same media answers as a real TV? |
| --- | --- | --- |
| Samsung TV emulator (Tizen Studio or Tizen SDK, TV Extension 9.0 or 10.0) | Not proven. It is QEMU with KVM, and jw-dev has nested KVM. But Samsung supports only Ubuntu hosts and says "Intel VTx". jw-dev is Debian 13 on an AMD Ryzen. It has no display device yet, and the emulator needs its GPU option on. Users report that the TV image hangs on AMD. | Not documented. Samsung lists media gaps (no 4K, no DRM, a seek bug). It also lists a different JavaScript engine for the emulator. It publishes no `canPlayType` comparison. |
| LG webOS TV Emulator 6.0 (webOS TV 6, 2021) | Not proven. LG requires VirtualBox 6.1. That version reached end of life in January 2024, and its last Linux host support is kernel 6.5, while Debian 13 runs kernel 6.12. A newer VirtualBox is untested by LG and conflicts with KVM on kernel 6.12. LG ships no emulator for webOS TV 22 or later. | Not documented. LG says the emulator plays HLS from version 5.0. It also lists audio differences from a real TV. |
| LG webOS TV Simulator (webOS TV 22 to 26) | Likely. It is a Linux x64 app that needs no VM, only a desktop session, and LG lists Ubuntu builds. | No. LG says its "video and audio specifications are different from actual TV devices". |

An emulator result is labelled evidence. It is not proof of what a real TV does. Jellyfin maintainers do test on webOS emulators and quote the results in pull requests. They also record cases where the emulator and a real TV disagree. Details are in [section 5](#5-precedent-in-jellyfin-reviews).

## 1. What the pull request asks of a TV

| Fact | Source |
| --- | --- |
| The pull request adds `canPlayAacInHls()`. When `canPlayNativeHls()` is true, it returns `audio.canPlayType('audio/mp4; codecs="mp4a.40.2"')`. Otherwise it returns `MediaSource.isTypeSupported` for the same string. The audio HLS transcoding profile is added only if this is true. | [pull request files](https://github.com/jellyfin/jellyfin-web/pull/8651/files) |
| `canPlayNativeHls()` returns `true` for `browser.tizen` without probing. On other browsers, webOS included, it probes `video.canPlayType('application/x-mpegURL')` and `'application/vnd.apple.mpegURL'`. | [browserDeviceProfile.js#L74-L82](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browserDeviceProfile.js#L74-L82) |
| Today's `master` already probes `mp4a.40.2` on TVs, through the video element. `canPlayH264` checks `video/mp4; codecs="avc1.42E01E, mp4a.40.2"`, and AAC-in-video checks `video/mp4; codecs="avc1.640029, mp4a.40.2"`. Neither check has a Tizen or webOS bypass. | [#L5-L7](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browserDeviceProfile.js#L5-L7), [#L556](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browserDeviceProfile.js#L556) |
| jellyfin-web already holds a webOS-specific override because "canPlayType lies about OPUS support". | [#L177-L180](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browserDeviceProfile.js#L177-L180) |

## 2. How the two Jellyfin TV apps report codec support

| Fact | Source |
| --- | --- |
| Jellyfin for Tizen bundles a built jellyfin-web `dist` into the `.wgt`. Its `getDeviceProfile` calls jellyfin-web's own profile builder with `{ enableMkvProgressive: false, enableSsaRender: true }`. It adds no codec list of its own, so every `canPlayType` probe runs on the TV or the emulator. | [gulpfile.babel.js#L10](https://github.com/jellyfin/jellyfin-tizen/blob/31408c121e34e73c9f50c4e201c05e95140675df/gulpfile.babel.js#L10), [tizen.js#L122-L125](https://github.com/jellyfin/jellyfin-tizen/blob/31408c121e34e73c9f50c4e201c05e95140675df/tizen.js#L122-L125) |
| Its README documents deploying to the emulator with `tizen install -n Jellyfin.wgt -t T-samsung-5.5-x86`. It has no note that emulator media behaviour differs. | [README.md#L84-L92](https://github.com/jellyfin/jellyfin-tizen/blob/31408c121e34e73c9f50c4e201c05e95140675df/README.md#L84-L92) |
| A user reported in March 2025 that the app installed and signed in on a `tv-samsung-9.0-x86` emulator. | [jellyfin-tizen#343](https://github.com/jellyfin/jellyfin-tizen/issues/343) |
| Jellyfin for webOS loads the server's hosted jellyfin-web in an iframe (`contentFrame.src = url`). To test pull request 8651 on webOS, the server must serve the pull request's build. | [index.js#L455-L510](https://github.com/jellyfin/jellyfin-webos/blob/ab4794046467cdb88212ccc29212300cf9112a43/frontend/js/index.js#L455-L510) |
| Its `getDeviceProfile` passes only `enableMkvProgressive`, `enableSsaRender`, and the Dolby Atmos, Dolby Vision, and HDR10 flags from `webOS.deviceInfo`. It adds no audio codec list. | [webOS.js#L74-L83](https://github.com/jellyfin/jellyfin-webos/blob/ab4794046467cdb88212ccc29212300cf9112a43/frontend/js/webOS.js#L74-L83) |
| Its README says to "Test the app on the emulator or ares-server or install it on your tv". It has no note that emulator media behaviour differs. | [README.md#L27](https://github.com/jellyfin/jellyfin-webos/blob/ab4794046467cdb88212ccc29212300cf9112a43/README.md#L27) |

## 3. Samsung Tizen TV emulator

### Host requirements

| Fact | Source |
| --- | --- |
| The emulator "is based on the open source QEMU project". An instance needs at least 1024 MB of RAM. Its "CPU VT" and "GPU" options must both be on, or "the emulator instance cannot be launched". | [TV Emulator](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-emulator.html) |
| On Linux, the x86 emulator uses KVM. Samsung says "No installation is required for KVM" on Ubuntu, and the CPU check accepts Intel VT-x or AMD-V. | [tizen-docs emulator.md](https://github.com/Samsung/tizen-docs/blob/master/docs/sdk-tools/baseline-sdk/common-tools/emulator.md) |
| The prerequisites page lists Ubuntu 20.04 and 18.04 (64-bit), 3 GB of RAM, and 6 GB of disk. It recommends Intel VT-x and the latest vendor GPU driver "for OpenGL ES acceleration". Its apt list for the emulator names Ubuntu packages such as `libpng12-0` and `libjpeg-turbo8`. | [tizen-docs prerequisites.md](https://github.com/Samsung/tizen-docs/blob/master/docs/sdk-tools/baseline-sdk/setup/prerequisites.md) |
| Tizen Studio 5.0 added Ubuntu 22.04. Tizen SDK 10.0 (November 2025) "officially supports Ubuntu 24.04 and macOS Sequoia". The same notes say "x86 emulators and related tools have been deprecated for Tizen-10.0 and later versions". No Debian release is listed anywhere. | [Tizen Studio 5.0 notes](https://samsungtizenos.com/release-notes/tizen-studio-5-0/), [Tizen SDK 10.0 notes](https://samsungtizenos.com/release-notes/tizen-sdk-10-0/) |
| The JDK is bundled from Tizen Studio 3.7, so no Java install is needed. | [tizen-docs prerequisites.md](https://github.com/Samsung/tizen-docs/blob/master/docs/sdk-tools/baseline-sdk/setup/prerequisites.md) |
| Debian 13 has no `libpng12-0`, `libjpeg-turbo8`, or `libwebkitgtk-1.0-0` package. It does have `libsdl1.2debian`, `libjpeg62-turbo`, `libv4l-0t64`, `libxcb-icccm4`, and `bridge-utils`. | [packages.debian.org (trixie)](https://packages.debian.org/trixie/libpng12-0) |
| TV Extension 10.0.0 (2026-02-26) ships a 64-bit "10.0 Emulator TV Platform" image, and the 32-bit image is deprecated. TV Extension 9.0.0 (2025-01-22) ships the 9.0 image. The Ubuntu image download is about 1.2 GB. | [release history](https://developer.samsung.com/smarttv/develop/tools/tv-extension/release-history.html), [download](https://developer.samsung.com/smarttv/develop/tools/tv-extension/download.html) |
| You accept a licence in the Tizen Studio installer and again for each extension. Samsung's install steps do not ask for a Samsung account. The Jellyfin Tizen README says a Samsung account is needed only if you use a Samsung certificate instead of a Tizen one. | [Installing TV SDK](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html), [jellyfin-tizen README](https://github.com/jellyfin/jellyfin-tizen/blob/31408c121e34e73c9f50c4e201c05e95140675df/README.md) |

### Known gaps against a real TV

| Fact | Source |
| --- | --- |
| TV Extension 9.0.0 and 10.0.0 list the same emulator limits: "4K video playback is not supported", "DRM (Digital Rights Management) is not supported", and "Seeking from a second video does not work before the first video is completed". | [release history](https://developer.samsung.com/smarttv/develop/tools/tv-extension/release-history.html) |
| Unsupported in the emulator: camera, gesture and voice, 3D, long-press, Bluetooth, UHD resolution, and secure WebSockets. Some ProductInfo and AVInfo methods are also missing. | [Emulator Limitations](https://developer.samsung.com/tv/develop/getting-started/using-sdk/tv-emulator/emulator-limitations) |
| Samsung's spec tables have a "TV Extension 9.0 Emulator" column. In it, HLS is v10 (the same as Tizen 8.0 to 10.0 TVs) and Widevine and PlayReady are "No". The JavaScript engine is listed as JSC for the emulator but V8 for every TV from Tizen 2.4 on. | [General Specifications, tables 2 and 3](https://developer.samsung.com/smarttv/develop/specifications/general-specifications.html) |
| Samsung's docs say nothing about `canPlayType` answers on the emulator compared with a TV. The audio element guide lists `canPlayType()` as a standard method and points to the media specifications. | [Playback Using Audio Elements](https://developer.samsung.com/smarttv/develop/guides/multimedia/media-playback/using-audio-elements.html) |
| jellyfin-web pull request 6408 fixed a focus bug that "only occurs on the TV itself and not on the Tizen emulator" (Tizen 8). | [jellyfin-web#6408](https://github.com/jellyfin/jellyfin-web/pull/6408) |

### AMD hosts (user reports, not Samsung docs)

| Fact | Source |
| --- | --- |
| A Jellyfin Tizen user on a Ryzen 2600 wrote in 2021 that the app "builds fine, but emulation doesn't work". | [jellyfin-tizen#61 comment](https://github.com/jellyfin/jellyfin-tizen/issues/61#issuecomment-775724513) |
| In Samsung's forum, users on Ryzen CPUs report that the TV image hangs at "Booting the kernel" while mobile images boot. A Samsung staff member called the AMD problem a graphics issue. | [Samsung forum thread 9380](https://forum.developer.samsung.com/t/tizen-tv-emulator-not-launching-haxm-not-detected/9380) |
| In jellyfin-web pull request 5688 (August 2024), a contributor wrote "I can't get Tizen Studio and the emulator to work". A maintainer answered that testing on webOS is "almost enough" for Tizen. | [comment](https://github.com/jellyfin/jellyfin-web/pull/5688#issuecomment-2295239123), [reply](https://github.com/jellyfin/jellyfin-web/pull/5688#issuecomment-2295269046) |

### Install steps (not run in jw-dev)

These steps come from Samsung's and Jellyfin's docs. They are untested on Debian 13 and AMD.

1. Download the Ubuntu 64-bit Tizen Studio installer from [Installing TV SDK](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html), then run it and accept the licence.
2. In Package Manager, open Extension SDK and install `TV Extensions-<version>` and "Samsung Certificate Extension" ([same page](https://developer.samsung.com/smarttv/develop/getting-started/setting-up-sdk/installing-tv-sdk.html)).
3. Install the Debian equivalents of the emulator libraries from the prerequisites list. Debian has no `libpng12-0` or `libjpeg-turbo8`.
4. Add the user to the `kvm` group. Start an X session, because the emulator needs a desktop and OpenGL.
5. In Emulator Manager, choose Create, then the `tv` image. Set RAM to 1024 MB or more, with CPU VT and GPU on, then Launch ([TV Emulator](https://developer.samsung.com/smarttv/develop/getting-started/using-sdk/tv-emulator.html)).
6. Build jellyfin-web from the pull request branch with `USE_SYSTEM_FONTS=1 npm run build:production`. Then run `JELLYFIN_WEB_DIR=<dist> npm ci` in jellyfin-tizen, followed by `tizen build-web` and `tizen package -t wgt`. Install with `tizen install -n Jellyfin.wgt -t <emulator target>` ([jellyfin-tizen README](https://github.com/jellyfin/jellyfin-tizen/blob/31408c121e34e73c9f50c4e201c05e95140675df/README.md)). You first need a certificate from Certificate Manager.
7. Open the app with "Debug As > Tizen Web Application" to get the Web Inspector, and read the probe values from the console (see [section 6](#6-the-probe-to-run)).

## 4. LG webOS TV emulator and simulator

### Emulator (webOS TV 6.0 and older)

| Fact | Source |
| --- | --- |
| The emulator "runs on the Oracle VM VirtualBox to emulate webOS TV". "From webOS TV 22, Emulator will not be provided." The newest image is 6.0.0, for webOS TV 6.x (2021). | [Emulator Introduction](https://webostv.developer.lge.com/develop/tools/emulator-introduction) |
| Linux host: "Ubuntu 14.04 or higher (64bit)", 3 GB of RAM or more. The prerequisite is "Virtualbox 6.1", linked to the old-builds page. LG gives no VirtualBox 7 guidance. The Linux 6.0 download is `Emulator_tv_linux_v6.0.0.zip` (1342 MB). Install with `vm_register.sh` and `create_desktop.sh`. | [Emulator Installation](https://webostv.developer.lge.com/develop/tools/emulator-installation) |
| VirtualBox 6.1 reached end of life on 2024-01-31, and 6.1.50 was its last release. The newest Linux host kernel listed in its changelog is 6.5 (in 6.1.46). | [endoflife.date](https://endoflife.date/virtualbox), [VirtualBox 6.1 changelog](https://www.virtualbox.org/wiki/Changelog-6.1) |
| Debian 13 ships the Linux 6.12 LTS kernel. jw-dev runs `6.12.113+deb13-cloud-amd64`. | [Debian 13 release announcement](https://www.debian.org/News/2025/20250809), jw-dev `uname -r` |
| VirtualBox 7.1.4 notes: "In kernel 6.12, KVM initializes virtualization on module loading by default. This prevents VirtualBox VMs from starting". The fix is `kvm.enable_virt_at_load=0` or unloading the `kvm_*` module. jw-dev's Android emulator, and any Tizen emulator, need that same KVM. | [VirtualBox 7.1 changelog](https://www.virtualbox.org/wiki/Changelog-7.1) |
| LG's streaming table: HLS is "Not supported" on emulator 1.0 to 4.0 and "Supported" on emulator 5.0 to 6.0. PlayReady and Widevine are "Not supported" on both the emulator and the simulator. | [Streaming Protocol and DRM](https://webostv.developer.lge.com/develop/specifications/streaming-protocol-drm) |
| Listed emulator gaps: "in real TV, you can only use one audio element in an app", while the emulator allows several. Media key codes are wrong on emulator 6.0. "There may be a difference in performance." | [Emulator Introduction](https://webostv.developer.lge.com/develop/tools/emulator-introduction) |
| webOS TV 6.x uses Chromium 79, and webOS TV 22 to 26 use Chromium 87 to 132. | [Web API and Web Engine](https://webostv.developer.lge.com/develop/specifications/web-api-and-web-engine) |

### Simulator (webOS TV 22 to 26)

| Fact | Source |
| --- | --- |
| The simulator gives "a web app development environment similar to a TV device on a PC" with "Chromium of the same version". "The video and audio specifications are different from actual TV devices." It "does not support DRM function", and some Luna and webOSSystem APIs are missing. | [Simulator Introduction](https://webostv.developer.lge.com/develop/tools/simulator-introduction) |
| There are Linux 64-bit builds for webOS TV 26 (1.5.0), 25, 24, 23, 22, and 6.0. The webOS TV 26 build supports "Ubuntu 16.04 or higher, including Ubuntu 24 (64bit only)". On Ubuntu 24 it runs as an AppImage with `--ozone-platform=x11 --no-sandbox`. | [Simulator Installation](https://webostv.developer.lge.com/develop/tools/simulator-installation) |
| Launch an app with `ares-launch -s <version> -sp <simulator path> <app dir>`. | [CLI Developer Guide](https://webostv.developer.lge.com/develop/tools/cli-dev-guide) |
| A 2024 jellyfin-webos bug on a webOS 4.4 TV could not be reproduced in the "webOS TV 22 Simulator". | [jellyfin-webos#236](https://github.com/jellyfin/jellyfin-webos/issues/236) |

### Install steps (not run in jw-dev)

Simulator (no VM needed):

1. Install the webOS TV CLI (`ares-*`) ([CLI Developer Guide](https://webostv.developer.lge.com/develop/tools/cli-dev-guide)).
2. Download the Linux zip for the webOS TV version you want from [Simulator Installation](https://webostv.developer.lge.com/develop/tools/simulator-installation) and unzip it.
3. In an X session, run `ares-launch -s <version> -sp <path> <jellyfin-webos>/frontend`. For webOS TV 26, start the AppImage by hand with `--ozone-platform=x11 --no-sandbox` and open the app from its UI.
4. Point the app at a server that serves the pull request's jellyfin-web build. The app loads jellyfin-web from the server.

Emulator 6.0 (VirtualBox):

1. Install VirtualBox. LG names 6.1, which predates kernel 6.12, so a 7.x release would be off LG's tested path.
2. Boot with `kvm.enable_virt_at_load=0`, or unload `kvm_amd` while VirtualBox runs. This stops the Android emulator from running at the same time.
3. Unzip `Emulator_tv_linux_v6.0.0.zip`, run `chmod`, `./vm_register.sh`, and `./create_desktop.sh`, then launch the emulator ([Emulator Installation](https://webostv.developer.lge.com/develop/tools/emulator-installation)).
4. Package jellyfin-webos with `ares-package --no-minify services frontend`. Install it with `ares-install` on the emulator target, then open it with `ares-inspect` ([jellyfin-webos README](https://github.com/jellyfin/jellyfin-webos/blob/ab4794046467cdb88212ccc29212300cf9112a43/README.md)).

## 5. Precedent in Jellyfin reviews

| Fact | Source |
| --- | --- |
| A jellyfin-web maintainer reproduced an `AudioContext` limit error "During testing on webOS emulators (3 and 4)" and merged the fix. | [jellyfin-web#4267](https://github.com/jellyfin/jellyfin-web/pull/4267) |
| A maintainer quoted a webOS 5 emulator console error as the reason a feature needs Chrome 69 or later. | [jellyfin-web#5688 comment](https://github.com/jellyfin/jellyfin-web/pull/5688#issuecomment-2291099137) |
| The same maintainer said he usually tests webOS 1 and webOS 5, and that webOS 3 "has glitchy emulator". | [jellyfin-web#5688 comment](https://github.com/jellyfin/jellyfin-web/pull/5688#issuecomment-2295269046), [jellyfin-webos#31 comment](https://github.com/jellyfin/jellyfin-webos/issues/31#issuecomment-1429323909) |
| On an audio-track change for webOS, the maintainer wrote "I can't test this (_no LG TV, emulator doesn't play sound_)" and asked the author to check on a real TV. In an earlier review he wrote "I can't test this in webOS 4 emulator - audio only works in music". | [jellyfin-web#5214 comment](https://github.com/jellyfin/jellyfin-web/pull/5214#issuecomment-1978818158), [jellyfin-web#5207 review](https://github.com/jellyfin/jellyfin-web/pull/5207#pullrequestreview-1887837887) |
| One bug fix states that the bug happened on the TV and not on the Tizen emulator. | [jellyfin-web#6408](https://github.com/jellyfin/jellyfin-web/pull/6408) |
| Neither Samsung nor LG publishes a statement that emulator or simulator `canPlayType` answers equal a TV's. LG says the simulator's video and audio specifications differ from a TV's. | [Simulator Introduction](https://webostv.developer.lge.com/develop/tools/simulator-introduction), [Emulator Limitations](https://developer.samsung.com/tv/develop/getting-started/using-sdk/tv-emulator/emulator-limitations) |

## 6. The probe to run

If an emulator does start, these lines in its Web Inspector console give the values the pull request depends on:

```js
document.createElement('video').canPlayType('application/x-mpegURL');
document.createElement('video').canPlayType('application/vnd.apple.mpegURL');
document.createElement('audio').canPlayType('audio/mp4; codecs="mp4a.40.2"');
window.MediaSource && MediaSource.isTypeSupported('audio/mp4; codecs="mp4a.40.2"');
navigator.userAgent;
```

## 7. jw-dev today

Read over SSH on 2026-11-03.

| Fact | Source |
| --- | --- |
| The CPU is an AMD Ryzen 9 5900X with `svm` and `npt` flags, and `/dev/kvm` is present. The kernel is Debian's cloud kernel `6.12.113+deb13-cloud-amd64`, there are 50 GB free on `/`, and there is 11 GiB of RAM. | `/proc/cpuinfo`, `ls /dev/kvm`, `df`, `free` |
| The VM has no display device on its PCI bus. The cloud kernel has no DRM and no sound support, so there is no `/dev/dri` or `/proc/asound`. Mesa (`libgl1-mesa-dri`) is installed. | `lspci`, `/boot/config-*` |
| jw-dev already runs Google's Android emulator, which is QEMU-based, headless with KVM. That shows nested KVM works for a QEMU emulator on this host. | `server-2` notes |
