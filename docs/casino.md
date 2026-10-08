# Gamba stage

Every play screen in Gamba (the casino app, id `casino`) draws inside one host frame, `CasinoStage`. The lobby, cashier, history, fairness and limits pages keep the normal app chrome; cabinets, tables and rooms go full bleed with no `AppHeader`. This page describes the stage kit as it ships. Everything lives in src/Aetherphone/Apps/Casino/Stage (namespace `Aetherphone.Apps.Casino.Stage`) unless noted.

## Key files

| Path | Role |
| --- | --- |
| Apps/Casino/Stage/CasinoStage.cs | The host frame: backdrop, chrome band, chips capsule, info chip, deck surface, bets rail, reality check, celebration, snap to truth |
| Apps/Casino/Stage/CasinoStageLayout.cs | Pure geometry of the frame (band, chips, ribbon, practice ribbon, deck, Safe) |
| Apps/Casino/Stage/CasinoStageSpec.cs | `CasinoStageSpec`, `CasinoStageFrame`, `CasinoStageAction`, `CasinoInfoRequest` |
| Apps/Casino/Stage/WinCelebration.cs, WinLadder.cs | The six-tier win ladder |
| Apps/Casino/Stage/CasinoLights.cs, CasinoColors.cs | Bulb chase, neon tube, spotlight, bokeh, coin shower and the Gamba colour tokens |
| Apps/Casino/CasinoSigns.cs | Stroked neon lettering for every game sign |
| Apps/Casino/BetComposer.cs | The v2 bet composer (amount, half, double, Max, knob slot, Manual and Auto) |
| Apps/Casino/Stage/AutoBetPlan.cs, AutoBetSheet.cs, RealityCheck.cs | Auto play state, its settings sheet, and the 100 round or 30 minute check-in |
| Core/Casino/CasinoLadder.cs | The global bet ladder, `LevelCap`, `MaxBet` and the ceiling read |
| Apps/Games/Framework/StageBackdrop.cs | The `Strip` and `Arena` presets and the felt lamp pool |

## Frame

The host (`CasinoApp.DrawStage`) opens `AppSurface.BeginEdgeToEdge(content, true)` and then, per frame:

```csharp
var frame = stage.Begin(area, spec, balance, casino.Ceiling);
cabinet.Draw(stage, frame, ui);
var action = stage.End(ui);
```

`Begin` draws the backdrop and the frosted deck, advances effects, and returns a `CasinoStageFrame`: `Layout`, `Safe`, `Deck`, `Body`, `Full`, `DeltaSeconds`, `Phase` (the lights clock), `Instant`, `Focused`, `Blocked` (a reality check is showing) and `SnapToTruth`. `End` draws particles, the celebration, screen effects, the practice ribbon, the reality-check card, the bets handle and the chrome, and returns `CasinoStageAction.Back` or `Cashier` for the host to act on. Overlays draw at window level through `stage.Gate()` and `stage.DrawOverlays(screen, ui)`; `TakeInfoRequest()` returns `Rules`, `Extra` or `Fairness`, and `TakeRoundRequest()` a round id tapped in the bets rail.

`CasinoStageSpec(GameId, Title, Preset, Room, DeckHeight, Practice, BetsRail, InstantAvailable, ReturnTenths, Extra, Warmth, LampPool)` declares the screen: `Room` adds the 40 unit phase ribbon, `DeckHeight` reserves the bet deck (0 for layouts that draw their own controls), `Practice` swaps the capsule to grey Practice chips and adds the practice ribbon, `ReturnTenths` prints the return in the info sheet, `Extra` names a second info button (a pay table or odds sheet), `Warmth` tints the Strip toward amber, `LampPool` lights the felt.

Geometry, in design units times `UiScale.Current`: chrome band 52; back chip and info chip 36 glass circles centred 28 in from either side at y 26; the chips capsule centred on the band; ribbon 40 and practice ribbon 22 under the band; deck at the bottom (118 by default, `BetComposer.DeckHeightFor(knob, fixedAmount)` for more); `Safe` sits between them, inset 12. Worlds fit inside `Safe`; lights and backdrops use `Full`. `CasinoStageLayout.ChromeContains(point)` tells a cabinet whether a press landed on the chrome.

Snap to truth: when the stage was not drawn for more than 2 seconds, or the phone regains focus after more than 2 seconds away, `frame.SnapToTruth` is true for one frame, the balance and the celebration snap to their final state, and cabinets finish their own animations (Scratch reveals the card, the wheel jumps to the landed segment).

`stage.RepeatPressed()` reports Space while the stage has focus, no overlay is open and no text field is active, claiming only the Space key through `GameInput`.

## Backdrops

| Preset | Look | For |
| --- | --- | --- |
| `Strip` | Indigo night, a skyline with 40 twinkling windows, rose and cyan neon haze drifting opposite ways, rising bokeh; `SetWarmth(1)` shifts it amber | Lobby previews, slots, scratch, wheel, daily spin, barkeep |
| `Arena` | Stadium night, two floodlight towers with four sweeping cones, a crowd band with 60 flickering phone lights, track dust | Bingo hall, the race, liftoff |
| `Felt` | The existing felt with `SetLampPool(strength)` for a warm pool over the table | Blackjack and poker |

## Lights, signs and colours

`CasinoLights` is static and allocation free: `BulbChase(drawList, rect, radius, scale, phase, spacing, colorA, colorB, lit)` (6 unit bulbs on a 14 unit pitch, every third dark, chasing at 6 bulbs a second), `NeonTube(drawList, path, color, width, glow)`, `Spotlight(drawList, origin, direction, length, spread, color, alpha)`, `Bokeh(drawList, rect, phase, density, scale)`, `CoinShower(scale)` and `CoinShowerEmitter(scale, rate)` (gold discs, gravity 180 units, a pulse size curve), `Sparkle`, `Shard`, `Ring` particle specs, and `LightSweep(backdrop, strength)`. `CasinoSigns.Draw(drawList, CasinoSign, center, height, color, lit)` strokes a sign in neon (`HeightToFit` sizes it to a box); a new game adds its word to the `CasinoSign` enum and the word table. `CasinoColors` holds `Money`, `MoneyHighlight`, `LightA` (rose), `LightB` (cyan), the felt pair, the warm inks, `Practice` and `Loss`. `AppPalettes.Gamba` is the app palette; the casino accent is `AccentRing.Rose`.

## Win celebrations

`stage.Celebration.Celebrate(stake, payout, origin, instant, jackpot)` picks a tier from the net win over the stake (`WinLadder.TierFor`): Win above 0, Nice from 3x, Big from 10x, Mega from 25x, Epic from 50x, Legendary from 100x or any jackpot. A payout at or below the stake returns `WinTier.None` and does nothing. Each tier carries its sparkles or confetti, coin shower seconds, light sweep, banner, bulb chase, full-screen card, sound and count-up seconds (`WinLadder.Spec`). Big and up flash, punch, vignette or slow the game as the standard table says. A tap on Safe skips after half a second; Instant mode shows the final amount with a 0.3 second pop. `Blocking` is true while an Epic or Legendary card covers the stage.

`CasinoSfx.Play(sound)`, `Pitched(sound, step)` and `Win(spec)` play only while the stage is drawn and focused, and WinBig, WinEpic and Fanfare share one 10 second slot (a throttled big win falls back to WinSmall). The casino cues (`UiSound.ReelTick` through `LevelUp`) are synthesized by tools/sound-generator (`python generate-sounds.py --casino`).

## Bet composer and the ceiling

`BetComposer.Draw(ui, frame.Deck, model, delta)` returns `Confirm`, `StartAuto`, `StopAuto` or `None`. `BetComposerModel(MinimumBet, MaximumBet, Stack, Action, Enabled, AutoAvailable, FixedAmount, Knob, Repeat, Busy)`: `MaximumBet` is the game's own cap clamped to `casino.Ceiling.MaxBet`; `Action` is a one-argument template ("Buy {0}", "Bet {0}") that receives the compact amount; `Knob` reserves `composer.KnobRect` for the game's own control; `FixedAmount` hides the amount row. The field taps into free input and commits on Enter or blur; every value snaps to the ladder through `CasinoLadder.Clamp`, `Half`, `Double` and `Top`. The Auto tab drives `composer.Auto` (an `AutoBetPlan`): the cabinet starts the next round with `Auto.Next` while `Auto.Running`, and reports each settled round with `Auto.Settle(stake, payout, bonus, min, max, stack)`, which applies on-win and on-loss adjustments and stops on count, profit, loss, bonus or chips. The gear opens the auto settings sheet (`composer.Gate()` and `composer.DrawOverlay(screen, ui, bonusAvailable)`).

`CasinoLadder` mirrors standard 7.3: 27 rungs from 100 to 50B, `LevelCap(level)` geometric between the anchors (L1 10K up to L100 50B), `MaxBet(level, balance) = max(LevelCap, balance / 20)` floored to a rung. `CasinoStore.Ceiling` returns the server's `CasinoCeilingDto` from `GET /casino` when present and falls back to the local formula (level from `Progress`, anchors from `LevelCapAnchors`) against an older floor. The info sheet prints the ceiling and its reason.

Every settled round goes through `stage.Settle(new CasinoBetRecord(game, stake, payout, roundId, settledAtUnixMs))`, which feeds the bets rail's My bets tab and the reality check (`RealityCheck`: a card every 100 rounds or 30 minutes of play showing rounds, minutes and session net, with Keep playing or Take a break).

## Other pieces

- `CabinetPreview` plus `ICabinetIdle` (`IdleBackdrop`, `DrawIdle(drawList, rect, deltaSeconds)`): the live idle container for lobby tiles.
- `PhaseRibbon.Draw(drawList, rect, label, remainingMs, windowSeconds, crowd, accent, scale)`: the room ribbon with a `TurnTimerRing`.
- `StatusTitle.For(balance)` and `Draw`: Shark 1M, High Roller 10M, VIP 100M, Whale 1B, Legend 1T.
- `LevelCapsule.Draw(drawList, rect, level, progress, cap, accent, scale)`: level ring, XP bar and cap.
- `CasinoNotice.Draw` and `DrawWithAction`: the Reason, Info and Card notice shapes.
- `ChipStack` (Windows/Components/Layout): ten denominations from 100 to 100M with fixed colours, five discs a column, notched edges, a practice variant, and compact amounts.
- `NumberText.Compact` reads K, M, B and T without ever rounding a balance up; `NumberText.Signed` caches the plus form.

## Reference cabinets

`Cabinets/ScratchCabinet.cs` is the solo reference (fixed-price knob, Auto as buy five, Instant, bets rail) and `Cabinets/WheelCabinet.cs` the room reference (phase ribbon, podiums, ceiling-capped stakes). `Cabinets/BarkeepCabinet.cs` is the skill cabinet: a full-bleed bar scene (`BarkeepSceneArt`), patrons choreographed by `BarkeepBarFlow` (seeded from the round id, snapped on a mid-shift join), a cosmetic combo and fever meter (`BarkeepTipMeter`), and practice reachable without chips. Slots and blackjack still run their previous layouts inside the stage body and use `ClassicBetComposer` where they had a composer.

## Bingo hall

`Cabinets/BingoCabinet.cs` is a non-scrolling room on the Arena backdrop, laid out by `BingoHallLayout` (tumbler and caller, 75-cell call board, hero card with a swipeable rail of the other cards, three prize podiums). `BingoTumbler` is a decorative `PhysicsWorld` drum of 20 balls kept aloft by a seeded blower; it decides nothing, and the called ball that pops out and flies to the board is always the server's latest call. `BingoRoundPlayback` turns the room state into choreography and cues (`BallPopped`, `BallLanded`, `Daubed`, `OneAway`, `StageWon`): a single new ball flies for `FlightSeconds` before the board lights it and auto-daub stamps it, while a first read or a burst of several balls (a mid-game join or a reconnect) jumps straight to the called balls with no flights and no sounds. Manual daub (the info sheet's Extra button) only stops the auto stamping; the prize is the server's either way, and the Result stamps every called number. A stage counts as the player's when one of their cards reached it on the stage's awarded ball (`BingoRules.CallReaching`); the payout and the celebration tier always come from the settled cards, with the Epic tier forced for the player who took an early-bird full house. Cards are bought through the bet deck knob (`BetComposer` with `FixedAmount` and `Knob`).

## Daily spin

`Cabinets/DailySpinCabinet.cs` runs on the Strip backdrop with no bet deck: the neon FREE SPIN sign, two crossing spotlights, a bulb rim, and a sprung pointer (`DailySpinPlayback`) that kicks on every peg and settles critically damped. The top wedge lands with the Epic tier and every other wedge with the Win tier through `SpinFlourish`, which mirrors `WinCelebration` with the coin glyph because the spin pays coins, not chips. `DailySpinIdle` is the turning idle wheel shared by `DrawIdle` and the home widget.
