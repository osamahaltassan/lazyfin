<p align="center">
  <img src="assets/lazyfin-256.png" alt="Lazyfin icon" width="128" height="128">
</p>

# Lazyfin

Lazyfin is one Jellyfin plugin that fixes parts of Jellyfin's user experience. Every enhancement is on as soon as Lazyfin is installed, so viewers get a good result without configuring anything.

Lazyfin is written so that any enhancement could later move into Jellyfin or Jellyfin Web with little rework.

## Install

Lazyfin supports Jellyfin 12.x.

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add this repository URL:

   ```text
   https://osamahaltassan.github.io/lazyfin/manifest.json
   ```

2. Open **Catalog**, pick **Lazyfin**, and install it.
3. Restart Jellyfin.

The repository goes live with the first release.

## Enhancements

- **RTL fix**: right-to-left subtitle lines, such as Arabic or Hebrew, show their end punctuation at the end of the line, the way mpv shows them.

## Development

See [docs/development.md](docs/development.md).

## License

GPL-2.0, the same as Jellyfin. See [LICENSE](LICENSE).
