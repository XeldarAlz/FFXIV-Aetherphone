# Mini-games framework

This page explains how the Games app hosts its mini-games and how to build a new one with the shared framework: the `IMiniGame` contract, the juice helpers (screen shake, hit-stop, particles, animated numbers), the scoring and daily-streak plumbing, and the rules that only apply inside games. Read it after [app-framework.md](app-framework.md), when you want to add or change a mini-game. Everything here is client-side; the games never talk to the Aethernet backend.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Apps/Games/GamesApp.cs | The Games hub: launcher UI, game list, routing into a running game |
| src/Aetherphone/Apps/Games/Framework/IMiniGame.cs | Contract every mini-game implements |
| src/Aetherphone/Apps/Games/Framework/GameContext.cs | Per-frame data handed to the running game |
| src/Aetherphone/Apps/Games/Framework/GameScene.cs | Ambient backdrop glow and arena panel drawing |
| src/Aetherphone/Apps/Games/Framework/GameJuice.cs | Entrance progress, stagger, and pop-in easing |
| src/Aetherphone/Apps/Games/Framework/FeedbackFx.cs | Shake, hit-stop, flash, shockwave rings, floating text |
| src/Aetherphone/Apps/Games/Framework/ParticleSystem.cs | Pooled particles: bursts, sparkles, streaks, confetti |
| src/Aetherphone/Apps/Games/Framework/RollingValue.cs | Animated number that rolls toward a target and pops |
| src/Aetherphone/Apps/Games/Framework/GameHud.cs | Score pills, restart button, accent buttons |
| src/Aetherphone/Apps/Games/Framework/GameOverlay.cs | End-of-round result card with confetti on a new best |
| src/Aetherphone/Apps/Games/Framework/GameGrid.cs | Centered cell-grid math for board games |
| src/Aetherphone/Apps/Games/Framework/GamePalette.cs | Shared board colors and ink-contrast picker |
| src/Aetherphone/Apps/Games/Framework/GameNumber.cs | Cached integer-to-string labels (no per-frame allocation) |
| src/Aetherphone/Core/Games/GameStatsStore.cs | Best scores, best times, win streaks, daily challenge |
| src/Aetherphone.Tests/ChessRulesTests.cs | Perft tests that pin the chess rules engine |

## How the Games app is structured

The whole arcade is one phone app. `GamesApp` implements `IPhoneApp` (the contract every phone app fulfils, see [app-framework.md](app-framework.md)) and is registered once in `AppRegistry.BuildDefault` in src/Aetherphone/Core/Apps/AppRegistry.cs:

```csharp
apps.Add(new GamesApp(services.GameStats, services.GameData, services.Textures));
```

Inside, `GamesApp` owns a plain `IMiniGame[]` array built in its constructor. That array is the registry: a game exists because a line constructs it there. Most games have parameterless constructors; `TriviaApp` shows that a game can take services if `GamesApp` passes them through.

Each game implements `IMiniGame` from src/Aetherphone/Apps/Games/Framework/IMiniGame.cs:

```csharp
internal interface IMiniGame : IDisposable
{
    string Id { get; }
    string Title { get; }
    string Genre { get; }
    Vector4 Accent => AppAccents.For(Id);
    void Open();
    void Close();
    void Draw(in GameContext context);
}
```

The launcher groups games into sections by `Genre`, draws a row per game with its accent tile, and shows a featured hero card. The featured game rotates daily: `RebuildLayout` computes `featuredIndex = GameStatsStore.TodayIndex * FeaturedStep % games.Length` and stores that game's id in `stats.DailyGameId`, which makes it the daily challenge.

Navigation uses a two-route `ViewRouter<GameRoute>` (`Launcher` and `Playing`). Tapping a row calls `OpenGame`, which sets `currentGame`, calls `game.Open()`, and pushes `Playing`. The back button pops the route, and `GamesApp.Draw` calls `CloseCurrentGame` (which calls `game.Close()`) once the transition lands back on the launcher.

While a game is active, `GamesApp.DrawActiveGame` clamps the frame delta and hands the game everything it needs as a `GameContext`:

```csharp
var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);
game.Draw(new GameContext(body, context.Theme, stats, deltaSeconds));
```

`GameContext` carries four fields: `Body` (the `Rect` the game may draw in), `Theme` (the current `PhoneTheme`), `Stats` (the `GameStatsStore`), and `DeltaSeconds`. Because the phone UI is Dear ImGui (an immediate-mode UI where everything is redrawn from scratch every frame), `Draw` runs every frame and the game keeps its own state in fields between frames.

### Checklist for registering a new game

1. Create a folder src/Aetherphone/Apps/Games/YourGame with a `YourGameApp : IMiniGame`. Most games split logic into a `*Board` class and drawing into a `*Renderer` class.
2. Add `new YourGameApp()` to the `games` array in the `GamesApp` constructor.
3. Add `Title` and, if new, `Genre` strings to the `Games` section of L.cs and the nine language JSONs (see [localization.md](localization.md)).
4. Add an accent color keyed by your game id in src/Aetherphone/Core/Apps/AppAccents.cs; `IMiniGame.Accent` defaults to `AppAccents.For(Id)`.
5. Optionally add icon art for your id in src/Aetherphone/Windows/Components/AppIconArt.cs; the launcher falls back to drawing your title text on the tile.
6. If the launcher should show a best-score chip for your game, add a case to `GamesApp.StatValue`.

## The juice framework

"Juice" is the game-feel layer: exaggerated visual feedback (shake, freeze-frames, particles, popping numbers) that makes inputs feel physical. All of it lives in src/Aetherphone/Apps/Games/Framework and is shared by every game.

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
- `GameOverlay.Draw(area, theme, accent, progress, result)` renders the end-of-round card from a `GameResult` (title, primary stat, optional secondary line, `NewBest` flag). Drive `progress` from 0 to 1 yourself; the card scales in, counts the score up, fires confetti when `NewBest` is true, and returns true when the player clicks Play Again.

## The motion exception

The rest of the phone uses critically damped motion: springs that settle without overshooting (see `Spring.Step` in src/Aetherphone/Core/Animation/Spring.cs). Mini-games are the one place allowed to bounce. `Easing.EaseOutBack` (an easing curve that overshoots its target and settles back) is defined in src/Aetherphone/Core/Animation/Easing.cs but is referenced only from files under src/Aetherphone/Apps/Games. Keep it that way: inside a game, reach for `GameJuice.PopIn`; outside the Games app, use springs.

## Scoring, streaks, and the daily challenge

`GameStatsStore` (src/Aetherphone/Core/Games/GameStatsStore.cs) is the only persistence a game touches. It wraps `Configuration` (src/Aetherphone/Configuration.cs), which stores a `List<GameStatRecord>` plus `DailyChallengeStreak` and `DailyChallengeLastDay`. See [state-and-persistence.md](state-and-persistence.md) for how `Configuration` is saved.

| Member | Semantics |
| --- | --- |
| `Get(gameId)` | Returns a `GameStats` value (`BestScore`, `BestTimeSeconds`, `Streak`); zeros if never played |
| `SubmitScore(gameId, score)` | Higher is better; returns true only on a new best |
| `SubmitTime(gameId, seconds)` | Lower is better; returns true only on a new best |
| `RecordWin(gameId)` | Increments and returns a win streak (used by Pairs, Reversi, and Chess) |
| `ResetStreak(gameId)` | Clears the streak on a loss |
| `DailyGameId`, `DailyDone`, `DailyStreak` | Daily challenge state; the launcher sets `DailyGameId` and its streak chip reads `DailyDone` and `DailyStreak` |

Stat ids may carry a difficulty suffix, for example `sudoku.easy` or `minesweeper.easy`. Every submit path first calls the private `RecordDailyPlay`, which prefix-matches the stat id against `DailyGameId` (so `sudoku.easy` counts for a `sudoku` daily) and advances or resets the streak based on `TodayIndex`. This means finishing the featured game through any `Submit*` or `RecordWin` call completes the daily automatically; there is no separate daily API.

## Worked example: a minimal game

A complete tap-the-arena game showing the standard frame shape. Real games split simulation into a `*Board` and drawing into a `*Renderer`; this one is small enough to skip that. The `Title` uses a literal here; a real game adds a `LocString` to L.cs instead.

```csharp
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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
- **Use `GameContext.DeltaSeconds`, not `ImGui.GetIO().DeltaTime`.** `GamesApp` clamps the delta to 0.1 seconds before building the context so a hitched frame cannot teleport the simulation. Reading the IO delta directly loses that protection.
- **Difficulty-suffixed stat ids need launcher support.** Stats keyed like `sudoku.easy` prefix-match for the daily via `GameStatsStore`, but `GamesApp.StatValue` picks one concrete record to display, so a new difficulty tier means updating that switch too.

## Related docs

- [App framework](app-framework.md): the `IPhoneApp` contract, `AppRegistry`, navigation
- [Creating an app](creating-an-app.md): the full tutorial for a new phone app
- [UI toolkit](ui-toolkit.md): `Typography`, `UiInteract`, `Squircle`, `Metrics`, and friends used throughout the games
- [State and persistence](state-and-persistence.md): how `Configuration` loads, saves, and what belongs in it
- [Localization](localization.md): adding `Title` and `Genre` strings to L.cs and the nine JSONs
- [Testing and release](testing-and-release.md): the test project that hosts the chess and sudoku suites
