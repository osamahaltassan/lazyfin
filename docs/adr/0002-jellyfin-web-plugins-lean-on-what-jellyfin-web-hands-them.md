# Jellyfin Web plugins lean only on what Jellyfin Web hands them

A Jellyfin Web plugin runs inside a Jellyfin Web that changes with every release, and each one must be able to move upstream. So it leans freely only on what Jellyfin Web hands a plugin: the plugin types, the twelve services its constructor receives, those services' events, Jellyfin Web's existing string keys, and the Jellyfin API. Anything else it reads or changes in Jellyfin Web, such as a page's elements, a route, a view event, a storage key, or a theme CSS variable, is an **anchor**: a last resort, named in the plugin's one `anchors.ts`, checked by an end-to-end test on every tested server, and skipped with one console log when it is missing. It never wraps or replaces Jellyfin Web's functions, reads globals Jellyfin Web did not hand it, or removes or moves Jellyfin Web's elements.

API calls use `@jellyfin/sdk`'s `get<Name>Api(api)` helpers on the `Api` Jellyfin Web already built, from `ServerConnections.getApi(serverId)`, taken at each call. This is how upstream's own code calls the API, so the calls move upstream unchanged, and Jellyfin Web keeps owning the server address, token, and device. The plugin bundles only the SDK's generated API classes and types it imports, never a second `Jellyfin` or `Api` of its own.

## Considered Options

- **Bundle the whole SDK and build a plugin's own `Api` from the legacy ApiClient.** Matches upstream's rule on paper, but it ships the SDK and axios once per plugin and keeps a second copy of the token that goes stale when the viewer signs out or switches server.
- **Call the API through the legacy ApiClient.** Smallest bundle, but upstream's CONTRIBUTING.md forbids it for new code, so every call would be rewritten on the way upstream.
- **Map upstream's import paths, such as `components/confirm/confirm`, onto the handed services at build time.** Moving upstream would be a plain copy, but the build would hide which modules are really available, and a plugin could import one Jellyfin Web never hands over.
- **Patch Jellyfin Web freely, by wrapping its functions or editing any element.** Reaches more screens, but it breaks silently on updates, collides with other plugins such as JavaScript Injector scripts, and nothing of it can move upstream.

## Consequences

- An enhancement that changes a screen no plugin type reaches keeps its logic in modules that touch no anchor, so that logic moves upstream as is. Its anchors are Lazyfin's own, like the loader, and are replaced by a direct change to that screen upstream.
- A new string goes in the plugin's own `strings/<locale>.json`, in Jellyfin Web's format, read through a Lazyfin helper with `globalize.translate`'s signature, because Jellyfin Web loads a plugin's translations only from its own `strings` folder.
- ADR 0001's accepted cost, the SDK shipping once per plugin, shrinks to the generated API classes each plugin imports.
