# Building a game

This tutorial builds Tap, a 15-second game where you tap the lit cell as fast as you can, and wires it into the Games app end to end: the class, the strings, registration, the release date, the color and icon, the best card, ranking, the daily rotation, tests, and the README and changelog. Two short variants then turn it into a level pack and a pass-the-phone game. Read [Games framework](games-framework.md) first for the concepts, and build and load the plugin as [Getting started](getting-started.md) describes.

## Key files

| Path | Why you open it |
| --- | --- |
| src/Aetherphone/Apps/Games/Tap/TapApp.cs | The new game |
| src/Aetherphone/Apps/Games/GamesApp.cs | The list of games, in the constructor |
| src/Aetherphone/Core/Localization/L.cs | The title and the one-line hook |
| src/Aetherphone/Localization/ | The same keys in the nine language files |
| src/Aetherphone/Apps/Games/GamesLibrary.cs | The release date and the best card |
| src/Aetherphone/Core/Apps/AppAccents.cs | The accent color |
| tools/icon-generator/generate-painted-icons.mjs | The painted icon |
| src/Aetherphone/Core/Games/ScoreStatIds.cs | The ranked stat |
| src/Aetherphone.Tests/ScoresWireContractTests.cs | The pinned copy of the ranked stat list |
| src/Aetherphone/Core/Coins/CoinGames.cs | The daily rotation |
| src/Aetherphone/Core/Changelog/ChangelogData.cs | The release's changelog sections (the line itself is a string in L.cs) |
| README.md | The game count on the Games line |

## 1. Choose the spec

Tap is a four by five grid with one lit cell. Tapping it scores, and quick taps in a row raise a combo multiplier. The round runs on a 15-second clock, so the spec is clocked (losing focus pauses it) and has a countdown. It scores higher-is-better points, so its kind is Score, and it sits on the Arcade shelf over the Nebula backdrop.

When you plan your own game, pick Landscape only when the world is wide (Doom, Moogle War) and the Compact HUD only when the board is tall. Name the game for what it does, as Whack, Snake and Water Sort are, and never use the word Aether in a game's name.

## 2. Write the class

Create src/Aetherphone/Apps/Games/Tap/TapApp.cs. The namespace must equal the folder path, which CI checks (see [Conventions](conventions.md)). Real games keep their rules in a board class with no ImGui calls, so tests can drive it, and their drawing in a renderer class; Tap is small enough for one file. The file starts with the spec:

```csharp
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;
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
    private const float UrgentSeconds = 5f;
    private const int Columns = 4;
    private const int Rows = 5;
    private const int CellCount = Columns * Rows;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Tap, GameGenre.Arcade, L.Tap.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true);
```

The game never draws a backdrop, a score pill, an intro or a result card; the host does. Next come the game's state and `Start`:

```csharp
    private readonly ParticleSystem particles = new();
    private readonly FeedbackFx fx = new();
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private int litCell;
    private int score;
    private int bestCombo;
    private float timeLeft;
    private float entrance;
    private bool finished;

    public GameSpec Spec => StageSpec;

    public Vector4 Accent => AppAccents.For(GameId);

    public void Start(in GameStart start)
    {
        random = start.Random;
        score = 0;
        bestCombo = 0;
        timeLeft = RoundSeconds;
        entrance = 0f;
        finished = false;
        combo.Reset();
        particles.Clear();
        fx.Clear();
        litCell = random.Next(CellCount);
    }
```

Tap declares its own `Accent`, the way WhackApp.cs does, because the interface's default is invisible inside the class ([Games framework](games-framework.md) explains why). The host calls `Start` for every run, including Restart and Play again, so it resets everything. It draws all randomness from `start.Random`, which is how the daily challenge hands everyone the same board.

```csharp
    public void Close()
    {
        particles.Clear();
        fx.Clear();
    }

    public void Dispose()
    {
    }

    public void OnQuit(GameSession session)
    {
        if (finished || score <= 0)
        {
            return;
        }

        Finish(session);
    }
```

When the player quits mid-round, the host settles the live score anyway, because Tap is a Score game. `OnQuit` lets a game record its own outcome first, as Bubbles and Lander do. Tap uses it only to show the pattern. It matters when the outcome carries more than the bare score (a secondary stat), and for a puzzle whose winning move is still animating, which the host never settles (Mahjong). The host calls `OnQuit` on every exit, even after the round ended, hence the check. The frame comes next:

```csharp
    public void Draw(in GameContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var simulationSeconds = fx.ScaleDelta(context.DeltaSeconds);
        particles.Update(context.RawDeltaSeconds);
        fx.Update(context.RawDeltaSeconds);
        combo.Update(simulationSeconds);
        entrance = GameJuice.Advance(entrance, context.RawDeltaSeconds);
        var grid = GameGrid.Centered(context.Safe, Columns, Rows, 0.12f);
        DrawBoard(drawList, grid, fx.ShakeOffset(scale), scale, context.Backdrop.Ink);
        if (!finished)
        {
            Step(grid, simulationSeconds, scale, context);
        }

        particles.Draw(drawList, scale);
        fx.DrawRings(drawList, scale);
        fx.DrawText();
        context.Hud.Score(score);
        context.Hud.Timer(timeLeft, RoundSeconds, timeLeft <= UrgentSeconds);
        context.Hud.Combo(combo);
        context.Hud.Best(context.Session.Best);
        context.Session.Report(score);
    }
```

`ScaleDelta` runs once with the session's delta, and only the simulation uses its result, so a hit-stop freezes the round while particles and shake keep moving on the raw delta. The board fits inside `Safe`, below the HUD. Every frame fills the HUD model and reports the live score. Then comes the round itself:

```csharp
    private void Step(in GameGrid grid, float deltaSeconds, float scale, in GameContext context)
    {
        timeLeft = MathF.Max(0f, timeLeft - deltaSeconds);
        if (timeLeft <= 0f)
        {
            Finish(context.Session);
            return;
        }

        var lit = grid.Cell(litCell % Columns, litCell / Columns);
        if (context.Session.State != StageFlow.Playing || !ImGui.IsMouseClicked(ImGuiMouseButton.Left) ||
            !UiInteract.Hover(lit.Min, lit.Max))
        {
            return;
        }

        score += combo.Hit();
        bestCombo = Math.Max(bestCombo, combo.Count);
        litCell = random.Next(CellCount);
        UiFeedback.Play(UiSound.GameHitSoft);
        particles.Burst(lit.Center, 10, Accent, 160f * scale, 3f, 0.5f);
        fx.Shockwave(lit.Center, 40f * scale, Accent, 0.35f);
        fx.AddTrauma(0.06f);
        fx.HitStop(0.03f);
        if (combo.Multiplier >= 3)
        {
            context.Fx.Punch(0.04f);
        }
    }
```

`Draw` also runs during the countdown, the pause menu and the result card, so a tap counts only while the session is playing. Tap reads the press on mouse down, as Whack does: `UiInteract.Click` fires on release and plays the phone's own tap sound, which suits buttons but not a reflex game. The last two methods draw the board and end the run:

```csharp
    private void DrawBoard(ImDrawListPtr drawList, in GameGrid grid, Vector2 shake, float scale, StageInk ink)
    {
        var plate = BoardPlate.Around(grid.Bounds, scale).Translate(shake);
        BoardPlate.Draw(drawList, plate, BoardPlate.Radius * scale, scale, Accent, ink);
        for (var cell = 0; cell < CellCount; cell++)
        {
            var lift = StageCell.Lift(GameJuice.Stagger(entrance, cell, CellCount)) * scale;
            var rect = grid.Cell(cell % Columns, cell / Columns).Translate(shake - new Vector2(0f, lift));
            var fill = cell == litCell ? Accent : GamePalette.Cell;
            StageCell.Draw(drawList, rect, fill, CellDepth.Raised, 8f * scale, scale);
        }
    }

    private void Finish(GameSession session)
    {
        finished = true;
        session.Finish(new GameOutcome(score, ScoreKind.Score, GameId)
            .WithStat(L.Games.Combo, GameNumber.Label(bestCombo)));
    }
}
```

The cells rise in one after another on the entrance value. `Finish` hands the session one outcome, with up to four stat lines for the result card.

## 3. Add the strings

The title goes in the `Games` class of L.cs, next to the other game names, and the hook in a class of its own:

```csharp
public static readonly LocString Tap = new("games.tap", "Tap");
```

```csharp
internal static class Tap
{
    public static readonly LocString Hook = new("games.tap.hook", "Tap the lit cell as fast as you can. Quick taps build a combo.");
}
```

Add both keys to all nine files under src/Aetherphone/Localization in the same commit. [Localization](localization.md) explains the files and what to write in a language you do not speak. A game with many strings may keep its title in its own class too, as Crates does.

## 4. Register it

Add the game to the end of the array in the `GamesApp` constructor, with `using Aetherphone.Apps.Games.Tap;` at the top of GamesApp.cs:

```csharp
            new TempoApp(),
            new TapApp(),
        };
```

Two more edits in GamesLibrary.cs finish the hub side. Add a row with the release date to the `Releases` table, which drives Just Added and the New label:

```csharp
        new("tap", 2026, 10, 20),
```

Then add `case "tap":` to the group of Score games in `BestRecord`, which builds the best card on Profile and the progress line under Continue Playing.

## 5. Give it a color and an icon

Add the accent to the table in AppAccents.cs, picking one of the ring colors that [Accent colors](design-accents.md) describes:

```csharp
        ["tap"] = AccentRing.Orange,
```

The icon is a painted pair of images. Add an entry with the same hue to the `map` in tools/icon-generator/generate-painted-icons.mjs, then run `node generate-painted-icons.mjs tap` in that folder (after `npm install`) to write src/Aetherphone/Icons/tap.png and tap.fg.png. The tool's README covers the glyph choice and the contact sheet:

```js
  tap: icon("hand-tap", "colour", "Orange"),
```

Instead of a painted icon, a `case` in src/Aetherphone/Windows/Components/Chrome/AppIconArt.cs can draw a vector glyph. With neither, the tile is blank in the accent color. Artists who want to paint a game's icon by hand start at the [Art guide](art-guide.md).

## 6. Rank it and add it to the daily rotation

To rank Tap, append its stat to `ScoreStatIds.Catalog`, using the helper that matches how it compares: `Score`, `Level` and `Streak` for higher-is-better values, `Time` for a Time or Count game. Then append `"tap"` to `ServerPluginStatIds` in ScoresWireContractTests.cs, the test's copy of the list:

```csharp
        Score("lander"), Time("minigolf"), Score("tap"),
```

To let Tap be the daily challenge when the phone has no coin wallet to ask, append `"tap"` to the end of `CoinGames.All`. A ranked or coin game also needs a matching entry on the server, so raise it with the maintainers in your pull request.

## 7. Test it and run it

Keep a real game's rules in a board class and give its tests a `SameSeedReplaysIdentically` case that plays the same seed twice and compares the results; src/Aetherphone.Tests/StackBoardTests.cs shows the shape. Run the suite as [Testing and release](testing-and-release.md) describes. Then build Debug, load the dev plugin, open Games, and find Tap at the top of Just Added and of the Library's Newest sort.

## 8. Ship it

When the game ships, update README.md: its Games line counts the games in words. Add a line to the Games strings of the release in progress (such as `L.Changelog.Release1310Games` in L.cs, plus the nine language files), and a Games section to its entry in ChangelogData.cs if it has none; [Testing and release](testing-and-release.md) covers the changelog's strings and keys. Commit with a scope, such as `feat(games): add Tap, a quick reflex grid`.

## Variant: a level pack

To give Tap 30 levels, each with a higher target, change the spec to the Level kind with a level count, and add a target:

```csharp
    private const int LevelCount = 30;
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Tap, GameGenre.Arcade, L.Tap.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Level, clocked: true, countdown: true, levelCount: LevelCount);
    private int target;
```

In `Start`, set `target = 8 + start.Level * 2;`. In `Step`, end the round when `timeLeft <= 0f || score >= target`. Remove `OnQuit`, because a half-played level has no honest result. Then finish with stars:

```csharp
    private void Finish(GameSession session)
    {
        finished = true;
        session.Finish(new GameOutcome(score, ScoreKind.Level, GameId, score >= target).WithStars(Stars())
            .WithStat(L.Games.Combo, GameNumber.Label(bestCombo)));
    }

    private int Stars()
    {
        if (score < target)
        {
            return 0;
        }

        if (timeLeft >= RoundSeconds / 2f)
        {
            return 3;
        }

        return timeLeft >= RoundSeconds / 4f ? 2 : 1;
    }
```

The kit supplies the level select, unlocking, Next level and Retry, and the star total on the best card, so `BestRecord` needs no case. The pack ranks by its total stars, under a `Level("tap")` catalog row. Real packs keep their levels in a data class, as CratesLevels.cs does.

## Variant: a hot-seat game

To let up to six players take turns on one phone, add seats to the spec and a score per seat:

```csharp
    private static readonly GameSpec StageSpec = new(GameId, L.Games.Tap, GameGenre.Arcade, L.Tap.Hook,
        Backdrop.Nebula, HudStyle.Standard, ScoreKind.Score, clocked: true, countdown: true, seats: GameSeats.Max);
    private readonly int[] seatScores = new int[GameSeats.Max];
    private int seat;
    private int seatCount;
```

In `Start`, set `seat = 0;` and `seatCount = start.Seats;`. In `Step`, when the clock runs out and `seatCount` is above one, call `EndTurn(context.Session)` instead of `Finish`:

```csharp
    private void EndTurn(GameSession session)
    {
        seatScores[seat] = score;
        seat++;
        if (seat < seatCount)
        {
            score = 0;
            timeLeft = RoundSeconds;
            combo.Reset();
            session.Handoff(seat);
            return;
        }

        finished = true;
        session.Finish(GameOutcome.Unranked().WithWinner(Leader()));
    }
```

`Leader` returns the seat with the best score:

```csharp
    private int Leader()
    {
        var leader = 0;
        for (var index = 1; index < seatCount; index++)
        {
            if (seatScores[index] > seatScores[leader])
            {
                leader = index;
            }
        }

        return leader;
    }
```

The host shows the Players strip on the intro and the pass-the-phone cover between turns. A run with one player still ranks; a shared run never does.

## Gotchas

- **The offline daily comes from `CoinGames.FeaturedFor`.** Without a wallet (signed out, or before it loads) the hub walks `CoinGames.All` by day, so a game missing from that list is never the daily offline. Append to the list and never reorder it, or every future day changes its game.
- **The stat list is pinned in order.** ScoresWireContractTests compares `ScoreStatIds.All` with its own copy item by item, so a new catalog row fails the tests until the copy has it in the same place.
- **No `Releases` row means never new.** The game sorts last in Newest, never joins Just Added, and never reads New.
- **No `BestRecord` case means no best card**, unless the game is a Level-kind pack, whose star total shows on its own.
- **A level outcome needs stars, even zero.** In a level pack, a Level-kind outcome without `WithStars` records the run's raw value as the pack's best and uploads it to the star board. Finish a failed level with `won: false` and `WithStars(0)`.
- **A side mode must not fold onto the main board.** A daily, endless or practice mode without its own catalog row folds onto the game's root, so its runs land on the main board. Give it its own row, or mark it in the spec's `unrankedModes`.
- **Report every frame.** Leaving a Score run settles the last reported value, so a game that reports only at the end records nothing when the player quits.
- **Never rename an id.** It keys saved records, the accent, the icon, the release date and the ranked stat, which is why some ids differ from their titles (`match3` is Gem Swap).

## Related docs

- [Games framework](games-framework.md): how the hub, the stage kit, scoring and the leaderboard work.
- [Creating your own app](creating-an-app.md): the same walk-through for a phone app.
- [Localization](localization.md): strings, the nine language files, and the copy rules.
- [Accent colors](design-accents.md): how app and game colors are chosen.
- [Art guide](art-guide.md): painting icons and other art without writing code.
- [Testing and release](testing-and-release.md): running the tests, CI, and the changelog.
