# Viewer settings live in Jellyfin's display preferences, under Lazyfin's own name

A setting Lazyfin adds is a **viewer setting** unless its right value differs by device by nature, such as LAZ-46's "Don't sync"; then it is a **device setting**. A viewer setting is stored on the server in Jellyfin's own display preferences, in one set per enhancement: `displayPreferencesId` is the enhancement's name as in `Add<Enhancement>()`, and `client` is `lazyfin`. The Jellyfin Web plugin reads and writes it through `@jellyfin/sdk`'s `getDisplayPreferencesApi`, and the server route reads it through `IDisplayPreferencesManager.ListCustomItemDisplayPreferences` for the request's user ID, so Lazyfin adds no endpoint and no store of its own. A device setting is stored through the `appSettings` service Jellyfin Web hands every plugin, under a name that starts with `lazyfin-`, with the viewer's ID unless the choice holds for anyone on that device.

These are Jellyfin Web's own two places for a setting, `CustomPrefs` and local storage, so a setting moves upstream as a key: one Jellyfin Web reads goes into its `usersettings` set, one the server reads into the user's configuration, and upstream's migration copies each viewer's value without converting it.

## Considered Options

- **Jellyfin Web's own `usersettings` set, client `emby`.** Native clients such as Android TV and Roku read that set, and moving upstream would be free. But the server replaces a whole set on every save, and Jellyfin Web saves the copy it fetched at sign-in, so its next save erases any key Lazyfin wrote since. Its keys are also anchors under ADR 0002, and no native client would read a key it does not know anyway.
- **Lazyfin's own per-viewer store behind a Lazyfin endpoint**, such as a file or database in the plugin's data folder. Full control over validation and writes, but Lazyfin would own a schema, an endpoint, and their migrations, and upstream has no such store to move into.
- **Lazyfin's plugin configuration.** Only an admin may write it, and Lazyfin has no admin settings.
- **The device only, for every setting.** Simplest, but a viewer would set each choice again on every device, which is the very problem LAZ-46 fixes.

## Consequences

- A viewer setting applies to every client the enhancement's change reaches, native ones included, like Jellyfin's own server-side user settings. A native client gets the changed answer but shows no control for it, so the Jellyfin Web control's description says the choice applies in all the viewer's apps. When a change narrows by client name, its setting narrows with it; no setting is stored per client.
- A viewer setting shapes only that viewer's own answers, never access or another viewer's answers, because any client signed in as the viewer can write any string to the set.
- A setting nobody changed has no stored value, and any missing or unreadable value means the default. Choosing the default again removes the key, so a better default reaches everyone who never chose.
- Keys are camelCase like Jellyfin Web's `CustomPrefs`, and values use Jellyfin Web's string forms (`true`, an enum name, an invariant number). A setting that copies a Jellyfin Web setting keeps Jellyfin Web's key. Keys avoid the names the server moves out of `CustomPrefs`: `chromecastVersion`, `skipForwardLength`, `skipBackLength`, `enableNextVideoInfoOverlay`, `tvhome`, `dashboardTheme`, and anything starting with `homesection` or `landing-`.
- Because the server replaces a whole set on every save, a write fetches the set fresh, changes one key, and saves it at once, never from a cached copy. Two devices changing the same key at once keep the last one.
- The set and key names freeze at the first release that stores a value; renaming one after that needs a migration.
