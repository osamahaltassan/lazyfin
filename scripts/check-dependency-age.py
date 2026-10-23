#!/usr/bin/env python3
"""Fail when any NuGet package Lazyfin restores is less than 7 days old.

Usage: python3 scripts/check-dependency-age.py [--min-days N]

Reads every packages.lock.json in the repo, so it covers transitive packages too,
and asks nuget.org when each resolved version was published. Prints one line per
package that is too new and exits 1, or exits 0 when every package passes.
See AGENTS.md, Dependency Age.
"""

import argparse
import datetime
import gzip
import json
import pathlib
import sys
import urllib.request
from concurrent.futures import ThreadPoolExecutor

REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/{id}/{version}.json"


def fetch_json(url):
    with urllib.request.urlopen(url, timeout=30) as response:
        data = response.read()
    if data[:2] == b"\x1f\x8b":
        data = gzip.decompress(data)
    return json.loads(data)


def published(package):
    name, version = package
    leaf = fetch_json(REGISTRATION.format(id=name.lower(), version=version.lower()))
    entry = fetch_json(leaf["catalogEntry"])
    seconds = datetime.datetime.strptime(entry["published"][:19], "%Y-%m-%dT%H:%M:%S")
    return seconds.replace(tzinfo=datetime.timezone.utc)


def locked_packages(root):
    packages = set()
    for lock_file in root.rglob("packages.lock.json"):
        if "bin" in lock_file.parts or "obj" in lock_file.parts:
            continue
        for framework in json.loads(lock_file.read_text())["dependencies"].values():
            for name, info in framework.items():
                if info.get("type") != "Project":
                    packages.add((name, info["resolved"]))
    return sorted(packages)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--min-days", type=int, default=7)
    args = parser.parse_args()

    packages = locked_packages(pathlib.Path(__file__).resolve().parent.parent)
    now = datetime.datetime.now(datetime.timezone.utc)
    with ThreadPoolExecutor(max_workers=8) as pool:
        dates = list(pool.map(published, packages))

    too_new = [
        (name, version, date)
        for (name, version), date in zip(packages, dates)
        if now - date < datetime.timedelta(days=args.min_days)
    ]
    for name, version, date in too_new:
        print(f"{name} {version} was published {date:%Y-%m-%d}, less than {args.min_days} days ago")

    print(f"Checked {len(packages)} packages, {len(too_new)} too new.")
    return 1 if too_new else 0


if __name__ == "__main__":
    sys.exit(main())
