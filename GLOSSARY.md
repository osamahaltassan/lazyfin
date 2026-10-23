# Lazyfin

Lazyfin is one Jellyfin plugin that fixes Jellyfin's user experience so that nobody has to configure it to get a good result. Admins install it from a Lazyfin plugin repository, and it is written so that any part of it could later move into Jellyfin or Jellyfin Web.

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
The Jellyfin server and Jellyfin Web projects that an enhancement may one day be merged into.
_Avoid_: Core, mainline

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

**Image subtitle**:
A subtitle track stored as pictures, such as PGS, which the RTL fix cannot change.
_Avoid_: Bitmap sub, graphic sub
