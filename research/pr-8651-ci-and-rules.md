# Pull request 8651: CI, tests, and the project's rules

Facts a reviewer checks outside the diff of [jellyfin-web pull request 8651](https://github.com/jellyfin/jellyfin-web/pull/8651), "Check AAC support before selecting audio HLS transcoding". Checked on 2026-11-03. This branch is never merged. The file holds facts only, with no verdict on the review.

## The pull request

| Fact | Source |
| --- | --- |
| One commit, `efa22f6f8e`, "Check AAC capability before offering audio HLS transcoding", with no commit body. | [commit](https://github.com/jellyfin/jellyfin-web/pull/8651/commits/efa22f6f8e13e7e859f543512fa16b1b4444defd) |
| Author `polydeuxes`, branch `fix/audio-aac-capability-gate`, base `master`. Opened 2026-10-13, last force-pushed 2026-10-20. | [pull request](https://github.com/jellyfin/jellyfin-web/pull/8651) |
| Its merge base with `master` is `fc80baea16` (2026-10-09). | [fc80baea16](https://github.com/jellyfin/jellyfin-web/commit/fc80baea16a8e9a29d1cb34c9a9a014988b41cb4) |
| It changes one file, `src/scripts/browserDeviceProfile.js`, by 11 lines added and 1 removed. It adds `canPlayAacInHls()` and adds that check to the `if` that pushes the audio HLS transcoding profile (line 883 on today's `master`). | [files](https://github.com/jellyfin/jellyfin-web/pull/8651/files), [line on master](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browserDeviceProfile.js#L883) |
| GitHub reports it as `MERGEABLE`, merge state `BLOCKED`, review decision `REVIEW_REQUIRED`, no labels, no reviews. | [pull request](https://github.com/jellyfin/jellyfin-web/pull/8651) |

## 1. CI checks

| Fact | Source |
| --- | --- |
| Two checks ran on head `efa22f6f8e`, and both passed: "SonarCloud" and "SonarCloud Code Analysis". The SonarCloud bot comment says the quality gate passed. | [SonarCloud run](https://github.com/jellyfin/jellyfin-web/runs/118791393792), [SonarCloud dashboard](https://sonarcloud.io/dashboard?id=jellyfin_jellyfin-web&pullRequest=8651) |
| The repo's "Pull Request" workflow did not run. Its run for this head ended `action_required` on 2026-10-20, which is GitHub waiting for a maintainer to approve workflows for an outside contributor. | [workflow run 39469889949](https://github.com/jellyfin/jellyfin-web/actions/runs/39469889949) |
| That workflow is the one that would build, run CodeQL, run the eslint suggestions, and run the quality checks. | [`pull_request.yml`](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/workflows/pull_request.yml#L15-L42) |
| The quality checks it would run are `build:es-check`, `lint`, `stylelint`, `build:check`, and `test` (Vitest), plus a dependency review. None of them has run on this pull request. | [`__quality_checks.yml`](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/workflows/__quality_checks.yml#L28-L41) |
| The commit's combined status is `pending`, with no status contexts. | [commit statuses API](https://api.github.com/repos/jellyfin/jellyfin-web/commits/efa22f6f8e13e7e859f543512fa16b1b4444defd/status) |

## 2. Tests

| Fact | Source |
| --- | --- |
| `master` has 15 test files (`*.test.ts`). None of them imports or names `browserDeviceProfile`. The nearest one is `src/scripts/browser.test.ts`, which tests the browser detection module. | [`src/scripts`](https://github.com/jellyfin/jellyfin-web/tree/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts), [`browser.test.ts`](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/scripts/browser.test.ts) |
| `browserDeviceProfile.js` is imported only by `src/components/apphost.js`, `src/plugins/htmlAudioPlayer/plugin.js`, and `src/plugins/htmlVideoPlayer/plugin.js`. | [`apphost.js`](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/src/components/apphost.js) |
| The pull request adds or changes no test. Its only file is `src/scripts/browserDeviceProfile.js`. | [files](https://github.com/jellyfin/jellyfin-web/pull/8651/files) |
| CONTRIBUTING says new code "**SHOULD** be covered by unit tests when possible (legacy component/view code is deemed untestable)". | [CONTRIBUTING.md#L16](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/CONTRIBUTING.md#L16) |

## 3. The pull request template

The template is `.github/pull_request_template.md`. It is the same at the pull request's base `fc80baea16` and on today's `master`; its last change was `f75f701651` on 2026-09-18.

| Fact | Source |
| --- | --- |
| It asks for a short imperative title. | [template#L1-L4](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/pull_request_template.md#L1-L4) |
| `### Changes`: describe the changes in 1 to 5 sentences, with before and after screenshots of any UI change. | [template#L6-L10](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/pull_request_template.md#L6-L10) |
| `### Issues`: tag the issues the pull request solves, as `Fixes #`. | [template#L12-L16](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/pull_request_template.md#L12-L16) |
| `### Code assistance`: say whether AI or LLM help was used, and list "the exact tool, model, and version used and the specific actions that it performed". It adds: "Do NOT editorialize on code ownership, confidence, or any other aspect of LLM usage." | [template#L18-L24](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/pull_request_template.md#L18-L24) |
| Four boxes: read and followed the contributing guidelines; tested the changes; checked it does not duplicate an existing pull request; gave a substantive review of another web pull request. | [template#L28-L31](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/pull_request_template.md#L28-L31) |
| CONTRIBUTING says a pull request "**MUST** fully complete the PR template. Failing to do so will result in the PR being closed as invalid without review." | [CONTRIBUTING.md#L34](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/CONTRIBUTING.md#L34) |

What the body of pull request 8651 has ([body](https://github.com/jellyfin/jellyfin-web/pull/8651)):

| Template part | In the body |
| --- | --- |
| `### Changes` heading | No heading. Two paragraphs describe the bug and the fix. |
| `### Issues` heading | No heading and no linked issue. The author linked the pull request later in a comment on [issue 8752](https://github.com/jellyfin/jellyfin-web/issues/8752#issuecomment-6576294496). |
| `### Code assistance` heading | No heading. One line: "Claude Opus 5.5 and GPT-6.1-Sol performed the research and work with my review." |
| Box: contributing guidelines | Absent. |
| Box: tested | Absent. |
| Box: not a duplicate | Absent. |
| Box: reviewed another pull request | Absent. |

The body contains none of the template's headings or checkboxes.

## 4. The LLM policy

CONTRIBUTING says a pull request "**MUST** follow the [LLM development policy](https://jellyfin.org/docs/general/contributing/llm-policies)" ([CONTRIBUTING.md#L32](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/CONTRIBUTING.md#L32)). The policy's source is [`llm-policies.md`](https://github.com/jellyfin/jellyfin.org/blob/bef6e2d2f360557a221d7d8382156e4b62bf2d2b/docs/general/contributing/llm-policies.md), last changed 2026-01-23.

What the policy requires, in short quotes:

| Rule | Source |
| --- | --- |
| LLM output is "expressly prohibited" for direct communication, including "pull request bodies or comments". | [General Guidelines 1](https://jellyfin.org/docs/general/contributing/llm-policies/#general-guidelines) |
| Code changes must be "concise and focused", and a large pull request is split into small commits. | [Code Contributions 1](https://jellyfin.org/docs/general/contributing/llm-policies/#llm-code-contributions-to-official-projects) |
| The author must "review the output" and "explain" in the body "what is being changed and why", in their own words. | [Code Contributions 3](https://jellyfin.org/docs/general/contributing/llm-policies/#llm-code-contributions-to-official-projects) |
| The changes must be "tested", and the author should "explicitly test the functionality being modified". | [Code Contributions 4](https://jellyfin.org/docs/general/contributing/llm-policies/#llm-code-contributions-to-official-projects) |
| The author must "handle review feedback" themselves. | [Code Contributions 5](https://jellyfin.org/docs/general/contributing/llm-policies/#llm-code-contributions-to-official-projects) |

The policy page itself has no rule to declare model use in a pull request. Its only declaration rule is for LLM-assisted translations. The declaration rule comes from the template's `### Code assistance` section: exact tool, model, version, and the actions it performed, with no editorializing.

How the body lines up, fact by fact:

| Requirement | In the body |
| --- | --- |
| Tool and model named (template) | Two models are named, "Claude Opus 5.5" and "GPT-6.1-Sol". No tool or harness is named. |
| Version (template) | The model names carry version numbers. No separate version is given. |
| Specific actions (template) | Both models together "performed the research and work". The actions are not split per model. |
| No editorializing (template) | The line ends "with my review". |
| Explain what changes and why (policy 3) | The body says which builds fail (Chromium without proprietary codecs), at what bitrate (32 kb/s), the error (`manifestIncompatibleCodecsError`), what the fix checks, and the fallback (MP3 ahead of Opus). |
| Tested (policy 4, CONTRIBUTING line 33) | The body does not say how or whether the change was tested, and the "tested" box is absent. |
| Focused (policy 1) | One file, one commit. |

## 5. Merging onto `master` and `release-12.z`

Checked with `git merge-tree --write-tree` on a fresh fetch, without a working tree.

| Fact | Source |
| --- | --- |
| Today's `master` is `c710e5c277` (2026-11-03). It is 156 commits past the pull request's base. None of those commits touch `browserDeviceProfile.js`. | [master](https://github.com/jellyfin/jellyfin-web/commit/c710e5c2773f4bdf007607efe2120139708ff3ed) |
| The branch merges onto `master` with no conflict. | [compare](https://github.com/jellyfin/jellyfin-web/compare/master...polydeuxes:jellyfin-web:fix/audio-aac-capability-gate) |
| `release-12.z` is `0a580ce8df`, "Bump version to 12.4" (2026-10-26). The pull request's base `fc80baea16` is not in `release-12.z`. | [release-12.z](https://github.com/jellyfin/jellyfin-web/commits/release-12.z) |
| Merging the whole branch into `release-12.z` conflicts in `package.json` and `package-lock.json`. Those conflicts come from `master` history under the branch, not from the fix. | [release-12.z](https://github.com/jellyfin/jellyfin-web/commits/release-12.z) |
| The one commit alone, rebased onto `release-12.z` as CONTRIBUTING describes, applies with no conflict. `release-12.z` has two commits on that file since the fork, `0f87efad60` "Drop OPUS in HLS-TS on Tizen" and `2c14c06904` "Drop OPUS in MP4 and TS on Tizen"; the fix still applies over them. | [0f87efad60](https://github.com/jellyfin/jellyfin-web/commit/0f87efad60), [2c14c06904](https://github.com/jellyfin/jellyfin-web/commit/2c14c06904) |
| Which branch bug fixes target: the Jellyfin development guide says "Except for emergency release hotfixes, all PRs should target `master`." | [development.md#L134](https://github.com/jellyfin/jellyfin.org/blob/62a22ad2032e1cf746b63bf74c48f30c87e7c4cf/docs/general/contributing/development.md?plain=1#L134) |
| CONTRIBUTING adds that an author "may be asked" to retarget a release branch "as part of our patch / bug-fix release process", and gives the `git rebase --onto` steps. | [CONTRIBUTING.md#L38-L44](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/CONTRIBUTING.md#L38-L44) |
| CONTRIBUTING also says a pull request "**SHOULD NOT** update from `master` needlessly once opened (only update if conflicts exist)". | [CONTRIBUTING.md#L36](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/CONTRIBUTING.md#L36) |
| The "Pull Request" workflow runs on pull requests to `master` and `release*`. | [`pull_request.yml#L7-L11`](https://github.com/jellyfin/jellyfin-web/blob/c710e5c2773f4bdf007607efe2120139708ff3ed/.github/workflows/pull_request.yml#L7-L11) |

## 6. Other open pull requests and issues

The search `browserDeviceProfile` over open pull requests finds only [#8332](https://github.com/jellyfin/jellyfin-web/pull/8332). The list of 248 open pull requests, filtered on the file path, finds seven others that change `src/scripts/browserDeviceProfile.js`. None of them changes the audio HLS transcoding profile block that #8651 changes. Each one merges together with #8651 with no conflict (`git merge-tree` of both heads).

| Pull request | State | What it changes in the file |
| --- | --- | --- |
| [#8707](https://github.com/jellyfin/jellyfin-web/pull/8707) Fix FLAC channel restriction on modern WebOS | open, 2026-10-24 | The webOS FLAC codec profile and the FLAC split of video transcoding profiles, near line 1060. Audio, but a different block. |
| [#8332](https://github.com/jellyfin/jellyfin-web/pull/8332) add WebKitGTK browser detection and device profile | open, labels `stale`, `merge conflict` | Browser checks for WebKitGTK in MKV, video container, audio channel, and profile code. |
| [#8273](https://github.com/jellyfin/jellyfin-web/pull/8273) Add HDR passthrough setting and report the transcode video range | open, 2026-07-28 | HDR video checks. |
| [#7329](https://github.com/jellyfin/jellyfin-web/pull/7329) Enable AC-4 audio codec if supported | draft | Adds AC-4 to the video audio codec lists, including HLS. |
| [#7049](https://github.com/jellyfin/jellyfin-web/pull/7049) Enable HEVC for MPEG-TS HLS over hls.js 1.6+ | draft | HEVC in the HLS video codec lists. |
| [#6595](https://github.com/jellyfin/jellyfin-web/pull/6595) Disable HEVC in Hisense Vidaa OS | open, labels `question`, `playback` | `canPlayHevc`. |
| [#5233](https://github.com/jellyfin/jellyfin-web/pull/5233) Single audio policy | draft, labels `enhancement`, `backend`, `playback` | AAC codec profile conditions for secondary audio and a `SingleAudioPolicy`. |

Open issues from the search `aac hls audio` that are about audio transcoding:

| Issue | Relation |
| --- | --- |
| [#8752](https://github.com/jellyfin/jellyfin-web/issues/8752) Audio transcoding always targets AAC, even when the browser can't decode it | Opened 2026-11-02. The #8651 author [commented](https://github.com/jellyfin/jellyfin-web/issues/8752#issuecomment-6576294496) that #8651 should fix it. The pull request body does not link it. |
| [#7546](https://github.com/jellyfin/jellyfin-web/issues/7546) [Regression] Web browser HLS playback times out when audio transcoding required | Opened 2026-01-28. About video files that now remux with audio transcoding over HLS, not audio-only playback. Not linked from #8651. |

The other results of that search are about subtitles, video, playback queues, and devices, and do not touch the audio transcoding profiles.
