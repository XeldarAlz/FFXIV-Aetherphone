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
| Apps/Games/Framework/StageBackdrop.cs | The `Strip` and `Arena` presets, the night felt and the felt lamp pool |
| Apps/Casino/Stage/StageText.cs, StageContrast.cs | Stage text with enforced minimum sizes and contrast, and the WCAG contrast math behind it |
| Apps/Casino/Stage/FeltTable.cs, FeltTableGeometry.cs | The stage as a card table: night cloth, rail, printed arc, betting circles and the seat geometry |
| Apps/Casino/Stage/SeatSpot.cs | Large tappable Sit spots and the occupied seat puck |
| Apps/Casino/Stage/DeckActions.cs | The bet deck action row: one 56 unit primary and up to two secondary pills |

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
| `Felt` | Night felt on every casino stage (`CasinoStage` calls `SetFeltStyle(FeltStyle.Night, spec.Rail)`): a desaturated emerald cloth whose lamp pool peaks at 0.22 luminance and falls to 0.08 at the edges, a fine cloth weave and a wooden rail along the stage edges (`CasinoStageSpec.Rail`, on by default). The Games app keeps the classic felt | Blackjack |

Every wash on every backdrop stays at or under 0.22 luminance (rec. 601 on the drawn colour): `StagePolishTests` stacks the Strip haze bands and blobs over the skyline glow, and all four Arena cones where they cross, against that ceiling. Point lights (windows, bokeh, phone lights, dust) are lights, not fills, and are not counted.

## Full screen and legibility (standard 15b)

Binding for every cabinet, table and room:

1. The game is the screen. The world (felt, track, reels, board, wheel) fills the stage edge to edge under the glass chrome. No boxed play area, no inset panel, no `GameScene.Arena`. Felt tables are the backdrop itself, with seats sitting on it.
2. Night, never bright. Backdrops stay at or under 0.22 luminance; no bright saturated full-screen fills. Text sits on a dark backdrop or on glass.
3. Readable text. Body copy at least `Subheadline`, status lines at least `Footnote` in strong ink (`StageText.Strong`), amounts at least `Title3`, the primary state line ("Place your bets", "Your turn") at least `Title2` with a soft shadow. Muted ink only on glass or dark felt and only for secondary labels. Contrast at least 4.5:1 against the brightest point behind the text.
4. One obvious next action: one full-width primary in the bet deck, secondary actions as smaller pills beside it. Empty seats are large Sit spots, not small "Open" circles.
5. Use the space: worlds grow to fill Safe, nothing smaller than a 44 unit touch target.
6. Clean entry: one tap from the Floor into any game, Back returns to where you came from, rules one tap away on the info chip.
7. Every idle state has motion and every state change has feedback.

### Stage text

`StageText` (Apps/Casino/Stage) is the one way to put words on the world. Each helper resolves the requested style up to its role minimum (`StageText.Minimum(role)`, never shrinking below it) and either fits or marquees against its container:

- `State(drawList, center, text, maxWidth, id)`: Title2 or larger in strong ink with a soft shadow; overflow moves onto a glass capsule and marquees. `StateLine(drawList, center, text, maxWidth, ink)` is the id-free form that ellipsizes.
- `Status(drawList, center, text, maxWidth, id, overWorld)`: Footnote or larger in strong ink, on a dark glass capsule when it sits over the world. `Status(drawList, center, text, maxWidth, scale)` and `Plate(..., ink, style, scale)` are the id-free capsule forms.
- `Amount(drawList, center, text, maxWidth, id)`: Title3 or larger in gold.
- `Label(drawList, center, text, maxWidth, id, muted)`: Footnote or larger; a muted label always sits on the capsule.
- `FitScale(text, maxWidth, style, role)`: the shrink-to-fit scale clamped at the role minimum.

`StageText.CapsuleFill` is dark enough that muted ink reads at 4.5:1 even over the brightest felt; `StageContrast.Ratio` and `Over` are the WCAG math the tests use.

### Felt tables, seats and the action row

`FeltTable.DrawCloth(drawList, full, rail, practice, scale)` paints the night cloth over any rect (a grey cloth for practice tables) for scenes that are not on a Felt stage, such as idle previews and Hold'em's own felt. A `FeltTable` instance draws the table layer over a Felt stage with `Draw(drawList, full, table, new FeltTableOptions(seats, print, cloth, rail, circles, practice), scale)`: a gold insurance arc, betting circles and an optional printed line such as "BLACKJACK PAYS 3 TO 2" set along an arc in low-contrast gold, and returns `FeltTableGeometry` (dealer anchor at the top centre, the seat arc, `Seat(index)`, `BettingCircle(index)`), all scaled to the table rect with seats of at least 28 units radius.

`SeatSpot.DrawEmpty(drawList, center, radius, label, accent, invite, scale)` is the empty seat: a dark disc of at least 56 units across with a plus glyph and a "Sit" label, pulsing a glow while `invite` is set (you are not seated), returning true on a tap. `SeatSpot.DrawOccupied` draws the occupied ring and returns a `SeatPuck` whose `Bounds` is the avatar slot.

`DeckActions.Row(deck, scale)` is the bottom row of the bet deck, 56 units tall and full width. `stage.SecondaryAction(label, enabled, ink)` lays out up to two pills from the left and `stage.PrimaryAction(label, enabled, ink)` takes the rest, the whole row when there are none. `BetComposer` uses the same row (the Manual and Auto switch and the auto gear are its secondary slots), `DeckActions.Above` places a status line over the row, and `DeckActions.Slice` splits it for two equal choices such as Insure and No insurance.

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
- `CasinoNotice.Draw` and `DrawWithAction`: the Reason, Info and Card notice shapes (Card bodies at Subheadline, the rest at Footnote).
- `ChipStack` (Windows/Components/Layout): ten denominations from 100 to 100M with fixed colours, five discs a column, notched edges, a practice variant, and compact amounts.
- `NumberText.Compact` reads K, M, B and T without ever rounding a balance up; `NumberText.Signed` caches the plus form.

## Reference cabinets

`Cabinets/ScratchCabinet.cs` is the solo reference (fixed-price knob, Auto as buy five, Instant, bets rail) and `Cabinets/WheelCabinet.cs` the room reference (phase ribbon, podiums, ceiling-capped stakes). `Cabinets/BarkeepCabinet.cs` is the skill cabinet: a full-bleed bar scene (`BarkeepSceneArt`), patrons choreographed by `BarkeepBarFlow` (seeded from the round id, snapped on a mid-shift join), a cosmetic combo and fever meter (`BarkeepTipMeter`), and practice reachable without chips. Apps/Casino/Machines holds the three slot machines (Golden Bird Deluxe, Crystal Cascade, Moogle Money) on one shell, `MachineCabinet`: the chassis, top glass and reel window fill the stage, `MachineRoundPlayback` replays the server's `steps[]` beat by beat (spin, tumble, expand, hold, respin, collect, meter), `MachineRollup` counts wins at half the bet a second up to 20x and compresses the rest, and the celebration fires when the rollup lands.

## Bingo hall

`Cabinets/BingoCabinet.cs` is a non-scrolling room on the Arena backdrop, laid out by `BingoHallLayout` (tumbler and caller, 75-cell call board, hero card with a swipeable rail of the other cards, three prize podiums). `BingoTumbler` is a decorative `PhysicsWorld` drum of 20 balls kept aloft by a seeded blower; it decides nothing, and the called ball that pops out and flies to the board is always the server's latest call. `BingoRoundPlayback` turns the room state into choreography and cues (`BallPopped`, `BallLanded`, `Daubed`, `OneAway`, `StageWon`): a single new ball flies for `FlightSeconds` before the board lights it and auto-daub stamps it, while a first read or a burst of several balls (a mid-game join or a reconnect) jumps straight to the called balls with no flights and no sounds. Manual daub (the info sheet's Extra button) only stops the auto stamping; the prize is the server's either way, and the Result stamps every called number. A stage counts as the player's when one of their cards reached it on the stage's awarded ball (`BingoRules.CallReaching`); the payout and the celebration tier always come from the settled cards, with the Epic tier forced for the player who took an early-bird full house. Cards are bought through the bet deck knob (`BetComposer` with `FixedAmount` and `Knob`).

## Daily spin

`Cabinets/DailySpinCabinet.cs` runs on the Strip backdrop with no bet deck: the neon FREE SPIN sign, two crossing spotlights, a bulb rim, and a sprung pointer (`DailySpinPlayback`) that kicks on every peg and settles critically damped. The top wedge lands with the Epic tier and every other wedge with the Win tier through `SpinFlourish`, which mirrors `WinCelebration` with the coin glyph because the spin pays coins, not chips. `DailySpinIdle` is the turning idle wheel shared by `DrawIdle` and the home widget.

## Venue layer

Hosted venue rooms (`casino.dice-table`, `casino.deathroll`, `casino.raffle`) open from the table browser, the door or a deep link into `CasinoScreen.VenueRoom`, drawn by `Apps/Casino/Venue/VenueCabinet.cs` on the Strip with a room ribbon. Each room has a Dalamud-free playback (`DiceTablePlayback`, `DeathrollPlayback`, `RafflePlayback`) that takes the room's GameState and choreographs only what the server returned; a mid-event join snaps to the current state. `CasinoVenueStore` parses the GameState off the draw path (`VenueRoomView`), sends `POST /casino/venue/{roomId}/act` with an idempotent `clientActionId`, verifies the last roll or draw through `GET /casino/rooms/{roomId}/verify/{seq}` (`VenueVerifier` over `VenueDraws`, pinned by `venue.json`), polls `GET /casino/tables/nearby` from the housing position (`IHousingPositionSource`), and indexes listed tables by venue address for the Venues app pill.

The stage Extra button on blackjack tables and venue rooms opens `VenueTableSheet`: verify, trade sync settings (gil tables), the broadcast view for spectators and the report action (`Plugin.Report`, target `casino_table`).

Trade sync is opt-in (`Configuration.CasinoTradeSync`). `TradeWindowReader` listens to the `Trade` addon lifecycle and only reads its text; `TradeSyncTracker` confirms a completed trade from the wallet delta, and `TradeLedgerMatcher` turns it into a buy-in or payout proposal or a confirmation of an existing ledger entry. The host always gets a one-tap prompt (`TradeSyncPrompt`) plus a notification; a player can let their own side confirm automatically.

`BroadcastView` (`CasinoScreen.Broadcast`, landscape) projects the blackjack or Hold'em snapshot into `BroadcastTable` with large seats, stacks and cards and no controls. `TournamentOverlay` draws the practice blackjack tournament strip, leaderboard, eliminations and winner over the table; hosts start and stop it from `TournamentDoorCard` on the door.
