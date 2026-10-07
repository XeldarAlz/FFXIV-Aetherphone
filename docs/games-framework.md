# Mini-games framework

This page explains how the Games app hosts its mini-games and how to build a new one with the shared framework, called the stage kit: the `IMiniGame` contract, the `GameSpec` a game declares, the host-owned session flow (intro, countdown, pause, result), the HUD model, the backdrops, boards and camera, the effects layer, the scoring plumbing, and the rules that only apply inside games. It also covers the hub around them: the Home, Together, Library and Profile tabs, the leaderboard screen, and the update notice. Read it after [app-framework.md](app-framework.md), when you want to add or change a mini-game. The mini-games themselves are fully client-side and never talk to the Aethernet backend; the `GamesApp` hub around them does, for the coin economy (play-session reporting, the server-picked featured game, coin awards) and for the online rooms of the Together tab, both described below, and for the opt-in global leaderboard, where every finished run's best travels through the `IScoreSink` and `IRankSource` seams into `LeaderboardStore` (see [Global leaderboard](#global-leaderboard)).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Apps/Games/GamesApp.cs | The Games hub: routing, the four tabs, the running game and its session flow, the chrome chips, the coin chip, widget deep links and the close guard (the screens live in the .Home, .Together, .Library, .Shelf, .Profile, .Leaderboard, .TopWeek, .Consent, .WhatsNew and .Launch partials) |
| src/Aetherphone/Apps/Games/GamesLibrary.cs | The catalog behind the hub: release order, Just Added, recents, genres and their play order, records with tier labels, the Library view, and every per-entry label the pages draw |
| src/Aetherphone/Apps/Games/GamesRoute.cs | Hub screens, tabs and shelves for the router, plus `GamesStack.HoldsGame` for the close guard |
| src/Aetherphone/Apps/Games/GamesHubArt.cs | Section headings with See All, and the onboarding anchor reporter |
| src/Aetherphone/Apps/Games/Hub/ | The hub's components, see [The hub](#the-hub): `HubMetrics`, `GameIconArt`, `GameTileView`, `HeroCarousel`, `LivePreview`, `PosterCard`, `ShelfColumns`, `PodiumCard`, `StreakCalendar`, `RoomCard`, `DailyCountdown`, `LaunchMorph` |
| src/Aetherphone/Apps/Games/TileRail.cs | The sideways pan behind every hub rail: claims the press, locks to the first axis, flings, pages with arrows |
| src/Aetherphone/Apps/Games/Widgets/DailyGameWidget.cs | Home screen widget for the daily game plus recent (or Just Added) games, with the reset countdown once the daily is done |
| src/Aetherphone/Core/Changelog/NewFeaturePins.cs | The versioned feature pins behind the dot on the Games icon and the What's new card |
| src/Aetherphone/Apps/Games/Framework/IMiniGame.cs | Contract every mini-game implements |
| src/Aetherphone/Apps/Games/Framework/GameSpec.cs | What a game declares once: id, title, hook, genre, backdrop, HUD style, score kind, modes, flags, level count, seats |
| src/Aetherphone/Apps/Games/Framework/GameStart.cs | The mode, seed, daily flag, level and seat count a run starts with |
| src/Aetherphone/Apps/Games/Framework/GameContext.cs | Per-frame data handed to the running game |
| src/Aetherphone/Apps/Games/Framework/GameSession.cs | The host-owned run: flow state, score reports, the single `Finish`, `Record`, levels, hot-seat handoffs, stats and leaderboard submission |
| src/Aetherphone/Apps/Games/Framework/GameOutcome.cs | What a game hands `Finish`: value, kind, win flag, stat id, up to four stat lines, a secondary stat, stars, unranked and winner seat |
| src/Aetherphone/Apps/Games/Framework/LevelSelect.cs | The level grid the intro's Levels button opens: stars per tile, locked tiles, tap to play |
| src/Aetherphone/Apps/Games/Framework/StageHandoff.cs | The full-screen pass-the-phone interstitial between hot-seat turns |
| src/Aetherphone/Apps/Games/Framework/GameSeats.cs | Seat colours and the cached, localized Player 1 to 6 names, pass lines and win lines |
| src/Aetherphone/Apps/Games/Framework/StarRow.cs | Three stars, earned ones filled, with an optional pop-in reveal (level tiles, result card) |
| src/Aetherphone/Apps/Games/Framework/StagePill.cs | The frosted pill behind the intro's best, stars and rank lines |
| src/Aetherphone/Apps/Games/Framework/Cards/ | The card table toolkit: `CardPose`, `HandLayout`, `SeatLayout`, `PileLayout`, `CardFlight` (plain math) and `CardFace`, `PileDraw` (drawing) |
| src/Aetherphone/Apps/Games/Framework/StageLayout.cs | Full and Safe rect geometry, chrome chip and HUD row positions |
| src/Aetherphone/Apps/Games/Framework/StageBackdrop.cs | The seven layered backdrops with pointer and camera parallax, vignette and light sweep |
| src/Aetherphone/Apps/Games/Framework/BoardPlate.cs | The glass plate under a grid |
| src/Aetherphone/Apps/Games/Framework/StageCell.cs | Cells with depth: raised, flat, sunken, pressed |
| src/Aetherphone/Apps/Games/Framework/Camera2D.cs | World-unit camera: fit, follow, punch, shake, world to screen mapping |
| src/Aetherphone/Apps/Games/Framework/HudModel.cs | The slots a game fills each frame; `StageHud` lays them out |
| src/Aetherphone/Apps/Games/Framework/StageHud.cs | Draws the score pill and the secondary capsules from a `HudModel` |
| src/Aetherphone/Apps/Games/Framework/StageIntro.cs | The intro screen: the painted icon, title, hook, best and rank pills, mode strip, Play |
| src/Aetherphone/Apps/Games/Framework/IntroLayout.cs | The intro's layout as plain math: one centred column in portrait, two columns on a landscape stage |
| src/Aetherphone/Apps/Games/Framework/StageInks.cs | The stage's fixed light inks (strong, muted, the secondary button surface and the strip track), whatever the phone theme |
| src/Aetherphone/Apps/Games/Framework/StarTotal.cs | The one "12 / 90 stars" formatter for a level pack's star total |
| src/Aetherphone/Apps/Games/Framework/StagePause.cs | The pause menu: Resume, Restart, Leaderboard, Quit |
| src/Aetherphone/Apps/Games/Framework/GameOverlay.cs | The result card (`DrawStage`) |
| src/Aetherphone/Apps/Games/Framework/StageChrome.cs | The back and pause glass chips |
| src/Aetherphone/Apps/Games/Framework/ScreenFx.cs | Screen-side effects: flash, vignette pulse, edge glow, slow motion, punch, sweep |
| src/Aetherphone/Apps/Games/Framework/FeedbackFx.cs | World-side effects: shake, hit-stop, shockwave rings, floating text, flash |
| src/Aetherphone/Apps/Games/Framework/ParticleSystem.cs | Pooled particles: bursts, sparkles, streaks, confetti, custom `ParticleSpec` emitters, world-space draw |
| src/Aetherphone/Apps/Games/Framework/Ribbon.cs | Tapered trail behind a fast object |
| src/Aetherphone/Apps/Games/Framework/NeonStroke.cs | Glowing vector strokes for neon line art (Drift, Crawler, Trails) |
| src/Aetherphone/Apps/Games/Framework/Shapes.cs | Filled and stroked ellipses (axis-aligned or rotated) and allocation-free concave polygon fills |
| src/Aetherphone/Apps/Games/Framework/Geometry2D.cs | Segment versus circle, swept circle, segment versus segment, point in polygon, closest point and distances (Dalamud-free) |
| src/Aetherphone/Apps/Games/Framework/Polygon.cs | Ear-clipping triangulation into caller buffers or a build-time array (Dalamud-free) |
| src/Aetherphone/Apps/Games/Framework/PadHold.cs | The held pad state machines behind `GamePad.HeldDPad` and `GamePad.HoldButton` (Dalamud-free) |
| src/Aetherphone/Apps/Games/Framework/ComboMeter.cs | Combo count, multiplier tiers and heat with a decay window |
| src/Aetherphone/Apps/Games/Framework/GameRandom.cs | Seeded xoshiro128** random source |
| src/Aetherphone/Apps/Games/Framework/GameSeed.cs | Fresh and daily seeds |
| src/Aetherphone/Apps/Games/Framework/GameSfx.cs | The kit's sound events |
| src/Aetherphone/Apps/Games/Framework/GameJuice.cs | Entrance progress, stagger, and pop-in easing |
| src/Aetherphone/Apps/Games/Framework/GameHud.cs | Score pills and accent buttons |
| src/Aetherphone/Apps/Games/Framework/GameGrid.cs | Centered cell-grid math for board games |
| src/Aetherphone/Apps/Games/Framework/GamePalette.cs | Shared board colors and ink-contrast picker |
| src/Aetherphone/Apps/Games/Framework/GameNumber.cs | Cached integer-to-string labels (no per-frame allocation) |
| src/Aetherphone/Apps/Games/Framework/LabelSlot.cs | One cached formatted label, rebuilt on value or language change (`LabelPairSlot` for two values, "12 / 120") |
| src/Aetherphone/Apps/Games/Framework/GameInput.cs | Keyboard reads that keep the keys away from the game client |
| src/Aetherphone/Apps/Games/Framework/GamePad.cs | On-screen d-pad, left/fire/right pad, held d-pad and hold buttons |
| src/Aetherphone/Apps/Games/Framework/Substeps.cs | Splits a frame delta into capped simulation substeps |
| src/Aetherphone/Apps/Games/Framework/FixedStepClock.cs | Fixed-timestep accumulator with a catch-up cap |
| src/Aetherphone/Apps/Games/Framework/Physics/PhysicsWorld.cs | Physics2D: rigid bodies, joints, ropes, queries and contact events on a fixed 1/120 s step (see [Physics2D](#physics2d)) |
| src/Aetherphone/Apps/Games/Framework/PixelSprite.cs | Bitmap sprites drawn as filled runs in one color |
| src/Aetherphone/Apps/Games/Framework/GameBanner.cs | Pop-in, hold, fade banner for "Ready" and "Wave 3" |
| src/Aetherphone/Apps/Games/Framework/PressSurface.cs | Claims the press over a board with an invisible item so a drag never moves the phone |
| src/Aetherphone/Apps/Games/Framework/StatCapsule.cs | A frosted HUD capsule with an icon, a count and an optional trophy best (Sweeper mines, Nonogram mistakes) |
| src/Aetherphone/Core/Games/GameStatsStore.cs | Best scores, best times, win streaks, mode choices, level stars, daily challenge |
| src/Aetherphone/Core/Games/IScoreSink.cs | `ScoreSubmission` and the sink the session hands finished runs to |
| src/Aetherphone/Core/Games/IRankSource.cs | Where intros and result cards read a `GameRank` from |
| src/Aetherphone/Core/Games/LeaderboardStore.cs | The registered sink and rank source: the upload queue, the board cache, your ranks |
| src/Aetherphone/Core/Games/ScoreUploadQueue.cs | The pure upload ledger: best per stat id, per-game spacing, reason handling, rank states, local bests on opt-in |
| src/Aetherphone/Core/Games/LeaderboardConsent.cs | The per-account record of who has answered the leaderboard prompt |
| src/Aetherphone/Core/Games/LeaderboardParticipation.cs | Reads an opt-in or opt-out flip on the same account as the player's choice |
| src/Aetherphone/Core/Games/ScoreStatIds.cs | The stat ids the server accepts, with the kind and direction of each |
| src/Aetherphone/Core/Aethernet/Clients/ScoresClient.cs | The typed client for the `/games/scores` routes and the leaderboard privacy switch |
| src/Aetherphone/Apps/Games/GamesApp.Leaderboard.cs | The leaderboard screen: the period menu, the Global and Friends strip, the mode chips, the podium, the rows from fourth place down, the sticky self bar |
| src/Aetherphone/Apps/Games/GamesApp.Consent.cs | The leaderboard consent card: the full first-launch card with the preview of your own row, and the compact Join card |
| src/Aetherphone/Core/Animation/RollingValue.cs | Animated number that rolls toward a target and pops (shared animation infrastructure, not games-only) |
| src/Aetherphone/Core/Coins/CoinGameSessionTracker.cs | Reports play sessions for coin awards |
| src/Aetherphone/Apps/Games/Online/OnlineHub.cs | The Together tab: your rooms, join by code, and the start-a-room tiles |
| src/Aetherphone/Apps/Games/Online/OnlineGameArt.cs | The `OnlineKindInfo` table: each room kind's accent id, host id, hint and seat cap |
| src/Aetherphone/Apps/Games/Online/OnlineRoomView.cs | One room: lobby, roster, and the table for the room's game kind |
| src/Aetherphone/Core/Games/GameRoomsStore.cs | Room directory, create/join/leave, actions, and the HTTP fallback poll |
| src/Aetherphone.Tests/GameSessionTests.cs | Pins the flow transitions, the single submission per run, level progress, hot-seat handoffs, `Record`, the Count kind, unranked and solo-only modes and the star formatter |
| src/Aetherphone.Tests/IntroLayoutTests.cs | Pins that every intro element fits the stage, portrait and landscape (780 x 360 design units) |
| src/Aetherphone.Tests/CardTableLayoutTests.cs | Pins the fan, seat ring, pile and card flight math |
| src/Aetherphone.Tests/LeaderboardStoreTests.cs | Pins the upload queue: dedupe, spacing, reasons, rank states, persistence, the opt-in gate, local bests on opt-in, the per-account consent answers |
| src/Aetherphone.Tests/ScoresWireContractTests.cs | Pins the score routes, the JSON shapes and the stat id list against the server |
| src/Aetherphone.Tests/GamesLibraryTests.cs, GamesHomeLayoutTests.cs, GamesStackTests.cs, WhatsNewNoticeTests.cs | Pin the hub: the catalog and Library view, the Home layout math, when a game closes, and the update notice pins |
| src/Aetherphone.Tests/OnlineKindInfoTests.cs | Pins one `OnlineKindInfo` per `GameRoomWire` kind |

## How the Games app is structured

The whole arcade is one phone app. `GamesApp` implements `IPhoneApp` (the contract every phone app fulfils, see [app-framework.md](app-framework.md)) and is registered once in `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs:

```csharp
apps.Add(new GamesApp(services.GameStats, services.GameData, services.Textures, services.Coins,
    services.CoinSessions, services.GameRooms, services.Configuration, services.Leaderboard,
    services.RemoteImages, services.Lodestone, services.MoogleClicker, services.SettingsLauncher));
```

`services.Coins` (the wallet store), `services.CoinSessions` (the play-session tracker) and `services.GameRooms` (the online room store) are the coin plumbing and the Together tab; `services.Configuration` holds the feature pins behind the update notice. `services.Leaderboard` is the `LeaderboardStore`, which implements both halves of the leaderboard seam: the `IScoreSink` that receives every finished run and the `IRankSource` that answers rank lookups (the hub hands the same instance to its `GameSession`). `services.RemoteImages` and `services.Lodestone` draw the avatars on the leaderboard and Profile screens. `services.MoogleClicker` is the framework ticker Moogle Clicker keeps earning on, and `services.SettingsLauncher` lets the What's new card open Settings on the changelog.

Inside, `GamesApp` owns a plain `IMiniGame[]` array built in its constructor. That array is the registry: a game exists because a line constructs it there. Most games have parameterless constructors; `TriviaApp`, `WordRunApp`, `LanderApp`, `CraterApp`, `HerdApp` and `MoogleClickerApp` show that a game can take services (game data, the texture provider, a ticker) if `GamesApp` passes them through.

`GamesApp` also implements `INameplateActivitySource`, so the Honorific nameplate title (see [Game integration](game-integration.md)) can show the local game or online room being played, and it shows a new-feature dot on its icon until it is first opened after an update (see [The update notice](#the-update-notice)).

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
| `Genre` | One of `Arcade`, `Action`, `Puzzle`, `Brain`, `Strategy`, `Tabletop` (the shelf); `Friends` is reserved for the online games |
| `Backdrop` | One of the seven `Backdrop` presets, drawn under everything |
| `Hud` | `HudStyle.Standard` (score pill on the chrome row, capsules below) or `HudStyle.Compact` (everything on the chrome row, for tall boards) |
| `Kind` | `ScoreKind.Score`, `Time`, `Level`, `Streak` or `Count`: how the run's value is stored and labelled. `Count` is a lower-is-better number (Mini Golf strokes, Pairs attempts), stored like a time and always shown as a plain number, never m:ss |
| `Unit`, `ModeLabels` | The caption for the run's primary value: `Unit` for every mode (Mini Golf's Strokes), `ModeLabels[mode]` per mode (Garden Siege: Total stars in the campaign, Waves survived in Endless). Without either the card says Score, Time, Level or Streak by kind. `Spec.LabelFor(mode)` answers it |
| `UnrankedModes` | Parallel to `Modes`: a practice or side mode that never ranks (Mini Golf's 9 holes, Tempo's practice). The session gives it no leaderboard id and finishes it unranked whatever the game hands `Finish`; the intro shows Not ranked with no best and no Leaderboard link, and the pause menu and result card drop their Leaderboard button |
| `ModesSoloOnly` | The modes only matter against bots or the clock, so the intro hides the mode strip once the Players strip picks more than one player (Lucky Draw, Crater, Broadside). `Session.ShowsModes` answers it |
| `Modes`, `ModeStatIds` | Difficulty or ruleset choices shown as a `SegmentStrip` on the intro; `ModeStatIds[mode]` is the stat id for that mode (defaults to `Id`). The choice persists per game through `GameStatsStore.LastMode` |
| `Clocked` | The simulation advances on a timer; focus loss pauses the run into the pause menu |
| `Countdown` | Show the 3, 2, 1, Go countdown before play (clocked reflex games only) |
| `Landscape` | The hub holds the landscape lock while the game is open (Doom, Crater), and the intro lays itself out in two columns |
| `Keyboard` | Informational: the game reads keys through `GameInput` |
| `LevelCount`, `LevelModes` | A level pack of that many levels with stars and a level select (0 means none); `LevelModes[mode]` keeps a daily or endless mode out of the pack, see [Level packs](#level-packs) |
| `Seats` | How many players can share the phone (1, the default, is solo; up to six); more than one adds the intro's Players strip, see [Hot-seat](#hot-seat) |

### The roster

The source of truth is the `games` array in the `GamesApp` constructor for local games and `OnlineGameArt.Infos` for online ones, with `GamesLibrary.Releases` dating each entry; read those when you need the exact list. At the time of writing the shelves hold these 59 local games and eight online kinds:

| Shelf | Game ids |
| --- | --- |
| Arcade | `whack`, `snake`, `flap`, `breakout`, `stack`, `beat`, `blade`, `hop`, `updraft`, `slice`, `spiral`, `pinball`, `moogleclicker`, `claim`, `lander`, `pegfall`, `minigolf`, `tempo` |
| Action | `skyfall`, `invaders`, `capman`, `squadron`, `doom`, `swoop`, `drift`, `crawler`, `trails`, `trailblaze`, `thrust`, `delve`, `fuse` |
| Puzzle | `match3`, `tetris`, `2048`, `watersort`, `bubbles`, `flow`, `crystaldrop`, `coil`, `mahjong`, `gloop`, `crates`, `fling`, `snip` |
| Brain | `minesweeper`, `memory`, `nonogram`, `simon`, `sudoku`, `trivia`, `wordrun` |
| Strategy | `broadside`, `siege`, `crater`, `herd` |
| Tabletop | `solitaire`, `reversi`, `chess`, `luckydraw` |
| Friends (online) | `online.uno`, `online.chess`, `online.pool`, `online.connectfour`, `online.broadside`, `online.minigolf`, `online.luckydraw`, `online.crater` |

A few ids predate their titles and class names: `match3` is Gem Swap (`GemSwapApp`), `memory` is Pairs (`PairsApp`), `minesweeper` is Sweeper (`SweeperApp`), `siege` is Garden Siege (`SiegeApp`), `mahjong` is Mahjong Solitaire. Never rename an id: it keys the saved stats, the release date and the accent.

The second wave (released 2026-10-08) brought these games and the kit pieces each leans on:

| Game | Id | Shelf | Ranked as | Kit pieces |
| --- | --- | --- | --- | --- |
| Mahjong Solitaire | `mahjong` | Puzzle | `mahjong.easy`, `.medium`, `.hard` (Time) | layouts as modes |
| Gloop | `gloop` | Puzzle | `gloop` (Score), `gloop.versus` (Streak) | mode kinds |
| Slice | `slice` | Arcade | `slice`, `slice.arcade` (Score) | Ribbon, world particles |
| Spiral | `spiral` | Arcade | `spiral` (Score) | Camera2D |
| Drift | `drift` | Action | `drift` (Score) | Camera2D, wrap-around world, glow strokes, hold buttons |
| Crawler | `crawler` | Action | `crawler` (Score) | Camera2D, glow strokes, held fire key |
| Trails | `trails` | Action | `trails` (Streak) | Camera2D, glow strokes |
| Trailblaze | `trailblaze` | Action | `trailblaze` (Score) | LaneProjection |
| Thrust | `thrust` | Action | `thrust` (Score) | Camera2D |
| Pinball | `pinball` | Arcade | `pinball` (Score) | Physics2D hinges and bullets |
| Pegfall | `pegfall` | Arcade | `pegfall` (Score; stars are local progress) | Physics2D, level pack |
| Fling | `fling` | Puzzle | `fling` (Level: total stars) | Physics2D, level pack |
| Snip | `snip` | Puzzle | `snip` (Level: total stars) | Physics2D ropes, level pack |
| Crates | `crates` | Puzzle | `crates` (Level: total stars) | level pack |
| Delve | `delve` | Action | `delve` (Level: total stars) | level pack, Camera2D |
| Claim | `claim` | Arcade | `claim` (Score) | held d-pad |
| Lander | `lander` | Arcade | `lander` (Score) | TerrainMask, Camera2D |
| Moogle Clicker | `moogleclicker` | Arcade | `moogleclicker` (Level: ledger level) | `Session.Record`, a FrameworkTicker service |
| Crater | `crater` | Strategy | `crater` (Streak: wins against bots) | TerrainMask with a scorch overlay, landscape, hot-seat, held fire button |
| Garden Siege | `siege` | Strategy | `siege` (Level: total stars), `siege.endless` (Level: waves) | LaneGrid, WaveSpawner, DripEconomy, level pack |
| Lucky Draw | `luckydraw` | Tabletop | `luckydraw` (Streak: wins against bots) | card table, hot-seat |
| Broadside | `broadside` | Strategy | `broadside` (Streak: wins against the AI) | hot-seat |
| Herd | `herd` | Strategy | `herd` (Level: total stars) | TerrainMask, level pack |
| Tempo | `tempo` | Arcade | `tempo` (Level: total stars; practice unranked) | level pack, Camera2D |
| Fuse | `fuse` | Action | `fuse` (Streak: match wins) | Camera2D, held pad state |
| Mini Golf | `minigolf` | Arcade | `minigolf` (Count: strokes over 18 holes; 9 holes unranked) | Physics2D, hot-seat |

### The hub

The hub is a tabbed app split across partials of `GamesApp`: `GamesApp.cs` (routing, the tabs, the running game, the chrome, the coin chip, widget deep links), `.Home` (the Home tab), `.Together` (the online tab, drawn by `OnlineHub`), `.Library` (the Library tab), `.Shelf` (the category pages), `.Profile` (the Profile tab and the Personal Bests grid), `.Leaderboard` (the leaderboard screen), `.TopWeek` (Home's Top This Week card), `.Consent` (the leaderboard consent cards), `.WhatsNew` (the update card) and `.Launch` (the zoom launch). The pieces they share live in src/Aetherphone/Apps/Games/Hub (namespace `Aetherphone.Apps.Games.Hub`); section headings with See All and the tour anchor reporter live in `GamesHubArt`, and every button, chip rail, search field, menu and empty state comes from the shared toolkit (see [UI toolkit](ui-toolkit.md)). Every view paints the neutral `AppPalettes.Games` skin; glass appears only on the tab bar, the nav bar buttons and the leaderboard's sticky self bar.

The root has four tabs on the floating `TabBar`, each a large-title page:

| Tab | Glyph | Route id | What it holds |
| --- | --- | --- | --- |
| Home | `Gamepad` | `games.tab.home` | The What's new card while it is unseen, the hero carousel, the compact Join card, your first open room, Continue Playing, Just Added, Top This Week, six genre shelves, and a Browse all link to Library |
| Together | `UserFriends` | `games.tab.together` | Your rooms, Join with a code, and the Start a room tiles (see [Play with friends: online rooms](#play-with-friends-online-rooms)) |
| Library | `ThLarge` | `games.tab.library` | Every game in one grid with search, genre chips and a sort menu |
| Profile | `UserCircle` | `games.tab.records` | Your identity, stat tiles, the streak calendar, your ranks and your personal bests |

The app is an `ITabRouteTarget`: those four route ids open a tab, `games.tab.search` opens Library with the search field focused, and `GamesApp.PlayRoute(id)` (`games.play.<id>`) opens a game directly, which the medium Daily Game widget uses for its recent (or, before anything has been played, Just Added) games. Profile keeps the old `games.tab.records` id so existing deep links and the tour still land on it. Rooms refresh when the app opens and on every tab tap (`SelectTab` calls `GameRoomsStore.EnsureFresh`), never per frame, and switching tabs never replays an entrance: a page fades in once per app open with `Motion.Appear`.

**Home**, top to bottom:

- The **What's new card** while its pin is unseen, see [The update notice](#the-update-notice).
- The **hero carousel** (`HeroCarousel`): a card 0.78 of the content width tall, clamped to 236 to 300 units, with page dots below. Up to three pages. **Daily** always leads: today's game runs live behind it through `LivePreview` for the games on its allowlist (Tetris, Breakout, Invaders, CapMan, Beat, Stack, Bubbles, Swoop, Trailblaze, Pinball, Pegfall, Mini Golf) and as a poster with the painted icon for the rest, with the Daily Challenge capsule, the streak capsule (a flame and the count, or a check once done), the icon, title and hook, Play (Play Again once done), and a status line such as "Resets in 5h 12m · Best 12,400 · #48" (`DailyCountdown`). **Spotlight** shows one recent release with a hook, rotating through Just Added by day. **Together** (signed in only) shows four online icons, the open room count and Start a room, which switches to the Together tab. A drag past a 6 unit slop pages the card, a release past 0.18 of a page flings to the next one, and the page settles on `Motion.Sheet`; the carousel advances every 7 seconds unless it is hovered, pressed, a game is open, the launch morph is running, or the tour is recording (which pins the Daily page). The card reports the tour anchor `games.featured`.
- The **compact Join card** for a signed-in player who answered Not now on the consent card, see [Global leaderboard](#global-leaderboard).
- The player's first **open room** as a `RoomCard` (the kind's icon with the owner's monogram, "Alex's room", seat dots in the accent, a live pill while playing), when they have one.
- **Continue Playing** (`PosterCard.DrawResume`, 156 by 100): `library.Recent` as posters on a frozen `StageBackdrop` of each game's preset, with the icon, the rank capsule, a Today capsule on the daily game, a progress bar for level packs, and the progress line under it ("Level 12 of 40", "84 / 120 stars", "Best 12,400").
- **Just Added** (`PosterCard.DrawEditorial`, 0.84 of the width): up to eight editorial cards with the icon, a "New game · Puzzle" eyebrow while the game is inside its 14-day release window, the title, a two-line hook and Play; See All pushes the New category page.
- **Top This Week** (`GamesApp.TopWeek.cs`): a `PodiumCard` of the daily game's weekly global board, with a footer that reads "You are #48 this week", Join for a player who is not on the boards, Sign in to rank when signed out, or Be the first this week with Play on an empty board; a skeleton podium while it loads and Retry on failure. See All opens the leaderboard at This week. The section hides when the daily game's mode has no catalog stat.
- Six **genre shelves** (`ShelfColumns`): three rows per column (icon 56, title, the first line of the hook, Play) with the next column peeking, ordered by how many of the player's played games fall in each genre (`GamesLibrary.GenreOrder`); See All pushes that genre's category page.
- A **Browse all N games** card that switches to Library.

Each section is skipped when its rect lies outside the clip rect, so a long Home costs only what is on screen. Every rail (Continue Playing, Just Added, the shelves, Profile's ranks) pans through `TileRail`, which claims the press with an `InvisibleButton` so a swipe never drags the phone window, locks to the first axis the pointer travels along, blocks taps once it moves sideways, flings on release, settles on a column with `Motion.PageSettle`, and shows paging arrows on hover. Hover and press springs (`HoverFx`, `PressFx`) are keyed by the precomputed `library.TileIds` under each rail's `PushID`, and every Play button by `library.PlayIds`, so nothing allocates an id per frame and no two rails share a spring.

**Library**: a search field (`SearchBar.Surface` under `GlassField.Search`), a `ChipRail` of genre filters (All, Arcade, Action, Puzzle, Brain, Strategy, Board & Cards, Together), a sort `DropdownMenu` behind the nav bar button (Newest, A to Z, Recently played), a count line, and a grid of `GameTileView` tiles (four columns at the phone's width) drawn only for the rows in view. The Together filter lists the online entries; tapping one opens the Together tab with that kind's tile highlighted. A search with no match shows `EmptyState` with Clear Search. While the sort menu is open the search field is disabled, so a tap on the menu never lands in it.

**Category pages** (`GamesScreen.Shelf`): a genre's games, or Just Added (`GamesShelf.New`), as one card of rows (icon 60, title, a two-line hook clamped by `ClampedLines`, Play), with the count above and a back button to Home.

**Profile**: the identity row (your avatar, display name with badges and @handle when signed in; a gamepad tile and Your games when signed out), three stat tiles (games played as a ring, the day streak, and the number of boards you are ranked on, or your total stars when you are signed out or not on the boards), the `StreakCalendar` (five weeks of daily challenge history in your culture's week order, done days in ember with runs joined, today ringed, your best streak, and today's game with Play), a Your ranks rail of rank cards (icon, "#12", "of 3,402", "#3 this week"; tap opens that board), and Personal Bests: the first eight best cards with See All pushing `GamesScreen.Bests`, the full two-column grid. Your ranks shows a skeleton while it loads, the compact Join card when you are not on the boards, and a compact sign-in card with Open Settings when signed out; a player with no records sees `EmptyState` with Play Today's Game.

The components in src/Aetherphone/Apps/Games/Hub:

| Component | What it draws |
| --- | --- |
| `HubMetrics` | The hub's tokens (section gap, card radii, the 0.26 icon radius, rail bleed, the Library grid numbers) and the grid column math |
| `GameIconArt` | A game's face everywhere: its painted `AppIconTile` texture, else its `AppIconArt` vector art on an accent tile |
| `GameTileView` | A Library tile: the icon with hover lift, pointer tilt (`VertexWarp.Tilt`) and press sink, the title, then New in the accent, a star row for level packs, or the genre; a rank capsule and a people badge on online entries |
| `HeroCarousel`, `LivePreview` | The hero's paging and its live daily card, which owns its own `GameSession`, HUD, backdrop and effects and never touches the play session |
| `PosterCard` | The Continue Playing posters and the Just Added editorial cards, with `ClampedLines` for two-line hooks |
| `ShelfColumns` | The genre shelves, three rows to a column |
| `PodiumCard` | The top three on steps with a crown and medal tints, shared by Top This Week and the leaderboard |
| `StreakCalendar` | Profile's daily challenge calendar (`StreakGrid` holds the day math) |
| `RoomCard` | A room's card, shared by Home and Together |
| `DailyCountdown` | "Resets in 5h 12m", cached per minute |
| `LaunchMorph` | The zoom launch's spring and transform, see [Routes and the running game](#routes-and-the-running-game) |

`GamesLibrary` (src/Aetherphone/Apps/Games/GamesLibrary.cs) is the catalog behind the hub. It wraps the `IMiniGame[]` plus one `GameEntry` per kind in `OnlineGameArt.Infos` (Uno, Chess, 8-Ball Pool, Connect Four, Broadside, Lucky Draw, Crater and Mini Golf; ids such as `online.uno` or `online.minigolf`, built by `GamesLibrary.OnlineEntryId`) and keeps every list the pages draw from as reusable `int[]` index arrays, so the draw code never allocates:

| List | What it holds |
| --- | --- |
| `Ordered` | Every entry, newest release first (the `Releases` table in the same file; add a row when you add a game) |
| `Latest` | Just Added: every entry released within 14 days of the newest release, with no cap (Home shows the first eight) |
| `Recent` | Entries with a `LastPlayedUnixSeconds` on their `GameStatRecord`, most recent first, capped at eight |
| `Genre(genre)` | One genre's entries, newest first, built once |
| `GenreOrder` | The six shelves, the genre with the most played games first, ties in enum order |
| `Records` | Entries with a personal best, most recently played first |
| `View(filter, sort, query)` | The Library grid: a `GamesFilter`, a `GamesSort` and a title or genre query, recomputed only when one of them or `Version` changes |

Every label a page draws is cached per entry: `Hook`, `Meta` (New for 14 days after release until the game is first played, else the genre), `Eyebrow` ("New game · Puzzle" inside the release window, else the genre), `ProgressLabel` and `Progress` (-1 means no bar), `Stars`, `StarMax` and `StarTier`, `RankLabel` ("#12", empty above `RankCap`, 999), and `BestValue`, `BestKind`, `KindLabel` and `BestTier` for the best cards. `IconIds`, `TileIds` and `PlayIds` are built once in the constructor. A level pack whose spec is `ScoreKind.Level` records its star total as `RecordKind.Stars` through `StarTotal.Label`, with no per-game case; a `Count` record (Mini Golf) shows a plain number captioned with the spec's `Unit`. Games with difficulty tiers (Flow, Sweeper, Nonogram, Sudoku) show the best across their `<id>.easy|medium|hard` records with the tier named. `Rebuild` refreshes the recents, records and labels; the hub calls it when it opens, when a game closes, and when the player leaves an online room. `EnsureLanguage` rebuilds the labels when the language or the clock format changes, and `RefreshRanks` rebuilds the rank chips whenever `LeaderboardStore.Version` moves.

### The update notice

The hub announces a release through the shared feature pins (`Configuration.SeenFeaturePins`, checked with `HasUnseenFeaturePin` and set with `MarkFeaturePinSeen`), keyed per release in src/Aetherphone/Core/Changelog/NewFeaturePins.cs:

- `NewFeaturePins.Games` (`app.games.1200`) puts a dot on the Games icon (`BadgeCount` with `BadgeAsDot`) until `OnOpened` marks it seen.
- `NewFeaturePins.GamesWhatsNew` (`games.whatsnew.1200`) shows the What's new in Games card at the top of Home: four lines (the new games, the opt-in leaderboards, the new online rooms, the redesign), Got it, and See what's new, which opens Settings on the changelog through `SettingsLauncher.Request(SettingsPageKind.Changelog)` and marks the changelog seen. Either button marks the pin seen, so the card never comes back. `WhatsNewNotice.Shows` holds the card back while the leaderboard consent card is up, and `WhatsNewNotice.ShowsJoinCard` keeps the compact Join card off Home while the card shows, so the two prompts never stack.

For the next release, give both pins a new versioned key and update the card's lines; a key a player has already seen never shows again.

The server picks the featured game when it can: `FeaturedIndex` (called from `RebuildLayout`) uses `coins.Wallet?.FeaturedGameId` (a field on the coin wallet DTO in src/Aetherphone/Core/Aethernet/Contracts/CoinDtos.cs) when it names a game in the array, and otherwise falls back to the daily rotation `GameStatsStore.TodayIndex * FeaturedStep % games.Length`. Whichever wins, its id lands in `stats.DailyGameId`, which makes it the daily challenge: opening that game starts it with `GameSeed.Daily(id, GameStatsStore.TodayIndex)`, so everyone on the daily plays the same board, and the intro says "Today's board".

### Routes and the running game

Navigation uses a `ViewRouter<GamesRoute>` over six screens (`Root`, `Shelf`, `Playing`, `OnlineRoom`, `Leaderboard`, `Bests`). Tapping a tile, poster, shelf row, hero card or best card calls `OpenGame(game, source)`, which begins a `GameSession` for the game's spec (mode from `GameStatsStore.LastMode`, a fresh or daily seed), resets the backdrop, effects, intro and pause state, stamps the game as played, and pushes `Playing`.

The push is a zoom from what was tapped. `OpenGame` stores the source rect relative to the app area and `GamesApp.Launch.cs` runs a `LaunchMorph`: the hub stays drawn underneath (inert, under a veil that deepens to 0.35), the Playing screen is drawn at rest in a `ScreenLayer.BeginWarped` stage and transformed from the source rect to the full screen, and when the source was an icon the painted icon fades out on top as the screen grows. The spring runs on `TransitionTiming.ZoomPresentSmoothTime` and starts with `TransitionTiming.LaunchVelocity`; back from the intro, the pause menu or the result card reverses it on `ZoomDismissSmoothTime` and pops the route when it lands. Landscape games and deep links (`games.play.<id>`) slide in instead. The intro then shows the game's painted icon (72 units) above the title, the same face the player tapped.

The back chip pops the route (or reverses the zoom), and `GamesApp.Draw` calls `CloseCurrentGame` (which calls `game.Close()`, releases the landscape lock and closes the coin session) once the router is at rest, no zoom is running and no `Playing` screen is left in the stack (`GamesStack.HoldsGame`). A leaderboard pushed from the intro, pause menu or result card therefore keeps the game alive underneath, while a game started from Personal Bests or a leaderboard's Play button closes as soon as the player backs out of it. `OnlineHub` (src/Aetherphone/Apps/Games/Online/OnlineHub.cs) draws the Together tab: the player's open rooms, the join-by-code field and the start-a-room tiles, with pull to refresh and a manual retry when the room list fails; `OnlineRoomView` is the room itself. When a round ends, `OnlineFinishHold` (src/Aetherphone/Apps/Games/Online/OnlineFinishHold.cs) keeps the finished table on screen until its last card flight or shot replay has settled, then shows a five-second countdown card (tap to skip) before the room shows the lobby again. The store and protocol behind these screens are covered under "Play with friends: online rooms" below.

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

`GameFocus.Active` (src/Aetherphone/Apps/Games/Framework/GameFocus.cs) is false while the phone window is unfocused or the game's own text input is active. A clocked stage game whose session is Playing is paused into the pause menu the moment focus is lost; a turn-based game (`Clocked = false`) simply receives a zero delta and stands still. Because the phone UI is Dear ImGui (an immediate-mode UI where everything is redrawn from scratch every frame), `Draw` runs every frame and the game keeps its own state in fields between frames.

### Checklist for registering a new game

1. Create a folder src/Aetherphone/Apps/Games/YourGame with a `YourGameApp : IMiniGame`. Most games split logic into a `*Board` class and drawing into a `*Renderer` class.
2. Declare a `private static readonly GameSpec Spec` with the id, `L.Games.YourGame` title, `L.YourGame.Hook`, genre, backdrop, HUD style, score kind, modes, flags, and `levelCount` or `seats` when it has a level pack or a hot-seat mode, and return it from `Spec`.
3. Add `new YourGameApp()` to the `games` array in the `GamesApp` constructor.
4. Add the `Title` string to L.cs (the `Games` section) and a nested `L.YourGame` class with `Hook`, plus both keys in the nine language JSONs (see [localization.md](localization.md)). A game with many strings gets its own section, as `L.Coil`, `L.Updraft` and `L.Swoop` do.
5. Add a row for your id to `GamesLibrary.Releases` with the release date, so the game sorts newest-first, joins Just Added, and reads New on its tile for its first 14 days.
6. Add an accent color keyed by your game id in src/Aetherphone/Core/Apps/AppAccents.cs; `IMiniGame.Accent` defaults to `AppAccents.For(Spec.Id)`.
7. Add a painted icon for your id: an `icon(...)` entry in tools/icon-generator/generate-painted-icons.mjs with the same hue as your accent, then run it to write src/Aetherphone/Icons/<id>.png and <id>.fg.png (see the tool's README). `GameIconArt` draws that texture through `AppIconTile` on every tile, card, the hero and the intro, and falls back to vector art in src/Aetherphone/Windows/Components/Chrome/AppIconArt.cs on an accent tile.
8. Add a case to `GamesLibrary.BestRecord` for the best card and the progress line (tiered ids go through `BestTimeAcrossTiers` or `BestLevelAcrossTiers`). A `ScoreKind.Level` level pack needs none: its star total shows on its own.
9. Seed the board from `start.Seed` (or `start.Random`) and add a `SameSeedReplaysIdentically` test for it.

## Play with friends: online rooms

The online games are not `IMiniGame`s: the Aethernet backend runs their rules, and the client renders a table and sends actions. They need an Aethernet account; signed out, the Together tab shows an `EmptyState` with Open Settings. The routes and room kinds on the wire are listed in [Networking](networking.md#game-room-routes).

**The store.** `GameRoomsStore` (src/Aetherphone/Core/Games/GameRoomsStore.cs) owns everything online for the hub:

- The room directory (the rooms this account is in) through `GamesClient.RoomsAsync`, on a `PollCadence` of 30 seconds while the phone is visible and 120 seconds while it is hidden. Opening the tab calls `EnsureFresh`, pull to refresh calls `RefreshNow`, and every intent answer or `game.ended` signal asks for an immediate refresh.
- Intents: `CreateRoom(kind)`, `JoinByCode`, `LeaveRoom`, and the host's `CloseRoom` and `Kick`. Each runs in the background and lands as one `GameRoomAnswer` that the draw code picks up with `TakeRoomAnswer`.
- Actions: `SendStart`, `SendPlay`, `SendDraw`, `SendPass`, `SendMove`, `SendResign`, `SendDrop`, `SendShoot`, `SendPlace`, `SendFleet`, `SendFire`, `SendHit`, `SendStay`, `SendTarget` and `SendCraterShot`, all thin wrappers over one private `SendAction`. Every request carries the roster's `ActionCount`; the server refuses a mismatch as stale instead of applying it twice, and a stale answer triggers an immediate room refresh. The result lands through `TakeActOutcome`.
- The live room, a `GameRoomSession` (src/Aetherphone/Core/Games/GameRoomSession.cs) fed by `game.*` signals from `RealtimeSignalBus` (see [Networking](networking.md)). Epoch and sequence numbers decide whether an event applies, a gap asks for a fresh snapshot instead of guessing, Uno's private hand and a Broadside player's own fleet ride their own lane (`GameRoomPrivate`, polled through `/you` when the socket is down), and the server clock offset is smoothed so turn countdowns stay honest. While the socket is down, the room is not attached, or a snapshot is pending, the store polls the room over HTTP instead (3 seconds visible, 10 seconds hidden); a 404 closes the room locally.

**The state.** `GameRoomSession.Build` parses each snapshot by its `GameKind` into a `GameRoomState` that carries one typed board per kind (`Uno`, `Chess`, `Pool`, `ConnectFour`, `Broadside`, `LuckyDraw`, `Crater`, `MiniGolf`, DTOs in src/Aetherphone/Core/Aethernet/Contracts/GamesDtos.cs) plus a kind-agnostic `GameRoomRoster` (host, players, action count, winner) that the lobby, the roster and the act guard read.

**The screens.** `OnlineHub` is the Together tab: Your rooms as `RoomCard`s (a skeleton until the list loads, a notice when it is empty, Retry when it fails), Join with a code, a notice for a refused intent, and Start a room, a grid of two tiles across built from `OnlineGameArt.Infos` (the kind's painted icon on its accent gradient, the name and "2 players" or "2 to 6 players"; the tapped tile spins while the room is created and the rest dim). `OnlineRoomView` draws one room: the lobby and roster while it waits, then the table for whichever board the state holds (`OnlineUnoTable`, `OnlineChessTable`, `OnlinePoolTable`, `OnlineConnectFourTable`, `OnlineBroadsideTable`, `OnlineLuckyDrawTable`, `OnlineCraterTable`, `OnlineMiniGolfTable`), and `OnlineFinishHold` once a round ends. `OnlineBroadsideTable` reuses the offline game's pieces (`BroadsideLayout`, `BroadsideArt`, the `BroadsidePlacement` editor and the `BroadsideJuice` effects) and only replays the shot the server already resolved. `OnlineMiniGolfTable` does the same with the offline course: `MiniGolfRenderer` draws the green, `MiniGolfJuice` fires the same particles and sounds, the aim preview runs on a local `MiniGolfBoard`, and `OnlineMiniGolfReplay` walks the server's sampled ball path (30 samples a second, with the marks it struck) while every windmill is set from the room's mill clock. The host picks 9 or 18 holes in the lobby, sent in the start action's card slot. A table that wants the whole screen says so through `OnlineRoomView.WantsLandscape` (8-Ball Pool and Crater), and `GamesApp.SyncOnlineRoomLandscape` requests or releases the landscape lock every frame. `GamesOnlineText` maps kinds and server reason codes to localized text; `OnlineGameArt` holds the `OnlineKindInfo` table, one row per kind: the accent id (which picks the painted icon and the accent, so online Chess, Broadside, Lucky Draw, Crater and Mini Golf wear their local game's face), the host tile's id, the hint and the seat cap.

### Checklist for adding an online game

The server has to know the kind first: game kinds and their rule engines live in the Aethernet backend repository. Then, on the client:

1. Add the kind constant, plus any action, end-reason or board-size constants, to `GameRoomWire`.
2. Add the room state DTO to GamesDtos.cs, register it in `AethernetJsonContext`, add a field for it to `GameRoomState`, and teach `GameRoomSession.Build` to parse it and build its `GameRoomRoster`.
3. Add a `Send*` wrapper to `GameRoomsStore` for any new action, extending `GameRoomActionRequest` if the action needs a new field.
4. Write `Online<Name>Table` in src/Aetherphone/Apps/Games/Online, construct it in `OnlineRoomView`, and dispatch to it in `DrawTable`, `ShowsTable` and `FinishedText`.
5. Add one `OnlineKindInfo` row to `OnlineGameArt.Infos`: the kind, its accent id, its host hint (`L.Games.Online<Name>HostHint`) and its seat cap. That row is the only list of kinds: `OnlineGameArt.Kinds`, the Library's online entries, the host tiles and their ids, the hint and `MaxPlayers` all read it, and an unknown kind falls back to the first row. `OnlineKindInfoTests` fails until every `GameRoomWire` kind has a row.
6. Add its name to `GamesOnlineText.GameName`, and every new string to L.cs and the nine JSONs.
7. Add an accent keyed by the accent id in src/Aetherphone/Core/Apps/AppAccents.cs (Connect Four uses `connectfour`), a painted icon for that id unless the kind reuses a local game's, and a `GamesLibrary.Releases` row for `online.<accent id>`, so the entry dates, sorts and reads New like a local game.

## The stage kit

The kit owns the frame; the game owns the world. Intro, countdown, pause, result, HUD layout, score persistence, leaderboard submission and chrome belong to the host. A game supplies its world, its rules, its HUD model and its effects. Everything lives in src/Aetherphone/Apps/Games/Framework, with one exception: `RollingValue` sits with the shared animation code in src/Aetherphone/Core/Animation because the rest of the phone uses it too.

### Session and flow

The host owns one `GameSession` per run. Its `State` is a `StageFlow`: `Intro`, `Countdown`, `Playing`, `Paused`, `Result`.

- **Intro** (`StageIntro`): the backdrop runs, the game's `DrawIdle` shows behind a dark veil (80 percent black, so the text stays readable over any game, and the intro always uses the light stage inks), and the kit draws the game's painted icon (72 units, through `GameIconArt`, dropped by `IntroLayout` when the stage is too short to fit it), the title (fit to the Safe width), the hook line, a Best pill and a Rank pill ("#12 · Global", "Not ranked", "Leaderboards off" or "Sign in to rank"), a `SegmentStrip` when `Spec.Modes` has more than one entry, the Play button and a Leaderboard text button, all staggered in. Space or Enter also starts. Picking a mode calls `session.SelectMode`, which persists through `GameStatsStore.LastMode(gameId)` and reloads the Best for that mode's stat id. A level pack swaps the Best pill for a star total ("12 / 120"), adds a "Level 12" line above Play and a Levels text button beside Leaderboard; a hot-seat spec adds a Players strip under the modes (see [Level packs](#level-packs) and [Hot-seat](#hot-seat)). An unranked mode shows Not ranked, no best and no Leaderboard link; a `ModesSoloOnly` spec hides its mode strip while more than one player is picked. `IntroLayout` places every element: one centred column under the HUD band in portrait, and on a landscape stage (`Spec.Landscape` with the lock held) two columns in the rect under the chrome band, title, hook and pills on the left, the mode and players strips, level line, Play and links on the right, so nothing falls off a 780 by 360 stage.
- **Countdown**: only when `Spec.CountdownFor(mode)` says so: `countdown: true` for every mode, or `countdownModes` per entry (Gem Swap counts down into Blitz and not into Classic). "3, 2, 1, Go" with `GameBanner`, 0.6 seconds per step with a `GameTick` each, the world visible and frozen (`DeltaSeconds` is zero).
- **Playing**: the game ticks with `context.DeltaSeconds`.
- **Paused** (`StagePause`): the pause chip, or focus loss for a clocked game. A 0.72 veil with Resume, Restart, Leaderboard and Quit stacked as `GameHud.Button`s. A turn-based game never auto-pauses; its delta is simply zero while unfocused. Leaving a run before it finishes settles it: Quit, Restart, the back chip, opening another game, closing the Games app and unloading the plugin all go through `GamesApp.SettleRun`, which calls the game's `OnQuit(session)` (the place to `Finish` with a richer outcome, as Bubbles, Gem Swap classic and Word Run do) and then `session.Settle()`. `Settle` finishes a run still playing or paused whose mode is `ScoreKind.Score` with the live score from `Report`, so an endless game or one with no losing state still reaches the leaderboard (the upload goes out even below the best, for the weekly board). Completion kinds (`Time`, `Count`, `Level`, `Streak`) are never settled: a half-solved puzzle has no honest result.
- **Result** (`GameOverlay.DrawStage`): the card with the title (You win or You lose from `Won`; a `GameOutcome.Drawn` run reads as a draw), the New Best badge, the primary stat label and counting value, a rank line ("#8 of 1,240 · Global", "#2 among friends", "Uploading" with a spinner, "Leaderboards off" for an account that has not opted in, or "Kept on this phone"), up to four stat lines in a two-by-two grid, Play again (or the `WithContinueLabel` text, Word Run's Next word) and a Leaderboard text button. Confetti and `GameWin` on a new personal best unless the outcome carries `WithQuietBest`; a gold palette when the global rank is 10 or better. A lost Time or Count run hides the primary value instead of showing a meaningless 0. The primary label comes from `Spec.LabelFor(mode)` when the spec names one, else from the kind (Total stars for a level pack). A level run shows its stars under the title and trades Play again for Next level plus Retry, or Retry alone; an unranked run shows no primary value and no rank line, and a run in an unranked mode no Leaderboard button either.

Game side, the whole contract is two calls:

```csharp
context.Session.Report(board.Score);
context.Session.Finish(new GameOutcome(board.Score, ScoreKind.Score, Spec.Id)
    .WithStat(L.Games.Combo, GameNumber.Label(board.BestCombo))
    .WithStat(L.Games.Time, TimeText.MinutesSeconds(board.Seconds)));
```

`Report` feeds the score pill and the beating-best glow every frame. `Finish` is accepted once per run (later calls are ignored): the session submits to `GameStatsStore` by `ScoreKind` (`SubmitScore` for Score and Level, `SubmitBest` for Time and Count, which keep the lower value in the time slot, `RecordWin` or `ResetStreak` for Streak by `Won`), submits the optional secondary stat (`WithSecondary`, for Updraft's height or Pairs' attempts as a Count) to the store and to the sink as its own `ScoreSubmission`, and a game reads that secondary best back with `Session.SecondaryBest(statId, kind)`, completes the daily, hands the primary `ScoreSubmission` to the `IScoreSink`, moves to Result and asks the `IRankSource` for the rank. Two outcomes only complete the daily and record nothing: a lost Time or Count run (`won: false`, the Sudoku you gave up on) and a draw (`GameOutcome.Drawn(statId)`, Reversi's tie or a Chess stalemate, which leaves the streak where it was). A game never calls `Stats.Submit*` itself.

The session's `Kind` is `Spec.KindFor(Mode)`: one spec can mix kinds per mode through `modeKinds` (Solitaire's Classic is a Time, Vegas a Score). Its `LeaderboardStatId` folds a tier or mode stat onto its catalog root through `ScoreStatIds.LeaderboardId` (`chess.easy` ranks and uploads as `chess`, `snake.wrap` as `snake`) and is empty when the catalog refuses the pair, in which case the rank pill stays Unknown.

### Level packs

A game with hand-made levels declares `levelCount` on its spec, and the kit owns the rest: progress, unlocking, the level select, the result card and the leaderboard value.

| Piece | What it does |
| --- | --- |
| `GameSpec.LevelCount`, `LevelModes` | The pack size (capped at `GameStatsStore.MaxLevels`, 999) and, parallel to `Modes`, which modes play it; an empty `LevelModes` means every mode. `Spec.LevelsFor(mode)` answers it |
| `GameStart.Level` | The 1-based level of the run, 0 when the mode has no levels. Read it in `Start` and build that level |
| `GameOutcome.WithStars(stars)` | Zero to three stars for `start.Level`; a run that finishes without it records no stars. A won outcome that reports zero stars records one, so a cleared level (Snip's fed moogle) always unlocks the next; finish a failed level with `won: false` |
| `GameSession.Level`, `LevelCount`, `HasLevels`, `TotalStars`, `LevelStars`, `CanAdvance` | The chosen level, the pack size for the current mode, the stars of the whole pack, the stars the finished run earned (`GameOutcome.NoStars` when it reported none) and whether Next level is on offer |
| `GameSession.SelectLevel(level)`, `AdvanceLevel()` | Pick an unlocked level on the intro or the result card; step to the next one after a starred run. The host calls both |
| `GameStatsStore.Stars`, `SetStars`, `TotalStars`, `IsUnlocked`, `HighestUnlocked`, `NextLevel` | Progress per game id. `SetStars` keeps the best and saves only on an improvement; level 1 is always open and level n opens once level n-1 holds a star; `NextLevel` is the first open level without a star, else the first below three stars, else level 1 |
| `Configuration.GameLevelProgress` | One `GameLevelProgress { GameId, Stars }` per game, `Stars` a digit string with one character per level ("3210" is three, two and one star, then level 4 open), behind `IGameStatsConfiguration` |
| `LevelSelect` | The overlay the intro's Levels button opens: a scrollable grid (five columns at phone width, more when wider) of tiles with the level number and zero to three stars, locked tiles dimmed behind a lock glyph, the current level ringed in the accent. Tap an open tile to play it; the back chip or Escape closes it |

The session opens on `NextLevel`, so Play always starts the next uncleared level. `Finish` with stars records them for `start.Level` first; for a `ScoreKind.Level` outcome it then ranks the pack by its total stars (the value submitted to the store and the sink is `TotalStars`, never the run's own value), while a `Score` or `Time` outcome keeps submitting its own value (Pegfall ranks its score, its stars are local progress). The result card shows the earned stars, titles a starred run "Level 12 cleared", and offers Next level as the primary button with Retry under it once the run earned at least one star and a next level exists; otherwise Retry alone. A Level kind pack never glows "beating best" on the score pill, because the best is a star total.

```csharp
private static readonly GameSpec StageSpec = new(GameId, L.Games.Crates, GameGenre.Puzzle, L.Crates.Hook,
    Backdrop.Slate, kind: ScoreKind.Level, levelCount: CratesLevels.Count);

public void Start(in GameStart start)
{
    board.Load(CratesLevels.Get(start.Level));
    level = start.Level;
}

context.Session.Finish(new GameOutcome(board.Moves, ScoreKind.Level, GameId)
    .WithStars(board.Moves <= par ? 3 : board.Moves <= par * 3 / 2 ? 2 : 1)
    .WithStat(L.Crates.Moves, GameNumber.Label(board.Moves)));
```

### Hot-seat

A game that can be played by several people passing one phone declares `seats` on its spec (2 to 6). The intro then shows a Players strip (1 to `Spec.Seats`, 1 meaning solo against bots or the clock), and the chosen count reaches the game as `GameStart.Seats` (`start.HotSeat` when above one).

- **Turns**: when the next player has to take the phone, call `context.Session.Handoff(seat)` (0-based). The host covers the whole screen at once in that seat's colour with "Pass to Player 2" and "Tap when ready", stops calling the game's `Draw`, freezes the clock and the delta, and lifts the cover when the player taps or presses Space or Enter. Hidden information therefore never shows to the wrong player. `Session.HandoffPending` and `Session.HandoffSeat` say where the handoff stands; `Handoff` returns false outside a run.
- **Results**: a run with more than one local player always finishes unranked, whatever the game hands `Finish`. `GameOutcome.Unranked()` is the explicit form (also for practice modes): it completes the daily and records nothing else, no best, no streak, no stars, no secondary stat and no upload, and the card shows no primary value, no rank line and no confetti. `WithWinner(seat)` titles the card "Player 2 wins" in that seat's colour; `GameOutcome.Drawn` reads as a draw.
- **Modes**: modes that only pick the bots (Lucky Draw's bot count, Crater's and Broadside's difficulty) declare `modesSoloOnly: true`, and the intro hides the strip while the Players strip is above one.
- **Names and colours**: `GameSeats.Name(seat)`, `PassLine(seat)`, `WinLine(seat)` and `Color(seat)` are cached per language from one `L.Stage.PlayerName` format, so seat labels never allocate in `Draw`.

```csharp
private void EndTurn(in GameContext context)
{
    turn = (turn + 1) % seatCount;
    if (context.Session.HotSeat)
    {
        context.Session.Handoff(turn);
    }
}
```

### Recording without a run

A game with no runs (Moogle Clicker never finishes) records a best with `context.Session.Record(statId, value, kind)`. The value goes to `GameStatsStore.SubmitBest` by kind (`SubmitScore` for Score and Level, `SubmitTime` for Time and Count, `SubmitStreak` for Streak) and, only when that improved the stored best, to the `IScoreSink` as a `ScoreSubmission`; it returns whether it improved. It never touches `State`, `Finished` or the result card, so it is safe from inside an endless run. Like every submit path it completes the daily when the stat belongs to the daily game.

### HUD

Games stop placing pills. Each frame a game fills `context.Hud`:

| Call | Capsule |
| --- | --- |
| `Score(int)`, `Score(int, label)` | The primary pill on the chrome row, rolling through `RollingValue`, glowing while beating Best; the overload swaps the SCORE caption for the label (Word Run's word count) |
| `Timer(left, total, urgent)` | Clock glyph, `TimeText.MinutesSeconds`, a draining bar, red pulse when urgent |
| `Clock(seconds)` | An elapsed clock with no bar (Sudoku, Sweeper, Solitaire); the kit floors the seconds so the label never runs ahead of the stored time |
| `Lives(left, max)` | Up to five hearts; "x7" with one heart beyond that |
| `Level(int)` | "LV 12" |
| `Combo(in ComboMeter)` | "x3" with a draining window bar, coloured from accent to warm to white-hot by heat; shown from two hits, asks `ScreenFx.EdgeGlow` from multiplier 3; an `Untimed` meter shows no bar |
| `Best(int)` | Trophy glyph and the value, formatted by the session's `Kind` (a time reads as a time, a count as a plain number) |
| `Custom(width)` | Reserves one of two slots (`HudModel.CustomSlots`) the game draws itself (Tetris next piece, Bubbles next bubble); read `Hud.CustomRect(index)` the next frame, check `Hud.CustomPlaced(index)` first, and paint the background with `StageHud.Capsule` |

Secondary capsules sit centred under the score pill in the order Timer, Lives, Level, Combo, Best, Custom, second Custom; at most four show, and the kit drops Best first, then Level. `Hud.SlotRect(HudSlot)` returns where any capsule landed last frame, for a game that anchors its own text to one (Gem Swap's bonus seconds under the timer). `HudStyle.Compact` puts the score left of centre and a single capsule right of centre on the chrome row, which buys 40 units of board height. Every label is cached in the kit (`GameNumber.Label`, `GameNumber.Signed`, `TimeText.MinutesSeconds`, `LabelSlot`), so filling the model allocates nothing.

### Geometry

All sizes are design units times `UiScale.Current`. `StageLayout` holds the numbers: the chrome band is the top 52, the chips are 36 glass circles centred 28 in from either side at y 26, the score pill is centred on that row with 72 reserved per side, the secondary row sits at y 71 with 28 tall capsules and 8 gaps, and `Safe` insets 12 left and right, 96 on top (56 in Compact) and 16 at the bottom. Boards fit inside `Safe`; worlds and backdrops use `Full`. Games with a `GamePad` float it over the bottom band (`StageLayout.PadBand`, `DPadBand` 140 for a d-pad, `ShooterBand` 70 for the shooter pad) on a frosted fill while the world continues behind it. A plate game without a camera grows its safe rect through `StageLayout.Punched(safe, context.Fx.PlateScale)` so a `Fx.Punch` lands on the board rather than nowhere.

The chrome is an instance the host owns (`context.Chrome`). `context.ChromeHit(pointer)` says whether the pointer sits on the back or pause chip, so a game checks it before it treats a click as a shot or a tap on a cell; the kit's own buttons already do. `Chrome.RecordCoinChip(rect)` is how the hub tells the chrome where the coin session chip landed so the same check covers it.

### Stage inks

The stage is always dark: the frosted material under the HUD capsules, score pill, banners, chips, pads, the result card, the pause menu and the level select is a fixed dark fill, and every backdrop is dark too. So nothing drawn on the stage takes its text colour from the phone theme, which turns dark in a light phone theme. `StageInks` holds the stage's inks: `Strong` and `Muted` for text and glyphs, `Surface` for the secondary buttons and `Track` for the intro strips; `StrongOn(ink)` and `MutedOn(ink)` follow `backdrop.Ink`, which is `StageInk.Light` on every preset, so text drawn straight on the stage is always light; text on a fixed light piece (a cream tile, a white key) picks its ink from that piece, never from the stage. Reach for `context.Theme` only for colours that are not text (`theme.Danger`, `theme.Accent`).

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
| `Slate` | Charcoal blue to near-black, a faint 26 unit grid that drifts with the pointer, two soft accent glows | Sudoku, Nonogram, Sweeper, Word Run, Trivia, 2048, Flow, Crates, Snip |

Layers shift with the pointer (2, 5 and 9 units across the rect, through a `Spring`) and, when the game calls `backdrop.SetCamera(in camera)` each frame, with the camera at parallax 0.05, 0.15 and 0.35. `ScreenFx.Sweep()` fires the light sweep; the kit fires it on a new best. Layers move slower than 20 units per second so they never distract (Reduce Motion is not a setting on this phone).

### Boards and cells

No mini-game draws a `GameScene.Arena` box any more (the helper survives only for the online tables and the casino felt). Grid games use `BoardPlate.Draw(drawList, rect, radius, scale, accent, backdrop.Ink)`: an accent glow beneath, a floating shadow, a fill that reads as glass over the backdrop (the last drawn ground colour darkened), a one unit rim and a top sheen. `BoardPlate.Around(gridBounds, scale)` gives the plate rect with its 10 unit padding. Cells go through `StageCell.Draw(drawList, rect, fill, depth, radius, scale)` with a `CellDepth` of `Raised` (drop shadow and top highlight), `Flat`, `Sunken` (inner shadow) or `Pressed` (Raised, shrunk 4 percent); `StageCell.Lift(progress)` returns the 0 to 3 unit lift for `GameJuice.PopIn` entrances. Light comes from the top-left in every game. Non-grid worlds use no plate: the world is the full rect and the backdrop is the floor.

### Camera

World games own a `Camera2D` (a struct; create it with `Camera2D.Create()`). The sim works in world units (the board decides the unit, for example one cell), and the camera derives the zoom from the view each frame so the game never depends on the window size:

| Member | What it does |
| --- | --- |
| `Fit(view, worldWidth, worldHeight, FitMode)` | Sets `View`, `Anchor` (the view centre) and `Zoom` in pixels per unit: `Contain`, `CoverWidth` or `CoverHeight`. Places the origin at the world centre on the first call only, so a following camera keeps its position |
| `Fit(view, worldWidth, worldHeight, FitMode, anchor)` | The same with the origin pinned to a screen point other than the centre (Swoop keeps its bird at 40 percent across and 72 percent down) |
| `Fit(view, worldRect, FitMode)` | The same fit for a static world, re-centred on the rect every call (Crystal Drop, Blade) |
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
- **`ParticleSystem`**: a fixed-capacity pool (512 by default). `Burst`, `Sparkle`, `Streaks` and `Confetti` as before, each with an optional gravity (`BurstGravity`, `SparkleGravity`, `StreakGravity` and `ConfettiGravity` are the defaults; pass 0 in a world with no down), plus `Emit(in ParticleSpec, origin, count)` for custom emitters and an `Emitter` struct (`new Emitter(spec, rate)`, then `emitter.Advance(deltaSeconds, position, particles)` for continuous trails). Shapes are `Circle`, `GlowCircle`, `Square`, `Star`, `Streak`, `Ring` (expanding stroke), `Shard` (rotating triangle), `Spark` (three-dot trail) and `Glyph` (one cached character, for +1 and combo digits). `ParticleSpec` carries start and end colour, size, speed, life, gravity, drag, spin, spread, direction, shape, a `SizeCurve` (`Shrink`, `Grow`, `Pulse`) and an additive flag that draws a halo. `Draw(drawList, scale)` draws screen-space particles; `Draw(drawList, in camera)` maps world-space particles through the camera and scales their sizes by the zoom. Particles draw from a `GameRandom`; `Reseed(seed)` makes them deterministic too.
- **`Ribbon`**: a ring buffer of 24 points for the trail behind a ball, bird, blade or snake head. `Push(point)` each frame, `Draw(drawList, color, width, additive)` tapers width and alpha from head to tail; the camera overload maps world points. One instance per trailing object, or one shared through `Claim(owner)` and `Release()` when only one object trails at a time (Blade's flying knife); `Owner` says who holds it and a claim by someone else clears the trail. A per-frame `Push` makes the trail shorter on a faster monitor; for an object that moves at a known speed use `PushSpaced(point, minimumDistance)` instead, which keeps the head on the object every frame but commits a point only every `minimumDistance` along the path, with `Ribbon.Spacing(speed)` giving one 60 Hz frame of travel (Tempo, Thrust, Snake), so a 144 Hz trail matches a 60 Hz one.
- **`NeonStroke`**: the glowing line art of Drift, Crawler and Trails, a wide soft pass under a bright core.
- **`ComboMeter`**: a struct with `Hit()` (returns the new multiplier: 1, 2, 3, 5, 8 at 1, 4, 8, 12 and 20 hits; `Hit(resetWindow: false)` counts without refilling the window), `Update(deltaSeconds)` (resets after `WindowSeconds` without a hit, then cools `Heat`), `Reset()`, `Count`, `Multiplier`, `Heat`, `WindowFraction`. `ComboMeter.Untimed()` is the chain counter for games where the window is the player's next move rather than a clock (Stack); `Timed` tells the HUD whether to draw the bar. Hand it to `hud.Combo`.
- **`GameSfx`**: `CountdownTick`, `ComboTierUp`, `NewBest`, `LevelClear`, all routed through `UiFeedback.Play`. Games keep calling `UiFeedback.Play` for world hits (`GameHitSoft`, `GameClear`, `GamePowerUp`, `GameMatch`, `GameWrong` and friends in src/Aetherphone/Core/Notifications/UiSound.cs) and never play files directly. Those entries sit on the `Game` channel, which the player can switch off on its own in Settings > Sounds; `GameWin` is on the `Event` channel so the new-best chime still sounds with game sounds off.

### Determinism

`GameRandom` is xoshiro128** as a mutable struct: `FromSeed(ulong)`, `Fresh()`, `NextUInt()`, `Next(max)`, `Next(min, max)`, `NextFloat()`, `Range(min, max)`, `Chance(probability)`, `Sign()`. Every board takes a `GameRandom` in its constructor or `Reset(seed)`; `new Random()` is banned under Apps/Games. `GameSeed.Fresh()` mixes the tick count with the stopwatch; `GameSeed.Daily(gameId, dayIndex)` is an FNV-1a hash of the id mixed with the day, so the daily board is the same for everyone. `GameStart.Random` hands a game the seeded source directly. Every board test suite gains a `SameSeedReplaysIdentically` case.

### Juice helpers that carry over

- `GameJuice.Advance(progress, deltaSeconds)` drives a 0-to-1 entrance value, `GameJuice.Stagger(progress, index, count)` splits it across cells so tiles appear in sequence, and `GameJuice.PopIn(progress)` maps it through `Easing.EaseOutBack` for an overshooting pop.
- `GameGrid.Centered(area, columns, rows, gapFraction)` computes a centered square-cell grid; `Cell(column, row)` and `CellCenter(column, row)` give you rects and centers, `Bounds` the whole board.
- `GameHud.Button(center, size, label, accent, theme)` is the accent button the kit uses for Play, Resume and Play again; `GameHud.ScorePill` is the rolling score pill `StageHud` draws, and `GameHud.LandscapeBack` the back chip of a landscape table.
- `GamePalette` holds the shared dark board colors plus `InkOn(fill)` to pick readable text ink; `GameNumber.Label(int)` returns a cached string so score text does not allocate every frame, and `GameNumber.Signed(int)` the "+12" form through `L.Stage.Plus` for score pops; `LabelSlot.Get(locString, value)` caches one formatted label per value and language.
- `RollingValue` animates a displayed integer toward a target and pops on change; `StageHud` drives it for the score pill.

### Shapes and geometry

Games draw their sprites from primitives, and the primitives are shared rather than copied into each renderer:

- `Shapes.EllipsePath`, `FillEllipse` and `StrokeEllipse` take a centre, radii (a `Vector2` or two floats), an optional angle and a segment count (24 by default; pass the count a sprite was tuned with); radii of 0.2 pixels or less draw nothing. `Shapes.FillConcave(drawList, polygon, color)` fills any simple polygon, convex or not, through pooled index buffers, so it allocates nothing per frame; `FillTriangles` draws a polygon triangulated once at build time, with a camera overload for world polygons (Mini Golf's course).
- `Polygon.Triangulate` is the ear clipper behind it: into caller buffers (`TriangleIndexCount(n)` sizes them) or as a build-time `int[]`. Either winding is accepted.
- `Geometry2D` holds the tests the games share: `SegmentCircle` (a blade or a rope against a body), `SweepCircle` (the first hit of a moving circle, for shells and fast balls), `SegmentSegment` with an optional hit parameter (rope cuts, line crossings), `PointInPolygon`, `ClosestPoint`, `SegmentDistance` and `ChainDistance`, `SignedArea`, `InTriangle` and `Cross`. Physics2D cuts ropes with it too.

### Input, clocks, sprites, banners

- `GameInput` is the only way a game may read the physical keyboard. `GameInput.Claim()` returns false unless `GameFocus.Active`; when it returns true it has raised `io.WantTextInput` for this frame and cleared the game client's key state for every key a game consumes. Dalamud honours `WantTextInput` (it swallows the key messages and clears `KeyState` on its input frame); it does not honour `WantCaptureKeyboard` against the game at all, so a game that only calls `SetNextFrameWantCaptureKeyboard` still walks the character with WASD. The convenience readers `Held(key, alternate)` and `Pressed(key, alternate, repeat)` call `Claim` for you and OR the two keys you pass (by convention a WASD key and its arrow); single-key overloads `Held(key)` and `Pressed(key, repeat)` exist too, and `Claim(ReadOnlySpan<VirtualKey>)` lets a game claim a narrower key set than the default (letters, digits, arrows, Space, Escape, and the editing and modifier keys). Call them only while the session is Playing, so the keyboard returns to the client the moment play stops; the intro claims Space and Enter itself.
- `GamePad.DPad(area, accent, theme)` draws a W/A/S/D cross and returns the `PadDirection` pressed this frame (press-fired, one per frame). `GamePad.Shooter(area, accent, theme)` draws A, W, D and returns `ShooterPadInput` with `Left` and `Right` held and `Fire` pressed. `DPadHeight(scale)` and `ShooterHeight(scale)` size the band. Games combine pad and keyboard themselves: `var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);`. For steering that lasts as long as a finger stays down, `GamePad.HeldDPad(area, accent, theme, out held)` draws the same cross with press-and-slide: the direction under the pointer stays held while the button is down, sliding onto a neighbour switches it, sliding off the pad keeps steering by sector until release (Claim). `GamePad.HoldButton(key, glyph, accent, out held)` is one held key (Crater's fire button), `ShooterPadInput.FireHeld` reports a held fire key (Crawler), and `KeyFace` and `GlyphInk` let a bespoke pad draw its keys the kit's way (Drift, Lander). The pure state lives in `HeldPadState` and `HoldLatch` (PadHold.cs) for pads with their own layout (Fuse's round pad).
- `new Substeps(deltaSeconds, maxStepSeconds)` gives `Count` and `Step` for a loop that advances fast projectiles without tunnelling; the count is capped at 16 so a stall never becomes a burst. `FixedStepClock(step, maxCatchUp)` is the alternative for sims that must run on an exact tick: `Advance(delta)` returns how many steps to run, `Alpha` is the render interpolation fraction, `Reset()` on restart.
- `PixelSprite` takes bitmap rows (`#` lit) once, at static init, and `Draw(drawList, topLeft, unit, color)` emits one rect per lit run. It is the sprite path for Invaders-style games; draw it in the game's accent with a `ProgressRing.Glow` behind it, never in a flat ink.
- `GameBanner.Draw(drawList, center, text, accent, theme, progress)` pops a frosted pill in over the first 18% of `progress`, holds, and fades over the last 25%. Drive `progress` with `GameBanner.Advance(progress, delta, lifetimeSeconds)`. Use it for stage and wave text that must hold; `FeedbackFx.AddText` rises and fades and is for score pops.

### Card table

Card games build on src/Aetherphone/Apps/Games/Framework/Cards (namespace `Aetherphone.Apps.Games.Framework.Cards`). The layout half is plain math with no Dalamud types, so `CardTableLayoutTests` pins it; the drawing half emits primitives and spins the emitted vertices, the way the online Uno table does (so nothing inside a card may push a clip rect). The online tables keep their own art; this kit is for the local card games.

| Piece | What it does |
| --- | --- |
| `CardPose` | A card on screen: centre, width (height is `Width * CardPose.Aspect`, 1.4), angle in radians (clockwise), face up or down, and a horizontal `Squash` for flips. `Contains(point)` hit-tests the rotated card; `Lifted`, `Moved`, `Turned`, `Sized` derive poses |
| `HandLayout.Fan(count, center, width, maxAngle)` | An arced hand: card centres on one circle, spanning `width` with the outer cards tilted to `±maxAngle` and dropped along the arc; `center` is where the middle card sits. Pass `maxStep` to cap the gap between neighbours so a short hand closes up on the same arc. The span overload fills a `Span<FanSlot>`; `FanSlot.Pose(cardWidth)` turns a slot into a pose. `HitTest(poses, point)` returns the topmost card under the pointer |
| `SeatLayout.Ring(seats, rect, centers)` | Two to six seats on the ellipse inscribed in `rect`, seat 0 at the bottom and the rest clockwise (left, top, right); `bottomSeat` rotates the ring so any seat sits at the bottom. `Seat`, `Angle` and `Inward(seat, rect, distance)` (where a seat's cards go, toward the table centre) |
| `PileLayout` | `Layers(count)` (one visible layer per five cards, at most six), `LayerOffset`, `Top(center, width, count, scale)` for where a drawn card leaves the deck, and `Scatter(center, width, sequence)` for a discard's seeded tilt and offset |
| `CardFlight` | A pooled list of card tweens (32 by default): `Launch(card, from, to, delay, flip, tag, seconds, arc)`, `Advance(deltaSeconds)`, then per index `Pose`, `Card`, `Tag`, `Waiting` and `Progress`. Flights arc above the straight line, ease out, and with `flip` turn edge-on halfway and land on the `to` pose's face. `TryTakeLanded` drains landing events (card, tag, final pose) in order; a full pool lands its oldest card at once. Launch with `hideWhileWaiting: true` for a card that should not be seen before it leaves, and skip it while `Visible(index)` is false |
| `CardFace` | `Draw(drawList, pose, design, backAccent, scale, alpha, highlight)` paints any card, its back when the pose is face down; `DrawBack` and `DrawSlot` (an empty pile outline) on their own. A `CardDesign` is `Numbered(0..12)` (cream face, big numeral, pips, corner indices, a tint per number), `Action(glyph, tint)` (a FontAwesome glyph on its tint) or `Modifier(label, tint)` ("+4", "x2" on a dark face). Keep a `CardDesign[]` table indexed by your card id |
| `PileDraw` | `Deck` (stacked backs, thicker with the count, an outline when empty) and `Discard` (the newest four cards of a discard list, scattered, drawn through the design table) |

```csharp
var count = hand.Count;
for (var index = 0; index < count; index++)
{
    var slot = HandLayout.Fan(index, count, handCenter, handWidth, 0.3f, 44f * scale);
    var pose = slot.Pose(58f * scale);
    CardFace.Draw(drawList, hovered == index ? pose.Lifted(14f * scale) : pose, Designs[hand[index]], Accent, scale);
}

flights.Advance(context.RawDeltaSeconds);
for (var index = 0; index < flights.Count; index++)
{
    CardFace.Draw(drawList, flights.Pose(index), Designs[flights.Card(index)], Accent, scale);
}

while (flights.TryTakeLanded(out var landing))
{
    board.Arrive(landing.Card, landing.Tag);
}
```

### Games with data files

Word Run reads its word banks from src/Aetherphone/Words (`<code>.answers.txt` and `<code>.valid.txt`, one word per line, shipped as content next to the plugin). They are generated, never hand-edited: `tools/build-word-banks.ps1` rebuilds them from SCOWL and the FrequencyWords lists, and THIRD-PARTY-NOTICES.md records both sources. Doom keeps no data in the repo at all; `DoomAssets` downloads the shareware episode, optionally Freedoom (Phase 1 and 2), and the soundfont into the `doom` folder under the plugin's config directory, verifies them against pinned SHA-256 checksums, and also runs any commercial IWAD the player drops there (`doom.wad`, `doom2.wad`, `plutonia.wad`, `tnt.wad`).

## Physics2D

`PhysicsWorld` (src/Aetherphone/Apps/Games/Framework/Physics) is the shared rigid-body engine for table and toy games: pinball, peg shooters, slingshots, rope puzzles, mini golf. It has no ImGui or Dalamud types, so a board drives it and a test pins it like any other rule set. Every body lives in parallel arrays sized once by the constructor, `Step` allocates nothing, and two worlds fed the same calls in the same order stay bit-identical.

### Units

Metres, kilograms, seconds and radians. World +Y points down, as in `Camera2D`, so gravity defaults to `(0, 9.81)`; set `Gravity = Vector2.Zero` for a top-down course. A positive angle turns +X toward +Y (clockwise on screen). The solver tolerances (a 0.005 slop, 0.02 speculative contacts) assume moving shapes between about 0.1 and 10 units across, so scale the table to the engine rather than the other way round: a pinball ball of radius 0.15 on a 6 by 12 table works well.

### Stepping

`world.Step(context.DeltaSeconds)` clears last frame's contact events, then runs as many fixed 1/120 s ticks as the `FixedStepClock` owes (at most 0.1 s of catch-up), and returns the tick count. Forces applied with `ApplyForce` and `ApplyTorque` act on every tick of that call and are cleared after it, so apply them every frame. `Tick()` runs exactly one step without touching the event buffer (tests and replays). Draw with `RenderPosition(body)` and `RenderAngle(body)`, which interpolate between the last two ticks, for smooth motion on high refresh rates; read `Position` and `Angle` for rules. `Clear()` empties the world for a level restart without allocating.

### Bodies

Handles are `int` ids. `PhysicsWorld.Ground` (0) is a static, shapeless body at the origin for anchoring joints to the world. Ids are reused after `DestroyBody`, so drop yours when you destroy one.

| Call | Shape |
| --- | --- |
| `CreateCircle(type, position, radius, material, flags)` | Circle |
| `CreateBox(type, position, halfExtents, angle, material, flags)` | Oriented box |
| `CreateSegment(start, end, material, flags)` | Static two-sided segment |
| `CreatePolyline(points, material, closed, flags)` | Static two-sided chain (walls, course edges); vertices between segments never snag a rolling circle |

`BodyType` is `Static`, `Dynamic` or `Kinematic` (moved only by `SetVelocity` and `SetAngularVelocity`, pushes dynamic bodies, ignores everything else). `PhysicsMaterial(density, restitution, friction)` mixes per pair as the larger restitution and the geometric mean of the frictions; `PhysicsMaterial.Default` is `(1, 0, 0.6)`. `BodyFlags`: `Sensor` reports overlaps and never pushes, `Bullet` sub-steps a fast circle against everything it could reach this tick (walls, flippers, pegs) so it cannot tunnel, `FixedRotation` locks the angle. Per body you also have `SetGravityScale` (a bubble lifts the candy with a negative scale), `SetDamping` (rolling resistance on a golf surface), `SetCollisionFilter(category, mask)` (two bodies collide when each one's category is in the other's mask; ramps switch layers this way), `SetTransform`, `SetVelocity`, `ApplyImpulse`, `Wake` and an `int` `Tag` for your own lookup. `ChainPoints(body)` returns a polyline's world points for drawing.

### Joints and ropes

- `CreateDistanceJoint(bodyA, bodyB, anchorA, anchorB)` keeps the anchors at their current distance; pass `stiffnessHertz` and `dampingRatio` for a spring instead.
- `CreateHinge(bodyA, bodyB, pivot)` pins two bodies at a world point. `SetHingeLimits(joint, lower, upper)` and `SetHingeMotor(joint, speed, maxTorque)` make a flipper: drive it toward the upper limit while the key is held and back while it is released, every frame, and read `HingeAngle(joint)`.
- `CreateRope(anchor, body, segments, length, anchorBody)` hangs `body` from a world point (on `Ground` unless you pass an anchor body) through `segments - 1` light circles linked by slack distance constraints, plus a direct length limit so the rope never stretches by more than a couple of percent. Each rope uses `segments - 1` bodies and `segments + 1` joints of the world's capacity (up to 64 segments, 32 ropes). `CutRope(rope, segment)` cuts one link and frees the body; `CutRopes(swipeStart, swipeEnd)` cuts every rope the swipe crosses and returns the count. Draw a rope from `RopePoint(rope, 0)` to `RopePoint(rope, RopeSegments(rope))`, skipping links where `IsRopeSegmentCut` is true. Destroying the anchor or the hanging body destroys its ropes.

### Queries and events

`Raycast(origin, direction, maxDistance, out RaycastHit hit, mask)` returns the nearest non-sensor body with the hit point, surface normal and distance. `OverlapCircle(center, radius, results, mask)` fills a caller span (use `stackalloc`) with every non-sensor body the circle overlaps; for a polyline that means crossing its line. `PredictPath(start, velocity, gravityScale, linearDamping, stepsPerPoint, path)` runs the same integrator as the solver, so an aiming preview matches real flight until the first contact.

After `Step`, read `EventCount` and `Event(index)` (oldest first; a ring buffer of the constructor's event capacity keeps the newest). Each `ContactEvent` carries `BodyA`, `BodyB`, `Point`, `Normal` (from A to B), `Impulse` and a `Kind`: `Hit` when two solid bodies start touching (including a bullet's mid-tick hit), `SensorEnter` and `SensorExit` for sensors. Use `contact.Involves(body)` and `contact.Other(body)` rather than assuming an order. A contact that is already touching reports another `Hit` only when its impulse in one tick reaches `ImpactThreshold`, which is infinite by default; set it to your weakest plank's strength to break structures under load.

### Solver

Sequential impulses with 8 velocity iterations per tick, warm started from the last tick by contact feature (falling back to the nearest point on the same face), Coulomb friction clamped to the normal impulse, and a two-point block solver for box faces so stacks stand still. Bounces apply after the iterations from the approach speed measured before them, and only above 1 m/s, so resting contacts never gain energy. Penetration is removed by Baumgarte stabilisation (0.2 with a 0.005 slop) through separate position velocities that are discarded after integration, so pushing bodies apart never adds speed. Joints use the same position pass. Bodies sleep together in islands after 0.5 s below 0.05 m/s and 0.05 rad/s, and wake on contact with a moving body, a new transform, velocity, impulse, force or gravity, a motor change, a cut, or the removal of a body they rest on.

### Debug view

`world.Debug.Outline(body, span)` writes a body's outline in world points (24 for a circle, starting at its angle so a spoke shows rotation, 4 for a box, the chain for a polyline) and `Debug.IsClosed(body)` says whether to close it. `Debug.ContactCount`, `ContactPosition(index)` and `ContactNormal(index)` show this tick's contacts, and `Debug.JointAnchors(joint, out a, out b)` the joint anchors. The engine never draws; map the points through your `Camera2D`.

### Limits

- A bullet sweeps against every other body except other bullets; two bullets meeting head-on rely on the normal contact path.
- Non-bullet bodies have no continuous collision. A fast box can pass through a thin wall, so mark small fast things as bullets and keep walls thicker than one tick of travel.
- A box sliding along a polyline that bends at a vertex can catch on it; flat runs of collinear segments are smooth.
- Polylines and segments are static. Moving walls are kinematic boxes.
- Capacities are fixed: creating past the body, joint, chain point or rope capacity throws, contacts past the contact capacity are dropped, and events past the event capacity overwrite the oldest.

## The motion exception

The rest of the phone uses critically damped motion: springs that settle without overshooting (see `Spring.Step` in src/Aetherphone/Core/Animation/Spring.cs). Games are the place allowed to bounce. `Easing.EaseOutBack` (an easing curve that overshoots its target and settles back) is defined in src/Aetherphone/Core/Animation/Easing.cs and is referenced only from files under src/Aetherphone/Apps/Games plus two Casino cabinet sites (BingoCabinet.cs and BingoCardArt.cs). Keep it that way: bouncy easing belongs to games and casino cabinets only. Inside a game, reach for `GameJuice.PopIn`; everywhere else, use springs. The kit's own motion (backdrop parallax, pause veil, camera follow) runs on `Spring`, and the hub around the games follows the rest of the phone: its hover lifts, press sinks, rail paging, hero paging and zoom launch read raw springs from the `Motion` table and `TransitionTiming`, with no easing curve on top and no entrance that replays on a tab switch.

## Scoring, streaks, and the daily challenge

`GameStatsStore` (src/Aetherphone/Core/Games/GameStatsStore.cs) is the only persistence a run touches, and in a stage game the session touches it, never the game. It wraps `Configuration` through the `IGameStatsConfiguration` interface (so tests substitute a fake), which stores a `List<GameStatRecord>`, a `List<GameModeChoice>`, a `List<GameLevelProgress>`, `DailyChallengeStreak`, `DailyChallengeLastDay`, `DailyChallengeHistory` (a `ulong`, bit n set when the daily was done n days before the last one) and `DailyChallengeBestStreak`. See [state-and-persistence.md](state-and-persistence.md) for how `Configuration` is saved.

| Member | Semantics |
| --- | --- |
| `Get(gameId)` | Returns a `GameStats` value (`BestScore`, `BestTimeSeconds`, `Streak`); zeros if never played |
| `SubmitScore(gameId, score)` | Higher is better; returns true only on a new best |
| `SubmitTime(gameId, seconds)` | Lower is better; returns true only on a new best |
| `Best(statId, kind)`, `SubmitBest(statId, value, kind)` | Read or submit the best in the slot the kind uses: the time slot for Time and Count, the streak for Streak, the score otherwise |
| `RecordWin(gameId)` | Increments and returns a win streak (Pairs, Reversi, Chess) |
| `ResetStreak(gameId)` | Clears the streak on a loss |
| `SubmitStreak(gameId, streak)` | Keeps the higher streak; returns true only on a new best (`GameSession.Record` with a Streak kind) |
| `Stars`, `SetStars`, `TotalStars`, `IsUnlocked`, `HighestUnlocked`, `NextLevel` | Level pack progress, see [Level packs](#level-packs) |
| `MarkPlayed(gameId)`, `LastPlayed(gameId)` | Stamp and read the last-played time the hub sorts `Recent`, `Records` and the Recently played sort by; the hub calls `MarkPlayed` when it opens a game or an online room, so a game never needs to |
| `LastMode(gameId)`, `SetLastMode(gameId, mode)` | The remembered `Spec.Modes` index per game, used by the intro's mode strip |
| `TetrisModern`, `WordBank` | `TetrisModern` is a view over `LastMode("tetris")` that still honours the pre-kit configuration flag; `WordBank` is Word Run's remembered bank code, which its mode strip mirrors |
| `TodayIndex` (static) | UTC day number behind the daily challenge and the featured rotation |
| `DailyGameId`, `DailyDone`, `DailyStreak` | Daily challenge state; the hub sets `DailyGameId`, and the hero's streak capsule, the Profile stat tile and the Daily Game widget read `DailyDone` and `DailyStreak` |
| `DailyDoneOn(dayIndex)`, `DailyBestStreak`, `DailyHistoryDays` | The last 64 days of daily history and the best streak, which Profile's streak calendar draws; `CompleteDaily` shifts the history, and a streak saved before the history existed seeds it on first load |

Stat ids may carry a difficulty suffix, for example `sudoku.easy` or `minesweeper.easy`, or a mode suffix like `tetris.modern`, `match3.blitz` or `updraft.height`. A stage game declares them through `Spec.ModeStatIds` (parallel to `Spec.Modes`, with `ModeKinds` and `CountdownModes` as optional parallel arrays when the modes differ in kind or opening) or passes them in its `GameOutcome` (`StatId`, `WithSecondary`). Every submit path first calls `CompleteDaily`, which prefix-matches the stat id against `DailyGameId` (so `sudoku.easy` counts for a `sudoku` daily) and advances or resets the streak based on `TodayIndex`; the session calls it on its own for a lost Time run and for a draw, which record nothing else. Finishing the featured game through the session completes the daily automatically, a recorded loss included; there is no separate daily API.

The leaderboard seam sits next to the store in src/Aetherphone/Core/Games: `IScoreSink.Submit(in ScoreSubmission)` receives `{ StatId, Value, Kind, Seed, Daily, GameId }` after every `Finish`, and `IRankSource.TryGetRank(statId, out GameRank)` returns `{ Rank, Total, FriendsRank, WeekRank, State }` with a `RankState` of `Unknown`, `Uploading`, `Ranked`, `SignedOut`, `Failed` or `Hidden`. `LeaderboardStore` implements both; `NullScoreSink` and `NullRankSource` remain for tests and for a `GamesLibrary` built without a store.

## Global leaderboard

The leaderboard is a social feature, not an economy: it moves no coins, unlocks nothing and awards no badges. The plugin is open source, so every score is a claim, and the server defends the boards with per-game plausibility caps against the timed coin play session, one upload per game per five seconds, weekly boards that reset damage, and a `game_score` report target. The client's job is to upload honestly and show the result.

**What uploads.** `LeaderboardStore.Submit` (the `IScoreSink`) accepts a finished run only when `ScoreStatIds.LeaderboardId(statId, gameId, kind)` resolves it against `ScoreStatIds.All`, the table that mirrors the server's catalog (`ScoresWireContractTests` pins the two lists against each other): an exact catalog id uploads as itself, a tier or mode id with no catalog row (`chess.easy`, `reversi.hard`, `snake.wrap`) folds onto its game's root when the kinds agree, and anything else is refused (Solitaire's Vegas score never folds onto the Time root, and a streak only ever lands on a streak stat, so Pairs' win streak never uploads as its time). The value must be positive. Accepted runs land in `ScoreUploadQueue`, persisted as `Configuration.PendingScoreUploads` (`PendingScoreUpload { StatId, GameId, Value, Kind, Seed, Daily, QueuedAtUnix }`), one entry per stat id holding only the best (higher for scores, levels and streaks; lower for times and the Pairs attempt count). A queued run survives a sign-out and a plugin reload. The server knows no Count kind: `ScoreKinds.Wire` maps a `Count` submission to `Time` before it is folded and queued, so Mini Golf's strokes rank on the server's lower-is-better `minigolf` board while every plugin screen shows them as a number.

**Opt-in.** The boards are opt-in: nothing uploads until the signed-in account has joined (`CurrentUser.ShowOnLeaderboards`, false by default on the wire and on the server). Until then `Submit` still queues the best and the local records are always kept, but the flush loop never starts (`LeaderboardStore.UploadsAllowed`). Joining and leaving both go through `LeaderboardStore.SetParticipation`, which posts `POST /me/games-privacy`, records the answer for that account in `Configuration.LeaderboardConsentAnswered` (`LeaderboardConsent`), and hands the reply to `session.SetUser`. `LeaderboardParticipation` watches `session.Changed` and reads a flip of the flag on the same account as the choice: joining queues the device's local bests for every catalog stat (`ScoreUploadQueue.EnqueueLocalBests`, mapped by `TryLocalBest`, through the usual best-per-stat rule and cadence; casino stats such as `casino.barkeep` are left out), and leaving clears the pending queue. An account switch or the first load of an account is never read as a choice, so reloading the plugin does not queue the bests again.

**Consent card.** The first time a signed-in player opens the Games app on an account that has neither joined nor answered (`LeaderboardStore.NeedsConsent`), `GamesApp.Consent.cs` draws a full-screen card before the hub and holds the app tour: the title, three wrapped lines, a "How you would appear" preview drawn with the leaderboard's own row (avatar, display name, `@handle`, badges, a sample rank and score), the "You can change this any time in Settings > Privacy" line, then Join leaderboards and Not now. Join calls `SetParticipation(true)`; a failure shows inline through `FailureText` and keeps the card without recording the answer. Not now records the answer locally (`DeclineConsent`) and sends nothing. The answer is kept per account id, so an alt character is asked on its own; signed-out players never see the card. After a Not now, a compact card ("You are not on leaderboards" with a Join button that calls `SetParticipation(true)` directly) takes its place in three spots: on Home under the hero (stepping aside while the What's new card shows), at the top of the leaderboard screen, and in Profile's Your ranks slot.

**How it flushes.** One background loop (`StoreWork`) runs while signed in, opted in and the queue is not empty: it takes the first due entry, posts `POST /games/scores { gameId, value }` through `ScoresClient`, and resolves the reply. Entries for the same root game are spaced `ScoreUploadQueue.SpacingMilliseconds` (5 s) apart, matching the server cadence. Reasons: an empty reason or `not_better` removes the entry and caches the returned ranks; `too_soon` keeps it and waits out the cadence; `unknown_game`, `implausible` and `hidden` drop it and mark the stat as failed. A transport failure (`Offline`, `Timeout`, a 5xx) keeps the entry and retries 30 s later; a 4xx drops it. A better score queued while its predecessor is in flight is kept (the reply only removes the value it uploaded). The loop restarts on every `Submit`, on sign-in, on opting in and on a realtime reconnect.

**Ranks.** `TryGetRank` (the `IRankSource`) reads, in order: `SignedOut` while no session is signed in; `Hidden` ("Leaderboards off") while the loaded account has not opted in, which also hides the hub's rank capsules and skips the `/games/scores/me` fetch; `Uploading` while the stat is queued or in flight; `Failed` after a refusal or a transport failure until the next attempt starts; `Ranked` from the last reply for that stat; otherwise the entry from `GET /games/scores/me`, which the store fetches when the hub opens (60 s TTL) and again after every accepted upload. `LeaderboardStore.Version` increments on every change; `GamesApp` watches it each frame, calls `session.RefreshRank()` so the intro and result pills move from "Uploading" to "#12 · Global" on their own, and refreshes the hub's rank capsules through `GamesLibrary.RefreshRanks`.

**Boards.** `Board(key)` returns a `LeaderboardBoard` snapshot per `LeaderboardKey(statId, scope, span)`; `EnsureFresh` fetches `GET /games/scores/{statId}?scope=global|friends&span=all|week&limit=50` with a 60 s TTL and a 30 s cooldown after a failure, `RefreshNow` ignores the TTL (pull to refresh). Reads go through the transport like every other client, so a 429 host pause applies and surfaces as `AepFailureKind.RateLimitPaused`. The store never throws; a failure sits on the snapshot as an `AepFailure`.

**Screens.** `GamesScreen.Leaderboard` (`GamesRoute.LeaderboardOf(gameId, statId)`) is pushed by the intro, pause and result Leaderboard buttons, by a Profile rank card and by Top This Week on Home; a game underneath stays open, so the back button returns to it. `GamesApp.Leaderboard.cs` draws:

- the game title in the large-title header, with a Period nav bar button (`PhoneIcons.Calendar`) that opens a `DropdownMenu` of This week (the default) and All time;
- an identity row: the painted icon, the genre and period, and a Tinted Play button when no game is open underneath;
- a Global / Friends `SegmentStrip`, then a `ChipRail` of modes when the game has more than one board (derived from `Spec.Modes`, folded through `LeaderboardId` so the three Chess tiers share one board and only distinct catalog ids remain, or from the catalog for a game whose spec names none: Easy, Medium, Hard, Classic, Modern, Blitz, Height, Attempts);
- the top three on a `PodiumCard` (avatars, a crown over first, steps with gold, silver and bronze numbers), then fourth place down as `GroupCard` rows: rank number, avatar (`AvatarView.DrawRemote`), name with badges (`UserName.DrawAuto`), `@handle`, and the value formatted by the kind the game's spec gives that board (`TimeText.MinutesSeconds` for times, a plain number for counts, so `minigolf` reads as strokes even though the catalog stores it as a time);
- your own row washed in the game's accent, and a sticky self bar (`Material.ThemedGlass`, the one piece of glass on a hub page) that fades in at the bottom with your rank and value whenever your row is out of view, including when you are ranked below the loaded top 50.

Loading shows a skeleton podium and rows, a failure an `EmptyState` with Retry, an empty board an `EmptyState` with Play, and signed out an `EmptyState` with Open Settings. Boards stay browsable without opting in: a player who has not joined sees the compact Join card above the podium and a self bar that reads "You are not on leaderboards" with Join (a failure replaces the text). Profile's Your ranks rail reads `MyRanks` (the board's mode name from the game's own `Spec.Modes`, "#12", "of 1,240", "#3 this week", tap to open that board), and tiles and posters carry a "#12" capsule for your best rank, cached per `GamesLibrary.RefreshRanks`. Settings > Privacy carries the "Show me on leaderboards" switch, which reads `LeaderboardStore.ShownParticipation` and drives the same `SetParticipation` path, with a hint that scores stay on this phone while it is off.

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
- **The custom HUD slot is one frame behind.** `Hud.Custom(width)` reserves the slot this frame and `Hud.CustomRect(index)` holds the rect from the previous layout, so draw into it only when `Hud.CustomPlaced(index)` is true (it is false on the first frame and after a `Clear`).
- **Report the live score.** A `ScoreKind.Score` game must call `session.Report(score)` every frame it changes; leaving the run settles that value. Override `OnQuit(session)` only to `Finish` with something richer than the live score (secondary stats, a mode's own outcome); a completion game leaves the default and a quit run simply ends.
- **Tier and mode stat ids rank under their root.** `chess.easy` is stored locally as its own best but ranks, uploads and opens the leaderboard as `chess`. When you add a tier, decide whether the catalog wants a row of its own (then it needs the backend first) or the fold (then nothing to do); a kind mismatch means neither, and the rank pill stays Unknown.
- **`WantCaptureKeyboard` does nothing against the game client.** Only `io.WantTextInput` makes Dalamud withhold keys from FFXIV. Read keys through `GameInput`, never through a bare `ImGui.IsKeyDown` behind `SetNextFrameWantCaptureKeyboard`. While a game claims the keyboard, Escape is swallowed too, so the client's system menu opens only after the phone loses focus; that is the intended trade.
- **Difficulty-suffixed stat ids need hub support.** Stats keyed like `sudoku.easy` prefix-match for the daily via `GameStatsStore`, but `GamesLibrary.BestRecord` picks the record the hub's best cards and progress lines display, so a new difficulty tier or mode means updating that switch too.
- **`HitStop` alone freezes nothing.** The freeze only happens, and only counts down, inside `ScaleDelta`. A game that calls `HitStop` without routing its simulation delta through `ScaleDelta` gets no pause at all.
- **Only `Session.Finish` reaches the records and the leaderboard.** A game never calls `GameStatsStore.Submit*` itself: that would record the run twice and still leave the `IScoreSink` unfed.
- **A side mode must not fold onto a star board.** `LeaderboardId` folds any non-streak, non-time mode stat onto its root, so a `crates.daily` Score or Level run would upload under `crates`, the total-stars board. Give a daily, endless or nine-hole mode its own catalog row (`siege.endless`), mark it in `UnrankedModes` (Mini Golf's 9 holes, Tempo's practice), or keep its kind apart from the root's (a Time mode never folds onto a Level root).
- **A Time kind reads as a clock everywhere the host shows it.** The intro Best pill, the HUD Best capsule, the result card and the leaderboard rows format a Time value as m:ss. Anything else that is lower-is-better (strokes, attempts, moves) is a `ScoreKind.Count` with a `Unit` on the spec; the upload layer still sends it to the server's Time kind.
- **Never take text colours from the phone theme on the stage.** `theme.TextStrong` is near black in a light phone theme, and the stage is dark. Use `StageInks` (see [Stage inks](#stage-inks)).
- **Camera order.** Each frame: `Fit` (with an anchor if the origin should not sit in the middle), then `Place` or `Follow`, then `context.Fx.ApplyTo(ref camera)`, then `camera.Update(context.RawDeltaSeconds, scale)`, then `context.Backdrop.SetCamera(in camera)`, and only then draw. A world drawn before the camera moves trails the backdrop and the HUD by a frame, and setting `Anchor` before a `Fit` loses it, because `Fit` writes the anchor.
- **World-space particles need world numbers and the camera draw.** A particle emitted at a world position must be drawn with `particles.Draw(drawList, in camera)`, and its speed, size and gravity must be in world units: pass the gravity argument of `Burst`, `Sparkle`, `Streaks` and `Confetti` (their defaults are pixels per second) or build a `ParticleSpec`. Mixing the two draws particles at the wrong scale, or puts them at the world origin.
- **`GamePad.DPad` and `Shooter` report presses, not holds.** A pad that steers or fires for as long as a finger stays down needs `GamePad.HeldDPad`, `HoldButton`, `ShooterPadInput.FireHeld` or the `HeldPadState` and `HoldLatch` state machines; polling a press-fired pad every frame moves one step per tap. Release a held pad when play stops, so a run that ends under a finger does not carry the direction into the next.
- **Physics steps itself; call `Step` once per frame.** `PhysicsWorld.Step(context.DeltaSeconds)` already runs as many fixed 1/120 s ticks as the frame owes and clears last frame's contact events first, so never wrap it in `Substeps` or call it twice a frame (the second call wipes the first call's events). Read `Event(index)` right after `Step`. `Tick()` runs exactly one step and keeps the events, which suits tests and replays, not frames. Draw bodies with `RenderPosition` and `RenderAngle`, rule on `Position` and `Angle`, and apply forces every frame because `Step` clears them.
- **A new stat id is a two-repository change.** `ScoreStatIds.Catalog` must match the server's catalog (`ScoresWireContractTests.StatIdsMirrorTheServerCatalog` pins the list), and the store silently ignores a submission whose stat id is not in it. Add the id to the backend catalog first, then here, with its kind and direction.

## Related docs

- [App framework](app-framework.md): the `IPhoneApp` contract, `AppRegistry`, navigation
- [Creating an app](creating-an-app.md): the full tutorial for a new phone app
- [UI toolkit](ui-toolkit.md): `Typography`, `UiInteract`, `Squircle`, `Metrics`, and friends used throughout the games
- [State and persistence](state-and-persistence.md): how `Configuration` loads, saves, and what belongs in it
- [Localization](localization.md): adding a game's `Title` and `Hook` strings to L.cs and the nine JSONs (genre labels already exist in `GameGenres.Label`)
- [Game integration](game-integration.md): Honorific nameplate titles and the Dalamud services (such as `IKeyState`) the games rely on
- [Networking](networking.md): the Aethernet client, the score and room routes, and the realtime socket the online rooms ride on
- [Testing and release](testing-and-release.md): the test project that hosts every game's board suite, the stage kit, the hub and the leaderboard suites

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

A game that paints marks of its own onto the ground (Crater's scorch rings) passes an `ITerrainOverlay` as the fourth constructor argument instead of keeping a second texture. The texture merges the overlay's `TakeDirty(out region)` with the mask's dirty cells and, after every repaint (construction and `SetMaterial` included) and before the single upload, calls `Paint(pixels, width, in region)` so the overlay can draw into the RGBA canvas; `TerrainPainter.Coverage` says which cells a repaint really touched. Nothing allocates per frame.

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
