<p align="center">
  <img src="assets/lazyfin-256.png" alt="Lazyfin icon" width="128" height="128">
</p>

# Lazyfin

Lazyfin is a Jellyfin plugin that adds a few small changes to how Jellyfin behaves for viewers. Each change is on as soon as Lazyfin is installed, with nothing to set up.

Each change is written so it could be offered to Jellyfin or Jellyfin Web later, if the Jellyfin team wants it.

## Install

Lazyfin supports Jellyfin 12.x.

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add this repository URL:

   ```text
   https://osamahaltassan.github.io/lazyfin/manifest.json
   ```

2. Open **Catalog**, pick **Lazyfin**, and install it.
3. Restart Jellyfin.

## Enhancements

- **RTL fix**: right-to-left subtitle lines, such as Arabic or Hebrew, show their end punctuation at the end of the line, the way mpv shows them.
- **Unlimited Continue Watching and Next Up**: Continue Watching, Continue Listening, Continue Reading, and Next Up list everything a viewer started, however long ago, on the home screen and inside each library, in every client.
- **Watched items in Latest**: new viewers keep watched items in the home screen's Latest rows.

## Development

See [docs/development.md](docs/development.md).

## License

GPL-2.0. See [LICENSE](LICENSE).
