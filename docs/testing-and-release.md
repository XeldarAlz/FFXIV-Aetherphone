# Testing, CI, and releases

This page explains how Aetherphone code gets validated and shipped: the unit test project, the GitHub Actions workflows that run on every pull request, the tag-and-release pipeline, the in-app changelog system, and the Discord announcement hooks. Read it before you open your first pull request, and again before you cut your first release as a maintainer. Everything here is about the plugin client; the Aethernet backend lives in a separate repository with its own pipeline.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone.Tests/Aetherphone.Tests.csproj | xUnit test project, references the plugin and the Dalamud assembly |
| .github/workflows/ci.yml | PR and master validation: version-sync guard, build, tests, artifact |
| .github/workflows/auto-tag.yml | Tags master automatically when Directory.Build.props gets a new version |
| .github/workflows/release.yml | Builds latest.zip, publishes the GitHub release, announces it, updates repo.json |
| .github/workflows/announce-commits.yml | Posts pushed commits to Discord, never pings anyone |
| .github/workflows/update-issue-template-versions.yml | Refreshes the version dropdown in issue templates after each release |
| .github/workflows/uptime.yml | Polls the backend health endpoint every five minutes |
| Directory.Build.props | Single source of truth for the plugin version |
| repo.json | Dalamud repository manifest that users install the plugin from |
| src/Aetherphone/Core/Changelog/ChangelogData.cs | Every release entry the in-app changelog shows |
| src/Aetherphone/Core/Localization/L.cs | English source strings, including all changelog bullets |

## The test project

Tests live in src/Aetherphone.Tests and use xUnit (the `xunit` and `xunit.runner.visualstudio` packages with `Microsoft.NET.Test.Sdk`). The project targets net10.0-windows, references the plugin project directly, and resolves the Dalamud assembly from the same path the plugin build uses. Dalamud is the community plugin framework that loads Aetherphone into Final Fantasy XIV; its DLL ships with XIVLauncher, not with NuGet, so the test csproj points `DalamudLibPath` at the XIVLauncher dev-hooks folder and honors a `DALAMUD_HOME` environment variable override.

The plugin project declares `InternalsVisibleTo` for Aetherphone.Tests in src/Aetherphone/Aetherphone.csproj, so tests can reach `internal` types without making anything public.

### What is covered today

The suite targets deterministic logic that runs without the game process. Highlights:

| Area | Files | What is pinned |
| --- | --- | --- |
| Crypto wire shapes | src/Aetherphone.Tests/CryptoBoxTests.cs, EnvelopeCodecTests.cs, MediaEnvelopeTests.cs, RecoveryKeyTests.cs, KeyDistributionTrustTests.cs, AdInquiryCryptoTests.cs | Round trips, fail-closed on wrong key or tampered associated data, scope and sender binding, the real `AE1.` envelope prefix (`EnvelopeCodec.Prefix`) |
| Game engines | src/Aetherphone.Tests/ChessRulesTests.cs, SudokuBoardTests.cs | Chess move generation pinned by perft (counting all legal move sequences to a depth) against the known counts 20, 400, 8902, 197281, 4865609; en passant, castling, promotion rules |
| Config migrations | src/Aetherphone.Tests/ConfigMigrationsTests.cs | `ConfigMigrations.RewriteTypeNames` rewrites relocated type names, idempotently, without touching unrelated JSON |
| Layout services | src/Aetherphone.Tests/HomeLayoutServicePlacementTests.cs, HomeLayoutServiceInstallTests.cs, ControlLayoutServiceInstallTests.cs, with shared fakes in HomeFakes.cs | Home screen and Control Center placement and install rules |
| Physics and media | src/Aetherphone.Tests/KineticScrollerTests.cs, WebmOpusDemuxerTests.cs | Scroll momentum math, WebM/Opus demuxing for voice notes |
| Networking logic | src/Aetherphone.Tests/IdentifiedMergeTests.cs, FeedLaneTests.cs, AccountSwitchTests.cs, SignOutAnnouncementTests.cs, ModerationNoticeTests.cs, LodestoneMatchTests.cs, MusterShareTests.cs, MessageArchiveTests.cs | Merge rules, feed lanes, account switching, moderation notices, /tell archives |
| Geometry | src/Aetherphone.Tests/ChassisGeometryTests.cs | The phone chassis squircle contract |

There is no UI rendering test. The phone is drawn with Dear ImGui, an immediate mode UI library that redraws every frame inside the game process, so drawing code is exercised in-game, not in the test runner.

### Running the tests

From the repository root:

```
dotnet test Aetherphone.sln
```

CI runs the Release configuration against the already-built solution:

```
dotnet test Aetherphone.sln --configuration Release --no-build --logger "console;verbosity=normal"
```

If the build cannot find Dalamud, install XIVLauncher (which places Dalamud under `%AppData%\XIVLauncher\addon\Hooks\dev`) or set `DALAMUD_HOME` to a folder containing the Dalamud assemblies. See [Getting started](getting-started.md).

### What the project expects tests for

Write tests when your change is deterministic logic with a contract worth pinning:

- Anything that touches an encryption or wire format. A silent format change bricks message decryption for every user, so round trips and fail-closed cases are mandatory.
- Game engine rules (chess, sudoku, and future mini-games with nontrivial rules).
- Configuration migrations. Old JSON from real installs must keep loading.
- Pure services with observable rules: layout placement, merge logic, parsers, physics.

UI composition, ImGui drawing, and code that needs Dalamud services running in-game are validated by loading the dev plugin instead.

## CI on pull requests

.github/workflows/ci.yml runs on every pull request targeting master and on every push to master. Two jobs:

1. **Guards (version sync)**, on Ubuntu. Extracts `<Version>` from Directory.Build.props and compares it to `AssemblyVersion` and `TestingAssemblyVersion` in repo.json. Any mismatch fails the run before the expensive build starts.
2. **Build (Windows)**, after guards pass. Downloads the latest Dalamud distribution to the standard dev-hooks path, then runs `dotnet restore Aetherphone.sln --locked-mode`, `dotnet build --configuration Release`, and the test suite. On master pushes it also uploads the SDK-built plugin zip (`src/Aetherphone/bin/Release/Aetherphone/latest.zip`) as a workflow artifact.

Restore runs in locked mode because Directory.Build.props sets `RestorePackagesWithLockFile`. Both projects commit a packages.lock.json; if you add or bump a NuGet package, run `dotnet restore` locally and commit the updated lock files, or CI restore fails.

## Versioning and the release pipeline

The plugin version lives in exactly one place: the `<Version>` property in Directory.Build.props. Everything downstream reads it.

A release happens in three automated stages:

### 1. auto-tag.yml tags the version

On every push to master, .github/workflows/auto-tag.yml reads `<Version>` from Directory.Build.props and checks whether a matching `v*` tag exists. If the tag is missing it creates and pushes `v<Version>`. If the tag already exists the job logs "nothing to do" and stops.

This is the rule the lead maintainers repeat most: **if you do not bump Directory.Build.props, auto-tag silently skips and no release happens.** Merging code without a version bump is normal and fine; it means "not a release yet".

Two details worth knowing:

- Any commit message in the push containing `[skip auto-tag]` skips the whole job.
- The tag is pushed with the `HUB_REPO_PAT` secret (a personal access token) instead of the default `GITHUB_TOKEN`, because GitHub never lets a `GITHUB_TOKEN` push trigger other workflows. Without the PAT the tag still gets created, but release.yml has to be dispatched by hand.

### 2. release.yml builds and publishes

.github/workflows/release.yml fires on `v*.*.*` tag pushes (with a `workflow_dispatch` fallback that accepts a `tag` input). It:

1. Checks out the tag, downloads Dalamud, restores in locked mode, and builds Release.
2. Stages `src/Aetherphone/bin/Release/Aetherphone/latest.zip` and publishes it as a GitHub release with auto-generated release notes.
3. Announces the release on Discord (see below).
4. Rewrites repo.json on master: `AssemblyVersion`, `TestingAssemblyVersion`, a refreshed `DownloadCount` summed across all releases, and a new `LastUpdate` timestamp, then commits it with a rebase-and-retry loop.
5. If the `HUB_REPO_PAT` secret is set, sends a `repository_dispatch` to the XeldarAlz/DalamudPlugins hub repo so the multi-plugin manifest regenerates immediately.

### 3. repo.json serves users

repo.json at the repository root is the Dalamud repository manifest: users paste its raw GitHub URL into Dalamud's custom plugin repositories list, and Dalamud reads `AssemblyVersion` plus `DownloadLinkInstall` (which points at `releases/latest/download/latest.zip`) to offer installs and updates. Because the download link always targets the latest release, publishing the GitHub release is what actually ships the update; the repo.json commit afterwards is what tells Dalamud a new version exists.

## The changelog system

The changelog is compiled into the plugin and shown in the Settings app (src/Aetherphone/Apps/Settings/Pages/ChangelogPage.cs). A badge appears on the page while `Configuration.HasUnseenChangelog` is true, which compares `LastSeenChangelogVersion` against `ChangelogData.LatestVersion`.

A release entry has three parts, and all three ship in the same pull request as the version bump:

1. A `ChangelogEntry` (version, date, highlights) prepended to `ChangelogData.Entries` in src/Aetherphone/Core/Changelog/ChangelogData.cs. Entries are newest first; `LatestVersion` reads `Entries[0]`.
2. A `LocString[]` in the `Changelog` class of src/Aetherphone/Core/Localization/L.cs holding the English bullets, keyed `changelog.rXXXX.N`.
3. Translations of every one of those keys in all nine JSON files under src/Aetherphone/Localization, because a key that exists in L.cs must exist in every JSON. Full story: [Localization](localization.md).

```csharp
public static readonly LocString[] Release0997 =
{
    new("changelog.r0997.0", "Fixed the Camera freezing when you rotate to landscape, contributed by Ehno"),
    new("changelog.r0997.1", "Photos now remembers which album you had open"),
};
```

```csharp
new ChangelogEntry("0.9.9.7", "2026-08-02", L.Changelog.Release0997),
```

Copy rules for bullets (see [Localization](localization.md) for the full set):

- One idea per bullet. Split "fixed X and added Y" into two bullets.
- Credit contributors by name at the end of the bullet: "..., contributed by Ehno". Real examples are in the `Release0995` and `Release0990` arrays in L.cs.
- Use the in-app feature names users see on screen (Chirper, Velvet, Linkpearl, Jobs), not internal type names.
- No em dashes anywhere, in any language. The release workflow refuses to send a Discord payload containing one.

## Discord announcements

Two webhooks, two very different behaviors:

- **Releases** (the announce step inside release.yml): posts a rich embed with the release notes, version, channel, and total install count to the `RELEASE_WEBHOOK_URL` secret. If the `RELEASE_PING_ROLE_ID` repository variable is set, the message pings that role, with `allowed_mentions` scoped to only that role.
- **Commits** (.github/workflows/announce-commits.yml): on every master push, posts up to ten commit subject lines to the `COMMITS_WEBHOOK_URL` secret. It never pings anyone: no role mention, no content ping, ever.

Both scrub em and en dashes from the text they post (the release notes in release.yml, the commit subjects in announce-commits.yml); release.yml goes further and fails the step if one survives anywhere in the final payload. Both exit quietly when their webhook secret is unset, so forks run green without any configuration.

## Issue template version updater

.github/workflows/update-issue-template-versions.yml also fires on `v*.*.*` tag pushes. It scans every .github/ISSUE_TEMPLATE/*.yml and rewrites each "Plugin version" dropdown it finds (today only bug_report.yml has one): the new version goes on top, the list keeps the five most recent versions, and the "Older / not sure" sentinel stays at the bottom. It commits with a rebase-and-retry loop like the one in release.yml, because both push to master on the same tag event. It can be dispatched manually with a version input to backfill.

## Uptime workflow

.github/workflows/uptime.yml probes the backend's public health endpoint every five minutes and posts failures to an alert webhook; it lives in this repo only because public repositories get free Actions minutes, and it monitors the backend, not the plugin.

## Release checklist

A maintainer cutting version X.Y.Z.W:

1. Bump `<Version>` in Directory.Build.props to X.Y.Z.W.
2. Bump `AssemblyVersion` and `TestingAssemblyVersion` in repo.json to the same value. The CI guards job fails the PR if they differ.
3. Write the changelog: prepend the `ChangelogEntry` in ChangelogData.cs, add the `ReleaseXYZW` array in L.cs, and add every `changelog.rXYZW.N` key to all nine JSONs in src/Aetherphone/Localization.
4. Run `dotnet build Aetherphone.sln` and `dotnet test Aetherphone.sln` locally.
5. Merge to master. Watch CI pass (guards, then the Windows build and tests).
6. auto-tag.yml tags `vX.Y.Z.W` and the tag push triggers release.yml and the issue template updater.
7. Verify the results: a GitHub release named vX.Y.Z.W with latest.zip attached, the Discord release post, a "Release vX.Y.Z.W: bump repo.json" commit on master, and the refreshed issue template dropdown.
8. If the tag exists but no release appeared (the PAT fallback case), run release.yml manually via workflow_dispatch with the tag as input.

## Gotchas

- **No version bump means no release, silently.** auto-tag.yml sees the existing tag for the unchanged version and logs "nothing to do". Nothing warns you that the merge did not ship.
- **The version guard runs before the build.** If Directory.Build.props and repo.json disagree, CI fails in seconds with a mismatch error; fix both, do not chase a build problem.
- **Locked restore bites package bumps.** CI restores with `--locked-mode`. Changing any `PackageReference` without committing regenerated packages.lock.json files fails restore, not build, so the error appears earlier than you expect.
- **Tags pushed by GITHUB_TOKEN do not cascade.** If the `HUB_REPO_PAT` secret is missing, the tag is created but release.yml never fires. The recovery is the manual dispatch in the checklist, not deleting and re-pushing the tag.
- **release.yml owns DownloadCount and LastUpdate.** Do not hand-edit those repo.json fields; the release run overwrites them (and resolves any concurrent-commit conflict in favor of its own values with `git rebase -X theirs`).
- **A missed translation is invisible at runtime.** `Loc.T` falls back to the English source string, so a changelog key absent from de.json ships as English text in the German UI with no error anywhere.
- **`[skip auto-tag]` in any commit message of a push** disables tagging for that entire push, including other commits in it.
- **Tests need Dalamud on disk.** src/Aetherphone.Tests/Aetherphone.Tests.csproj references the Dalamud assembly from the XIVLauncher dev-hooks path or `DALAMUD_HOME`; a bare CI-less machine without either cannot build the test project.

## Related docs

- [Getting started](getting-started.md): prerequisites, building, loading the dev plugin
- [Conventions](conventions.md): the code, copy, and commit rules your pull request is reviewed against
- [Localization](localization.md): L.cs as source of truth, the nine JSONs, copy rules in full
- [Networking](networking.md): the Aethernet client and the encryption whose wire shapes the tests pin
- [Architecture](architecture.md): how the plugin boots and where services live
