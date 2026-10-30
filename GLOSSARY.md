# Lazyfin

Lazyfin is one Jellyfin plugin that fixes Jellyfin's user experience so that nobody has to configure it to get a good result. Admins install it from a Lazyfin plugin repository, and it is written so that any part of it could later move upstream.

## Language

**Lazyfin**:
The single Jellyfin plugin this project builds, and the project itself.
_Avoid_: Gilliffin, lazy plugin, Lazyfin plugins

**Enhancement**:
One user-experience fix that Lazyfin ships, such as the RTL fix.
_Avoid_: Feature, module, mod

**Viewer**:
A person who watches media through a Jellyfin client.
_Avoid_: User, watcher

**Admin**:
A person who runs the Jellyfin server and installs Lazyfin on it.
_Avoid_: Owner, operator

**Plugin repository**:
The manifest URL an admin adds under Jellyfin's plugin repositories to install and update Lazyfin.
_Avoid_: Catalog, store, feed

**Upstream**:
The official Jellyfin projects that an enhancement may one day be merged into: the Jellyfin server, Jellyfin Web, and the official Jellyfin clients.
_Avoid_: Core, mainline

## Reaching clients

**Server route**:
The way Lazyfin changes what the Jellyfin server receives, sends, and stores, which reaches every client.
_Avoid_: Server side, backend route, API route

**Jellyfin Web route**:
The way Lazyfin changes the Jellyfin Web code a viewer's browser or app runs.
_Avoid_: Client route, web injection

**Jellyfin Web plugin**:
A piece of code that Jellyfin Web's own plugin system loads into Jellyfin Web. An enhancement that changes Jellyfin Web defines one or more.
_Avoid_: plugin or web plugin alone, when it means this rather than a Jellyfin server plugin

**Lazyfin script**:
The one script Lazyfin adds to Jellyfin Web, which carries the Jellyfin Web plugin of every enhancement.
_Avoid_: Injector, loader script, custom JavaScript

## Settings

**Viewer setting**:
A choice an enhancement lets a viewer make that follows them to every device and app they sign in on.
_Avoid_: User setting, preference, server setting

**Device setting**:
A choice an enhancement lets a viewer make that stays on the one device and app they made it on, because its right value differs by device.
_Avoid_: Local setting, app setting

## Subtitles

**RTL fix**:
The enhancement that makes punctuation in right-to-left subtitle lines, such as Arabic, show at the end of the line instead of the start.
_Avoid_: RTL flip, punctuation fix, bidi fix

**Burn-in**:
Drawing a subtitle into the video picture during a transcode, so the client shows it as part of the video.
_Avoid_: Hardsub, encoding subtitles

**Text subtitle**:
A subtitle track stored as text, such as SRT, VTT, or ASS, which the RTL fix can change.
_Avoid_: Soft sub

**RTL line**:
A subtitle line whose first letter belongs to a right-to-left script, such as Arabic or Hebrew. The RTL fix lays out each RTL line right to left.
_Avoid_: Arabic line, RTL text

**Image subtitle**:
A subtitle track stored as pictures, such as PGS, which the RTL fix cannot change.
_Avoid_: Bitmap sub, graphic sub
