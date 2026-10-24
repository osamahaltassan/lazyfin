# Repository Agent Map

The agent reads the root `GLOSSARY.md` and uses its domain language.

## Issue Tracking

Before creating, finding, reading, or updating issues and specs, the agent reads `docs/agents/issue-tracker.md`. This repository tracks them in Kaneo.

The agent uses the `kaneo` CLI, already logged in to `~/.agents/kaneo-url.md` on this machine, for every Kaneo operation.

The order of the `To do` column on the Kaneo board is the repo's queue.

The agent uses the `gh` CLI, already logged in on this machine, for pull requests and every other GitHub operation.

## Agent Instructions

The agent follows `~/.agents/skills/writing-for-agents/SKILL.md` before it writes or edits a doc that agents read, such as `AGENTS.md`, `SKILL.md`, or a doc they point to.

When the agent makes a change, it judges the design with no limits, EVEN if the right answer is REWRITING the WHOLE app. If something should be done differently, the agent says so. The agent follows industry best practices and the `~/.agents/skills/codebase-design/SKILL.md` guidelines.

Big changes ARE welcomed by the user and the agent. But when the user or an agent proposes a change with a big impact or a hard problem, the agent explains it to the user and waits for confirmation before starting.

When the user or an agent suggests something the agent THINKS is wrong, the agent PUSHES BACK and explains WHY.

When the agent has dispatched a sub-agent, it replies to the user only after every sub-agent has reported back.

When the agent changes documentation, it follows `docs/agents/documentation-lifecycle.md` and uses `docs/README.md` as the map of active docs.

Before the agent edits copy, signals, manifests, or agent routes, or dispatches parallel work, it reads `docs/agents/recurring-traps.md`.

The agent uses the review standards in `CODING_STANDARDS.md` when the project has one. The agent applies the judgement rules in that file and leaves the checks listed there for the tooling to enforce.

When the user asks the agent to use the browser, it uses the built-in browser of its harness, unless the user EXPLICITLY says otherwise.

## Related Repos

- `~/git/jellyfin`: Jellyfin server
- `~/git/jellyfin-web`: Jellyfin Web
- `~/git/jellyfin-ios`: the official iPhone and iPad app
- `~/git/mpv`: the reference player for the RTL fix
- `~/git/subtitleedit`: Subtitle Edit, whose `libse` Jellyfin uses for text subtitles

The clones follow their default branch, ahead of the stable Jellyfin release that Lazyfin targets.

## Dependency Age

Every package, SDK, tool, GitHub Action, and Jellyfin build the agent pulls was released at least 7 days earlier. The agent checks the release date before it adds, installs, or bumps one, and picks the newest version that passes. Dependabot enforces the same 7-day cooldown.

## Docker

Docker on this machine is for testing only, and the disk is nearly full.

When the agent finishes a task that used Docker, it removes every container, image, volume, network, and build cache, then runs `docker system df` to confirm none of them remain.

## The Server

The agent uses `server-1` within the instructions listed in `~/.agents/server-1.md`.

## Code Documentation

Code Documentation explains how code is USED. Code Comments explain how code WORKS.

When the agent adds, rewrites, or fixes code, it places Code Documentation at the top of that code where it is suitable.

The agent writes Code Documentation, never Code Comments.

A Code Comment that starts with `Why:` records the reason the code beside it is written that weird way. The user writes `Why:` comments by hand. The agent NEVER writes, removes, or rewrites a `Why:` comment.

When the agent edits code beside a `Why:` comment, it keeps the behavior, flow, or logic that comment explains.

When the agent thinks the reason in a `Why:` comment no longer holds, it explains that to the user and WAITS for confirmation before changing the comment or the behavior, flow, or logic it explains.

## Planning and Operations

Before the agent implements a plan, it reads `docs/agents/feature-build.md` when the project has one.

Before the agent builds, tests, or releases Lazyfin, adds an enhancement, or adds a dependency, it reads `docs/development.md`.

## Changelog

When a change NEEDS a changelog entry, the agent adds it under `## [Unreleased]` at the top of the changelog.

When a new change is RELATED to an existing unreleased bullet, the agent merges it into that bullet, never creating a new one.

When the agent releases, it replaces `## [Unreleased]` with `## n.n.n - yyyy-mm-dd`.

## Final Replies to The User

The agent uses simple language, never mannered prose.

The user listens to the agent's replies through text-to-speech. The agent writes every reply the way it would say it out loud: short, spoken sentences in plain English, simple enough for someone who hasn't read the code.

When the agent explains a logic problem to the user, it starts with "Imagine" and walks through a concrete scenario.

The agent never writes in hard-wrap prose. It writes each paragraph on one line.

When the agent gives the user text to copy or reuse, it puts it in a fenced `text` code block, never in a blockquote.