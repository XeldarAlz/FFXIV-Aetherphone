# Gamba

Gamba is the casino app (app id `casino`, route prefix `/casino`, key prefix `casino.`). Coins become chips at the cashier, and those chips play every house game on one floor-wide bankroll. Hosted tables can also run on practice chips or, for venues, on gil banked by the host. This page describes the client as it ships: the economy it mirrors, the stores, the stage kit every play screen runs on, the cabinets and their playback, hosting and the venue layer, the lobby, and how to add a game. The HTTP routes and realtime room kinds are listed in [Networking](networking.md#casino-routes).

The server decides and the client performs. Every outcome is drawn by the server from a committed seed; the client only choreographs a result it has already been sent (reels decelerate onto it, the ball bounces along its path, the race script ends in its order) and never fabricates a near miss.

## Key files

Paths are under src/Aetherphone. Namespaces follow the folders (`Aetherphone.Core.Casino`, `Aetherphone.Apps.Casino.Stage`, and so on).

| Path | Role |
| --- | --- |
| Core/Aethernet/Clients/CasinoClient*.cs | Every casino route, split by area (`CasinoClient.Floor`, `.Machines`, `.Originals`, `.Plinko`) |
| Core/Aethernet/Contracts/Casino*Dtos.cs | Wire records (`CasinoDtos`, `CasinoFloorDtos`, `CasinoHoldemDtos`, `CasinoOriginalsDtos`, `CasinoPlinkoDtos`), all registered in `AethernetJsonContext` |
| Core/Casino/ | Stores, the room session, rules mirrors, the fairness verifier, the ladder, trade sync; Dalamud-free wherever it can be |
| Apps/Casino/CasinoApp*.cs | The app: router, tabs (Floor, Live, Tables, Cashier), the stage host and the records pages (History, Fairness, Limits) |
| Apps/Casino/Stage/ | The stage kit: frame, layout, celebration ladder, lights, stage text, felt, seats, bets rail, reality check |
| Apps/Casino/Cabinets/ | Scratch, Wheel, Bingo, Barkeep and the daily spin |
| Apps/Casino/Machines/ | The three slot machines on one shell |
| Apps/Casino/Originals/ | Mines, Dice, Limbo, Keno and Hi-Lo on one cabinet with a skin per game |
| Apps/Casino/Plinko/, Race/ | Plinko and the Chocobo race |
| Apps/Casino/Tables/ | Blackjack and Hold'em tables, the pits, the table browser, the host sheet, the door and the session ledger |
| Apps/Casino/Venue/ | Venue rooms (dice table, deathroll, raffle), the broadcast view, the tournament overlay, trade sync prompt |
| Apps/Casino/Strip/ | The Floor tab: hero, carousel, wins ticker, live board, missions, club, shelves, Hall of Fame |
| Apps/Casino/Cashier*.cs | The cashier drawer: buy-in, cash-out with the daily cap, bonus shelf, club card |
| Apps/Games/Framework/StageBackdrop.cs | The `Strip` and `Arena` presets, the night felt and its lamp pool |

## Economy on the client

The client mirrors the economy for display only; every amount that moves money is checked by the server again.

- **Rate.** `CasinoChipLots.ChipPerCoin` is 1,000 chips per coin, both ways. Buy-ins and cash-outs are whole coins.
- **Daily cash-out cap.** Coins converted at the cashier minus coins bought in may not exceed the day's allowance (500 coins by default, policy data). `GET /casino` sends a `cashier` block (`allowanceCoins`, `queuedChips`, today's totals); a cash-out converts what fits, the rest stays in the bankroll and converts on later days, and the cashier and History show that queue. Nothing is ever lost.
- **Ladder and ceiling.** `CasinoLadder` mirrors the global ladder (27 rungs from 100 to 50B), `LevelCap(level)` (geometric between the anchors, level 1 at 10K up to level 100 at 50B) and `MaxBet(level, balance) = max(LevelCap, balance / 20)` floored to a rung. `CasinoStore.Ceiling` returns the server's `ceiling` block when present and falls back to the local formula against an older floor. Every player-chosen stake snaps to the ladder; the server refuses the rest with `ladder` or `ceiling`.
- **Bonuses.** `CasinoBonuses` and the cashier's bonus shelf claim `welcome`, `timed`, `reload`, `streak`, `levelup`, `broke` and `rebate` through `POST /casino/bonus/{kind}/claim`. Grants land as chips in the bankroll (opening one when none is live) and convert under the same cap.
- **Levels, club, titles.** `GET /casino` carries `progress` (level, XP bounds, lifetime figures, balance title) and `club` (tier, points, multiplier, rebate rate). `LevelCapsule`, `StatusTitle` (Shark 1M, High Roller 10M, VIP 100M, Whale 1B, Legend 1T) and the club capsule and sheet render them; the client computes none of it.
- **Feature flags.** `GET /casino` sends `features[]`, parsed once into a `CasinoFeatureSet` (`CasinoFeatures` holds the names: `economy.v3`, `bonus`, `levels`, `club`, `hosting.v2`, `venue`, `gil.tables`, `machines`, `missions`, `challenges`, `fame`, `feed`, `rain`, `holdem`, `race`, `plinko`, `originals`). `CasinoGameGate.FlagFor(gameId)` maps a game to its flag, and a game whose flag is missing shows a "Not open yet" tile instead of opening, so a new client degrades cleanly against an older server.
- **Limits.** The self-set daily loss limit stays opt-in (0 on the wire means no limit); see the gotcha in [Networking](networking.md#gotchas).

## Stores

Every store follows the house pattern: requests run off the draw thread through `StoreWork`, results are parked in a field and the draw path takes them once with a `Take*` method (`TakeSpinResult`, `TakeSeatOutcome`, `TakeFailure`), so Draw never awaits and never allocates for a result. Built in `PhoneServices`.

| Store | Owns |
| --- | --- |
| `CasinoStore` | `GET /casino` (state, sittings, features, ceiling, cashier, bonuses, club, progress), buy-in, top-up, cash-out, limits, bonus claims |
| `CasinoPlayStore` | Solo machine routes: slot spins, the Golden Bird gamble, Moogle Money meters, scratch, Barkeep, and the pending-round recovery (`PendingCasinoRound`, per character); owns `Originals` (`CasinoOriginalsStore`) and `Plinko` (`CasinoPlinkoStore`) |
| `CasinoRoomsStore` | The room list, the one live `CasinoRoomSession` (attach, snapshot, events with seq and epoch, private lanes, clock skew), wheel and race bets and bingo cards |
| `CasinoTablesStore` | The table browser, quick seat, blackjack seats, hosting (create, door, invites, kick, rename, co-dealers, pause, deal, tournament, close), the session ledger, nearby tables |
| `HoldemStore` | Hold'em seats and actions, time bank, sit out, top-up, hand history, the rejoin penalty |
| `CasinoVenueStore` | Venue room GameState (`VenueRoomView`), venue acts, roll and raffle verification, tournaments, trade-sync proposals |
| `CasinoFloorStore` | Missions and claims, challenges, Hall of Fame boards, the bets feed, the `casino-floor` watch with its ticker and rain notes |
| `CasinoHistoryStore` | History pages, round verification, and hosted table hands recorded locally (practice and gil hands never reach the server's history) |
| `CasinoSpinStore` | The free daily spin, which pays wallet coins rather than chips |

`CasinoRoomSession` parses the snapshot's GameState into one typed slot per kind (`Wheel`, `Bingo`, `Blackjack`, `Race`, `Holdem`); venue GameState is parsed by `CasinoVenueStore`. A sequence gap or an epoch change triggers a resync. `CasinoSeatMachine` and `CasinoJoinGate` hold the seat states and the "joins next hand" rules shared by the tables, and `CasinoTurnNotifier` posts a notification when your turn comes while the table is not in view.

## Stage frame

Every play screen draws inside one host frame, `CasinoStage`. The lobby, cashier, history, fairness and limits pages keep the normal app chrome; cabinets, tables and rooms go full bleed with no `AppHeader`.

The host (`CasinoApp.DrawStage`) opens `AppSurface.BeginEdgeToEdge(content, true)` and then, per frame:

```csharp
var frame = stage.Begin(area, spec, balance, casino.Ceiling);
cabinet.Draw(stage, frame, ui);
var action = stage.End(ui);
```

`Begin` draws the backdrop and the frosted deck, advances effects, and returns a `CasinoStageFrame`: `Layout`, `Safe`, `Deck`, `Body`, `Full`, `DeltaSeconds`, `Phase` (the lights clock), `Instant`, `Focused`, `Blocked` (a reality check is showing) and `SnapToTruth`. `End` draws particles, the celebration, screen effects, the practice ribbon, the reality-check card, the bets handle and the chrome, and returns `CasinoStageAction.Back` or `Cashier` for the host to act on. Overlays draw at window level through `stage.Gate()` and `stage.DrawOverlays(screen, ui)`; `TakeInfoRequest()` returns `Rules`, `Extra` or `Fairness`, and `TakeRoundRequest()` a round id tapped in the bets rail.

`CasinoStageSpec(GameId, Title, Preset, Room, DeckHeight, Practice, BetsRail, InstantAvailable, ReturnTenths, Extra, Warmth, LampPool)` declares the screen: `Room` adds the 40 unit phase ribbon, `DeckHeight` reserves the bet deck (0 for layouts that draw their own controls), `Practice` swaps the capsule to grey Practice chips and adds the practice ribbon, `ReturnTenths` prints the return in the info sheet, `Extra` names a second info button (a pay table, odds sheet or the table sheet), `Warmth` tints the Strip toward amber, `LampPool` lights the felt. `CasinoApp.StageSpecFor(route)` picks the spec per screen.

Geometry, in design units times `UiScale.Current`: chrome band 52; back chip and info chip 36 glass circles centred 28 in from either side at y 26; the chips capsule centred on the band; ribbon 40 and practice ribbon 22 under the band; deck at the bottom (118 by default, `BetComposer.DeckHeightFor(knob, fixedAmount)` for more); `Safe` sits between them, inset 12. Worlds fit inside `Safe`; lights and backdrops use `Full`. `CasinoStageLayout.ChromeContains(point)` tells a cabinet whether a press landed on the chrome.

Snap to truth: when the stage was not drawn for more than 2 seconds, or the phone regains focus after more than 2 seconds away, `frame.SnapToTruth` is true for one frame, the balance and the celebration snap to their final state, and cabinets finish their own animations.

`stage.RepeatPressed()` reports Space while the stage has focus, no overlay is open and no text field is active, claiming only the Space key through `GameInput`.

## Backdrops

| Preset | Look | For |
| --- | --- | --- |
| `Strip` | Indigo night, a skyline with 40 twinkling windows, rose and cyan neon haze drifting opposite ways, rising bokeh; `SetWarmth(1)` shifts it amber | Lobby previews, the slot machines, the originals, Plinko, scratch, wheel, daily spin, Barkeep (warm), Hold'em (under its own felt) and venue rooms |
| `Arena` | Stadium night, two floodlight towers with four sweeping cones, a crowd band with 60 flickering phone lights, track dust | The bingo hall and the Chocobo race |
| `Felt` | Night felt on every casino stage (`CasinoStage` calls `SetFeltStyle(FeltStyle.Night, spec.Rail)`): a desaturated emerald cloth whose lamp pool peaks at 0.22 luminance and falls to 0.08 at the edges, a fine cloth weave and a wooden rail along the stage edges (`CasinoStageSpec.Rail`, on by default). The Games app keeps the classic felt | Blackjack and the broadcast view |

Every wash on every backdrop stays at or under 0.22 luminance (rec. 601 on the drawn colour): `StagePolishTests` stacks the Strip haze bands and blobs over the skyline glow, and all four Arena cones where they cross, against that ceiling. Point lights (windows, bokeh, phone lights, dust) are lights, not fills, and are not counted.

## Full screen and legibility

Binding for every cabinet, table and room:

1. The game is the screen. The world (felt, track, reels, board, wheel) fills the stage edge to edge under the glass chrome. No boxed play area, no inset panel, no `GameScene.Arena`. Felt tables are the backdrop itself, with seats sitting on it.
2. Night, never bright. Backdrops stay at or under 0.22 luminance; no bright saturated full-screen fills. Text sits on a dark backdrop or on glass.
3. Readable text. Body copy at least `Subheadline`, status lines at least `Footnote` in strong ink (`StageText.Strong`), amounts at least `Title3`, the primary state line ("Place your bets", "Your turn") at least `Title2` with a soft shadow. Muted ink only on glass or dark felt and only for secondary labels. Contrast at least 4.5:1 against the brightest point behind the text.
4. One obvious next action: one full-width primary in the bet deck, secondary actions as smaller pills beside it. Empty seats are large Sit spots, not small "Open" circles.
5. Use the space: worlds grow to fill Safe, nothing smaller than a 44 unit touch target.
6. Clean entry: one tap from the Floor into any game, Back returns to where you came from, rules one tap away on the info chip.
7. Every idle state has motion and every state change has feedback.

### Stage text

`StageText` is the one way to put words on the world. Each helper resolves the requested style up to its role minimum (`StageText.Minimum(role)`, never shrinking below it) and either fits or marquees against its container:

- `State(drawList, center, text, maxWidth, id)`: Title2 or larger in strong ink with a soft shadow; overflow moves onto a glass capsule and marquees. `StateLine(drawList, center, text, maxWidth, ink)` is the id-free form that ellipsizes.
- `Status(drawList, center, text, maxWidth, id, overWorld)`: Footnote or larger in strong ink, on a dark glass capsule when it sits over the world. `Status(drawList, center, text, maxWidth, scale)` and `Plate(..., ink, style, scale)` are the id-free capsule forms.
- `Amount(drawList, center, text, maxWidth, id)`: Title3 or larger in gold.
- `Label(drawList, center, text, maxWidth, id, muted)`: Footnote or larger; a muted label always sits on the capsule.
- `FitScale(text, maxWidth, style, role)`: the shrink-to-fit scale clamped at the role minimum.

`StageText.CapsuleFill` is dark enough that muted ink reads at 4.5:1 even over the brightest felt; `StageContrast.Ratio` and `Over` are the WCAG math the tests use.

### Felt tables, seats and the action row

`FeltTable.DrawCloth(drawList, full, rail, practice, scale)` paints the night cloth over any rect (a grey cloth for practice tables) for scenes that are not on a Felt stage, such as idle previews and Hold'em's own felt. A `FeltTable` instance draws the table layer over a Felt stage with `Draw(drawList, full, table, new FeltTableOptions(seats, print, cloth, rail, circles, practice), scale)`: a gold insurance arc, betting circles and an optional printed line such as "BLACKJACK PAYS 3 TO 2" set along an arc in low-contrast gold, and returns `FeltTableGeometry` (dealer anchor at the top centre, the seat arc, `Seat(index)`, `BettingCircle(index)`), all scaled to the table rect with seats of at least 28 units radius.

`SeatSpot.DrawEmpty(drawList, center, radius, label, accent, invite, scale)` is the empty seat: a dark disc of at least 56 units across with a plus glyph and a "Sit" label, pulsing a glow while `invite` is set (you are not seated), returning true on a tap.

`DeckActions.Row(deck, scale)` is the bottom row of the bet deck, 56 units tall and full width. `stage.SecondaryAction(label, enabled, ink)` lays out up to two pills from the left and `stage.PrimaryAction(label, enabled, ink)` takes the rest, the whole row when there are none. `BetComposer` uses the same row (the Manual and Auto switch and the auto gear are its secondary slots), `DeckActions.Above` places a status line over the row, and `DeckActions.Slice` splits it for two equal choices such as Insure and No insurance.

## Lights, signs and colours

`CasinoLights` is static and allocation free: `BulbChase(drawList, rect, radius, scale, phase, spacing, colorA, colorB, lit)` (6 unit bulbs on a 14 unit pitch, every third dark, chasing at 6 bulbs a second), `NeonTube(drawList, path, color, width, glow)`, `Spotlight(drawList, origin, direction, length, spread, color, alpha)`, `Bokeh(drawList, rect, phase, density, scale)`, `CoinShower(scale)` and `CoinShowerEmitter(scale, rate)` (gold discs, gravity 180 units, a pulse size curve), `Sparkle`, `Shard`, `Ring` particle specs, and `LightSweep(backdrop, strength)`. `CasinoSigns.Draw(drawList, CasinoSign, center, height, color, lit)` strokes a sign in neon (`HeightToFit` sizes it to a box); a new game adds its word to the `CasinoSign` enum and the word table. `CasinoColors` holds `Money`, `MoneyHighlight`, `LightA` (rose), `LightB` (cyan), the felt pair, the warm inks, `Practice` and `Loss`. `AppPalettes.Gamba` is the app palette; the casino accent is `AccentRing.Rose`.

## Win celebrations

`stage.Celebration.Celebrate(stake, payout, origin, instant, jackpot)` picks a tier from the net win over the stake (`WinLadder.TierFor`): Win above 0, Nice from 3x, Big from 10x, Mega from 25x, Epic from 50x, Legendary from 100x or any jackpot. A payout at or below the stake returns `WinTier.None` and does nothing: no light, no particles, no sound, the paying symbols simply light. Each tier carries its sparkles or confetti, coin shower seconds, light sweep, banner, bulb chase, full-screen card, sound and count-up seconds (`WinLadder.Spec`). Big and up flash, punch, vignette or slow the game. A tap on Safe skips after half a second; Instant mode shows the final amount with a 0.3 second pop. `Blocking` is true while an Epic or Legendary card covers the stage. `WinCelebration` is the only celebration path in the app.

`CasinoSfx.Play(sound)`, `Pitched(sound, step)` and `Win(spec)` play only while the stage is drawn and focused, and WinBig, WinEpic and Fanfare share one 10 second slot (a throttled big win falls back to WinSmall). The casino cues (`UiSound.ReelTick` through `LevelUp`) are synthesized by tools/sound-generator (`python generate-sounds.py --casino`).

## Bet composer and the ceiling

`BetComposer.Draw(ui, frame.Deck, model, delta)` returns `Confirm`, `StartAuto`, `StopAuto` or `None`. `BetComposerModel(MinimumBet, MaximumBet, Stack, Action, Enabled, AutoAvailable, FixedAmount, Knob, Repeat, Busy)`: `MaximumBet` is the game's own cap clamped to `casino.Ceiling.MaxBet`; `Action` is a one-argument template ("Buy {0}", "Bet {0}") that receives the compact amount; `Knob` reserves `composer.KnobRect` for the game's own control (risk, rows, mines, target, runner, spot, card count); `FixedAmount` hides the amount row. The field taps into free input and commits on Enter or blur; every value snaps to the ladder through `CasinoLadder.Clamp`, `Half`, `Double` and `Top`. The Auto tab drives `composer.Auto` (an `AutoBetPlan`): the cabinet starts the next round with `Auto.Next` while `Auto.Running`, and reports each settled round with `Auto.Settle(stake, payout, bonus, min, max, stack)`, which applies on-win and on-loss adjustments and stops on count, profit, loss, bonus or chips. The gear opens the auto settings sheet (`composer.Gate()` and `composer.DrawOverlay(screen, ui, bonusAvailable)`). The info sheet prints the ceiling and its reason.

Gil is not on the ladder, so gil blackjack tables bet through `ClassicBetComposer`, a plain amount field bounded by the host's limits. Hold'em raises use `HoldemRaiseComposer` (Min, half pot, three quarters, Pot, All in, a slider and a one big blind stepper, the button label always the exact amount).

Every settled chip round goes through `stage.Settle(new CasinoBetRecord(game, stake, payout, roundId, settledAtUnixMs))`, which feeds the bets rail's My bets tab and the reality check (`RealityCheck`: a card every 100 rounds or 30 minutes of play showing rounds, minutes and session net, with Keep playing or Take a break). The bets rail's All bets and High rollers tabs read `GET /casino/feed`.

## Playback

A playback class turns a server answer into choreography and is the only thing a cabinet animates from. The shape is the same everywhere: `Begin` or `Update` takes the DTO (or the room state plus the server clock), `Advance` moves time, the cabinet drains cues with `TryTake*` or `Take*` (a tick, a landing, a reveal, a settle) to fire sounds, particles and the celebration, and `Snap` or a snap flag jumps to the final state for a mid-event join, a reconnect or `frame.SnapToTruth`. Playbacks hold no ImGui state, so the tests drive them directly (`*PlaybackTests`). Client-side flourishes that need randomness (a ball's wobble, a patron's walk, commentary lines) draw from `GameRandom` seeded from the round, so the same round replays identically.

| Playback | Replays |
| --- | --- |
| `MachineRoundPlayback`, `MachineRollup` | A spin's `steps[]` beat by beat (spin, tumble, expand, hold, respin, collect, meter), then the rollup at half the bet a second up to 20x |
| `PlinkoFlight` | Each ball along its server `path`, a scripted bounce per row, up to ten balls in flight |
| `DiceRollPlayback`, `LimboClimbPlayback`, `KenoDrawPlayback` | The instant originals: the sliding roll marker, the climbing multiplier, drawn tiles in sequence (Mines and Hi-Lo keep their open round in `MinesBoard` and `HiLoChain`) |
| `ScratchCardPlayback` | The foil reveal of a bought ticket |
| `WheelRoundPlayback`, `WheelChoreography`, `WheelPointer` | The wheel landing on the server's segment |
| `BingoRoundPlayback` | Called balls, flights, daubs and stage wins (a burst of balls after a join jumps straight to the board) |
| `RaceRoundPlayback` | The race from `RaceScript`, rebuilt from the order and seed revealed at Locked |
| `BlackjackDealPlayback`, `BlackjackDealChoreography` | Card flights, the hole card reveal and per-seat settlement |
| `HoldemPlayback` | Hole cards, board streets, reveals at showdown and chips sweeping to winners |
| `DailySpinPlayback` | The sprung pointer of the free spin |
| `DiceTablePlayback`, `DeathrollPlayback`, `RafflePlayback`, `TournamentPlayback` | Venue rolls, duels, the raffle draw and the blackjack tournament strip |

## Rules mirrors, vectors and fairness

Core/Casino holds one Dalamud-free mirror per game (`BlackjackRules`, `BlackjackSideBets`, `HoldemRules`, `HoldemHands`, `RaceRules`, `RaceScript`, `PlinkoRules`, `OriginalsRules`, `SlotsRules`, `SlotsMachines`, `GoldenBirdRules`, `CrystalCascadeRules`, `MoogleMoneyRules`, `ScratchRules`, `WheelRules`, `BingoRules`, `BarkeepRules`, `DailySpinRules`, `VenueRules`, `CasinoHostingRules`, `CasinoFloorRules`, `CasinoLadder`). They exist to print pay tables, returns, bands and odds, and to replay draws for verification; they never decide a payout. When a backend constant changes, the matching mirror and its `*RulesTests` change in the same release.

Seeded games share vector files with the backend: `src/Aetherphone.Tests/Vectors/` holds `holdem.json`, `race.json`, `plinko.json`, `originals.json`, `slots-bird.json`, `slots-cascade.json`, `slots-moogle.json`, `blackjack-sidebets.json` and `venue.json`, each a byte-for-byte copy of the file in the backend's test project. Every vector test first pins the file's SHA-256, so a drifted copy fails loudly, then replays each case through the client mirror (`HoldemVectorTests`, `RaceVectors`, `PlinkoVectorTests`, `OriginalsVectorTests`, `SlotsVectorTests`, `BlackjackSideBetVectorTests`, `CasinoVenueVectorTests`). Never edit a vector by hand; copy the backend's file again.

`CasinoVerifier.Verify` checks a settled round from `GET /casino/rounds/{roundId}/verify`: the revealed seed must hash to the commit, then the draw log must replay purpose by purpose (`segment`, `card`, `ball`, `shuffle`, `prize`, `patrons`, `jackpot`, `gamble`, `peg`, `field`, `strength`, `runner`, and the originals' float stream with `mine`, `roll`, `limbo`, `keno`, `card`). Hold'em hands and blackjack hands verify through the room's hand index (`GET /casino/rooms/{roomId}/verify/{index}`), and venue rolls and raffle draws through `VenueVerifier` over `VenueDraws`. Verification fails closed: anything it cannot replay is a mismatch, never a pass.

## The games

| Game | Code | Kind | Notes |
| --- | --- | --- | --- |
| Golden Bird Deluxe, Crystal Cascade, Moogle Money | Machines/MachineCabinet | solo, `casino.slots` with `machineId` `slots.bird`, `slots.cascade`, `slots.moogle` | One shell: chassis, top glass and reel window fill the stage; the Golden Bird gamble ladder (`GambleLadder`), the Crystal Cascade ante and bonus buy modes, the Moogle Money Mini and Minor meters; every machine also draws the floor jackpot |
| Plinko | Plinko/PlinkoCabinet | solo, `casino.plinko` | 8, 12 or 16 rows, three risks, a result rail of recent drops |
| Mines, Dice, Limbo, Keno, Hi-Lo | Originals/OriginalsCabinet | solo, `casino.mines` and the rest | One cabinet with an `IOriginalsSkin` per game; Mines and Hi-Lo keep an open round across picks and cash out |
| Scratch | Cabinets/ScratchCabinet | solo, `casino.scratch` | Five ticket tiers, buy five in a row, the solo reference cabinet |
| Barkeep | Cabinets/BarkeepCabinet | solo skill, `casino.bartender` | A full-bleed bar scene (`BarkeepSceneArt`), patrons choreographed by `BarkeepBarFlow`, a cosmetic tip meter, practice reachable without chips |
| Wheel | Cabinets/WheelCabinet | room, `casino.wheel` | The room reference: phase ribbon, podiums, the printed return per spot |
| Bingo | Cabinets/BingoCabinet | room, `casino.bingo` | `BingoHallLayout` with a decorative `PhysicsWorld` tumbler (`BingoTumbler`) that decides nothing; the payout always comes from the settled cards |
| Chocobo race | Race/RaceCabinet | room `race-track`, `casino.race` | Arena backdrop, landscape track with a portrait layout (`RaceLayout`), Win, Place, Forecast and Reverse forecast tickets (`RaceTicketBuilder`), tote and result boards |
| Blackjack | Tables/BlackjackTable | table, `casino.blackjack` | House pit (`BlackjackPit`), side bets, insurance, late surrender, a dealer puck, per-seat settlement made visible by `BlackjackRecap` |
| Texas Hold'em | Tables/HoldemTable | table, `casino.holdem` | House rooms (`HoldemPit`), `SeatLayout.Ring` rotated to the hero, win chance from the private prompt, side pots (`HoldemPotScatter`), hand history sheet |
| Daily spin | Cabinets/DailySpinCabinet | free, `casino.dailyspin` | Pays wallet coins, so it celebrates through `SpinFlourish` with the coin glyph; `DailySpinIdle` also drives the home widget |
| Dice table, Deathroll, Raffle | Venue/VenueCabinet | hosted venue rooms | See the venue layer below |

Every cabinet implements `ICabinetIdle` (`IdleBackdrop`, `DrawIdle(drawList, rect, deltaSeconds)`), which `CabinetPreview` uses for the lobby's live tiles and hero cards.

## Tables, hosting, practice and gil

House tables are furniture the server keeps open: the blackjack pit (`blackjack-pit`, `blackjack-parlour`, `blackjack-salon`, and `blackjack-vault` for the Whale title) and the Hold'em rooms (`holdem-low`, `holdem-mid`, `holdem-high`, and `holdem-royal` for the Whale title). Sitting at a chip table moves a rack from the bankroll chip-to-chip; leaving walks it home. One card-table seat per identity across blackjack and Hold'em.

`HostSheet` builds a `CasinoTableConfig` for `POST /casino/tables`: game, name, seats, stakes, buy-in band, listing (Private, Knock, Open), spectators, turn clock, time bank, and for practice and gil tables the house rules sheet, dealer mode and co-dealers. `TableBrowser` lists house, mine, invited and listed tables with filters; `TableDoor` is the host panel (invites, knocks, kick, rename, pause, deal, tournament, close); `TableLedger` shows the session ledger and copies it as plain text.

The table's currency (`CasinoCurrencies`) decides how money moves:

- **Chips** (blackjack and Hold'em): the bank, every economy rule, standard rules only.
- **Practice** (blackjack and Hold'em): the table never touches the bank. No sitting, rack, round, meter, XP, mission or jackpot; every seat starts at the practice stack and may rebuy when the host allows. The stage shows grey chips and the practice ribbon, the cashier never opens, and hands are recorded only in the local History group. Fairness is unchanged: hands still draw from the room's seed chain and verify.
- **Gil** (blackjack and venue rooms): host-banked. The host declares a bank, a max bet and a max payout per hand, and a bet whose worst case the bank cannot cover is refused (`bank_limit`). The server never holds or converts gil; it keeps a two-sided ledger in which a buy-in, rebuy or payout settles only when both sides confirm it, unconfirmed entries show amber on both screens, and a payee can dispute a payout that never arrived. Gil tables touch no coin, chip, XP, mission, club point or Hall of Fame row. Gil tables take no side bets and offer neither insurance nor surrender.

`CasinoCurrencies.SeatBanked(currency)` is true for practice and gil: the stage balance is the seat's own stack, not the bankroll.

## Venue layer

Hosted venue rooms (`casino.dice-table`, `casino.deathroll`, `casino.raffle`) run on practice chips or gil, open from the table browser, the door or a deep link into `CasinoScreen.VenueRoom`, and are drawn by `Venue/VenueCabinet.cs` on the Strip with a room ribbon. Each room has a Dalamud-free playback (`DiceTablePlayback`, `DeathrollPlayback`, `RafflePlayback`) that takes the room's GameState and choreographs only what the server returned; a mid-event join snaps to the current state. `CasinoVenueStore` parses the GameState off the draw path (`VenueRoomView`), sends `POST /casino/venue/{roomId}/act` with an idempotent `clientActionId`, verifies the last roll or draw through `GET /casino/rooms/{roomId}/verify/{seq}` (`VenueVerifier` over `VenueDraws`, pinned by `venue.json`), polls `GET /casino/tables/nearby` from the housing position (`IHousingPositionSource`), and indexes listed tables by venue address for the Venues app pill.

The stage Extra button on blackjack tables and venue rooms opens `VenueTableSheet`: verify, trade sync settings (gil tables), the broadcast view for spectators and the report action (`Plugin.Report`, target `casino_table`).

Trade sync is opt-in (`Configuration.CasinoTradeSync`). `TradeWindowReader` listens to the `Trade` addon lifecycle and only reads its text; `TradeSyncTracker` confirms a completed trade from the wallet delta, and `TradeLedgerMatcher` turns it into a buy-in or payout proposal or a confirmation of an existing ledger entry (`source: "trade"`). The host always gets a one-tap prompt (`TradeSyncPrompt`) plus a notification; a player can let their own side confirm automatically. Nothing in the game is automated.

`BroadcastView` (`CasinoScreen.Broadcast`, landscape) projects the blackjack or Hold'em snapshot into `BroadcastTable` with large seats, stacks and cards and no controls. `TournamentOverlay` draws the practice blackjack tournament strip, leaderboard, eliminations and winner over the table; hosts start and stop it from `TournamentDoorCard` on the door.

## Lobby

The app has four tabs (`CasinoTab`: Floor, Live, Tables, Cashier); every other page is a `CasinoRoute` on the router (`CasinoScreen`: Cabinet, Table, Pit, TableDoor, HostTable, TableLedger, VenueRoom, Broadcast, DailySpin, History, Fairness, Limits, RoundDetail, Fame).

- **Floor** (`CasinoApp.Lobby`, `Strip/`): the chips hero with balance title, `LevelCapsule` and bonus buttons (`StripHero`), the resume card when seated, the hero carousel (`StripCarousel`: jackpot marquee, a machine with live idle reels, the next race, a hot table, the live challenge), the wins ticker (`WinsTicker`, fed by the `casino-floor` room), "At this venue" tables (`NearbyTablesCard`), the live now rail, the missions card (`MissionsCard`) and club capsule (`ClubCapsule`), the shelves (`StripShelves` over `StripCatalog`: Tables, Machines, Originals, Live floor, Instant, Skill, For venues), the Hall of Fame podium (`FamePodium`, full boards in `FameView`) and the records row (History, Fairness, Limits). `StripIntro` is the first-visit walkthrough.
- **Live** (`LiveBoard`): rooms and listed tables with phase, occupancy and Watch, filtered by All, Open seats, Practice, Friends and High roller.
- **Tables**: the browser, quick seat, Host a table, join by token.
- **Cashier** (`CashierDrawer`): buy-in in coin lots (`CashierBuyIn`), cash-out with today's allowance and the queued chips (`CashierCashOut`), the bonus shelf (`CashierBonusShelf`) and the club card (`CashierClubCard`). The chips capsule on every stage opens the same drawer.

Missions, challenges, the Hall of Fame and the feed only count real-chip rounds. Hall of Fame boards only name players who opted in through the existing leaderboard flag.

## Adding a game

The server ships the game first: its kind, routes, feature flag, and a vector file when the game is seeded. Then, on the client:

1. **Ids.** Add the game id to `Apps/Casino/CasinoGames.cs` and the wire kind to `Core/Casino/CasinoWire.cs`. Add the flag to `CasinoFeatures`, `CasinoFeatureSet.Known` and `CasinoGameGate.FlagFor`.
2. **Wire.** Add the DTOs to a `Casino*Dtos.cs` file (every field defaulted) and register them in `AethernetJsonContext`; add the routes to `CasinoClient` (a new partial file for a new area); pin route and JSON shape in a `*WireContractTests` class. New refusal reasons go into `CasinoReasons` with a string and a `CasinoReasonCoverageTests` entry.
3. **Rules and vectors.** Mirror the pay tables and draws in `Core/Casino/<Game>Rules.cs` with tests against the backend constants. Copy the backend vector file verbatim into `src/Aetherphone.Tests/Vectors/`, pin its SHA-256 and replay it. Teach `CasinoVerifier` the game's draw purposes.
4. **Store.** Extend `CasinoPlayStore` for a solo game or `CasinoRoomsStore` for a room; results come back through `Take*`, never through a callback into Draw.
5. **Cabinet.** A class under its own `Apps/Casino/<Game>/` folder with `Draw(stage, frame, ui)`, a `CasinoStageSpec`, a playback class, `BetComposer` for every stake, `stage.Celebration` for every win, `stage.Settle` for every settled round, and `ICabinetIdle`. Follow the full screen and legibility rules above.
6. **Host wiring.** In `CasinoApp`: the cabinet field, its case in `DrawStage`, its spec in `StageSpecFor`, its branch in `ResetCabinetOf` (check after merges that no branch ends in a doubled `return;`), and `StageBalance` if it shows a stack other than the bankroll.
7. **Lobby.** A `StripCatalog` entry on the right shelf, a `CasinoSign` word, the name in `CasinoGameNames`, the rules sheet steps and pitch in `CasinoRules`, a glyph in `CasinoGlyphs`, an accent in `CasinoArt`, the minimum bet and printed return in `CasinoApp.Floor`, and the mission sentence in `MissionText`.
8. **Copy.** Every string as a `LocString` in its own nested class in `L.cs` plus all nine JSONs. Game names never take an "Aether" prefix.

## Other pieces

- `PhaseRibbon.Draw(drawList, rect, label, remainingMs, windowSeconds, crowd, accent, scale)`: the room ribbon with a `TurnTimerRing`, the only countdown in the app.
- `CasinoNotice.Draw` and `DrawWithAction`: the Reason, Info and Card notice shapes (Card bodies at Subheadline, the rest at Footnote).
- `ChipStack` (Windows/Components/Layout): ten denominations from 100 to 100M with fixed colours, five discs a column, notched edges, a practice variant, and compact amounts.
- `NumberText.Compact` reads K, M, B and T without ever rounding a balance up; `NumberText.Signed` caches the plus form; `CasinoTextCache` caches every other label a cabinet formats.
- `DailySpinWidget` (Apps/Casino/Widgets) puts the turning idle wheel on the home screen.
