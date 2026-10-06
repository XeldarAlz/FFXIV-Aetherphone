# Mini-games framework

This page explains how the Games app hosts its mini-games and how to build a new one with the shared framework, called the stage kit: the `IMiniGame` contract, the `GameSpec` a game declares, the host-owned session flow (intro, countdown, pause, result), the HUD model, the backdrops, boards and camera, the effects layer, the scoring plumbing, and the rules that only apply inside games. Read it after [app-framework.md](app-framework.md), when you want to add or change a mini-game. The mini-games themselves are fully client-side and never talk to the Aethernet backend; the `GamesApp` hub around them does, for the coin economy (play-session reporting, the server-picked featured game, coin awards) and for the online rooms of the Play with friends tab, both described below, and for the global leaderboard, where every finished run's best travels through the `IScoreSink` and `IRankSource` seams into `LeaderboardStore` (see [Global leaderboard](#global-leaderboard)).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Apps/Games/GamesApp.cs | The Games hub: routing, tabs, the running game and its session flow, the chrome chips, the coin chip (pages live in the .Home, .Tiles, .Shelf, .Records and .Together partials) |
| src/Aetherphone/Apps/Games/GamesLibrary.cs | The catalog behind the launcher: release order, latest wave, recents, genres, records (with tier labels) and search |
| src/Aetherphone/Apps/Games/GamesRoute.cs | Hub screens, tabs and shelves for the router |
| src/Aetherphone/Apps/Games/GamesHubArt.cs | Shared hub pieces: section headings, empty states, medallions, onboarding anchors |
| src/Aetherphone/Apps/Games/TileRail.cs | Sideways-panning shelf of tiles |
| src/Aetherphone/Apps/Games/Widgets/DailyGameWidget.cs | Home screen widget for the daily game plus recent (or latest) games |
| src/Aetherphone/Apps/Games/Framework/IMiniGame.cs | Contract every mini-game implements |
| src/Aetherphone/Apps/Games/Framework/GameSpec.cs | What a game declares once: id, title, hook, genre, backdrop, HUD style, score kind, modes, flags |
| src/Aetherphone/Apps/Games/Framework/GameStart.cs | The mode, seed and daily flag a run starts with |
| src/Aetherphone/Apps/Games/Framework/GameContext.cs | Per-frame data handed to the running game |
| src/Aetherphone/Apps/Games/Framework/GameSession.cs | The host-owned run: flow state, score reports, the single `Finish`, stats and leaderboard submission |
| src/Aetherphone/Apps/Games/Framework/GameOutcome.cs | What a game hands `Finish`: value, kind, win flag, stat id, up to four stat lines, a secondary stat |
| src/Aetherphone/Apps/Games/Framework/StageLayout.cs | Full and Safe rect geometry, chrome chip and HUD row positions |
| src/Aetherphone/Apps/Games/Framework/StageBackdrop.cs | The seven layered backdrops with pointer and camera parallax, vignette and light sweep |
| src/Aetherphone/Apps/Games/Framework/BoardPlate.cs | The glass plate under a grid |
| src/Aetherphone/Apps/Games/Framework/StageCell.cs | Cells with depth: raised, flat, sunken, pressed |
| src/Aetherphone/Apps/Games/Framework/Camera2D.cs | World-unit camera: fit, follow, punch, shake, world to screen mapping |
| src/Aetherphone/Apps/Games/Framework/HudModel.cs | The slots a game fills each frame; `StageHud` lays them out |
| src/Aetherphone/Apps/Games/Framework/StageHud.cs | Draws the score pill and the secondary capsules from a `HudModel` |
| src/Aetherphone/Apps/Games/Framework/StageIntro.cs | The intro screen: title, hook, best and rank pills, mode strip, Play |
| src/Aetherphone/Apps/Games/Framework/StagePause.cs | The pause menu: Resume, Restart, Leaderboard, Quit |
| src/Aetherphone/Apps/Games/Framework/GameOverlay.cs | The result card (`DrawStage` for stage games, `Draw` for legacy games) |
| src/Aetherphone/Apps/Games/Framework/StageChrome.cs | The back and pause glass chips |
| src/Aetherphone/Apps/Games/Framework/ScreenFx.cs | Screen-side effects: flash, vignette pulse, edge glow, slow motion, punch, sweep |
| src/Aetherphone/Apps/Games/Framework/FeedbackFx.cs | World-side effects: shake, hit-stop, shockwave rings, floating text, flash |
| src/Aetherphone/Apps/Games/Framework/ParticleSystem.cs | Pooled particles: bursts, sparkles, streaks, confetti, custom `ParticleSpec` emitters, world-space draw |
| src/Aetherphone/Apps/Games/Framework/Ribbon.cs | Tapered trail behind a fast object |
| src/Aetherphone/Apps/Games/Framework/ComboMeter.cs | Combo count, multiplier tiers and heat with a decay window |
| src/Aetherphone/Apps/Games/Framework/GameRandom.cs | Seeded xoshiro128** random source |
| src/Aetherphone/Apps/Games/Framework/GameSeed.cs | Fresh and daily seeds |
| src/Aetherphone/Apps/Games/Framework/GameSfx.cs | The kit's sound events |
| src/Aetherphone/Apps/Games/Framework/GameJuice.cs | Entrance progress, stagger, and pop-in easing |
| src/Aetherphone/Apps/Games/Framework/GameHud.cs | Score pills and accent buttons |
| src/Aetherphone/Apps/Games/Framework/GameGrid.cs | Centered cell-grid math for board games |
| src/Aetherphone/Apps/Games/Framework/GamePalette.cs | Shared board colors and ink-contrast picker |
| src/Aetherphone/Apps/Games/Framework/GameNumber.cs | Cached integer-to-string labels (no per-frame allocation) |
| src/Aetherphone/Apps/Games/Framework/LabelSlot.cs | One cached formatted label, rebuilt on value or language change |
| src/Aetherphone/Apps/Games/Framework/GameInput.cs | Keyboard reads that keep the keys away from the game client |
| src/Aetherphone/Apps/Games/Framework/GamePad.cs | On-screen d-pad and left/fire/right pad |
| src/Aetherphone/Apps/Games/Framework/Substeps.cs | Splits a frame delta into capped simulation substeps |
| src/Aetherphone/Apps/Games/Framework/FixedStepClock.cs | Fixed-timestep accumulator with a catch-up cap |
| src/Aetherphone/Apps/Games/Framework/PixelSprite.cs | Bitmap sprites drawn as filled runs in one color |
| src/Aetherphone/Apps/Games/Framework/GameBanner.cs | Pop-in, hold, fade banner for "Ready" and "Wave 3" |
| src/Aetherphone/Apps/Games/Framework/ILegacyMiniGame.cs | The previous contract, kept while games are migrated |
| src/Aetherphone/Apps/Games/Framework/LegacyGameAdapter.cs | Wraps an `ILegacyMiniGame` as an `IMiniGame` |
| src/Aetherphone/Core/Games/GameStatsStore.cs | Best scores, best times, win streaks, mode choices, daily challenge |
| src/Aetherphone/Core/Games/IScoreSink.cs | `ScoreSubmission` and the sink the session hands finished runs to |
| src/Aetherphone/Core/Games/IRankSource.cs | Where intros and result cards read a `GameRank` from |
| src/Aetherphone/Core/Games/LeaderboardStore.cs | The registered sink and rank source: the upload queue, the board cache, your ranks |
| src/Aetherphone/Core/Games/ScoreUploadQueue.cs | The pure upload ledger: best per stat id, per-game spacing, reason handling, rank states |
| src/Aetherphone/Core/Games/ScoreStatIds.cs | The stat ids the server accepts, with the kind and direction of each |
| src/Aetherphone/Core/Aethernet/Clients/ScoresClient.cs | The typed client for the `/games/scores` routes and the leaderboard privacy switch |
| src/Aetherphone/Apps/Games/GamesApp.Leaderboard.cs | The leaderboard screen: mode, scope and span strips, the top 50, your pinned row |
| src/Aetherphone/Core/Animation/RollingValue.cs | Animated number that rolls toward a target and pops (shared animation infrastructure, not games-only) |
| src/Aetherphone/Core/Coins/CoinGameSessionTracker.cs | Reports play sessions for coin awards |
| src/Aetherphone/Apps/Games/Online/OnlineHub.cs | The friends lobby: host cards, join by code, open rooms |
| src/Aetherphone/Apps/Games/Online/OnlineRoomView.cs | One room: lobby, roster, and the table for the room's game kind |
| src/Aetherphone/Core/Games/GameRoomsStore.cs | Room directory, create/join/leave, actions, and the HTTP fallback poll |
| src/Aetherphone.Tests/GameSessionTests.cs | Pins the flow transitions and the single submission per run |
| src/Aetherphone.Tests/LeaderboardStoreTests.cs | Pins the upload queue: dedupe, spacing, reasons, rank states, persistence |
| src/Aetherphone.Tests/ScoresWireContractTests.cs | Pins the score routes, the JSON shapes and the stat id list against the server |

## How the Games app is structured

The whole arcade is one phone app. `GamesApp` implements `IPhoneApp` (the contract every phone app fulfils, see [app-framework.md](app-framework.md)) and is registered once in `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs:

```csharp
apps.Add(new GamesApp(services.GameStats, services.GameData, services.Textures, services.Coins,
    services.CoinSessions, services.GameRooms, services.Configuration, services.Leaderboard,
    services.RemoteImages, services.Lodestone));
```

`services.Coins` (the wallet store), `services.CoinSessions` (the play-session tracker) and `services.GameRooms` (the online room store) are the coin plumbing and the friends lobby; `services.Configuration` drives the new-feature badge. `services.Leaderboard` is the `LeaderboardStore`, which implements both halves of the leaderboard seam: the `IScoreSink` that receives every finished run and the `IRankSource` that answers rank lookups (the hub hands the same instance to its `GameSession`). `services.RemoteImages` and `services.Lodestone` draw the avatars on the leaderboard screen.

Inside, `GamesApp` owns a plain `IMiniGame[]` array built in its constructor. That array is the registry: a game exists because a line constructs it there. Most games have parameterless constructors; `TriviaApp` and `WordRunApp` show that a game can take services if `GamesApp` passes them through.

`GamesApp` also implements `INameplateActivitySource`, so the Honorific nameplate title (see [Game integration](game-integration.md)) can show the local game or online room being played, and it shows a new-feature dot on its icon until it is first opened.

Each game implements `IMiniGame` from src/Aetherphone/Apps/Games/Framework/IMiniGame.cs:

```csharp
internal interface IMiniGame : IDisposable
{
    GameSpec Spec { get; }
    string Id => Spec.Id;
    string Title => Loc.T(Spec.Title);
    GameGenre Genre => Spec.Genre;
    Vector4 Accent => AppAccents.For(Spec.Id);
    void Start(in GameStart start);
    void Close();
    void Draw(in GameContext context);
    void DrawIdle(in GameContext context) { }
}
```

`Spec` is a static `GameSpec` the game declares once. `Start` replaces the old `Open` plus the per-game restart: the host calls it when the player presses Play on the intro, Play again on the result card, or Restart in the pause menu, with the mode, seed and daily flag of the run. `Draw` runs every frame while the session is in Countdown, Playing, Paused or Result; `DrawIdle` is an optional preview the host draws dimmed under the intro.

`GameSpec` carries:

| Field | Meaning |
| --- | --- |
| `Id` | The stat and accent key. Never rename one |
| `Title`, `Hook` | `LocString`s; the hook is one sentence of how to play, shown on the intro (`L.<Game>.Hook`) |
| `Genre` | One of `Arcade`, `Action`, `Puzzle`, `Brain`, `Tabletop` (the shelf); `Friends` is reserved for the online games |
| `Backdrop` | One of the seven `Backdrop` presets, drawn under everything |
| `Hud` | `HudStyle.Standard` (score pill on the chrome row, capsules below) or `HudStyle.Compact` (everything on the chrome row, for tall boards) |
| `Kind` | `ScoreKind.Score`, `Time`, `Level` or `Streak`: how the run's value is stored and labelled |
| `Modes`, `ModeStatIds` | Difficulty or ruleset choices shown as a `SegmentStrip` on the intro; `ModeStatIds[mode]` is the stat id for that mode (defaults to `Id`). The choice persists per game through `GameStatsStore.LastMode` |
| `Clocked` | The simulation advances on a timer; focus loss pauses the run into the pause menu |
| `Countdown` | Show the 3, 2, 1, Go countdown before play (clocked reflex games only) |
| `Landscape` | The hub holds the landscape lock while the game is open (Doom) |
| `Keyboard` | Informational: the game reads keys through `GameInput` |
| `Legacy` | Set only by `LegacyGameAdapter`; the host skips intro, countdown, pause and result for it |

### The roster

The source of truth is the `games` array in the `GamesApp` constructor for local games and `OnlineGameArt.Kinds` for online ones, with `GamesLibrary.Releases` dating each entry; read those when you need the exact list. At the time of writing the shelves hold these ids:

| Shelf | Game ids |
| --- | --- |
| Arcade | `whack`, `snake`, `flap`, `breakout`, `stack`, `beat`, `blade`, `hop`, `updraft` |
| Action | `skyfall`, `invaders`, `capman`, `squadron`, `doom`, `swoop` |
| Puzzle | `match3`, `tetris`, `2048`, `watersort`, `bubbles`, `flow`, `crystaldrop`, `coil` |
| Brain | `minesweeper`, `memory`, `nonogram`, `simon`, `sudoku`, `trivia`, `wordrun` |
| Tabletop | `solitaire`, `reversi`, `chess` |
| Friends (online) | `online.uno`, `online.chess`, `online.pool` (8-Ball Pool), `online.connectfour` (Connect Four) |

A few ids predate their titles and class names: `match3` is Gem Swap (`GemSwapApp`), `memory` is Pairs (`PairsApp`), `minesweeper` is Sweeper (`SweeperApp`). Never rename an id: it keys the saved stats, the release date and the accent.

### Transitional note: the legacy adapter

Every game shipped before the stage kit implements `ILegacyMiniGame` (the previous contract: `Id`, `Title`, `Genre`, `RunsOnAClock`, `WantsLandscape`, `Open`, `Close`, `Draw`). The constructor wraps each one in a `LegacyGameAdapter`, which builds a `GameSpec` with `Legacy = true`, the Nebula backdrop and the game's hook line, maps `Start` to `Open`, and hands the game a `GameContext` whose `Body` is the full rect inset by the 52 unit chrome band so its existing pill row lands under the chips. For a legacy spec the host still draws the backdrop, the chrome chips and the paused veil on focus loss, but the game draws its own start, restart and result as before. The adapter, `ILegacyMiniGame`, `GameScene.Arena` and `GameContext.Body`/`Stats` exist only for this migration window; a migrated game uses none of them, and they go when the last game moves over.

### The launcher

The hub is a tabbed app split across partials: `GamesApp.cs` (routing, tabs, the running game, the chrome, the coin chip, widget deep links), `GamesApp.Home.cs` (the Home tab and the shelves), `GamesApp.Tiles.cs` (the hero, tiles and the Play with friends card), `GamesApp.Shelf.cs` (a shelf's full grid and the Search tab), `GamesApp.Records.cs` (the Records tab) and `GamesApp.Together.cs` (the online tab, drawn by `OnlineHub`). Shared pieces (section headings with See All, designed empty states, medallions, onboarding anchors) live in `GamesHubArt`; pills use the shared `Button` kit. It draws on the neutral `AppPalettes.Games` skin with the featured game's accent washed over it by `GameScene.Ambient`.

`GamesLibrary` (src/Aetherphone/Apps/Games/GamesLibrary.cs) is the catalog behind the launcher. It wraps the `IMiniGame[]` plus one `GameEntry` per kind in `OnlineGameArt.Kinds` (Uno, Chess, 8-Ball Pool and Connect Four; ids `online.uno`, `online.chess`, `online.pool`, `online.connectfour`, built by `GamesLibrary.OnlineEntryId`) and keeps every list the pages draw from as reusable `int[]` index arrays, so the draw code never allocates:

| List | What it holds |
| --- | --- |
| `Ordered` | Every entry, newest release first (the `Releases` table in the same file; add a row when you add a game) |
| `Latest` | The newest wave: entries released within a week of the newest one, capped at ten |
| `Recent` | Entries with a `LastPlayedUnixSeconds` on their `GameStatRecord`, most recent first, capped at eight |
| `Genre(genre)` | One genre's entries, newest first, built once |
| `Records` | Entries with a personal best, most recently played first |
| `Search(query)` | Entries whose title or genre name contains the query; empty for a blank query |

`IsNew` marks an entry for thirty days after its release; `Best` and `Subtitle` carry the cached best-score line ("Best · 1,240", "Best · 1:05 · Easy", "Best · Level 12 · Medium", "Streak · 3") or fall back to the genre label, while `BestValue`, `BestKind` and `BestTier` give the Records tab the bare value, what it measures and the tier it came from. Games with difficulty tiers (Flow, Sweeper, Nonogram, Sudoku) show the best across their `<id>.easy|medium|hard` records with the tier named. `Rebuild` refreshes the recents, records and best labels; the hub calls it when it opens, when a game closes, and when the player leaves an online room. `EnsureLanguage` rebuilds the labels when the language or the clock format changes.

The root has four tabs on the floating `TabBar`, each a large-title page:

- **Home**: the daily hero, `Continue Playing` (only once something has been played), the `Play with friends` card, then `Latest additions` and one shelf per genre, each with See All pushing that shelf's full grid.
- **Play with friends**: `OnlineHub`, see below.
- **Records**: a summary card (games played, daily streak, records), the daily challenge row, and every personal best.
- **Search**: a search field over `Browse` cards for each genre, the online games and the whole library.

Shelves pan sideways through `TileRail`, which claims the press with an `InvisibleButton` so a swipe never drags the phone window, locks to the first axis the pointer travels along, flings on release and shows paging arrows on hover; grids pick three to six columns from the content width. Tiles are accent-gradient squircles with the game's painted `AppIconTile` icon or its `AppIconArt` vector art (or `OnlineGameArt` for the online games), a `NEW` pill inside the thirty-day window, a people badge on online entries, and a hover lift on a per-entry `Spring`. Tapping a local tile opens the game; tapping an online tile switches to the online tab with that game's card highlighted.

The app is an `ITabRouteTarget`: `games.tab.home`, `games.tab.together`, `games.tab.records` and `games.tab.search` open a tab, and `GamesApp.PlayRoute(id)` (`games.play.<id>`) opens a game directly, which the medium Daily Game widget uses for its recent (or, before anything has been played, latest) games.

The server picks the featured game when it can: `FeaturedIndex` (called from `RebuildLayout`) uses `coins.Wallet?.FeaturedGameId` (a field on the coin wallet DTO in src/Aetherphone/Core/Aethernet/Contracts/CoinDtos.cs) when it names a game in the array, and otherwise falls back to the daily rotation `GameStatsStore.TodayIndex * FeaturedStep % games.Length`. Whichever wins, its id lands in `stats.DailyGameId`, which makes it the daily challenge: opening that game starts it with `GameSeed.Daily(id, GameStatsStore.TodayIndex)`, so everyone on the daily plays the same board, and the intro says "Today's board".

### Routes and the running game

Navigation uses a `ViewRouter<GamesRoute>` over five screens (`Root`, `Shelf`, `Playing`, `OnlineRoom`, `Leaderboard`). Tapping a tile calls `OpenGame`, which begins a `GameSession` for the game's spec (mode from `GameStatsStore.LastMode`, a fresh or daily seed), resets the backdrop, effects, intro and pause state, stamps the game as played, and pushes `Playing`. The back chip pops the route, and `GamesApp.Draw` calls `CloseCurrentGame` (which calls `game.Close()`) once the transition lands back on the launcher. `OnlineHub` (src/Aetherphone/Apps/Games/Online/OnlineHub.cs) is the friends lobby on its own tab: one host card per online game, the join-by-code field, and the player's open rooms, with pull to refresh and a manual retry when the room list fails; `OnlineRoomView` is the room itself. When a round ends, `OnlineFinishHold` (src/Aetherphone/Apps/Games/Online/OnlineFinishHold.cs) keeps the finished table on screen until its last card flight or shot replay has settled, then shows a five-second countdown card (tap to skip) before the room shows the lobby again. The store and protocol behind these screens are covered under "Play with friends: online rooms" below.

The hub also owns the coin plumbing that wraps every game. `OpenGame` and `CloseCurrentGame` report the play session to the backend through `CoinGameSessionTracker` (`GameOpened` and `GameClosed`), a chip on the chrome row (left of the pause chip) counts the open session toward the server's earning thresholds, and `GamesApp.Draw` polls `coinSessions.TakeAward` to spawn a floating coin reward when the server grants one. None of this reaches the games: an `IMiniGame` only ever sees its `GameContext`.

While a game is active, `GamesApp.DrawActiveGame` draws, in order, inside `AppSurface.BeginEdgeToEdge(content)`: the backdrop over the whole content rect, then the game (or its idle preview under the intro), then the HUD from the game's `HudModel`, then the screen effects, then whichever session overlay applies (intro, countdown banner, result card, pause menu), and finally the chrome chips on top: the back chip at the top-left, the pause chip at the top-right (a Pause glyph while playing, Play while paused, hidden on the intro and result), and the coin session chip to its left. There is no header: the game rect is the full content rect edge to edge.

Each frame the host builds the `GameContext`:

| Field | Meaning |
| --- | --- |
| `Full` | The whole content rect. Backdrops and worlds draw here |
| `Safe` | `Full` inset 12 left and right, 96 on top (56 in Compact), 16 at the bottom, all times `UiScale.Current`. Boards fit inside it |
| `Theme` | The current `PhoneTheme` |
| `DeltaSeconds` | The simulation delta: clamped to 0.1, zero unless the session is Playing and the phone has focus, scaled by `ScreenFx.SlowMo` |
| `RawDeltaSeconds` | The clamped frame delta regardless of state, for things that keep moving while paused (entrance springs, idle art) |
| `Session` | The `GameSession`: `Report` scores to it, `Finish` once, read `Start`, `Seed`, `Daily`, `Best`, `Mode` |
| `Hud` | The `HudModel` to fill this frame |
| `Fx` | The host-owned `ScreenFx` |
| `Backdrop` | The `StageBackdrop`, for `SetSky` and `Ink` |
| `Body`, `Stats` | Legacy fields for the adapter: the chrome-inset body and the stats store. A stage game never reads them |

`GameFocus.Active` (src/Aetherphone/Apps/Games/Framework/GameFocus.cs) is false while the phone window is unfocused or the game's own text input is active. A clocked stage game whose session is Playing is paused into the pause menu the moment focus is lost; a turn-based game (`Clocked = false`) simply receives a zero delta and stands still. Because the phone UI is Dear ImGui (an immediate-mode UI where everything is redrawn from scratch every frame), `Draw` runs every frame and the game keeps its own state in fields between frames.

### Checklist for registering a new game

1. Create a folder src/Aetherphone/Apps/Games/YourGame with a `YourGameApp : IMiniGame`. Most games split logic into a `*Board` class and drawing into a `*Renderer` class.
2. Declare a `private static readonly GameSpec Spec` with the id, `L.Games.YourGame` title, `L.YourGame.Hook`, genre, backdrop, HUD style, score kind, modes and flags, and return it from `Spec`.
3. Add `new YourGameApp()` to the `games` array in the `GamesApp` constructor.
4. Add the `Title` string to L.cs (the `Games` section) and a nested `L.YourGame` class with `Hook`, plus both keys in the nine language JSONs (see [localization.md](localization.md)). A game with many strings gets its own section, as `L.Coil`, `L.Updraft` and `L.Swoop` do.
5. Add a row for your id to `GamesLibrary.Releases` with the release date, so the game sorts newest-first, joins the `Latest additions` shelf and wears the `NEW` pill for its first month.
6. Add an accent color keyed by your game id in src/Aetherphone/Core/Apps/AppAccents.cs; `IMiniGame.Accent` defaults to `AppAccents.For(Spec.Id)`.
7. Optionally add a painted icon for your id (`AppIconTile`, src/Aetherphone/Windows/Components/Chrome/AppIconTile.cs) or vector art in src/Aetherphone/Windows/Components/Chrome/AppIconArt.cs. The tile tries `AppIconTile` first, then `AppIconArt`, and falls back to drawing your title text.
8. Add a case to `GamesLibrary.BestRecord` for the launcher's best-score line (tiered ids go through `BestTimeAcrossTiers` or `BestLevelAcrossTiers`).
9. Seed the board from `start.Seed` (or `start.Random`) and add a `SameSeedReplaysIdentically` test for it.

## Play with friends: online rooms

The online games are not `IMiniGame`s: the Aethernet backend runs their rules, and the client renders a table and sends actions. They need an Aethernet account; signed out, `OnlineHub` shows a sign-in card that opens Settings.

**The store.** `GameRoomsStore` (src/Aetherphone/Core/Games/GameRoomsStore.cs) owns everything online for the hub:

- The room directory (the rooms this account is in) through `GamesClient.RoomsAsync`, on a `PollCadence` of 30 seconds while the phone is visible and 120 seconds while it is hidden. Opening the tab calls `EnsureFresh`, pull to refresh calls `RefreshNow`, and every intent answer or `game.ended` signal asks for an immediate refresh.
- Intents: `CreateRoom(kind)`, `JoinByCode`, `LeaveRoom`, and the host's `CloseRoom` and `Kick`. Each runs in the background and lands as one `GameRoomAnswer` that the draw code picks up with `TakeRoomAnswer`.
- Actions: `SendStart`, `SendPlay`, `SendDraw`, `SendPass`, `SendMove`, `SendResign`, `SendDrop`, `SendShoot`, and `SendPlace`, all thin wrappers over one private `SendAction`. Every request carries the roster's `ActionCount`; the server refuses a mismatch as stale instead of applying it twice, and a stale answer triggers an immediate room refresh. The result lands through `TakeActOutcome`.
- The live room, a `GameRoomSession` (src/Aetherphone/Core/Games/GameRoomSession.cs) fed by `game.*` signals from `RealtimeSignalBus` (see [Networking](networking.md)). Epoch and sequence numbers decide whether an event applies, a gap asks for a fresh snapshot instead of guessing, Uno's private hand rides its own lane, and the server clock offset is smoothed so turn countdowns stay honest. While the socket is down, the room is not attached, or a snapshot is pending, the store polls the room over HTTP instead (3 seconds visible, 10 seconds hidden); a 404 closes the room locally.

**The state.** `GameRoomSession.Build` parses each snapshot by its `GameKind` into a `GameRoomState` that carries one typed board per kind (`Uno`, `Chess`, `Pool`, `ConnectFour`, DTOs in src/Aetherphone/Core/Aethernet/Contracts/GamesDtos.cs) plus a kind-agnostic `GameRoomRoster` (host, players, action count, winner) that the lobby, the roster and the act guard read.

**The screens.** `OnlineHub` is the lobby tab: one host card per kind in `OnlineGameArt.Kinds`, join by code, and the open rooms. `OnlineRoomView` draws one room: the lobby and roster while it waits, then the table for whichever board the state holds (`OnlineUnoTable`, `OnlineChessTable`, `OnlinePoolTable`, `OnlineConnectFourTable`), and `OnlineFinishHold` once a round ends. A table that wants the whole screen says so through `OnlineRoomView.WantsLandscape` (today only Pool), and `GamesApp.SyncOnlineRoomLandscape` requests or releases the landscape lock every frame. `GamesOnlineText` maps kinds and server reason codes to localized text; `OnlineGameArt` holds each kind's medallion, accent id and seat cap.

### Checklist for adding an online game

The server has to know the kind first: game kinds and their rule engines live in the Aethernet backend repository. Then, on the client:

1. Add the kind constant, plus any action, end-reason or board-size constants, to `GameRoomWire`.
2. Add the room state DTO to GamesDtos.cs, register it in `AethernetJsonContext`, add a field for it to `GameRoomState`, and teach `GameRoomSession.Build` to parse it and build its `GameRoomRoster`.
3. Add a `Send*` wrapper to `GameRoomsStore` for any new action, extending `GameRoomActionRequest` if the action needs a new field.
4. Write `Online<Name>Table` in src/Aetherphone/Apps/Games/Online, construct it in `OnlineRoomView`, and dispatch to it in `DrawTable`, `ShowsTable` and `FinishedText`.
5. Add the kind to `OnlineGameArt.Kinds` and give it a branch in `OnlineGameArt.AccentId` (unknown kinds fall back to `uno`) and in `OnlineGameArt.Draw`, plus a seat cap in `MaxPlayers` when it is not a two-player game. Add a matching host anchor to `OnlineHub.HostIds` (the array lines up with `Kinds`) and a branch in `OnlineHub.HostHint`.
6. Add its name to `GamesOnlineText.GameName`, and every new string to L.cs and the nine JSONs.
7. Add an accent keyed by the accent id in src/Aetherphone/Core/Apps/AppAccents.cs (Connect Four uses `connectfour`) and a `GamesLibrary.Releases` row for `online.<accent id>`, so the entry dates, sorts and wears the `NEW` pill like a local game.

## The stage kit

The kit owns the frame; the game owns the world. Intro, countdown, pause, result, HUD layout, score persistence, leaderboard submission and chrome belong to the host. A game supplies its world, its rules, its HUD model and its effects. Everything lives in src/Aetherphone/Apps/Games/Framework, with one exception: `RollingValue` sits with the shared animation code in src/Aetherphone/Core/Animation because the rest of the phone uses it too.

### Session and flow

The host owns one `GameSession` per run. Its `State` is a `StageFlow`: `Intro`, `Countdown`, `Playing`, `Paused`, `Result`.

- **Intro** (`StageIntro`): the backdrop runs, the game's `DrawIdle` shows dimmed behind, and the kit draws the title (fit to the Safe width), the hook line, a Best pill and a Rank pill ("#12 · Global", "Not ranked" or "Sign in to rank"), a `SegmentStrip` when `Spec.Modes` has more than one entry, the Play button and a Leaderboard text button, all staggered in. Space or Enter also starts. Picking a mode calls `session.SelectMode`, which persists through `GameStatsStore.LastMode(gameId)` and reloads the Best for that mode's stat id.
- **Countdown**: only for `Spec.Countdown`. "3, 2, 1, Go" with `GameBanner`, 0.6 seconds per step with a `GameTick` each, the world visible and frozen (`DeltaSeconds` is zero).
- **Playing**: the game ticks with `context.DeltaSeconds`.
- **Paused** (`StagePause`): the pause chip, or focus loss for a clocked game. A 0.72 veil with Resume, Restart, Leaderboard and Quit stacked as `GameHud.Button`s. A turn-based game never auto-pauses; its delta is simply zero while unfocused.
- **Result** (`GameOverlay.DrawStage`): the card with the title, the New Best badge, the primary stat label and counting value, a rank line ("#8 of 1,240 · Global", "#2 among friends", "Uploading" with a spinner, or "Kept on this phone"), up to four stat lines in a two-by-two grid, Play again and a Leaderboard text button. Confetti and `GameWin` on a new personal best; a gold palette when the global rank is 10 or better.

Game side, the whole contract is two calls:

```csharp
context.Session.Report(board.Score);
context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, Spec.Id)
    .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
    .WithStat(L.Games.Time, TimeText.MinutesSeconds(board.Seconds)));
```

`Report` feeds the score pill and the beating-best glow every frame. `Finish` is accepted once per run (later calls are ignored): the session submits to `GameStatsStore` by `ScoreKind` (`SubmitScore` for Score and Level, `SubmitTime` for Time, `RecordWin` or `ResetStreak` for Streak by `Won`), submits the optional secondary stat (`WithSecondary`, for Updraft's height or Pairs' attempts), completes the daily, hands a `ScoreSubmission` to the `IScoreSink`, moves to Result and asks the `IRankSource` for the rank. A game never calls `Stats.Submit*` itself.

### HUD

Games stop placing pills. Each frame a game fills `context.Hud`:

| Call | Capsule |
| --- | --- |
| `Score(int)` | The primary pill on the chrome row, rolling through `RollingValue`, glowing while beating Best |
| `Timer(left, total, urgent)` | Clock glyph, `TimeText.MinutesSeconds`, a draining bar, red pulse when urgent |
| `Lives(left, max)` | Up to five hearts; "x7" with one heart beyond that |
| `Level(int)` | "LV 12" |
| `Combo(in ComboMeter)` | "x3" with a draining window bar, coloured from accent to warm to white-hot by heat; shown from two hits, asks `ScreenFx.EdgeGlow` from multiplier 3 |
| `Best(int)` | Trophy glyph and the value |
| `Custom(width)` | Reserves a slot the game draws itself (Tetris next piece); read `Hud.CustomRect` the next frame and paint its background with `StageHud.Capsule` |

Secondary capsules sit centred under the score pill in the order Timer, Lives, Level, Combo, Best, Custom; at most four show, and the kit drops Best first, then Level. `HudStyle.Compact` puts the score left of centre and a single capsule right of centre on the chrome row, which buys 40 units of board height. Every label is cached in the kit (`GameNumber.Label`, `TimeText.MinutesSeconds`, `LabelSlot`), so filling the model allocates nothing.

### Geometry

All sizes are design units times `UiScale.Current`. `StageLayout` holds the numbers: the chrome band is the top 52, the chips are 36 glass circles centred 28 in from either side at y 26, the score pill is centred on that row with 72 reserved per side, the secondary row sits at y 71 with 28 tall capsules and 8 gaps, and `Safe` insets 12 left and right, 96 on top (56 in Compact) and 16 at the bottom. Boards fit inside `Safe`; worlds and backdrops use `Full`. Games with a `GamePad` float it over the bottom band (`StageLayout.PadBand`, 110 for a d-pad, 70 for the shooter pad) on a frosted fill while the world continues behind it.

### Backdrops

`StageBackdrop` draws first every frame, clipped to `Full`: a two-stop gradient, three parallax layers, a vignette (bottom 34 percent plus the top corners) and a light sweep. The preset comes from `Spec.Backdrop`:

| Preset | Look | For |
| --- | --- | --- |
| `Nebula` (default) | Accent darkened to near-black, twinkling star motes, three accent glow blobs, drifting dust | Puzzle, brain, arcade |
| `Sky` | Five bands from dawn to deep night driven by `backdrop.SetSky(progress)`, stars, sun or moon, two cloud rows | Flap, Updraft, Swoop, Hop, Blade |
| `Felt` | Deep accent cloth, a spotlight pool at the top, two slow weave bands | Solitaire, Reversi, Chess, Pairs |
| `Meadow` | Sky to grass, sun, cloud banks, two hill layers, swaying grass blades | Whack, Snake |
| `Neon` | Near-black blue, a horizon with a perspective grid scrolling toward the viewer, accent haze bands, sparks | Skyfall, Invaders, Squadron, CapMan, Beat, Breakout, Tetris |
| `Cavern` | Charcoal brown to black, faint crystal facets, stalactite silhouettes, drifting specks | Crystal Drop, Coil, Bubbles, Water Sort |
| `Paper` | Warm off-white, two soft accent circles; the only light preset, so `backdrop.Ink` is `StageInk.Dark` | Sudoku, Nonogram, Sweeper, Word Run, Trivia, 2048, Flow |

Layers shift with the pointer (2, 5 and 9 units across the rect, through a `Spring`) and, when the game calls `backdrop.SetCamera(in camera)` each frame, with the camera at parallax 0.05, 0.15 and 0.35. `ScreenFx.Sweep()` fires the light sweep; the kit fires it on a new best. Layers move slower than 20 units per second so they never distract (Reduce Motion is not a setting on this phone).

### Boards and cells

`GameScene.Arena` is retired for stage games (it still exists for the legacy adapter window). Grid games use `BoardPlate.Draw(drawList, rect, radius, scale, accent, backdrop.Ink)`: an accent glow beneath, a floating shadow, a fill that reads as glass over the backdrop (the last drawn ground colour darkened, or white on Paper), a one unit rim and a top sheen. `BoardPlate.Around(gridBounds, scale)` gives the plate rect with its 10 unit padding. Cells go through `StageCell.Draw(drawList, rect, fill, depth, radius, scale)` with a `CellDepth` of `Raised` (drop shadow and top highlight), `Flat`, `Sunken` (inner shadow) or `Pressed` (Raised, shrunk 4 percent); `StageCell.Lift(progress)` returns the 0 to 3 unit lift for `GameJuice.PopIn` entrances. Light comes from the top-left in every game. Non-grid worlds use no plate: the world is the full rect and the backdrop is the floor.

### Camera

World games own a `Camera2D` (a struct; create it with `Camera2D.Create()`). The sim works in world units (the board decides the unit, for example one cell), and the camera derives the zoom from the view each frame so the game never depends on the window size:

| Member | What it does |
| --- | --- |
| `Fit(view, worldWidth, worldHeight, FitMode)` | Sets `View`, `Anchor` (the view centre) and `Zoom` in pixels per unit: `Contain`, `CoverWidth` or `CoverHeight`. Places the origin at the world centre on the first call |
| `Place(origin)` | Snaps the origin |
| `Follow(target, lead, smoothTime, deltaSeconds)` | Springs the origin toward `target + lead` |
| `Punch(amount)` | A zoom kick (0.04 to 0.08 for a big hit) that decays over 0.25 seconds |
| `Shake(trauma)` | Screen shake with the same curve as `FeedbackFx.AddTrauma`; the offset applies to the camera, never to a rect |
| `Update(deltaSeconds, pixelScale)` | Decays punch and trauma; call once per frame |
| `ToScreen(world)`, `ToWorld(screen)`, `Px(length)`, `Units(pixels)`, `VisibleWorld` | The mapping. World +Y is screen +Y |

World +Y pointing down matches the screen; a game that thinks in altitude negates Y. `ScreenFx.ApplyTo(ref camera)` each frame forwards any `Fx.Punch` to the camera; a game without a camera gets the plate scaled instead (`Fx.PlateScale`).

### Effects

- **`ScreenFx`** (host-owned, `context.Fx`): `Flash(color, alpha)`, `Vignette(color, strength, seconds)` for the danger pulse, `EdgeGlow(strength)` for combo heat, `SlowMo(factor, seconds)` which scales the game delta only (backdrop, particles and HUD keep running), `Punch(amount)`, `Sweep()`, `TimeScale`, `PlateScale`, `ApplyTo(ref camera)`.
- **`FeedbackFx`** (world-side, one per game): `AddTrauma`, `ShakeOffset`, `HitStop` and `ScaleDelta`, `Flash` and `DrawFlash`, `Shockwave` and `DrawRings`, `AddText` and `DrawText`, `Update`, `Clear`. The hit-stop contract is strict: call `ScaleDelta` exactly once per frame with the raw delta, feed its result to the simulation only, and feed the raw delta to the feedback systems so shake and particles keep animating during the freeze.
- **`ParticleSystem`**: a fixed-capacity pool (512 by default). `Burst`, `Sparkle`, `Streaks` and `Confetti` as before, plus `Emit(in ParticleSpec, origin, count)` for custom emitters and an `Emitter` struct (`new Emitter(spec, rate)`, then `emitter.Advance(deltaSeconds, position, particles)` for continuous trails). Shapes are `Circle`, `GlowCircle`, `Square`, `Star`, `Streak`, `Ring` (expanding stroke), `Shard` (rotating triangle), `Spark` (three-dot trail) and `Glyph` (one cached character, for +1 and combo digits). `ParticleSpec` carries start and end colour, size, speed, life, gravity, drag, spin, spread, direction, shape, a `SizeCurve` (`Shrink`, `Grow`, `Pulse`) and an additive flag that draws a halo. `Draw(drawList, scale)` draws screen-space particles; `Draw(drawList, in camera)` maps world-space particles through the camera and scales their sizes by the zoom. Particles draw from a `GameRandom`; `Reseed(seed)` makes them deterministic too.
- **`Ribbon`**: a ring buffer of 24 points for the trail behind a ball, bird, blade or snake head. `Push(point)` each frame, `Draw(drawList, color, width, additive)` tapers width and alpha from head to tail; the camera overload maps world points. One instance per trailing object.
- **`ComboMeter`**: a struct with `Hit()` (returns the new multiplier: 1, 2, 3, 5, 8 at 1, 4, 8, 12 and 20 hits), `Update(deltaSeconds)` (resets after `WindowSeconds` without a hit, then cools `Heat`), `Reset()`, `Count`, `Multiplier`, `Heat`, `WindowFraction`. Hand it to `hud.Combo`.
- **`GameSfx`**: `CountdownTick`, `ComboTierUp`, `NewBest`, `LevelClear`, all routed through `UiFeedback.Play`. Games keep calling `UiFeedback.Play` for world hits (`GameHitSoft`, `GameClear`, `GamePowerUp`, `GameMatch`, `GameWrong` and friends in src/Aetherphone/Core/Notifications/UiSound.cs) and never play files directly. Those entries sit on the `Game` channel, which the player can switch off on its own in Settings > Sounds; `GameWin` is on the `Event` channel so the new-best chime still sounds with game sounds off.

### Determinism

`GameRandom` is xoshiro128** as a mutable struct: `FromSeed(ulong)`, `Fresh()`, `NextUInt()`, `Next(max)`, `Next(min, max)`, `NextFloat()`, `Range(min, max)`, `Chance(probability)`, `Sign()`. Every board takes a `GameRandom` in its constructor or `Reset(seed)`; `new Random()` is banned under Apps/Games. `GameSeed.Fresh()` mixes the tick count with the stopwatch; `GameSeed.Daily(gameId, dayIndex)` is an FNV-1a hash of the id mixed with the day, so the daily board is the same for everyone. `GameStart.Random` hands a game the seeded source directly. Every board test suite gains a `SameSeedReplaysIdentically` case.

### Juice helpers that carry over

- `GameJuice.Advance(progress, deltaSeconds)` drives a 0-to-1 entrance value, `GameJuice.Stagger(progress, index, count)` splits it across cells so tiles appear in sequence, and `GameJuice.PopIn(progress)` maps it through `Easing.EaseOutBack` for an overshooting pop.
- `GameGrid.Centered(area, columns, rows, gapFraction)` computes a centered square-cell grid; `Cell(column, row)` and `CellCenter(column, row)` give you rects and centers, `Bounds` the whole board.
- `GameHud.Button(center, size, label, accent, theme)` is the accent button the kit uses for Play, Resume and Play again; `GameHud.Pill` and `GameHud.ScorePill` remain for legacy games.
- `GamePalette` holds the shared dark board colors plus `InkOn(fill)` to pick readable text ink; `GameNumber.Label(int)` returns a cached string so score text does not allocate every frame; `LabelSlot.Get(locString, value)` caches one formatted label per value and language.
- `RollingValue` animates a displayed integer toward a target and pops on change; `StageHud` drives it for the score pill.

### Input, clocks, sprites, banners

- `GameInput` is the only way a game may read the physical keyboard. `GameInput.Claim()` returns false unless `GameFocus.Active`; when it returns true it has raised `io.WantTextInput` for this frame and cleared the game client's key state for every key a game consumes. Dalamud honours `WantTextInput` (it swallows the key messages and clears `KeyState` on its input frame); it does not honour `WantCaptureKeyboard` against the game at all, so a game that only calls `SetNextFrameWantCaptureKeyboard` still walks the character with WASD. The convenience readers `Held(key, alternate)` and `Pressed(key, alternate, repeat)` call `Claim` for you and OR the two keys you pass (by convention a WASD key and its arrow); single-key overloads `Held(key)` and `Pressed(key, repeat)` exist too, and `Claim(ReadOnlySpan<VirtualKey>)` lets a game claim a narrower key set than the default (letters, digits, arrows, Space, Escape, and the editing and modifier keys). Call them only while the session is Playing, so the keyboard returns to the client the moment play stops; the intro claims Space and Enter itself.
- `GamePad.DPad(area, accent, theme)` draws a W/A/S/D cross and returns the `PadDirection` pressed this frame (press-fired, one per frame). `GamePad.Shooter(area, accent, theme)` draws A, W, D and returns `ShooterPadInput` with `Left` and `Right` held and `Fire` pressed. `DPadHeight(scale)` and `ShooterHeight(scale)` size the band. Games combine pad and keyboard themselves: `var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);`.
- `new Substeps(deltaSeconds, maxStepSeconds)` gives `Count` and `Step` for a loop that advances fast projectiles without tunnelling; the count is capped at 16 so a stall never becomes a burst. `FixedStepClock(step, maxCatchUp)` is the alternative for sims that must run on an exact tick: `Advance(delta)` returns how many steps to run, `Alpha` is the render interpolation fraction, `Reset()` on restart.
- `PixelSprite` takes bitmap rows (`#` lit) once, at static init, and `Draw(drawList, topLeft, unit, color)` emits one rect per lit run. It is the sprite path for Invaders-style games; draw it in the game's accent with a `ProgressRing.Glow` behind it, never in a flat ink.
- `GameBanner.Draw(drawList, center, text, accent, theme, progress)` pops a frosted pill in over the first 18% of `progress`, holds, and fades over the last 25%. Drive `progress` with `GameBanner.Advance(progress, delta, lifetimeSeconds)`. Use it for stage and wave text that must hold; `FeedbackFx.AddText` rises and fades and is for score pops.

### Games with data files

Word Run reads its word banks from src/Aetherphone/Words (`<code>.answers.txt` and `<code>.valid.txt`, one word per line, shipped as content next to the plugin). They are generated, never hand-edited: `tools/build-word-banks.ps1` rebuilds them from SCOWL and the FrequencyWords lists, and THIRD-PARTY-NOTICES.md records both sources. Doom keeps no data in the repo at all; `DoomAssets` downloads the shareware episode, optionally Freedoom (Phase 1 and 2), and the soundfont into the `doom` folder under the plugin's config directory, verifies them against pinned SHA-256 checksums, and also runs any commercial IWAD the player drops there (`doom.wad`, `doom2.wad`, `plutonia.wad`, `tnt.wad`).

## The motion exception

The rest of the phone uses critically damped motion: springs that settle without overshooting (see `Spring.Step` in src/Aetherphone/Core/Animation/Spring.cs). Games are the place allowed to bounce. `Easing.EaseOutBack` (an easing curve that overshoots its target and settles back) is defined in src/Aetherphone/Core/Animation/Easing.cs and is referenced only from files under src/Aetherphone/Apps/Games plus two Casino cabinet sites (BingoCabinet.cs and BingoCardArt.cs). Keep it that way: bouncy easing belongs to games and casino cabinets only. Inside a game, reach for `GameJuice.PopIn`; everywhere else, use springs. The kit's own motion (backdrop parallax, pause veil, camera follow) runs on `Spring`.

## Scoring, streaks, and the daily challenge

`GameStatsStore` (src/Aetherphone/Core/Games/GameStatsStore.cs) is the only persistence a run touches, and in a stage game the session touches it, never the game. It wraps `Configuration` through the `IGameStatsConfiguration` interface (so tests substitute a fake), which stores a `List<GameStatRecord>`, a `List<GameModeChoice>`, `DailyChallengeStreak` and `DailyChallengeLastDay`. See [state-and-persistence.md](state-and-persistence.md) for how `Configuration` is saved.

| Member | Semantics |
| --- | --- |
| `Get(gameId)` | Returns a `GameStats` value (`BestScore`, `BestTimeSeconds`, `Streak`); zeros if never played |
| `SubmitScore(gameId, score)` | Higher is better; returns true only on a new best |
| `SubmitTime(gameId, seconds)` | Lower is better; returns true only on a new best |
| `RecordWin(gameId)` | Increments and returns a win streak (Pairs, Reversi, Chess) |
| `ResetStreak(gameId)` | Clears the streak on a loss |
| `MarkPlayed(gameId)`, `LastPlayed(gameId)` | Stamp and read the last-played time the launcher sorts `Recent` and `Records` by; the hub calls `MarkPlayed` when it opens a game or an online room, so a game never needs to |
| `LastMode(gameId)`, `SetLastMode(gameId, mode)` | The remembered `Spec.Modes` index per game, used by the intro's mode strip |
| `TetrisModern`, `WordBank` | Legacy views kept for the unmigrated Tetris (a view over `LastMode("tetris")`) and Word Run (the bank code) |
| `TodayIndex` (static) | UTC day number behind the daily challenge and the featured rotation |
| `DailyGameId`, `DailyDone`, `DailyStreak` | Daily challenge state; the launcher sets `DailyGameId` and its streak chip reads `DailyDone` and `DailyStreak` |

Stat ids may carry a difficulty suffix, for example `sudoku.easy` or `minesweeper.easy`, or a mode suffix like `tetris.modern`, `match3.blitz` or `updraft.height`. A stage game declares them through `Spec.ModeStatIds` (parallel to `Spec.Modes`) or passes them in its `GameOutcome` (`StatId`, `WithSecondary`). Every submit path first calls the private `RecordDailyPlay`, which prefix-matches the stat id against `DailyGameId` (so `sudoku.easy` counts for a `sudoku` daily) and advances or resets the streak based on `TodayIndex`. Finishing the featured game through the session completes the daily automatically, a recorded loss included; there is no separate daily API.

The leaderboard seam sits next to the store in src/Aetherphone/Core/Games: `IScoreSink.Submit(in ScoreSubmission)` receives `{ StatId, Value, Kind, Seed, Daily, GameId }` after every `Finish`, and `IRankSource.TryGetRank(statId, out GameRank)` returns `{ Rank, Total, FriendsRank, WeekRank, State }` with a `RankState` of `Unknown`, `Uploading`, `Ranked`, `SignedOut` or `Failed`. `LeaderboardStore` implements both; `NullScoreSink` and `NullRankSource` remain for tests and for a `GamesLibrary` built without a store.

## Global leaderboard

The leaderboard is a social feature, not an economy: it moves no coins, unlocks nothing and awards no badges. The plugin is open source, so every score is a claim, and the server defends the boards with per-game plausibility caps against the timed coin play session, one upload per game per five seconds, weekly boards that reset damage, and a `game_score` report target. The client's job is to upload honestly and show the result.

**What uploads.** `LeaderboardStore.Submit` (the `IScoreSink`) accepts a finished run only when its stat id is in `ScoreStatIds.All`, the table that mirrors the server's catalog (`ScoresWireContractTests` pins the two lists against each other), the value is positive, and the kind agrees with the catalog (a streak only for a streak stat, so Pairs' win streak never uploads as its time). Accepted runs land in `ScoreUploadQueue`, persisted as `Configuration.PendingScoreUploads` (`PendingScoreUpload { StatId, GameId, Value, Kind, Seed, Daily, QueuedAtUnix }`), one entry per stat id holding only the best (higher for scores, levels and streaks; lower for times and the Pairs attempt count). A queued run survives a sign-out and a plugin reload.

**How it flushes.** One background loop (`StoreWork`) runs while signed in and the queue is not empty: it takes the first due entry, posts `POST /games/scores { gameId, value }` through `ScoresClient`, and resolves the reply. Entries for the same root game are spaced `ScoreUploadQueue.SpacingMilliseconds` (5 s) apart, matching the server cadence. Reasons: an empty reason or `not_better` removes the entry and caches the returned ranks; `too_soon` keeps it and waits out the cadence; `unknown_game`, `implausible` and `hidden` drop it and mark the stat as failed. A transport failure (`Offline`, `Timeout`, a 5xx) keeps the entry and retries 30 s later; a 4xx drops it. A better score queued while its predecessor is in flight is kept (the reply only removes the value it uploaded). The loop restarts on every `Submit`, on sign-in and on a realtime reconnect.

**Ranks.** `TryGetRank` (the `IRankSource`) reads, in order: `SignedOut` while no session is signed in; `Uploading` while the stat is queued or in flight; `Failed` after a refusal or a transport failure until the next attempt starts; `Ranked` from the last reply for that stat; otherwise the entry from `GET /games/scores/me`, which the store fetches when the hub opens (60 s TTL) and again after every accepted upload. `LeaderboardStore.Version` increments on every change; `GamesApp` watches it each frame, calls `session.RefreshRank()` so the intro and result pills move from "Uploading" to "#12 · Global" on their own, and refreshes the launcher's rank chips through `GamesLibrary.RefreshRanks`.

**Boards.** `Board(key)` returns a `LeaderboardBoard` snapshot per `LeaderboardKey(statId, scope, span)`; `EnsureFresh` fetches `GET /games/scores/{statId}?scope=global|friends&span=all|week&limit=50` with a 60 s TTL and a 30 s cooldown after a failure, `RefreshNow` ignores the TTL (pull to refresh). Reads go through the transport like every other client, so a 429 host pause applies and surfaces as `AepFailureKind.RateLimitPaused`. The store never throws; a failure sits on the snapshot as an `AepFailure`.

**Screens.** `GamesScreen.Leaderboard` (`GamesRoute.LeaderboardOf(gameId, statId)`) is pushed by the intro, pause and result Leaderboard buttons and by a row of the Records tab; the game underneath stays open, so the back button returns to it. `GamesApp.Leaderboard.cs` draws the game title in the large-title header, a mode strip when the game has more than one stat id (derived from `Spec.Modes` or, for the games that predate the stage kit, from the catalog: Easy, Medium, Hard, Classic, Modern, Blitz, Height, Attempts), the Global / Friends and All time / This week `SegmentStrip` pair, then a `GroupCard` of rows: rank number, avatar (`AvatarView.DrawRemote`), name with badges (`UserName.DrawAuto`), `@handle`, and the value formatted by the stat's kind (`TimeText.MinutesSeconds` for times). Your own row is tinted with the game's accent, and when you are ranked but outside the loaded top 50 it is pinned in its own card under the list. Signed out, the screen is a `GamesHubArt.StateScreen` that opens Settings; an empty board says "No scores yet". The Records tab shows a "Your ranks" `GroupCard` from `MyRanks` above the personal bests (title with the tier or mode, "#12 of 1,240 · #3 this week", tap to open that board), and tiles show a small "#12" chip after the best, cached per `GamesLibrary` rebuild. Settings > Privacy carries the "Show me on leaderboards" switch, which posts `POST /me/games-privacy` and refreshes `CurrentUser` from the reply; a hidden account's uploads come back as `hidden` and read "Kept on this phone".

## Worked example: a minimal grid game

A complete tap-the-cells game showing the stage frame shape. Real games split simulation into a `*Board` and drawing into a `*Renderer`; this one is small enough to skip that. Because the round runs on a 15 second clock, the spec sets `clocked: true` (focus loss pauses it) and `countdown: true` (the 3, 2, 1 before play). The game never draws a backdrop, pills, a start screen or a result: it fills the HUD, reports the score and finishes once.

```csharp
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tap;

internal sealed class TapApp : IMiniGame
{
    private const string GameId = "tap";
    private const float RoundSeconds = 15f;
    private const int Columns = 4;
    private const int Rows = 5;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Tap, GameGenre.Arcade, L.Tap.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private int litCell;
    private int score;
    private float timeLeft;
    private float entrance;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public void Start(in GameStart start)
    {
        random = start.Random;
        score = 0;
        timeLeft = RoundSeconds;
        entrance = 0f;
        finished = false;
        combo.Reset();
        particles.Clear();
        fx.Clear();
        litCell = random.Next(Columns * Rows);
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        combo.Update(simDelta);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        var grid = GameGrid.Centered(context.Safe, Columns, Rows, 0.12f);
        var plate = BoardPlate.Around(grid.Bounds, scale).Translate(fx.ShakeOffset(scale));
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, Accent, context.Backdrop.Ink);
        for (var cell = 0; cell < Columns * Rows; cell++)
        {
            var rect = grid.Cell(cell % Columns, cell / Columns).Translate(fx.ShakeOffset(scale));
            var lift = StageCell.Lift(GameJuice.Stagger(entrance, cell, Columns * Rows)) * scale;
            var fill = cell == litCell ? Accent : GamePalette.Cell;
            StageCell.Draw(drawList, rect.Translate(new Vector2(0f, -lift)), fill, CellDepth.Raised, 8f * scale, scale);
        }

        if (!finished)
        {
            Step(grid, simDelta, scale, context);
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        context.Hud.Score(score);
        context.Hud.Timer(timeLeft, RoundSeconds, timeLeft <= 5f);
        context.Hud.Combo(combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(score);
    }

    private void Step(GameGrid grid, float deltaSeconds, float scale, in GameContext context)
    {
        timeLeft -= deltaSeconds;
        if (timeLeft <= 0f)
        {
            finished = true;
            context.Session.Finish(new GameOutcome(score, ScoreKind.Score, GameId)
                .WithStat(L.Games.Combo, GameNumber.Label(combo.Count)));
            return;
        }

        var lit = grid.Cell(litCell % Columns, litCell / Columns);
        if (!UiInteract.HoverClick(lit.Min, lit.Max))
        {
            return;
        }

        var multiplier = combo.Hit();
        score += multiplier;
        litCell = random.Next(Columns * Rows);
        UiFeedback.Play(UiSound.GameHitSoft);
        particles.Burst(lit.Center, 10, Accent, 160f * scale, 3f, 0.5f);
        fx.Shockwave(lit.Center, 40f * scale, Accent, 0.35f);
        fx.AddTrauma(0.06f);
        fx.HitStop(0.03f);
        if (multiplier >= 3)
        {
            context.Fx.Punch(0.04f);
        }
    }
}
```

The `Title` and `Hook` come from `L.Games.Tap` and `L.Tap.Hook`, declared in L.cs and the nine JSONs. The host draws the Nebula backdrop first, then this `Draw`, then the HUD from the four slots filled above, then the chrome. A world game follows the same frame with a `Camera2D`: `camera.Fit(context.Full, worldWidth, worldHeight, FitMode.Contain)` each frame, `camera.Follow(player, lead, 0.3f, context.DeltaSeconds)`, `context.Fx.ApplyTo(ref camera)`, `camera.Update(context.RawDeltaSeconds, scale)`, `context.Backdrop.SetCamera(in camera)`, every world position drawn through `camera.ToScreen`, and `particles.Draw(drawList, in camera)` for world-space particles.

## Per-game patterns worth copying

- **Pin a rules engine with perft before building on it.** Perft counts every legal move sequence to a given depth; the totals for standard chess are published, so any generation bug changes the number. `ChessRulesTests.PerftFromTheStartingPositionMatchesKnownCounts` in src/Aetherphone.Tests/ChessRulesTests.cs asserts depths 1 through 5 (20 up to 4,865,609 nodes) against `ChessBoard.GenerateMoves` with `Make`/`Unmake` round-trips. The search AI in ChessEngine.cs builds on the same `GenerateMoves` and `Make`/`Unmake` surface the perft pins. Do the same for any game with nontrivial rules.
- **Decide what your generator guarantees.** `SudokuBoardTests.EveryGeneratedPuzzleHasExactlyOneSolution` verifies Sudoku puzzles with an independent solution counter, so Sudoku can safely mark a specific digit "wrong". Flow makes the opposite trade: `FlowBoard.Generate` builds a Hamiltonian path (a path visiting every cell once) and cuts it into colored segments, which guarantees at least one solution but not a unique one. Accordingly, `FlowBoard.IsSolved` accepts any complete connected fill rather than comparing against the generator's answer. If your generator cannot prove uniqueness, your win check must validate the player's answer on its own terms.
- **Seed everything.** A board that takes its `GameRandom` from `GameStart` replays identically for the same seed, which is what makes the daily challenge one shared board and what a future replay check will verify. `GameSessionTests` and `GameRandomTests` pin the kit side; each board adds its own `SameSeedReplaysIdentically`.

## Naming rule

Games are named for what they do: Whack, Snake, Stack, Water Sort, Crystal Drop, Flow. Never theme a game name with the word "Aether"; that prefix is reserved for platform features (Aethernet, Aethergram). Check the game titles in L.cs (the `Games` section and the per-game sections such as `Coil`): none uses it, and new ones must not either.

## Gotchas

- **`Finish` once, then stop stepping.** The session ignores a second `Finish`, but the game must stop its own simulation after the first (the `finished` flag above). `DeltaSeconds` is zero in Result, so a game that keeps calling `Step` with it stands still anyway, yet input handlers still run.
- **Hit-stop delta split.** `FeedbackFx.ScaleDelta` mutates the freeze timer, so call it exactly once per frame with the raw delta; only that call counts the freeze down, so feeding it an already-scaled (zero) delta makes the freeze last forever. Pass its result to the simulation only. `FeedbackFx.Update` and `ParticleSystem.Update` early-return when the delta is 0 or less, so feed them `RawDeltaSeconds` if effects should keep moving while the game is paused.
- **Fixed pools drop silently.** `FeedbackFx` caps at 32 floating texts and 12 rings, `ParticleSystem` at its constructor capacity (512 default), `Ribbon` at 24 points. Never build gameplay logic that depends on an emitted effect existing.
- **`GameOverlay` is a single static instance.** Its celebration and count-up state is static and resets when the overlay has not been drawn for 0.25 seconds or its progress moves backwards. One game at a time is fine (the router guarantees that); drawing it twice in one frame is not.
- **Use `GameContext.DeltaSeconds`, not `ImGui.GetIO().DeltaTime`.** The host clamps the delta to 0.1 seconds so a hitched frame cannot teleport the simulation, zeroes it while `GameFocus.Active` is false or the session is not Playing, and scales it by `SlowMo`. Reading the IO delta directly loses all three protections.
- **The custom HUD slot is one frame behind.** `Hud.Custom(width)` reserves the slot this frame and `Hud.CustomRect` holds the rect from the previous layout, so skip drawing into it while its width is zero (the first frame).
- **`WantCaptureKeyboard` does nothing against the game client.** Only `io.WantTextInput` makes Dalamud withhold keys from FFXIV. Read keys through `GameInput`, never through a bare `ImGui.IsKeyDown` behind `SetNextFrameWantCaptureKeyboard`. While a game claims the keyboard, Escape is swallowed too, so the client's system menu opens only after the phone loses focus; that is the intended trade.
- **Difficulty-suffixed stat ids need launcher support.** Stats keyed like `sudoku.easy` prefix-match for the daily via `GameStatsStore`, but `GamesLibrary.BestRecord` picks the record the launcher and the Records tab display, so a new difficulty tier or mode means updating that switch too.
- **`HitStop` alone freezes nothing.** The freeze only happens, and only counts down, inside `ScaleDelta`. A game that calls `HitStop` without routing its simulation delta through `ScaleDelta` gets no pause at all.
- **Legacy games submit on their own.** Until a game is migrated it still calls `context.Stats.Submit*` and draws `GameOverlay.Draw` itself; do not add a `Session.Finish` to a legacy game, or the run is submitted twice. A legacy game also never reaches the leaderboard: only `Session.Finish` feeds the `IScoreSink`.
- **A new stat id is a two-repository change.** `ScoreStatIds.Catalog` must match the server's catalog (`ScoresWireContractTests.StatIdsMirrorTheServerCatalog` pins the list), and the store silently ignores a submission whose stat id is not in it. Add the id to the backend catalog first, then here, with its kind and direction.

## Related docs

- [App framework](app-framework.md): the `IPhoneApp` contract, `AppRegistry`, navigation
- [Creating an app](creating-an-app.md): the full tutorial for a new phone app
- [UI toolkit](ui-toolkit.md): `Typography`, `UiInteract`, `Squircle`, `Metrics`, and friends used throughout the games
- [State and persistence](state-and-persistence.md): how `Configuration` loads, saves, and what belongs in it
- [Localization](localization.md): adding a game's `Title` and `Hook` strings to L.cs and the nine JSONs (genre labels already exist in `GameGenres.Label`)
- [Game integration](game-integration.md): Honorific nameplate titles and the Dalamud services (such as `IKeyState`) the games rely on
- [Networking](networking.md): the Aethernet client and realtime socket the online rooms ride on
- [Testing and release](testing-and-release.md): the test project that hosts the chess, sudoku and stage kit suites

## World pieces

Reusable worlds for games that need more than a grid: destructible terrain (Crater, Lander, Herd), a pseudo-3D lane projection (Trailblaze) and the lane defense trio (Garden Siege). They live in src/Aetherphone/Apps/Games/Framework/World (namespace `Aetherphone.Apps.Games.Framework.World`). Everything except `TerrainTexture` is Dalamud-free, so the rules are pinned by TerrainMaskTests, TerrainPainterTests, LaneProjectionTests, LaneGridTests, WaveSpawnerTests and DripEconomyTests. None of them allocates per frame (`TerrainTexture` creates a texture wrap only when the terrain changed); give a game one instance of each and reuse it across runs.

### TerrainMask

A bit grid of solid cells, row-major in a `ulong[]` (64 cells per word), built as `new TerrainMask(width, height, metresPerCell)`; 640 by 360 cells is the expected size. World units are metres with +Y down, the same space as `Camera2D`, and cell (column, row) covers `column * metresPerCell` to `(column + 1) * metresPerCell`.

| Member | What it does |
| --- | --- |
| `Generate(ref random, style)` | Clears the grid and shapes seeded terrain. `Hills` is rolling ground; `Islands` splits the land with open channels down to the bottom edge and leaves sea at both sides; `Caverns` raises the ground, hollows it with noise caves and hangs a ragged roof from the top edge. It advances the caller's `GameRandom`, and the same seed always gives the same cells |
| `Plateaus`, `SpawnPoints(count)` | Up to `MaxPlateaus` (8) flat plateaus spread left to right, each 21 cells wide on a 640 mask, with clear air above and solid ground below (caves never undercut them). `SpawnPoints` returns `count` plateau centres on the top surface, spread evenly from the leftmost plateau to the rightmost; stand a body of radius r at `point.Y - r`. The returned span is reused by the next call. Carving can of course destroy a plateau later |
| `IsSolid(column, row)`, `IsSolid(point)` | Out of bounds is air |
| `Carve(center, radius)`, `Fill(center, radius)` | Clears or sets every cell whose centre lies inside the circle and returns how many cells changed |
| `CarveRect(rect)`, `FillRect(rect)` | The same for an axis-aligned world rect (Herd's dig and bridge, stamping a hand-made level) |
| `SurfaceY(x)`, `SurfaceY(x, fromY)` | World Y of the top of the first solid cell in that column, scanning down from the top or from `fromY`; `WorldHeight` when nothing is solid. Use the second form under a cavern roof |
| `Normal(point)` | Outward surface normal from a 7-cell disc of samples; straight up when the disc is all air or all ground |
| `Raycast(origin, direction, maxDistance, out hit)` | Steps cell by cell (clipped to the mask) to the first solid cell; `TerrainHit` carries the point, the outward normal, the distance and the cell |
| `CollideCircle(center, radius, out normal, out depth)` | True when the circle overlaps ground: move the body by `normal * depth` and reflect or cancel its velocity along `normal` |
| `TakeDirty(out region)` | The union of changed cells since the last call (generation, `Clear`, and every carve or fill that changed something). `TerrainTexture` consumes it, so a game never calls it while a texture is attached |

### TerrainTexture

The Dalamud half. `new TerrainTexture(textures, mask, TerrainMaterial.Earth)` takes the `ITextureProvider` a game receives from `GamesApp` (as Trivia does) and owns a `TerrainPainter`, which allocates the RGBA buffer once, at mask resolution. There are no mip maps (Dalamud gives plugin textures none), and a 640 by 360 texture drawn near its native size never minifies far. The painter draws a top-to-bottom gradient between the material's `Surface` and `Deep` colours, a brighter topsoil band over the first 10 cells under open air, a 2-cell lit edge in `Edge` on every upward-facing surface (crater floors included), darker wavy strata bands, a shaded underside where ground overhangs air, per-cell grain, and transparent air that keeps the edge colour in its RGB so bilinear sampling never draws a dark rim. Materials: `Earth`, `Sand`, `Stone`, `Lunar`, or any `new TerrainMaterial(surface, deep, edge)`; `SetMaterial` repaints the whole mask.

`Draw(drawList, in camera)` repaints only the dirty region (plus the 10 rows below it, which the topsoil depends on), re-uploads at most once per ImGui frame and only after a change, then draws one `AddImage` quad over the visible part of the world with matching UVs. The replaced texture is disposed a frame later, never inside the frame that may still reference it. Draw the terrain after the backdrop and before bodies and particles, and dispose it with the game.

### LaneProjection

A `readonly struct` for the three-lane runner: a pinhole camera `CameraHeight` above the ground looking along +Z, the horizon at `HorizonY`, the vanishing point at `VanishingX`, the ground at `NearZ` mapped to `BottomY`, and `LaneCount` lanes of `LaneWidth` world units. Rebuild it each frame with `LaneProjection.Fit(view, laneCount, laneWidth, horizonFraction, roadWidthFraction, nearZ, farZ)`, which derives the camera height so the road spans `roadWidthFraction` of the view width at the near plane.

- `ToScreen(laneX, height, depth)`: `laneX` counts lanes from the middle of the road (`LaneOffset(lane)` gives -1, 0 and 1 for three lanes; a player sliding between lanes passes a fraction), `height` is in world units above the ground, `depth` is the distance ahead of the camera.
- `Scale(depth)`: pixels per world unit at that depth; size sprites and coins with it.
- `LaneEdge(edge, depth)`: the ground point of lane boundary `edge` (0 is the left edge of lane 0, `LaneCount` the right edge of the last lane). Draw the road as quads between two depths.
- `Fog(depth)`: 0 at `NearZ` rising to 1 at `FarZ` (quadratic); lerp distant things toward the horizon colour.

Depths at or behind the camera are clamped to a tiny positive value, so they project far below the screen instead of dividing by zero. Cull by depth before drawing.

### Lane defense: LaneGrid, WaveSpawner, DripEconomy

- **`LaneGrid(columns, rows, cellSize = 1)`**: occupancy per cell as a `byte` kind (0 is empty) plus an `int` entity id. `CanPlace`, `Place(column, row, kind, entity)` (false when occupied, outside or kind 0), `Remove`, `KindAt`, `EntityAt` (`NoEntity` when empty), `NextOccupied(column, fromRow, step)` for the first defender ahead of an enemy or a digger's target, and `CellCenter` and `CellAt` in world units with (0, 0) at the top-left.
- **`WaveSpawner(laneCount, capacity = 256)`**: `Load(table)` copies a level's `WaveEntry { Time, Lane, EnemyKind }` array in any order and sorts it by time (ties keep table order). `Endless(ref random, wave, in budget)` builds a seeded wave instead: it spends `budget.BudgetFor(wave)` on kinds unlocked by that wave that still fit the remaining budget, gives each spawn the less loaded of two random lanes, and spaces the spawns evenly, with jitter, across `budget.WaveSeconds`. Each frame `Advance(deltaSeconds, due)` writes the spawns whose time has come into the caller's span and returns how many; spawns that do not fit stay due for the next call. Also `Rewind`, `Clear`, `Finished`, `Remaining`, `Duration`, `SpentBudget` and `Entries`.
- **`EnemyBudget(costs, unlockWaves, baseBudget, budgetPerWave, waveSeconds)`**: the game's endless recipe. Both arrays are indexed by enemy kind and a cost of 0 never spawns; keep them in `static readonly` fields. `BudgetFor(wave)` is `baseBudget + budgetPerWave * (wave - 1)`.
- **`DripEconomy(amount, ratePerSecond, cap)`**: a mutable struct. `Advance(deltaSeconds)` accrues `RatePerSecond` up to `Cap` (raise `RatePerSecond` when a generator is planted), `TrySpend(cost)` deducts only when affordable, `CanAfford` dims a card, `Add(pickup)` returns what fit under the cap, and `Whole` is the display value.
