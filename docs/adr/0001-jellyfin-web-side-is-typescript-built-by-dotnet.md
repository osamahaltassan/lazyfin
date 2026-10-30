# The Jellyfin Web side is TypeScript, built inside `dotnet build`

Lazyfin is a .NET plugin, yet its Jellyfin Web route ships code that runs in every Jellyfin Web the server serves. That code is TypeScript, compiled by Babel through Rollup to Jellyfin Web's own browserslist, and the build runs from an MSBuild target in the plugin's project, so `dotnet build` and `dotnet test` stay the only commands and Node becomes a build requirement. Jellyfin Web requires TypeScript for new files, so this keeps each Jellyfin Web plugin able to move upstream, and its browserslist reaches back to Chrome 27 and 38 on old webOS TVs, which load Jellyfin Web from the server and would fail to parse modern JavaScript.

## Considered Options

- **Plain JavaScript written to ES5, embedded with no build step.** No Node toolchain, but every Jellyfin Web plugin would need a rewrite to move upstream, and nothing checks its types or style.
- **Plain modern JavaScript.** No toolchain, but the Lazyfin script would fail to parse on the old TVs, so their viewers would get no enhancement.
- **esbuild.** Fast, but it cannot lower classes, arrow functions, or `let` to ES5, so it cannot reach Chrome 27 or 38.

## Consequences

- Node, pinned and at least 7 days old, is needed to build the plugin, locally and in CI. npm packages follow the same 7-day rule as NuGet packages, enforced by `scripts/check-dependency-age.py` reading `package-lock.json`.
- Each Jellyfin Web plugin is bundled on its own, so code two plugins share, such as the Jellyfin SDK, ships once per plugin. That cost is accepted until it matters; the fix then is a shared chunk inside the one Lazyfin script, with no change to the layout.
