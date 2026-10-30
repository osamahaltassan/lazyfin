# Running code

Code is written on the Mac and runs in `lf-dev`, a Debian 13 VM on `server-2`. Every build, test, test server, and release check happens in `lf-dev`. The Mac has almost no free disk.

`~/.agents/server-2.md` holds the VM's ID, address, and login.

## Run a command

From inside a worktree on the Mac, run `~/.agents/bin/lf-remote <command>`, for example `lf-remote dotnet test Lazyfin.slnx` or `lf-remote scripts/dev.sh install`. The script's header lists every option.

- Each call copies the worktree's tracked and untracked-but-not-ignored files to `~/lf-dev/<worktree folder name>` in the VM, deletes there what the Mac deleted, and runs the command. `bin/`, `obj/`, `.temp/`, and `node_modules/` stay in the VM between calls, so builds stay incremental. The exit code is the command's.
- Files the VM makes come back only with `--pull <path>`. Pull every lock file and generated file you commit, such as a `packages.lock.json` a restore changed.
- When a worktree's work is merged, `lf-remote --clean` deletes its folder in the VM.

## What `lf-dev` holds

The .NET SDK pinned in `global.json`, in `~/.dotnet`; uv; Python 3; git; rsync; and fontconfig with the DejaVu fonts, without which Jellyfin's image drawing stops the server at startup. `~/.profile` puts them on `PATH`.

A change to what it holds updates this list in the same change.

## Test servers

The two Jellyfin test servers that `scripts/dev.sh` runs live in `~/lazyfin-dev` in the VM, outside every worktree, so all worktrees share one pair. `lf-remote scripts/dev.sh install` puts the calling worktree's build on them, so one session at a time installs and tests on them. The Mac's browser opens them on the VM's address, which `lf-remote scripts/dev.sh status` prints. [development.md](../development.md) covers each `dev.sh` command.

## Worktrees on the Mac

The desktop app needs several gigabytes free to make a session worktree, and the Mac lacks them. Make each worktree with git instead, then start the session in that folder:

```bash
git worktree add .claude/worktrees/<name> -b <branch> <base branch>
```
