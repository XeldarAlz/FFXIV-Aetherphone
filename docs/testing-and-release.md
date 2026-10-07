# Testing, CI, and releases

This page explains how Aetherphone code gets validated and shipped: the unit test project, the GitHub Actions workflows that run on every pull request, the tag-and-release pipeline, the in-app changelog system, and the Discord announcement hooks. Read it before you open your first pull request, and again before you cut your first release as a maintainer. Everything here is about the plugin client; the Aethernet backend lives in a separate repository with its own pipeline.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone.Tests/Aetherphone.Tests.csproj | xUnit test project, references the plugin and the Dalamud assembly |
| .github/workflows/ci.yml | Validation on master and dev: guard checks, build, tests, artifacts |
| .github/workflows/auto-tag.yml | Tags master automatically when Directory.Build.props gets a new version |
| .github/workflows/release.yml | Builds latest.zip, publishes the GitHub release, announces it, updates repo.json |
| .github/scripts/Get-ChangelogNotes.ps1 | Turns a version's changelog entry into the Discord release post text |
| .github/workflows/announce-commits.yml | Posts commits pushed to dev to Discord, never pings anyone |
| .github/workflows/update-issue-template-versions.yml | Backfills the version dropdown in issue templates after a release |
| .github/ISSUE_TEMPLATE/bug_report.yml | Holds the "Plugin version" dropdown, bumped by hand in each release commit |
| .github/dependabot.yml | Weekly Dependabot updates for Actions, NuGet, and the icon generator's npm packages |
| .github/workflows/dependabot-lockfiles.yml | Regenerates packages.lock.json on Dependabot NuGet branches so the locked restore passes |
| .github/workflows/uptime.yml | Polls the backend health endpoint every five minutes |
| Directory.Build.props | Single source of truth for the plugin version; also turns CI warnings into errors |
| repo.json | This plugin's Dalamud repository manifest entry: version, download links, install count |
| src/Aetherphone/Aetherphone.json, AetherphoneDev.json, AetherphoneBeta.json | Dalamud manifest metadata for the stable, Debug, and Beta builds |
| src/Aetherphone/Core/Changelog/ChangelogData.cs | Every release entry the in-app changelog shows |
| src/Aetherphone/Core/Localization/L.cs | English source strings, including all changelog bullets |

## The test project

Tests live in src/Aetherphone.Tests and use xUnit (the `xunit` and `xunit.runner.visualstudio` packages with `Microsoft.NET.Test.Sdk`). The project targets net10.0-windows, references the plugin project directly, and resolves the Dalamud assembly from the same path the plugin build uses. Dalamud is the community plugin framework that loads Aetherphone into Final Fantasy XIV; its DLL ships with the launcher, not with NuGet, so the test csproj mirrors the Dalamud SDK's per-OS default for `DalamudLibPath` (XIVLauncher's dev-hooks folder on Windows, XLCore's on Linux, XIV on Mac's on macOS) and honors a `DALAMUD_HOME` environment variable override.

The plugin project declares `InternalsVisibleTo` for Aetherphone.Tests in src/Aetherphone/Aetherphone.csproj, so tests can reach `internal` types without making anything public.

### What is covered today

The suite targets deterministic logic that runs without the game process. Highlights:

| Area | Files | What is pinned |
| --- | --- | --- |
| Crypto wire shapes | src/Aetherphone.Tests/CryptoBoxTests.cs, EnvelopeCodecTests.cs, MediaEnvelopeTests.cs, RecoveryKeyTests.cs, KeyDistributionTrustTests.cs, AdInquiryCryptoTests.cs | Round trips, fail-closed on wrong key or tampered associated data, scope and sender binding, the real `AE1.` envelope prefix (`EnvelopeCodec.Prefix`) |
| Mini-game rules | src/Aetherphone.Tests/ChessRulesTests.cs, one `*BoardTests.cs` per local game (SnakeBoardTests.cs, SudokuBoardTests.cs, CraterBoardTests.cs, MiniGolfBoardTests.cs and the rest), TetrisModernTests.cs, TriviaDeckTests.cs, DoomAssetsTests.cs | Each game's rules, scoring and level packs; a `SameSeedReplaysIdentically` case wherever a board is seeded, so the daily challenge deals one board to everyone; chess move generation pinned by perft (counting all legal move sequences to a depth) against the known counts 20, 400, 8902, 197281, 4865609; which Doom game data loads first |
| Stage kit | src/Aetherphone.Tests/GameSessionTests.cs, IntroLayoutTests.cs, StageLayoutTests.cs, HudModelTests.cs, ComboMeterTests.cs, Camera2DTests.cs, RibbonTests.cs, RollingValueTests.cs, GameRandomTests.cs, PadHoldTests.cs, CardTableLayoutTests.cs | The session flow and its single submission per run, levels, hot-seat handoffs, unranked modes, the intro layout in portrait and landscape, HUD slots, combo tiers, the camera, trails, seeded random, held pads, card table math |
| Game worlds and physics | src/Aetherphone.Tests/PhysicsWorldTests.cs, Geometry2DTests.cs, PolygonTests.cs, TerrainMaskTests.cs, TerrainPainterTests.cs, LaneProjectionTests.cs, LaneGridTests.cs, WaveSpawnerTests.cs, DripEconomyTests.cs | Physics2D bodies, joints, ropes and contacts, the shared geometry and triangulation, destructible terrain, the lane runner projection and the lane defense pieces |
| Games hub | src/Aetherphone.Tests/GamesLibraryTests.cs, GamesLibraryPagesTests.cs, GamesHomeLayoutTests.cs, GamesRankOrderTests.cs, GamesStackTests.cs, HubMetricsTests.cs, LaunchMorphTests.cs, StreakGridTests.cs, DailyCountdownTests.cs, DailyChallengeHistoryTests.cs, TopThisWeekModeTests.cs, WhatsNewNoticeTests.cs, TourRegistryTests.cs | The catalog's release order, shelves, records and Library view, Home and category page layout math, rank card order and names, when a game closes, the zoom launch, the streak calendar and daily history, the reset countdown, the update notice pins, and the Games tour anchors |
| Leaderboard and online rooms | src/Aetherphone.Tests/LeaderboardStoreTests.cs, ScoresWireContractTests.cs, OnlineKindInfoTests.cs, UnoRulesTests.cs, OnlineConnectFourTableTests.cs, OnlineBroadsideWireContractTests.cs, OnlineCraterWireTests.cs, OnlineLuckyDrawWireContractTests.cs, OnlineMiniGolfWireContractTests.cs | The opt-in upload queue and rank states, the score routes and stat ids against the server catalog, one `OnlineKindInfo` per room kind, the Uno rules, each room kind's wire shapes, and when a Connect Four disc animates |
| Config migrations | src/Aetherphone.Tests/ConfigMigrationsTests.cs | `ConfigMigrations.RewriteTypeNames` rewrites relocated type names, idempotently, without touching unrelated JSON |
| Layout services | src/Aetherphone.Tests/HomeLayoutServicePlacementTests.cs, HomeLayoutServiceInstallTests.cs, ControlLayoutServiceInstallTests.cs, with shared fakes in HomeFakes.cs | Home screen and Control Center placement and install rules |
| Physics and media | src/Aetherphone.Tests/KineticScrollerTests.cs, WebmOpusDemuxerTests.cs | Scroll momentum math, WebM/Opus demuxing for voice notes |
| Networking logic | src/Aetherphone.Tests/IdentifiedMergeTests.cs, FeedLaneTests.cs, AccountSwitchTests.cs, SignOutAnnouncementTests.cs, ModerationNoticeTests.cs, LodestoneMatchTests.cs, MusterShareTests.cs, MessageArchiveTests.cs | Merge rules, feed lanes, account switching, moderation notices, /tell archives |
| Localization lockstep | src/Aetherphone.Tests/LocalizationParityTests.cs | Every key declared in L.cs exists in all nine catalogs, and no catalog carries a key L.cs no longer declares |
| Casino | src/Aetherphone.Tests/CasinoWireContractTests.cs, CasinoRoomWireContractTests.cs, CasinoTableWireContractTests.cs, and the rest of the Casino, Blackjack, Wheel, Bingo, Slots, and Scratch test files | Wire contracts against the backend casino endpoints, game rules, and round playback |
| Chat | src/Aetherphone.Tests/ChatInboxTests.cs, ChatLogTests.cs, ChatSearchTests.cs, ChatChunkTests.cs, ChatRunsTests.cs, ChatStreamViewTests.cs, GameChannelsTests.cs | The game chat pipeline: log and inbox rules, chunking, search, stream views, channel handling |
| Audio decoding | src/Aetherphone.Tests/AdtsReaderTests.cs, IcyMetadataStreamTests.cs, StreamDecoderSelectionTests.cs | ADTS header parsing, ICY metadata streams, decoder choice by content type |
| Geometry | src/Aetherphone.Tests/ChassisGeometryTests.cs | The phone chassis squircle contract |

There is no UI rendering test. The phone is drawn with Dear ImGui, an immediate mode UI library that redraws every frame inside the game process, so drawing code is exercised in-game, not in the test runner.

### Running the tests

From the repository root:

```
dotnet test Aetherphone.sln
```

CI runs the Release configuration against the already-built solution:

```
dotnet test Aetherphone.sln --configuration Release --no-build --logger "console;verbosity=normal" --logger "trx;LogFileName=test-results.trx" --results-directory TestResults
```

If the build cannot find Dalamud, install XIVLauncher (which places Dalamud under `%AppData%\XIVLauncher\addon\Hooks\dev`) or set `DALAMUD_HOME` to a folder containing the Dalamud assemblies. On Linux the plugin project reads only `DALAMUD_HOME`, so set it there even with XLCore installed. See [Getting started](getting-started.md).

### What the project expects tests for

Write tests when your change is deterministic logic with a contract worth pinning:

- Anything that touches an encryption or wire format. A silent format change bricks message decryption for every user, so round trips and fail-closed cases are mandatory.
- Game engine rules: a `*BoardTests.cs` for every mini-game, with a `SameSeedReplaysIdentically` case for a seeded board (see [Mini-games framework](games-framework.md)).
- Configuration migrations. Old JSON from real installs must keep loading.
- Pure services with observable rules: layout placement, merge logic, parsers, physics.

UI composition, ImGui drawing, and code that needs Dalamud services running in-game are validated by loading the dev plugin instead.

## CI on pull requests

Pull requests target `dev`, the default branch; `master` only moves when a release is cut (see the release checklist below). .github/workflows/ci.yml runs on every pull request targeting master or dev and on every push to either branch. Two jobs:

1. **Guards**, on Ubuntu: fail-fast grep checks that need no compiler. Any failure stops the run before the expensive build starts. They check:
   - version sync: `<Version>` in Directory.Build.props against `AssemblyVersion` and `TestingAssemblyVersion` in repo.json;
   - the UI scale seam: only UiScale.cs may read `ImGuiHelpers.GlobalScale`;
   - no em dashes in any tracked file (announce-commits.yml is the one exemption);
   - no `async void`;
   - no new LINQ: no `using System.Linq;` under src/Aetherphone outside the allowlisted cold-path files;
   - the clock format seam: only TimeText.cs may hand-format a time pattern;
   - layering: Core and the Windows toolkit never import an app, except the AppRegistry and WidgetCatalog composition roots;
   - no underscore-prefixed private or protected fields;
   - no UTF-8 byte order mark in .cs files;
   - every file-scoped namespace equals its folder path (Windows/Components is one flat namespace, ManagedDoom is exempt).

   [Conventions](conventions.md) explains the rule behind each one.
2. **Build (Windows)**, after guards pass. Downloads the latest Dalamud distribution to the standard dev-hooks path, then runs `dotnet restore Aetherphone.sln --locked-mode`, `dotnet build --configuration Release`, and the test suite. Directory.Build.props runs the .editorconfig code-style rules and the .NET analyzers in every build, and on CI (where `GITHUB_ACTIONS` is `true`) it also turns on `TreatWarningsAsErrors` and deterministic builds. Every compiler, analyzer, and code-style warning is therefore an error in CI, while a local build only prints it, so build Release and clear every warning before you push. Every green run uploads the SDK-built plugin zip (`src/Aetherphone/bin/Release/Aetherphone/latest.zip`) as the `Aetherphone-plugin` artifact, kept for 14 days, so every pull request that passes gets a loadable build for in-game review. When the test suite fails, the trx log (`TestResults/test-results.trx`) is uploaded as a `test-results` artifact instead.

Restore runs in locked mode because Directory.Build.props sets `RestorePackagesWithLockFile`. All three projects in the solution (Aetherphone, Aetherphone.Tests, and the vendored ManagedDoom) commit a packages.lock.json; if you add or bump a NuGet package, run `dotnet restore` locally and commit the updated lock files, or CI restore fails. Dependabot (.github/dependabot.yml) opens weekly NuGet bumps for the plugin and test projects only, not ManagedDoom. Its NuGet branches are handled by .github/workflows/dependabot-lockfiles.yml, which regenerates the lock files on the Dependabot branch (it needs the `HUB_REPO_PAT` secret in the Dependabot scope; without it, it skips quietly).

## Versioning and the release pipeline

The plugin version lives in exactly one place: the `<Version>` property in Directory.Build.props. Everything downstream reads it.

Two branches carry the work. `dev` is the default branch: every pull request merges there, and merging to dev never releases anything. `master` is the release branch: a maintainer fast-forwards it to dev to cut a release, the pipeline below commits back to it, and master is then merged back into dev. The full sequence is in the [release checklist](#release-checklist).

Once master moves, a release happens in three automated stages:

### 1. auto-tag.yml tags the version

On every push to master, .github/workflows/auto-tag.yml reads `<Version>` from Directory.Build.props and checks whether a matching `v*` tag exists. If the tag is missing it creates and pushes `v<Version>`. If the tag already exists the job logs "nothing to do" and stops.

This is the rule the lead maintainers repeat most: **if you do not bump Directory.Build.props, auto-tag silently skips and no release happens.** Moving master without a version bump ships nothing.

Three details worth knowing:

- Any commit message in the push containing `[skip auto-tag]` skips the whole job.
- The tag is pushed with the `HUB_REPO_PAT` secret (a personal access token) instead of the default `GITHUB_TOKEN`, because GitHub never lets a `GITHUB_TOKEN` push trigger other workflows. Without the PAT the tag still gets created, but release.yml has to be dispatched by hand.
- release.yml checks out with the same PAT, because master is a protected branch that the default token cannot push to (the comment on its checkout step explains the rule). Without the PAT, the repo.json commit in stage 2 is rejected and the step fails after five attempts.

### 2. release.yml builds and publishes

.github/workflows/release.yml fires on `v*.*.*` tag pushes (with a `workflow_dispatch` fallback that accepts a `tag` input). It:

1. Checks out the tag, downloads Dalamud, restores in locked mode, and builds Release.
2. Stages `src/Aetherphone/bin/Release/Aetherphone/latest.zip` and publishes it as a GitHub release with auto-generated release notes.
3. Announces the release on Discord (see below).
4. Rewrites repo.json on master: `AssemblyVersion`, `TestingAssemblyVersion`, a refreshed `DownloadCount` summed across all releases, and a new `LastUpdate` timestamp, then commits it with a rebase-and-retry loop.
5. If the `HUB_REPO_PAT` secret is set, sends a `repository_dispatch` to the XeldarAlz/DalamudPlugins hub repo so the multi-plugin manifest regenerates immediately.

### 3. repo.json serves users

repo.json at the repository root is this plugin's Dalamud repository manifest entry. Users add https://aetherphone.net/repo.json (the URL the README and the release post advertise) to Dalamud's custom plugin repositories, and the XeldarAlz/DalamudPlugins hub re-reads this file when release.yml pings it. Dalamud reads `AssemblyVersion` plus `DownloadLinkInstall` (which points at `releases/latest/download/latest.zip`) to offer installs and updates. Because the download link always targets the latest release, publishing the GitHub release is what actually ships the update; the repo.json commit afterwards is what tells Dalamud a new version exists.

### Manifests and build variants

src/Aetherphone holds one Dalamud manifest per build variant: Aetherphone.json (Release), AetherphoneDev.json (Debug), and AetherphoneBeta.json (Beta). Each carries the name, punchline, description, tags, and icon Dalamud shows. repo.json repeats the stable plugin's metadata by hand, so when you change the description, punchline, or tags in Aetherphone.json, change them in repo.json too. repo.json's `DalamudApiLevel` tracks the Dalamud.NET.Sdk version in src/Aetherphone/Aetherphone.csproj; bump both together.

The Beta variant is a Release build with `-p:AetherphoneBeta=true` (`dotnet build Aetherphone.sln -c Release -p:AetherphoneBeta=true`). It defines `BETA`, builds AetherphoneBeta.dll, installs side by side with the stable plugin, opens with `/phonebeta`, keeps its own config, and talks to the development Aethernet instance by default (Settings, About has a test-server toggle on prerelease builds).

## The changelog system

The changelog is compiled into the plugin and shown in the Settings app (src/Aetherphone/Apps/Settings/Pages/ChangelogPage.cs). A badge appears on the page while `Configuration.HasUnseenChangelog` is true, which compares `LastSeenChangelogVersion` against `ChangelogData.LatestVersion`.

The App Store reads the same data. StoreIndex (src/Aetherphone/Apps/AppStore/StoreIndex.cs) matches every section whose title is an app's App Store name (the `L.Apps` string AppStoreCatalog gives it) to that app: the newest release's first bullet for each app appears in the Updates list, and the app's page shows its most recent notes. A section titled with a grouping string such as `L.Changelog.SectionPhone` never reaches the App Store.

A release entry has three parts:

1. A `ChangelogEntry` (version, date, highlights) prepended to `ChangelogData.Entries` in src/Aetherphone/Core/Changelog/ChangelogData.cs. Entries are newest first; `LatestVersion` reads `Entries[0]`.
2. A `LocString[]` in the `Changelog` class of src/Aetherphone/Core/Localization/L.cs holding the English bullets, keyed `changelog.r<digits>.N`, where the digits are the version's parts run together (1.0.0.3 is `r1003`, 1.0.1.10 is `r10110`).
3. Translations of every one of those keys in all nine JSON files under src/Aetherphone/Localization, because a key that exists in L.cs must exist in every JSON. Full story: [Localization](localization.md).

The three parts travel together, but not always in the version bump itself. Bullets usually land in `docs(changelog)` commits as their fixes merge into dev, under the upcoming version, and the `chore(release)` bump commit finalizes the entry (moving or retitling bullets when the plan changed). All of it must be on dev before master moves.

A real example, the 1.0.0.3 entry (abridged; the full array holds five bullets):

```csharp
public static readonly LocString[] Release1003 =
{
    new("changelog.r1003.0",
        "The phone now tells you when an avatar frame is given to you or taken away, instead of the ring around your avatar changing in silence"),
    new("changelog.r1003.1", "Two new frames in the Aether Coin shop"),
};
```

```csharp
new ChangelogEntry("1.0.0.3", "2026-08-17", L.Changelog.Release1003),
```

Since 1.0.1.6 a release can group its bullets under headers instead of one flat list. Keep one `LocString[]` per group in L.cs (`Release1016Aethergram`, `Release1016Messaging`, `Release1016Coin`, `Release1016Linkpearl`) and hand `ChangelogEntry` a `ChangelogSection[]` that pairs each array with its title. A title is any existing LocString: an app name from `L.Apps` for an app (which also feeds the App Store), a `L.Changelog.SectionX` key next to the arrays for a grouping that is not an app (`changelog.sectionMessaging`), or another title string (1.1.0.0 uses `L.Character.Activity` and `L.Health.Title`). Keep it a two-part `L.Class.Member` name, the only shape .github/scripts/Get-ChangelogNotes.ps1 resolves for the Discord post. Flat entries keep working, so older releases are untouched.

```csharp
new ChangelogEntry("1.0.1.6", "2026-08-30", new ChangelogSection[]
{
    new(L.Apps.Aethergram, L.Changelog.Release1016Aethergram),
    new(L.Changelog.SectionMessaging, L.Changelog.Release1016Messaging),
    new(L.Apps.Coin, L.Changelog.Release1016Coin),
    new(L.Apps.Linkpearl, L.Changelog.Release1016Linkpearl),
}),
```

Copy rules for bullets (see [Localization](localization.md) for the full set):

- One idea per bullet. Split "fixed X and added Y" into two bullets.
- Credit contributors by name at the end of the bullet: "..., contributed by Ehno". Real examples are in the `Release0995` and `Release0990` arrays in L.cs.
- Use the in-app feature names users see on screen (Chirper, Velvet, Linkpearl, Jobs), not internal type names.
- No em dashes anywhere, in any language. The release workflow refuses to send a Discord payload containing one.

## Discord announcements

Two webhooks, two very different behaviors:

- **Releases** (the announce step inside release.yml): posts a rich embed with the version, channel, total install count, and the full English changelog of the release to the `RELEASE_WEBHOOK_URL` secret. The changelog comes from .github/scripts/Get-ChangelogNotes.ps1, which finds the version's `ChangelogEntry` in ChangelogData.cs and resolves every section title and bullet from L.cs, so the post reads like the in-app Changelog page: one bold header per section, one bullet per highlight, flat entries as plain bullets. A changelog longer than one embed continues in follow-up embeds posted one after another; nothing is truncated. When the version has no changelog entry the step warns and posts the announcement without a changelog; the GitHub release, linked from the post as "Release notes", still carries its auto-generated notes. The post also carries the https://aetherphone.net/repo.json install URL. If the `RELEASE_PING_ROLE_ID` repository variable is set, the first message pings that role, with `allowed_mentions` scoped to only that role. The step runs with `continue-on-error`, so a failed webhook never blocks the repo.json bump or the hub ping.
- **Commits** (.github/workflows/announce-commits.yml): on every push to dev, posts the pushed commits to the `COMMITS_WEBHOOK_URL` secret as one embed. It drops github-actions[bot] commits and merge commits, sorts the rest by conventional-commit type with an emoji each, lists up to twelve subject lines, and sums up the remainder as "... and N more". A push with nothing left after filtering posts nothing. It never pings anyone: no role mention, no content ping, ever.

Both scrub em and en dashes from the text they post (the changelog pages in release.yml, the commit subjects in announce-commits.yml); release.yml goes further and fails the step if one survives anywhere in the final payload. Both exit quietly when their webhook secret is unset, so forks run green without any configuration.

## Issue template version updater

.github/workflows/update-issue-template-versions.yml also fires on `v*.*.*` tag pushes. It checks out the default branch (dev), scans every .github/ISSUE_TEMPLATE/*.yml, and rewrites each "Plugin version" dropdown it finds (today only bug_report.yml has one): the new version goes on top, the list keeps the five most recent versions, and the "Older / not sure" sentinel stays at the bottom. Any change is committed and pushed to dev with a rebase-and-retry loop.

Maintainers now bump the dropdown by hand inside the release commit (see the checklist), so on a normal release the job finds nothing to change. It remains as a safety net and as a backfill: dispatch it manually with a version input.

## Uptime workflow

.github/workflows/uptime.yml probes the backend's public health endpoint every five minutes and posts failures to an alert webhook; it lives in this repo only because public repositories get free Actions minutes, and it monitors the backend, not the plugin.

## Release checklist

A maintainer cutting version X.Y.Z.W:

1. On a `chore/release-X.Y.Z.W` branch off dev, make one `chore(release): bump version to X.Y.Z.W` commit that:
   - bumps `<Version>` in Directory.Build.props to X.Y.Z.W;
   - bumps `AssemblyVersion` and `TestingAssemblyVersion` in repo.json to the same value (the CI guards job fails if they differ);
   - adds X.Y.Z.W to the top of the "Plugin version" dropdown in .github/ISSUE_TEMPLATE/bug_report.yml, drops the oldest so five versions stay above "Older / not sure", and updates the version in the support info placeholder below it;
   - finalizes the changelog: the `ChangelogEntry` in ChangelogData.cs, its `ReleaseXYZW` arrays in L.cs, and every `changelog.rXYZW.N` key in all nine JSONs in src/Aetherphone/Localization. Bullets that already landed in `docs(changelog)` commits only need moving or retitling.
2. Run `dotnet build Aetherphone.sln -c Release` with zero warnings and `dotnet test Aetherphone.sln` locally.
3. Merge the release branch into dev and watch CI pass (guards, then the Windows build and tests).
4. Fast-forward master to dev and push it. CI runs on master, auto-tag.yml tags `vX.Y.Z.W`, and the tag push triggers release.yml and the issue template updater.
5. Verify the results: a GitHub release named vX.Y.Z.W with latest.zip attached, the Discord release post, and a "Release vX.Y.Z.W: bump repo.json" commit on master.
6. Merge master back into dev (a fast-forward when dev has not moved) so dev carries the bot's repo.json commit.
7. If the tag exists but no release appeared (the PAT fallback case), run release.yml manually via workflow_dispatch with the tag as input.

## Gotchas

- **No version bump means no release, silently.** auto-tag.yml sees the existing tag for the unchanged version and logs "nothing to do". Nothing warns you that moving master did not ship.
- **A clean local build can still fail CI.** Locally, analyzer and code-style warnings only print; CI builds with `TreatWarningsAsErrors`. A zero exit code locally proves nothing; read the warning count.
- **Forgetting to merge master back leaves the branches diverged.** The bot's repo.json commit lands only on master. Until master is merged back into dev, the next release cannot fast-forward master to dev, and dev carries a stale `DownloadCount` and `LastUpdate`.
- **The version guard runs before the build.** If Directory.Build.props and repo.json disagree, CI fails in seconds with a mismatch error; fix both, do not chase a build problem.
- **Locked restore bites package bumps.** CI restores with `--locked-mode`. Changing any `PackageReference` without committing regenerated packages.lock.json files fails restore, not build, so the error appears earlier than you expect.
- **Tags pushed by GITHUB_TOKEN do not cascade.** If the `HUB_REPO_PAT` secret is missing, the tag is created but release.yml never fires. The recovery is the manual dispatch in the checklist, not deleting and re-pushing the tag. The PAT is also what lets release.yml push its repo.json commit to the protected master branch, so a dispatched release without it still fails at that step.
- **release.yml owns DownloadCount and LastUpdate.** Do not hand-edit those repo.json fields; the release run overwrites them (and resolves any concurrent-commit conflict in favor of its own values with `git rebase -X theirs`).
- **A missing key fails CI, an untranslated value does not.** LocalizationParityTests fails the pull request when a key declared in L.cs is absent from any of the nine catalogs, or a catalog carries an orphaned key. What still ships silently is a key that exists in a catalog with its text left in English: nothing checks translation quality, so proofread the values themselves.
- **`[skip auto-tag]` in any commit message of a push** disables tagging for that entire push, including other commits in it.
- **Tests need Dalamud on disk.** src/Aetherphone.Tests/Aetherphone.Tests.csproj references the Dalamud assembly from the per-OS dev-hooks default (XIVLauncher on Windows, XLCore on Linux, XIV on Mac on macOS) or `DALAMUD_HOME`; a bare CI-less machine without either cannot build the test project.

## Related docs

- [Getting started](getting-started.md): prerequisites, building, loading the dev plugin
- [Conventions](conventions.md): the code, copy, and commit rules your pull request is reviewed against
- [Localization](localization.md): L.cs as source of truth, the nine JSONs, copy rules in full
- [Networking](networking.md): the Aethernet client and the encryption whose wire shapes the tests pin
- [Architecture](architecture.md): how the plugin boots and where services live
