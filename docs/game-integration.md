# Game integration

This doc explains how Aetherphone reads from and acts on the running game without crashing it or overstepping: what the plugin never does in game, the threading rule, static game data (Excel sheets) versus live game memory (ClientStructs), acting on the game safely, and the optional plugin integrations. Read it before you write code that touches game state, an `unsafe` block, or an Excel sheet, and on every game patch day.

## Key files

| Path | Why you open it |
| --- | --- |
| src/Aetherphone/Plugin.cs | Every injected Dalamud service, and the constructor's deferral pattern |
| src/Aetherphone/Core/Runtime/FrameworkTicker.cs | Throttled per-frame callback on the framework thread |
| src/Aetherphone/Core/Runtime/PendingFrameworkAction.cs | Waits for the game to be ready, then retries an attempt until it succeeds or times out |
| src/Aetherphone/Core/Game/GameData.cs | The central Excel sheet reader; look here before adding a reader |
| src/Aetherphone/Core/Game/PlayerBusy.cs | The shared "player is busy" gate |
| src/Aetherphone/Core/GameChat/ChatSender.cs | The one way to send a chat line or command |
| src/Aetherphone/Core/Game/PlayerActions.cs | Target, plate, invite, friend request, and blacklist for another player |
| src/Aetherphone/Core/Jobs/GearsetActions.cs | Equips gearsets, the worked example for acting on the game |
| src/Aetherphone/Core/Game/WeatherControl.cs | The reference for an override that reverts itself |
| src/Aetherphone/Core/Venues/LifestreamBridge.cs | Travel through the Lifestream plugin |
| src/Aetherphone/Core/Honorific/HonorificBridge.cs | Nameplate titles through the Honorific plugin |
| src/Aetherphone.Tests/JobsRosterTests.cs | How game-derived logic is tested without the game |

## What this plugin never does in game

Aetherphone is a phone on the screen, not a tool that plays the game. These rules hold in the code today; keep them when you add a feature:

- **It never plays for the player.** The plugin's own code never uses an action or ability, moves or steers the character, or targets an enemy. The Target button targets a nearby player character, and only when pressed.
- **Commands come from the player.** A chat line or command reaches the game only from something the player did or left on: a message they typed, a shortcut they wrote (or imported and accepted) and started, a button they pressed (friend request, blacklist, travel), or the Scroll While Idle emote when the phone opens, a Settings switch that is on by default.
- **No packets, no gameplay hooks.** The plugin sends no network packets of its own, and its only function hook is on DirectX's Present call, to draw video frames. Everything else goes through Dalamud services and the game's own UI functions, the paths a click or a typed command takes.
- **Few memory writes.** The plugin never writes gameplay state such as position, stats, inventory, or cooldowns. Its direct writes are this client's rendered weather and Eorzea time (Skywatcher), reverted on combat, zone change, and unload, and the player's own target, which only the Target, friend request, and blacklist buttons touch (the last two leave it as it was).
- **Other plugins keep their features.** Travel goes through Lifestream and titles through Honorific, over their public IPC and commands, only when the player has installed them.

## Dalamud services

Dalamud is the plugin framework that loads Aetherphone inside FFXIV. It injects typed services into static properties marked `[PluginService]` at the top of src/Aetherphone/Plugin.cs before the plugin constructor runs. Most core services receive them in `PhoneServices.Build` instead of reaching for the statics, which keeps them testable. The ones an app author meets most:

| Service | Used for |
| --- | --- |
| `IFramework` | The per-frame `Update` event and hopping onto the framework thread |
| `IClientState` | Login state, territory, client language, login and zone events |
| `IObjectTable` | The local player and nearby characters |
| `ICondition` | Game state flags: combat, cutscene, between areas, mounted |
| `IDataManager` | Excel sheets |
| `IUnlockState` | Mount, minion, emote, and other unlock checks |
| `IChatGui` | Reading game chat, printing feedback lines |
| `IGameGui` | Map links, world-to-screen projection, addon lookups |
| `IDalamudPluginInterface` | The config file, the UI builder, IPC to other plugins |

## The threading rule

Everything that reads or writes live game state must run on the framework thread, the game's main thread. Dalamud raises `IFramework.Update` and `UiBuilder.Draw` on that thread, so a tick handler or the phone's draw path is safe. Anything else (a `Task.Run` body, an HTTP continuation, a websocket callback) is not, and reading `ObjectTable.LocalPlayer` or a ClientStructs pointer there can crash the game.

Four tools keep code on the right thread:

- **`FrameworkTicker`** reads the game on an interval: `new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate)`. It throttles itself and fires only while its optional `AppGate` is open (the owning app is installed), and `OnTick` runs on the framework thread.
- **`PendingFrameworkAction`** is for "do this once the game is ready". Its constructor takes a timeout, a retry delay, a readiness check (arrived in the zone, Lifestream idle), the attempt, and an optional attempt cap. `Arm()` starts it, and it retries each tick until the attempt succeeds, the attempts run out, or the timeout passes.
- **`RunOnFrameworkThread`** hops back from background work, as `Configuration.Save` does.
- **`IsInFrameworkUpdateThread` guards** protect reads that return garbage off-thread: `CollectionsCatalogService.EnsureLocalUnlocks` returns null there so the caller retries next frame.

### The constructor is not the framework thread

Dalamud may run the plugin constructor on a loader thread, so never read character data there. For Open at startup, the constructor only flags the request and subscribes a tick handler, which does the player and zone checks (`QueueAutoOpen` and `OnAutoOpenTick` in src/Aetherphone/Plugin.cs). Copy this deferral for any startup logic that needs the character.

## Static game data: Excel sheets

The game ships its databases as "Excel sheets" (nothing to do with Microsoft Excel): typed tables such as `ClassJob`, `World`, and `Item`, parsed by the Lumina library and read through `IDataManager`. The house pattern, from `GameData.WorldName`:

```csharp
public string WorldName(uint rowId)
{
    if (rowId != 0 && data.GetExcelSheet<World>().TryGetRow(rowId, out var world))
    {
        return world.Name.ExtractText();
    }

    return string.Empty;
}
```

Guard against row 0, use `TryGetRow`, and call `ExtractText()` on string columns. `GameData` centralizes almost all sheet reads and caches derived arrays. Sheets are read-only and far safer than game memory, but keep sheet access on framework-thread paths like the rest of the codebase.

### The RowId vs array index landmine

`TryGetRow` takes a **RowId**, the sheet's stable primary key. Some sheets also carry columns whose names end in `ArrayIndex`: indexes into raw in-memory arrays, not row keys. Both are small integers, so passing the wrong one compiles and returns plausible data for the wrong thing. It is the most expensive recurring bug in this codebase.

- **ClientStructs accessors take the RowId.** `DailiesReader` passes `ContentRoulette` RowIds to `InstanceContent.IsRouletteComplete`; the game function does its own mapping.
- **Raw game arrays take the array-index column.** `JobsReader.LevelAt` indexes the player's job level array with `ClassJob.ExpArrayIndex`, because that array is laid out by experience slot, not by RowId.

Name variables `...RowId` or `...ArrayIndex` so the next reader cannot confuse them, and bounds-check every array-index read the way `LevelAt` does.

## Live game memory: ClientStructs

FFXIVClientStructs (ClientStructs for short) is a community-maintained set of C# structs laid over the game's memory, shipped with Dalamud. It is raw pointers behind `unsafe`, so every file that uses it is `unsafe` and framework-thread only. The pattern is always: get the singleton, null-check it, read.

```csharp
public static ulong ReadLocalContentId()
{
    var playerState = PlayerState.Instance();
    if (playerState is null)
    {
        return 0;
    }

    return playerState->ContentId;
}
```

`Instance()` can return null during login, logout, and zone transitions, so the null check is mandatory. Prefer a Dalamud service when one answers the question: `IUnlockState` answers "does this character own this mount" without a hand-rolled read.

## Acting on the game

Reading is mostly harmless; acting is where you can hurt the player. Every action follows five rules: check the game state first, run on the framework thread, wrap the game call so a failure becomes a result instead of an exception in `Draw`, check the return value, and revert anything you change.

**Worked example: gearsets.** Class switching goes through gearsets only. `GearsetActions.Equip` returns `Busy` in combat, while casting, between areas, or in a cutscene, and schedules itself onto the framework thread when called from anywhere else. The game call itself:

```csharp
var module = RaptureGearsetModule.Instance();
if (module is null || !module->IsValidGearset(gearsetId))
{
    return GearsetEquipResult.Missing;
}

return module->EquipGearset(gearsetId) == 0 ? GearsetEquipResult.Sent : GearsetEquipResult.Failed;
```

`EquipGearset` returns 0 on success, so the check is `== 0`. The Jobs app treats `Sent` as pending and reports success only once the gearset reads as active.

**Sending chat: use `ChatSender`.** It is the only way to put a line into the game's chat box, and whatever you pass runs as if the player typed it. `TrySend` refuses an empty line, anything over 500 UTF-8 bytes, and any text the game's sanitizer would change, returning false rather than sending an altered string. `TrySendSanitised` lets the game clean the text first, for player-authored commands. Call either on the framework thread and check the result.

**Acting on another player: use `PlayerActions`.** Call `Resolve(name, world)` first: it reports which actions are available now (none for the local player or while logged out, and targeting, friend requests, and blacklisting only for a nearby targetable player). Each action returns false instead of throwing, and a friend request or blacklist leaves the player's own target as it was.

**Shortcuts** send player-authored command steps through `ChatSender`, holding while the game would swallow chat (zoning, cutscenes) and giving up with a reason when the hold runs long. A shortcut shared as a code is the one path where a stranger's text can reach the chat box. The decoder treats the code as untrusted and refuses anything malformed (links must be http or https), and importing runs nothing: the import screen shows every step in full, and the player must accept it, then start it.

**Overrides revert on everything.** `WeatherControl` (Skywatcher) changes the rendered weather and time only when logged in, out of combat, and not zoning, and reverts on combat, zone change, uninstall, and `Dispose`. `GameUiVisibility` (the Camera hiding the HUD) refuses when the player already hid their UI and restores on `Dispose`. Copy this shape for any new override.

## Plugin integrations (IPC)

Dalamud IPC (inter-plugin communication) lets plugins call each other through typed call gates registered by name. Both bridges follow one policy, and a new one should too: check that the plugin is installed and loaded (and its API version, where it has one), wrap every IPC call in try and catch, offer a fallback when the plugin is missing, and undo on `Dispose` anything you subscribed to or applied.

**Lifestream (travel).** Call `LifestreamBridge.TeleportToAetheryte` with an `Aetheryte` sheet RowId and switch on the `LifestreamOutcome` it returns (started, not installed, busy, not attuned, cannot teleport now); the bridge checks all of those before it calls anything. Travel buttons resolve their target with `TravelPlanner` (src/Aetherphone/Core/Maps/), which turns a territory and world, or a shared location, into a world hop, an aetheryte, an aethernet shard, or a housing plot. An aethernet shard, and a venue address once the player confirms, go to Lifestream's own `/li` command through Dalamud's command manager instead of IPC. Without Lifestream installed, travel buttons copy the matching `/li` command to the clipboard or link to Lifestream's page; the phone never travels any other way.

**Honorific (nameplate titles).** `NameplateTitleService` owns the feature: opt-in per character, it picks a title from what the player is doing in the phone and keeps it applied. An app supplies a title by implementing `INameplateActivitySource` (Games and Gamba) or `INameplateHandleSource` (the social apps). The bridge writes only the local player's title and accepts only Honorific API major version 3, and the service clears the title on `Dispose`.

## Game states and gates

Features that only need "is the player busy" share `PlayerBusy.Now`: in combat, in a duty, in a cutscene, or zoning. Finer checks read `ICondition` flags directly. Dalamud hides plugin windows in cutscenes and Group Pose; the phone opts out of the Group Pose hide by default, and out of the cutscene hide only when the player turns it on in Settings > General. Services that should idle in the background ask `PhoneVisibility` whether the phone is on screen.

## Testing game-dependent code

The test project (src/Aetherphone.Tests) references Dalamud and the plugin but runs without the game: the `Plugin` statics are null and no ClientStructs singleton exists. So split game code in two: a thin reader on the framework thread copies game values into plain structs, and pure static functions you can test take those structs. `JobsCatalog` reads the `ClassJob` sheet into `ClassJobFacts`, `JobsRoster` sorts and summarizes them, and JobsRosterTests builds the facts by hand.

Then test in the game with the Debug build, which loads beside Release with its own data and opens with `/phonedev` (see [Getting started](getting-started.md)). Walk the edges: log in and out, change zones, enter a duty, watch a cutscene, open Group Pose, and unload the plugin in the middle of an action. Dalamud's `/xllog` shows the log.

## Patch day

After a game patch, Dalamud loads only once it ships an update for the new game version, with fresh ClientStructs layouts. CI builds against the latest Dalamud, so a renamed or removed struct member shows up there as a build error. Then:

1. Build Release against the new Dalamud with zero warnings, and run the tests.
2. In game, re-check each feature that reads memory: job levels and gearsets, dailies, inventory, the friend list, the housing location in a shared location, weather and time, the phone emote, and hunt map markers.
3. Check new sheet rows: a new job must land in the right Jobs bucket, and new worlds, zones, and aetherytes must resolve in travel.
4. Play a video: if the Present hook fails, playback falls back to the UI render pump and logs a warning.
5. After Lifestream or Honorific updates, check travel and titles; a new Honorific major version turns titles off until the bridge is updated.

An API level bump moves the Dalamud.NET.Sdk version in src/Aetherphone/Aetherphone.csproj and `DalamudApiLevel` in repo.json together; see [Testing and release](testing-and-release.md).

## Which calls are dangerous when

| API | Dangerous when | Protection in code |
| --- | --- | --- |
| `IObjectTable.LocalPlayer` | Off the framework thread, or in the plugin constructor | Deferred to a tick handler |
| Any ClientStructs `Instance()` | Off-thread, or during login, logout, or a zone change (null) | Null-check every call site, framework thread only |
| `IUnlockState` checks | Off the framework thread | `IsInFrameworkUpdateThread` guard |
| `ChatSender` | Any time the text is not fully under the player's control | Length cap, sanitizer check, state gates upstream |
| Weather, time, and UI visibility writes | Left set after combat, a zone change, or unload | Revert on all of those, and on `Dispose` |
| Friend list refresh | Inside a duty | `FriendListReader` refuses there |

## Gotchas

- Base classes (gladiator, thaumaturge) have `JobType` 0 in the `ClassJob` sheet. Bucketing by `JobType` alone drops them; `JobsRoster.BucketFor` falls back to the `Role` column.
- `EnsureLocalUnlocks` returning null means "try again next frame", not "no unlocks".
- The friend list is only as fresh as the last successful refresh, and the refresh refuses inside duties. Poll; never assume.
- `EorzeaTime.CurrentSeconds` uses a real-time formula while the framework pointer is null (early boot), so two reads across that moment are not monotonic.
- `GameUiVisibility` writes the same flag as the game's own Hide UI key, so it also opts the phone out of Dalamud's UI hiding, or the phone would vanish with the HUD.

## Related docs

- [Getting started](getting-started.md): building, loading the Debug plugin, chat commands, and logs
- [Architecture](architecture.md): boot, `PhoneServices`, and the frame loop the threading rule lives in
- [App framework](app-framework.md): how apps receive services and the `AppGate` install gates
- [State and persistence](state-and-persistence.md): ContentId-keyed stores and saving config from any thread
- [Notifications](notifications.md): how game state shapes notification delivery
