#!/usr/bin/env bash
# Local Jellyfin test servers for Lazyfin development.
#
# Usage: scripts/dev.sh <command>
#
#   setup     Download the pinned server builds, start them, and finish their startup wizard
#             with the test admin below. Safe to run again.
#   start     Start every test server that is not running.
#   stop      Stop every running test server.
#   status    Show each server's URL and whether it is running.
#   install   Build Lazyfin, copy it into every server's plugins folder, and restart them.
#   release   Build the release zip and manifest exactly as CI does, serve them locally,
#             install Lazyfin through the plugin repository on every server, and check it loaded.
#
# Everything lives in .temp/servers, which git ignores. Delete that folder to start over.
# Test admin on every server: user "lazyfin", password "lazyfin".
#
# Every build in SERVERS was released at least 7 days before it was pinned. See AGENTS.md, Dependency Age.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ROOT="$REPO/.temp/servers"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# name|port|download URL
SERVERS=(
    "12.2|8122|https://repo.jellyfin.org/files/server/macos/stable/v12.2/arm64/jellyfin_12.2-arm64.tar.xz"
    "13.0-2026101212|8130|https://repo.jellyfin.org/files/server/macos/unstable/2026101212/arm64/jellyfin_2026101212-arm64.tar.xz"
)

ADMIN_USER="lazyfin"
ADMIN_PASSWORD="lazyfin"
AUTH_HEADER='MediaBrowser Client="lazyfin-dev", Device="dev", DeviceId="lazyfin-dev", Version="0.0.0"'
PLUGIN_GUID="e8fe2245-b033-4a98-9d02-f035bd7044bf"
REPO_PORT=8199

log() { printf '%s\n' "$*" >&2; }

server_field() { cut -d'|' -f"$2" <<<"$1"; }

server_dir() { echo "$ROOT/$(server_field "$1" 1)"; }

server_url() { echo "http://127.0.0.1:$(server_field "$1" 2)"; }

json_field() { python3 -I -c "import json,sys; print(json.load(sys.stdin)$1)"; }

is_running() {
    local pid_file
    pid_file="$(server_dir "$1")/server.pid"
    [[ -f "$pid_file" ]] && kill -0 "$(cat "$pid_file")" 2>/dev/null
}

wait_until_up() {
    local url
    url="$(server_url "$1")"
    for _ in $(seq 1 120); do
        if curl -sf "$url/System/Info/Public" 2>/dev/null | grep -q StartupWizardCompleted; then
            return 0
        fi
        sleep 1
    done
    log "Server $(server_field "$1" 1) did not answer at $url. See $(server_dir "$1")/log."
    return 1
}

download() {
    local dir url
    dir="$(server_dir "$1")"
    url="$(server_field "$1" 3)"
    if [[ -x "$dir/jellyfin/jellyfin" ]]; then
        return 0
    fi
    log "Downloading $(server_field "$1" 1)"
    mkdir -p "$dir"
    curl -sSLf "$url" | tar -xJ -C "$dir"
}

seed_port() {
    local config port
    config="$(server_dir "$1")/config"
    port="$(server_field "$1" 2)"
    if [[ -f "$config/network.xml" ]]; then
        return 0
    fi
    mkdir -p "$config"
    cat >"$config/network.xml" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<NetworkConfiguration xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <InternalHttpPort>$port</InternalHttpPort>
  <PublicHttpPort>$port</PublicHttpPort>
</NetworkConfiguration>
EOF
}

start_server() {
    local dir
    dir="$(server_dir "$1")"
    if is_running "$1"; then
        return 0
    fi
    seed_port "$1"
    mkdir -p "$dir/data" "$dir/cache" "$dir/log"
    nohup "$dir/jellyfin/jellyfin" \
        --datadir "$dir/data" \
        --configdir "$dir/config" \
        --cachedir "$dir/cache" \
        --logdir "$dir/log" \
        --webdir "$dir/jellyfin/jellyfin-web" \
        >"$dir/server.out" 2>&1 &
    echo $! >"$dir/server.pid"
    wait_until_up "$1"
    log "Started $(server_field "$1" 1) at $(server_url "$1")"
}

stop_server() {
    local pid_file
    pid_file="$(server_dir "$1")/server.pid"
    if ! is_running "$1"; then
        rm -f "$pid_file"
        return 0
    fi
    kill "$(cat "$pid_file")"
    while kill -0 "$(cat "$pid_file")" 2>/dev/null; do
        sleep 0.5
    done
    rm -f "$pid_file"
    log "Stopped $(server_field "$1" 1)"
}

finish_wizard() {
    local url
    url="$(server_url "$1")"
    if [[ "$(curl -sf "$url/System/Info/Public" | json_field "['StartupWizardCompleted']")" == "True" ]]; then
        return 0
    fi
    curl -sf -X POST "$url/Startup/Configuration" -H 'Content-Type: application/json' \
        -d '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}'
    curl -sf "$url/Startup/User" >/dev/null
    curl -sf -X POST "$url/Startup/User" -H 'Content-Type: application/json' \
        -d "{\"Name\":\"$ADMIN_USER\",\"Password\":\"$ADMIN_PASSWORD\"}"
    curl -sf -X POST "$url/Startup/RemoteAccess" -H 'Content-Type: application/json' \
        -d '{"EnableRemoteAccess":false}'
    curl -sf -X POST "$url/Startup/Complete"
    log "Finished the startup wizard on $(server_field "$1" 1)"
}

token() {
    curl -sf -X POST "$(server_url "$1")/Users/AuthenticateByName" \
        -H 'Content-Type: application/json' -H "Authorization: $AUTH_HEADER" \
        -d "{\"Username\":\"$ADMIN_USER\",\"Pw\":\"$ADMIN_PASSWORD\"}" | json_field "['AccessToken']"
}

api() {
    local server="$1" method="$2" path="$3"
    shift 3
    curl -sf -X "$method" "$(server_url "$server")$path" \
        -H "Authorization: $AUTH_HEADER, Token=\"$(token "$server")\"" "$@"
}

lazyfin_version() {
    api "$1" GET /Plugins | python3 -I -c "
import json, sys
for plugin in json.load(sys.stdin):
    if plugin['Id'].replace('-', '') == '${PLUGIN_GUID//-/}':
        print(plugin['Version'], plugin['Status'])
"
}

remove_lazyfin() {
    rm -rf "$(server_dir "$1")"/data/plugins/Lazyfin_*
}

cmd_setup() {
    for server in "${SERVERS[@]}"; do
        download "$server"
        start_server "$server"
        finish_wizard "$server"
    done
    cmd_status
}

cmd_start() {
    for server in "${SERVERS[@]}"; do
        start_server "$server"
    done
}

cmd_stop() {
    for server in "${SERVERS[@]}"; do
        stop_server "$server"
    done
}

cmd_status() {
    for server in "${SERVERS[@]}"; do
        if is_running "$server"; then
            log "$(server_field "$server" 1): running at $(server_url "$server")"
        else
            log "$(server_field "$server" 1): stopped"
        fi
    done
}

cmd_install() {
    dotnet build "$REPO/Jellyfin.Plugin.Lazyfin/Jellyfin.Plugin.Lazyfin.csproj" --configuration Debug --nologo -v quiet
    local dll="$REPO/Jellyfin.Plugin.Lazyfin/bin/Debug/net10.0/Jellyfin.Plugin.Lazyfin.dll"
    for server in "${SERVERS[@]}"; do
        stop_server "$server"
        remove_lazyfin "$server"
        mkdir -p "$(server_dir "$server")/data/plugins/Lazyfin_0.0.0.0"
        cp "$dll" "$(server_dir "$server")/data/plugins/Lazyfin_0.0.0.0/"
        start_server "$server"
        log "$(server_field "$server" 1): Lazyfin $(lazyfin_version "$server")"
    done
}

cmd_release() {
    local work="$REPO/.temp/release" version="0.0.1.0"
    rm -rf "$work"
    mkdir -p "$work/source" "$work/artifacts" "$work/pages"
    rsync -a --exclude .git --exclude .temp --exclude bin --exclude obj "$REPO/" "$work/source/"

    local exclude_newer
    exclude_newer="$(date -u -v-7d +%Y-%m-%dT%H:%M:%SZ)"
    (cd "$work/source" && uvx --exclude-newer "$exclude_newer" jprm==1.1.0 \
        plugin build . --output "$work/artifacts" --version "$version" --dotnet-framework net10.0 >/dev/null)
    uvx --exclude-newer "$exclude_newer" jprm==1.1.0 repo init "$work/pages" >/dev/null
    uvx --exclude-newer "$exclude_newer" jprm==1.1.0 repo add "$work/pages" "$work/artifacts/lazyfin_$version.zip" \
        --plugin-url "http://127.0.0.1:$REPO_PORT/lazyfin_$version.zip" >/dev/null
    cp "$work/artifacts/lazyfin_$version.zip" "$work/pages/"

    python3 -I -m http.server "$REPO_PORT" --bind 127.0.0.1 --directory "$work/pages" >/dev/null 2>&1 &
    local http_pid=$!
    trap 'kill $http_pid 2>/dev/null' RETURN

    local manifest_url="http://127.0.0.1:$REPO_PORT/manifest.json"
    for server in "${SERVERS[@]}"; do
        stop_server "$server"
        remove_lazyfin "$server"
        start_server "$server"
        api "$server" POST /Repositories -H 'Content-Type: application/json' \
            -d "[{\"Name\":\"Lazyfin (local)\",\"Url\":\"$manifest_url\",\"Enabled\":true}]"
        api "$server" POST "/Packages/Installed/Lazyfin?assemblyGuid=$PLUGIN_GUID&version=$version&repositoryUrl=$manifest_url"
        for _ in $(seq 1 30); do
            if compgen -G "$(server_dir "$server")/data/plugins/Lazyfin_*/Jellyfin.Plugin.Lazyfin.dll" >/dev/null; then
                break
            fi
            sleep 1
        done
        stop_server "$server"
        start_server "$server"
        log "$(server_field "$server" 1): Lazyfin $(lazyfin_version "$server")"
    done
}

case "${1:-}" in
    setup | start | stop | status | install | release) "cmd_$1" ;;
    *)
        sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'
        exit 1
        ;;
esac
