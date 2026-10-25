#!/usr/bin/env bash
# Runs jprm, the Jellyfin plugin repository manager, pinned for every Lazyfin build.
#
# Usage: scripts/jprm.sh zip <version> <output folder>
#            Builds lazyfin_<version>.zip from the repo this script sits in, creating the output
#            folder first, and prints the zip's path. Fails when no zip was made.
#        scripts/jprm.sh <jprm arguments>
#            Runs jprm with any other arguments, such as `repo init` or `repo add`.
#
# The Release workflow, CI, and `scripts/dev.sh release` all build through this script, so a zip
# that builds locally or in CI also builds for a release.
#
# Needs uv. jprm and its Python packages are limited to releases at least 7 days old. See AGENTS.md,
# Dependency Age.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
JPRM="jprm==1.1.0"

jprm() {
    local exclude_newer
    exclude_newer="$(python3 -I -c "
import datetime
print((datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=7)).strftime('%Y-%m-%dT%H:%M:%SZ'))
")"
    uvx --exclude-newer "$exclude_newer" "$JPRM" "$@"
}

build_zip() {
    local version="$1" output="$2" zip
    mkdir -p "$output"
    output="$(cd "$output" && pwd)"
    zip="$output/lazyfin_$version.zip"
    jprm plugin build "$REPO" --output "$output" --version "$version" --dotnet-framework net10.0 >&2
    if [[ ! -f "$zip" ]]; then
        printf 'jprm finished, but %s is missing.\n' "$zip" >&2
        return 1
    fi
    echo "$zip"
}

if [[ "${1:-}" == "zip" ]]; then
    if [[ $# -ne 3 ]]; then
        sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//' >&2
        exit 1
    fi
    build_zip "$2" "$3"
else
    jprm "$@"
fi
