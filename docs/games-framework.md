# Mini-games framework

This page explains how the Games app hosts its mini-games and how to build a new one with the shared framework: the `IMiniGame` contract, the juice helpers (screen shake, hit-stop, particles, animated numbers), the scoring and daily-streak plumbing, and the rules that only apply inside games. Read it after [app-framework.md](app-framework.md), when you want to add or change a mini-game. The mini-games themselves are fully client-side and never talk to the Aethernet backend; the `GamesApp` hub around them does, for the coin economy (play-session reporting, the server-picked featured game, coin awards), as described below.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Apps/Games/GamesApp.cs | The Games hub: routing, the running game, the coin chip (launcher pages live in the .Launcher and .Tiles partials) |
| src/Aetherphone/Apps/Games/GamesLibrary.cs | The catalog behind the launcher: release order, latest wave, recents, filters and search |
| src/Aetherphone/Apps/Games/Framework/GameGenre.cs | The genre shelves a game can declare |
| src/Aetherphone/Apps/Games/Online/OnlineHub.cs | The friends lobby: host cards, join by code, open rooms |
| src/Aetherphone/Apps/Games/Framework/IMiniGame.cs | Contract every mini-game implements |
| src/Aetherphone/Apps/Games/Framework/GameContext.cs | Per-frame data handed to the running game |
| src/Aetherphone/Apps/Games/Framework/GameScene.cs | Ambient backdrop glow and arena panel drawing |
| src/Aetherphone/Apps/Games/Framework/GameJuice.cs | Entrance progress, stagger, and pop-in easing |
| src/Aetherphone/Apps/Games/Framework/FeedbackFx.cs | Shake, hit-stop, flash, shockwave rings, floating text |
| src/Aetherphone/Apps/Games/Framework/ParticleSystem.cs | Pooled particles: bursts, sparkles, streaks, confetti |
| src/Aetherphone/Core/Animation/RollingValue.cs | Animated number that rolls toward a target and pops (shared animation infrastructure, not games-only) |
| src/Aetherphone/Apps/Games/Framework/GameHud.cs | Score pills, restart button, accent buttons |
| src/Aetherphone/Apps/Games/Framework/GameOverlay.cs | End-of-round result card with confetti on a new best |
| src/Aetherphone/Apps/Games/Framework/GameGrid.cs | Centered cell-grid math for board games |
| src/Aetherphone/Apps/Games/Framework/GamePalette.cs | Shared board colors and ink-contrast picker |
| src/Aetherphone/Apps/Games/Framework/GameNumber.cs | Cached integer-to-string labels (no per-frame allocation) |
| src/Aetherphone/Apps/Games/Framework/GameInput.cs | Keyboard reads that keep the keys away from the game client |
| src/Aetherphone/Apps/Games/Framework/GamePad.cs | On-screen d-pad and left/fire/right pad |
| src/Aetherphone/Apps/Games/Framework/Substeps.cs | Splits a frame delta into capped simulation substeps |
| src/Aetherphone/Apps/Games/Framework/FixedStepClock.cs | Fixed-timestep accumulator with a catch-up cap |
| src/Aetherphone/Apps/Games/Framework/PixelSprite.cs | Bitmap sprites drawn as filled runs in one color |
| src/Aetherphone/Apps/Games/Framework/GameBanner.cs | Pop-in, hold, fade banner for "Ready" and "Wave 3" |
| src/Aetherphone/Core/Games/GameStatsStore.cs | Best scores, best times, win streaks, daily challenge |
| src/Aetherphone.Tests/ChessRulesTests.cs | Perft tests that pin the chess rules engine |

## How the Games app is structured

The whole arcade is one phone app. `GamesApp` implements `IPhoneApp` (the contract every phone app fulfils, see [app-framework.md](app-framework.md)) and is registered once in `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs:

```csharp
apps.Add(new GamesApp(services.GameStats, services.GameData, services.Textures, services.Coins,
    services.CoinSessions, services.GameRooms));
```

The last three arguments are the coin plumbing and the friends lobby: `services.Coins` (the wallet store), `services.CoinSessions` (the play-session tracker), and `services.GameRooms` (the online room store). What the hub does with them is described below.

Inside, `GamesApp` owns a plain `IMiniGame[]` array built in its constructor. That array is the registry: a game exists because a line constructs it there. Most games have parameterless constructors; `TriviaApp` shows that a game can take services if `GamesApp` passes them through.

Each game implements `IMiniGame` from src/Aetherphone/Apps/Games/Framework/IMiniGame.cs:

```csharp
internal interface IMiniGame : IDisposable
{
    string Id { get; }
    string Title { get; }
    GameGenre Genre { get; }
    Vector4 Accent => AppAccents.For(Id);
    bool RunsOnAClock => false;
    bool WantsLandscape => false;
    void Open();
    void Close();
    void Draw(in GameContext context);
}
```

`Genre` is a `GameGenre` value (src/Aetherphone/Apps/Games/Framework/GameGenre.cs), one of five shelves for local games: `Arcade` (reflex classics), `Action` (shooters and mazes), `Puzzle`, `Brain` (logic, words, memory, trivia) and `Tabletop` (board and card games). A sixth value, `Friends`, is reserved for the online games the hub adds itself; no `IMiniGame` declares it. `GameGenres.Label` maps each value to its `LocString`.

`WantsLandscape` defaults to false. A game that overrides it to true (Doom) makes the hub hold the same landscape lock the camera and MogCast theater use, so the phone rotates while the game is open; in that orientation the hub draws no header, hands the game the whole content rect, and floats a small back chip at the top-left over whatever the game draws.

`RunsOnAClock` defaults to false. A game whose simulation advances on a timer overrides it to true so the hub can fade a Paused veil over it while the phone is unfocused (see the focus gate below); a turn-based game leaves the default and simply stands still.

### The launcher

The launcher is split across three partials: `GamesApp.cs` (routing, the running game, the coin chip), `GamesApp.Launcher.cs` (page layout) and `GamesApp.Tiles.cs` (the hero, tiles, shelf headings and the friends card). It draws on the neutral `AppPalettes.Games` skin with the featured game's accent washed over it by `GameScene.Ambient`.

`GamesLibrary` (src/Aetherphone/Apps/Games/GamesLibrary.cs) is the catalog behind the launcher. It wraps the `IMiniGame[]` plus one `GameEntry` per online game (Uno, Chess, 8-Ball Pool, ids `online.uno`, `online.chess`, `online.pool`) and keeps every list the pages draw from as reusable `int[]` index arrays, so the draw code never allocates:

| List | What it holds |
| --- | --- |
| `Ordered` | Every entry, newest release first (the `Releases` table in the same file; add a row when you add a game) |
| `Latest` | The newest wave: entries released within a week of the newest one, capped at ten |
| `Recent` | Entries with a `LastPlayedUnixSeconds` on their `GameStatRecord`, most recent first, capped at eight |
| `Filter(kind, query)` | One chip's view, or a title search across every shelf when the query is not blank |

`IsNew` marks an entry for thirty days after its release; `Best` and `Subtitle` carry the cached best-score line ("Best · 1,240", "Best · 1:05", "Streak · 3") or fall back to the genre label. `Rebuild` refreshes the recents and best labels; the hub calls it when it opens, when a game closes, and when the player leaves an online room.

The page itself is a pinned header (title plus a search toggle that slides a `SearchField` in under it), a pannable `ChipRail` of filters (`All`, `New`, the five genre shelves, `With friends`), and an `AppSurface` body:

- `All` stacks the daily hero, a `Latest additions` shelf, a `Jump back in` shelf (only once something has been played), the `Play with friends` card, and an `All games` grid newest-first.
- A genre chip shows that shelf's grid with a count; `New` shows the latest wave; `With friends` shows the friends card and the three online tiles.
- A non-blank search shows matching tiles from every shelf, or an `EmptyState` when nothing matches.

Shelves pan sideways through `TileRail` (drag with slop, clipped to the phone edge); grids pick three to six columns from the content width. Tiles are accent-gradient squircles with the game's `AppIconArt` (or `OnlineGameArt` for the online three), a `NEW` pill inside the thirty-day window, a people badge on online entries, and a hover lift on a per-entry `Spring`. Tapping a local tile opens the game; tapping an online tile opens the friends lobby with that game's card highlighted.

The server picks the featured game when it can: `RebuildLayout` first computes the daily-rotation fallback `featuredIndex = GameStatsStore.TodayIndex * FeaturedStep % games.Length`, then overrides it when `coins.Wallet?.FeaturedGameId` (a field on the coin wallet DTO in src/Aetherphone/Core/Aethernet/Contracts/CoinDtos.cs) names a game in the array. Whichever wins, its id lands in `stats.DailyGameId`, which makes it the daily challenge.

### Routes and the running game

Navigation uses a four-route `ViewRouter<GameRoute>` (`Launcher`, `Playing`, `OnlineHub`, `OnlineRoom`). Tapping a tile calls `OpenGame`, which sets `currentGame`, calls `game.Open()`, stamps the game as played, and pushes `Playing`. The back button pops the route, and `GamesApp.Draw` calls `CloseCurrentGame` (which calls `game.Close()`) once the transition lands back on the launcher. `OnlineHub` (src/Aetherphone/Apps/Games/Online/OnlineHub.cs) is the friends lobby: one host card per online game, the join-by-code field, and the player's open rooms; `OnlineRoomView` is the room itself. When a round ends, `OnlineFinishHold` (src/Aetherphone/Apps/Games/Online/OnlineFinishHold.cs) keeps the finished table on screen until its last card flight or shot replay has settled, then shows a five-second countdown card (tap to skip) before the room shows the lobby again.

The hub also owns the coin plumbing that wraps every game. `OpenGame` and `CloseCurrentGame` report the play session to the backend through `CoinGameSessionTracker` (`GameOpened` and `GameClosed`), a chip in the in-game header counts the open session toward the server's earning thresholds, and `GamesApp.Draw` polls `coinSessions.TakeAward` to spawn a floating coin reward when the server grants one. None of this reaches the games: an `IMiniGame` only ever sees its `GameContext`.

While a game is active, `GamesApp.DrawActiveGame` clamps the frame delta, zeroes it when the game should not be simulating, and hands the game everything it needs as a `GameContext`:

```csharp
var attentive = GameFocus.Active;
var frameSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
game.Draw(new GameContext(body, context.Theme, stats, attentive ? frameSeconds : 0f));
```

`GameFocus.Active` (src/Aetherphone/Apps/Games/Framework/GameFocus.cs) is false while the phone window is unfocused or the game's own text input is active, so an unattended game receives `DeltaSeconds` of zero and stands still. On top of that, a game whose `RunsOnAClock` is true gets the Paused veil faded in over its board so the frozen clock reads as a pause.

`GameContext` carries four fields: `Body` (the `Rect` the game may draw in), `Theme` (the current `PhoneTheme`), `Stats` (the `GameStatsStore`), and `DeltaSeconds`. Because the phone UI is Dear ImGui (an immediate-mode UI where everything is redrawn from scratch every frame), `Draw` runs every frame and the game keeps its own state in fields between frames.

### Checklist for registering a new game

1. Create a folder src/Aetherphone/Apps/Games/YourGame with a `YourGameApp : IMiniGame`. Most games split logic into a `*Board` class and drawing into a `*Renderer` class.
2. Add `new YourGameApp()` to the `games` array in the `GamesApp` constructor.
3. Pick a `GameGenre` for `Genre` and add the `Title` string to the `Games` section of L.cs and the nine language JSONs (see [localization.md](localization.md)).
4. Add a row for your id to `GamesLibrary.Releases` with the release date, so the game sorts newest-first, joins the `Latest additions` shelf and wears the `NEW` pill for its first month.
5. Add an accent color keyed by your game id in src/Aetherphone/Core/Apps/AppAccents.cs; `IMiniGame.Accent` defaults to `AppAccents.For(Id)`.
6. Optionally add icon art for your id in src/Aetherphone/Windows/Components/AppIconArt.cs; the launcher falls back to drawing your title text on the tile.
7. If the launcher should show a best-score line for your game, add a case to `GamesLibrary.BestLabel`.

## The juice framework

"Juice" is the game-feel layer: exaggerated visual feedback (shake, freeze-frames, particles, popping numbers) that makes inputs feel physical. It lives in src/Aetherphone/Apps/Games/Framework and is shared by every game, with one exception: `RollingValue` sits with the shared animation code in src/Aetherphone/Core/Animation because the rest of the phone uses it too.

### FeedbackFx: shake, hit-stop, flash, rings, floating text

`FeedbackFx` is an instance class; each game owns one. Its API:

| Member | What it does |
| --- | --- |
| `AddTrauma(float amount)` | Adds screen-shake energy (0 to 1); decays automatically |
| `ShakeOffset(float scale)` | Random offset for this frame; add it to your arena rect |
| `HitStop(float seconds)` | Freezes the simulation for a beat (a "hit-stop", the brief pause fighting games use on impact) |
| `ScaleDelta(float deltaSeconds)` | Returns 0 while frozen, otherwise the delta; also counts the freeze down |
| `Flash(Vector4 color, float alpha)` / `DrawFlash` | Full-arena color flash |
| `Shockwave(center, toRadius, color, ...)` / `DrawRings` | Expanding impact ring |
| `AddText(text, position, color, ...)` / `DrawText` | Floating score text that rises and fades |
| `Update(float deltaSeconds)` | Advances all of the above; call once per frame |
| `Clear()` | Reset on restart |

The hit-stop contract has a strict frame order. Call `ScaleDelta` exactly once per frame with the raw delta, feed its result to the simulation only, and feed the raw delta to the feedback systems so shake and particles keep animating during the freeze. Every game that uses hit-stop follows this shape (here from src/Aetherphone/Apps/Games/Snake/SnakeApp.cs):

```csharp
var simDelta = fx.ScaleDelta(deltaSeconds);
var crashed = board.Step(simDelta, area, mouse);
particles.Update(deltaSeconds);
fx.Update(deltaSeconds);
```

### ParticleSystem

`ParticleSystem` is a fixed-capacity pool (512 particles by default, set in the constructor). Emitters silently drop particles when the pool is full. The shapes come from the `ParticleShape` enum (`Circle`, `GlowCircle`, `Square`, `Star`, `Streak`).

| Member | Use for |
| --- | --- |
| `Burst(origin, count, color, speed, size, life, ...)` | Generic radial explosion |
| `Sparkle(origin, count, color, speed, size, life)` | Twinkling star particles |
| `Streaks(origin, count, color, speed, size, life, ...)` | Fast motion-line debris |
| `Confetti(origin, count, palette, speed, size, life)` | Celebration squares from a color span |
| `Update(deltaSeconds)` / `Draw(drawList, scale)` / `Clear()` | Per-frame advance, render, reset |

### RollingValue

`RollingValue` is a mutable struct that animates a displayed integer toward a target and "pops" its scale when the target changes:

| Member | What it does |
| --- | --- |
| `Snap(int value)` | Jump straight to a value (call on restart) |
| `Update(int value, float deltaSeconds)` | Retarget and advance; returns true the frame the target changed |
| `Display` | The integer to draw this frame |
| `PopScale` | Text scale multiplier, 1.0 at rest, up to 1.30 right after a change |

You rarely call it directly: `GameHud.ScorePill` takes `ref RollingValue`, calls `Update` for you, and draws the pill with the pop applied:

```csharp
GameHud.ScorePill(center, Loc.T(L.Games.Score), ref scoreRoll, board.Score, Accent, theme, deltaSeconds);
```

### Scene, HUD, grid, overlay

- `GameScene.Ambient(drawList, body, accent)` draws the drifting glow-blob backdrop plus vignette that every game except Flap (which paints its own sky) uses behind its board. `GameScene.Arena(drawList, rect, rounding, scale, accent)` draws a raised board panel.
- `GameJuice.Advance(progress, deltaSeconds)` drives a 0-to-1 entrance value, `GameJuice.Stagger(progress, index, count)` splits it across cells so tiles appear in sequence, and `GameJuice.PopIn(progress)` maps it through `Easing.EaseOutBack` for an overshooting pop.
- `GameGrid.Centered(area, columns, rows, gapFraction)` computes a centered square-cell grid; `Cell(column, row)` and `CellCenter(column, row)` give you rects and centers, `Bounds` the whole board.
- `GameHud` also has `Pill` (static value), `RestartButton`, and `Button`.
- `GamePalette` holds the shared dark board colors plus `InkOn(fill)` to pick readable text ink, and `GameNumber.Label(int)` returns a cached string so score text does not allocate every frame.
- `GameOverlay.Draw(area, theme, accent, progress, result)` renders the end-of-round card from a `GameResult` (title, primary stat, optional secondary line, `NewBest` flag). Drive `progress` from 0 to 1 yourself; the card scales in, counts the score up, fires confetti when `NewBest` is true, and returns true when the player clicks Play Again. The card measures itself: the stack is title, new-best badge, uppercase stat label, stat value, secondary line, button, separated by Metrics.Space tokens, and both the card width and height follow the measured content. Long titles and long values shrink to fit rather than overflow, so a localized title needs no per-game tuning.

### Input, clocks, sprites, banners

- `GameInput` is the only way a game may read the physical keyboard. `GameInput.Claim()` returns false unless `GameFocus.Active`; when it returns true it has raised `io.WantTextInput` for this frame and cleared the game client's key state for every key a game consumes. Dalamud honours `WantTextInput` (it swallows the key messages and clears `KeyState` on its input frame); it does not honour `WantCaptureKeyboard` against the game at all, so a game that only calls `SetNextFrameWantCaptureKeyboard` still walks the character with WASD. The convenience readers `Held(key, alternate)` and `Pressed(key, alternate, repeat)` call `Claim` for you and pair WASD with the arrows. Call them only while the game actually wants keys (not under the result overlay), so the keyboard returns to the client the moment play stops.
- `GamePad.DPad(area, accent, theme)` draws a W/A/S/D cross and returns the `PadDirection` pressed this frame (press-fired, one per frame). `GamePad.Shooter(area, accent, theme)` draws A, W, D and returns `ShooterPadInput` with `Left` and `Right` held and `Fire` pressed. `DPadHeight(scale)` and `ShooterHeight(scale)` size the band. Games combine pad and keyboard themselves: `var left = pad.Left || GameInput.Held(ImGuiKey.A, ImGuiKey.LeftArrow);`.
- `new Substeps(deltaSeconds, maxStepSeconds)` gives `Count` and `Step` for a loop that advances fast projectiles without tunnelling; the count is capped at 16 so a stall never becomes a burst. `FixedStepClock(step, maxCatchUp)` is the alternative for sims that must run on an exact tick: `Advance(delta)` returns how many steps to run, `Alpha` is the render interpolation fraction, `Reset()` on restart.
- `PixelSprite` takes bitmap rows (`#` lit) once, at static init, and `Draw(drawList, topLeft, unit, color)` emits one rect per lit run. It is the sprite path for Invaders-style games; draw it in the game's accent with a `ProgressRing.Glow` behind it, never in a flat ink.
- `GameBanner.Draw(drawList, center, text, accent, theme, progress)` pops a frosted pill in over the first 18% of `progress`, holds, and fades over the last 25%. Drive `progress` with `GameBanner.Advance(progress, delta, lifetimeSeconds)`. Use it for stage and ready text that must hold; `FeedbackFx.AddText` rises and fades and is for score pops.

### Games with data files

Word Run reads its word banks from src/Aetherphone/Words (`<code>.answers.txt` and `<code>.valid.txt`, one word per line, shipped as content next to the plugin). They are generated, never hand-edited: `tools/build-word-banks.ps1` rebuilds them from SCOWL and the FrequencyWords lists, and THIRD-PARTY-NOTICES.md records both sources. Doom keeps no data in the repo at all; `DoomAssets` downloads the shareware episode and the soundfont into the plugin's config folder on first use and verifies them against pinned checksums.

## The motion exception

The rest of the phone uses critically damped motion: springs that settle without overshooting (see `Spring.Step` in src/Aetherphone/Core/Animation/Spring.cs). Games are the place allowed to bounce. `Easing.EaseOutBack` (an easing curve that overshoots its target and settles back) is defined in src/Aetherphone/Core/Animation/Easing.cs and is referenced only from files under src/Aetherphone/Apps/Games plus two Casino cabinet sites (BingoCabinet.cs and BingoCardArt.cs). Keep it that way: bouncy easing belongs to games and casino cabinets only. Inside a game, reach for `GameJuice.PopIn`; everywhere else, use springs.

## Scoring, streaks, and the daily challenge

`GameStatsStore` (src/Aetherphone/Core/Games/GameStatsStore.cs) is the only persistence an individual game touches; the coin traffic described earlier belongs to the hub, never to a game. It wraps `Configuration` (src/Aetherphone/Configuration.cs), which stores a `List<GameStatRecord>` plus `DailyChallengeStreak` and `DailyChallengeLastDay`. See [state-and-persistence.md](state-and-persistence.md) for how `Configuration` is saved.

| Member | Semantics |
| --- | --- |
| `Get(gameId)` | Returns a `GameStats` value (`BestScore`, `BestTimeSeconds`, `Streak`); zeros if never played |
| `SubmitScore(gameId, score)` | Higher is better; returns true only on a new best |
| `SubmitTime(gameId, seconds)` | Lower is better; returns true only on a new best |
| `RecordWin(gameId)` | Increments and returns a win streak (used by Pairs, Reversi, and Chess) |
| `ResetStreak(gameId)` | Clears the streak on a loss |
| `DailyGameId`, `DailyDone`, `DailyStreak` | Daily challenge state; the launcher sets `DailyGameId` and its streak chip reads `DailyDone` and `DailyStreak` |

Stat ids may carry a difficulty suffix, for example `sudoku.easy` or `minesweeper.easy`, or a ruleset suffix like `tetris.modern` (Tetris keeps separate bests for its Classic and Modern rulesets and remembers the last choice in `Configuration.TetrisModern`). Every submit path first calls the private `RecordDailyPlay`, which prefix-matches the stat id against `DailyGameId` (so `sudoku.easy` counts for a `sudoku` daily) and advances or resets the streak based on `TodayIndex`. This means finishing the featured game through any `Submit*` or `RecordWin` call completes the daily automatically; there is no separate daily API.

## Worked example: a minimal game

A complete tap-the-arena game showing the standard frame shape. Real games split simulation into a `*Board` and drawing into a `*Renderer`; this one is small enough to skip that. The `Title` uses a literal here; a real game adds a `LocString` to L.cs instead. Because the round runs on a 15 second countdown, the game overrides `RunsOnAClock` to true: the hub already zeroes its delta while the phone is unfocused, and this flag additionally fades the Paused veil over the board so the stalled timer reads as a pause rather than a hang.

```csharp
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Tap;

internal sealed class TapApp : IMiniGame
{
    private const string GameId = "tap";
    private const float RoundSeconds = 15f;
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private RollingValue scoreRoll;
    private int score;
    private float timeLeft;
    private bool over;
    private bool newBest;
    private float resultAppear;
    public string Id => GameId;
    public string Title => "Tap";
    public string Genre => Loc.T(L.Games.GenreArcade);
    public Vector4 Accent => AppAccents.For(Id);
    public bool RunsOnAClock => true;

    public void Open()
    {
        Restart();
    }

    public void Close()
    {
    }

    public void Dispose()
    {
    }

    private void Restart()
    {
        score = 0;
        timeLeft = RoundSeconds;
        over = false;
        newBest = false;
        resultAppear = 0f;
        scoreRoll.Snap(0);
        particles.Clear();
        fx.Clear();
    }

    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var body = context.Body;
        var simDelta = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.DeltaSeconds);
        fx.Update(context.DeltaSeconds);
        var drawList = ImGui.GetWindowDrawList();
        GameScene.Ambient(drawList, body, Accent);
        var shake = fx.ShakeOffset(scale);
        var arena = new Rect(body.Min + new Vector2(12f * scale, 60f * scale) + shake,
            body.Max - new Vector2(12f * scale, 12f * scale) + shake);
        GameScene.Arena(drawList, arena, 18f * scale, scale, Accent);
        GameHud.ScorePill(new Vector2(body.Center.X, body.Min.Y + 30f * scale), Loc.T(L.Games.Score),
            ref scoreRoll, score, Accent, context.Theme, context.DeltaSeconds);
        if (!over)
        {
            Step(arena, simDelta, scale, context);
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        fx.DrawFlash(drawList, body, 0f);
        if (over)
        {
            DrawResult(context);
        }
    }

    private void Step(Rect arena, float deltaSeconds, float scale, in GameContext context)
    {
        timeLeft -= deltaSeconds;
        if (timeLeft <= 0f)
        {
            over = true;
            newBest = context.Stats.SubmitScore(GameId, score);
            return;
        }

        if (!UiInteract.Hover(arena.Min, arena.Max) || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        var hit = ImGui.GetMousePos();
        score += 1;
        particles.Burst(hit, 10, Accent, 160f * scale, 3f, 0.5f);
        fx.Shockwave(hit, 40f * scale, Accent, 0.35f);
        fx.AddText("+1", hit, Accent);
        fx.AddTrauma(0.06f);
        fx.HitStop(0.03f);
    }

    private void DrawResult(in GameContext context)
    {
        resultAppear = MathF.Min(1f, resultAppear + context.DeltaSeconds * 3.4f);
        var result = new GameResult(Loc.T(L.Games.GameOver), Accent, Loc.T(L.Games.Score),
            GameNumber.Label(score), null, newBest);
        if (GameOverlay.Draw(context.Body, context.Theme, Accent, resultAppear, result))
        {
            Restart();
        }
    }
}
```

Compare with src/Aetherphone/Apps/Games/Whack/WhackApp.cs, which is the same skeleton with a real board and renderer.

## Per-game patterns worth copying

- **Pin a rules engine with perft before building on it.** Perft counts every legal move sequence to a given depth; the totals for standard chess are published, so any generation bug changes the number. `ChessRulesTests.PerftFromTheStartingPositionMatchesKnownCounts` in src/Aetherphone.Tests/ChessRulesTests.cs asserts depths 1 through 5 (20 up to 4,865,609 nodes) against `ChessBoard.GenerateMoves` with `Make`/`Unmake` round-trips. The search AI in ChessEngine.cs builds on the same `GenerateMoves` and `Make`/`Unmake` surface the perft pins. Do the same for any game with nontrivial rules.
- **Decide what your generator guarantees.** `SudokuBoardTests.EveryGeneratedPuzzleHasExactlyOneSolution` verifies Sudoku puzzles with an independent solution counter, so Sudoku can safely mark a specific digit "wrong". Flow makes the opposite trade: `FlowBoard.Generate` builds a Hamiltonian path (a path visiting every cell once) and cuts it into colored segments, which guarantees at least one solution but not a unique one. Accordingly, `FlowBoard.IsSolved` accepts any complete connected fill rather than comparing against the generator's answer. If your generator cannot prove uniqueness, your win check must validate the player's answer on its own terms.

## Naming rule

Games are named for what they do: Whack, Snake, Stack, Water Sort, Crystal Drop, Flow. Never theme a game name with the word "Aether"; that prefix is reserved for platform features (Aethernet, Aethergram). Check the `Games` section of L.cs: no game title uses it, and new ones must not either.

## Gotchas

- **Hit-stop delta split.** `FeedbackFx.ScaleDelta` mutates the freeze timer, so call it exactly once per frame with the raw delta; only that call counts the freeze down, so feeding it an already-scaled (zero) delta makes the freeze last forever. Pass its result to the simulation only. `FeedbackFx.Update` and `ParticleSystem.Update` early-return when the delta is 0 or less, so feeding them the scaled delta freezes shake, flash decay, and particles along with the game.
- **`RollingValue` needs `Snap` on restart.** It initializes itself on the first `Update`, but after a restart the old value is still stored, so the score visibly rolls down from the previous run unless you call `Snap(0)` in your restart path.
- **`GameOverlay` is a single static instance.** Its celebration and count-up state is static and resets when the overlay has not been drawn for 0.25 seconds or its progress moves backwards. One game at a time is fine (the router guarantees that); drawing it twice in one frame is not.
- **Fixed pools drop silently.** `FeedbackFx` caps at 32 floating texts and 12 rings, `ParticleSystem` at its constructor capacity (512 default). Never build gameplay logic that depends on an emitted effect existing.
- **Always submit through `GameStatsStore`, even for losing runs.** `SubmitScore` calls `RecordDailyPlay` before rejecting a non-positive or non-best score, so a zero-point run still completes the daily challenge. Bypassing the store (or only submitting on a new best) silently breaks the streak.
- **Use `GameContext.DeltaSeconds`, not `ImGui.GetIO().DeltaTime`.** `GamesApp` clamps the delta to 0.1 seconds before building the context so a hitched frame cannot teleport the simulation, and it zeroes the delta while `GameFocus.Active` is false. Reading the IO delta directly loses both protections: a hitch teleports the game and it keeps simulating while the phone is unfocused.
- **`WantCaptureKeyboard` does nothing against the game client.** Only `io.WantTextInput` makes Dalamud withhold keys from FFXIV. Read keys through `GameInput`, never through a bare `ImGui.IsKeyDown` behind `SetNextFrameWantCaptureKeyboard`. While a game claims the keyboard, Escape is swallowed too, so the client's system menu opens only after the phone loses focus; that is the intended trade.
- **Difficulty-suffixed stat ids need launcher support.** Stats keyed like `sudoku.easy` prefix-match for the daily via `GameStatsStore`, but `GamesApp.StatValue` picks one concrete record to display, so a new difficulty tier means updating that switch too.

## Related docs

- [App framework](app-framework.md): the `IPhoneApp` contract, `AppRegistry`, navigation
- [Creating an app](creating-an-app.md): the full tutorial for a new phone app
- [UI toolkit](ui-toolkit.md): `Typography`, `UiInteract`, `Squircle`, `Metrics`, and friends used throughout the games
- [State and persistence](state-and-persistence.md): how `Configuration` loads, saves, and what belongs in it
- [Localization](localization.md): adding `Title` and `Genre` strings to L.cs and the nine JSONs
- [Testing and release](testing-and-release.md): the test project that hosts the chess and sudoku suites
