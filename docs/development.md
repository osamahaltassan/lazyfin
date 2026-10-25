# Development

How to build, test, and release Lazyfin, and where a new enhancement goes.

## Tools

- The .NET SDK pinned in `global.json`. Install it with Microsoft's `dotnet-install.sh --version <that version> --install-dir ~/.dotnet`.
- `uv`, for the release packaging tool `jprm`.
- Python 3, for the scripts in `scripts/`.

## Build and test

```bash
dotnet build Lazyfin.slnx
dotnet test Lazyfin.slnx
```

Warnings fail the build. The analyzers, `.editorconfig`, `stylecop.json`, and `BannedSymbols.txt` are the Jellyfin server's own, so code that passes here already follows upstream style.

## Local test servers

`scripts/dev.sh` runs two Jellyfin servers from `.temp/servers`:

| Server | URL | Purpose |
| --- | --- | --- |
| 12.2 | <http://127.0.0.1:8122> | The newest stable release that passes the 7-day rule |
| 13.0-2026101212 | <http://127.0.0.1:8130> | The upcoming major version |

Both have the test admin `lazyfin`, password `lazyfin`.

| Command | What it does |
| --- | --- |
| `scripts/dev.sh setup` | Downloads both servers and ffmpeg, starts the servers, and finishes their startup wizard. Run it once. |
| `scripts/dev.sh install` | Builds Lazyfin, installs it on both servers, and restarts them. This is the edit loop. |
| `scripts/dev.sh release` | Builds the release zip and manifest the way CI does, then installs Lazyfin through a local plugin repository on both servers. |
| `scripts/dev.sh start`, `stop`, `status` | Controls the servers. |

Both servers run Jellyfin's own ffmpeg, the Apple silicon build of `jellyfin-ffmpeg` pinned in `FFMPEG` at the top of `scripts/dev.sh`, so transcoding and burn-in work. The script downloads it once, checks it against its pinned SHA-256 digest, and unpacks it into `.temp/ffmpeg/<version>`, which both servers share. It starts every server with Jellyfin's own `--ffmpeg` startup option pointing there, so each server's Playback settings page shows that path. A server that is already running picks up ffmpeg on its next restart, such as `install`, `release`, or `stop` then `start`.

To test a newer server build, change its line in `SERVERS` at the top of `scripts/dev.sh`, and delete its folder in `.temp/servers`.

To bump ffmpeg, pick a `jellyfin/jellyfin-ffmpeg` release that is at least 7 days old, and change `FFMPEG` to its version, the URL of its `portable_macarm64-gpl` asset, and the SHA-256 digest GitHub shows for that asset. The new version lands in its own folder on the next start, and you can delete the old one.

## Adding an enhancement

1. Create `Jellyfin.Plugin.Lazyfin/Enhancements/<Enhancement>/`, with the namespace `Jellyfin.Plugin.Lazyfin.Enhancements.<Enhancement>`.
2. Give it one `Add<Enhancement>(this IServiceCollection services)` extension method that registers everything it needs.
3. Call that method from `PluginServiceRegistrator.RegisterServices`.
4. Put its tests in `tests/Jellyfin.Plugin.Lazyfin.Tests/Enhancements/<Enhancement>/`.
5. Add a line for it under **Enhancements** in `README.md`, and a changelog entry.

Each enhancement depends only on Jellyfin and on its own folder. That keeps each one small enough to lift into upstream on its own.

## Dependencies

Every package, SDK, Action, and server build must have been released at least 7 days before it is added or bumped.

- `scripts/check-dependency-age.py` checks every NuGet package in the lock files, transitive ones included. CI runs it on every push.
- Dependabot opens bumps only after the same 7-day cooldown.
- The release workflow runs `jprm` with `uvx --exclude-newer`, so its Python packages follow the rule too.

Lazyfin references `Jellyfin.Controller` and `Jellyfin.Model` 12.0.0, the oldest 12.x release. Jellyfin loads a plugin only on servers at or above its `targetAbi`, so building against 12.0.0 is what lets Lazyfin load on every 12.x server. `targetAbi` in `build.yaml` must equal that version, and a test checks it. Dependabot skips Jellyfin packages, because raising them drops support for older servers.

## Releasing

1. Move the `## [Unreleased]` changelog entries under `## X.Y.Z - yyyy-mm-dd`.
2. Publish a GitHub Release with the tag `vX.Y.Z`.
3. On the first release only, once the `Release` workflow has created the `gh-pages` branch, turn on GitHub Pages for the root of `gh-pages`, so the repository URL in `README.md` works. GitHub refuses to turn on Pages for a branch that does not exist yet.

The `Release` workflow tests and builds `lazyfin_X.Y.Z.0.zip`, attaches it to the release, and adds that version to `manifest.json` on the `gh-pages` branch, creating the branch on the first release. `jprm` packs the icon named by `image` in `build.yaml` into the zip and publishes it on `gh-pages` beside the manifest, so the catalog can show it. A prerelease gets its zip but stays out of the manifest.
