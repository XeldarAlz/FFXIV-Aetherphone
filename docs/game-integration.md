# Game integration

This doc explains how Aetherphone reads from and acts on the live game without crashing it: which Dalamud services the plugin uses, the threading rule that governs every game access, how static game data (Lumina Excel sheets) differs from live game memory (FFXIVClientStructs), and how the plugin equips gearsets, sends emotes, teleports, and reads weather, time, character identity, and the friend list. Read it before you write any code that touches `Plugin.ObjectTable`, `Plugin.ClientState`, an `unsafe` block, or an Excel sheet. The Aethernet backend (the separate ASP.NET service) is not covered here; this is purely the client side.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Plugin.cs | Plugin entry point; declares every injected Dalamud service |
| src/Aetherphone/Core/FrameworkTicker.cs | Throttled per-frame callback on the framework thread |
| src/Aetherphone/Core/Game/GameData.cs | Central Lumina Excel sheet reader (jobs, worlds, items, territories) |
| src/Aetherphone/Core/Game/WeatherService.cs | Zone weather forecast from sheets plus live rendered weather |
| src/Aetherphone/Core/Game/WeatherControl.cs | Weather and Eorzea time overrides through game memory |
| src/Aetherphone/Core/Game/GameUiVisibility.cs | Hides and restores the whole game UI through the game's own visibility flag |
| src/Aetherphone/Core/Game/EorzeaTime.cs | Reads the in-game clock from `ClientTime` |
| src/Aetherphone/Core/Game/CharacterWatch.cs | Polls the local character's ContentId and raises `Changed` |
| src/Aetherphone/Core/Inventory/InventoryReader.cs | `ReadLocalContentId()` and inventory memory reads |
| src/Aetherphone/Core/Jobs/JobsReader.cs | Builds the Jobs app model from gearsets, sheets, and player state |
| src/Aetherphone/Core/Jobs/GearsetActions.cs | Equips gearsets (the only class-switching path) |
| src/Aetherphone/Core/Emote/PhoneEmoteController.cs | Plays the phone-scrolling emote when safe |
| src/Aetherphone/Core/GameChat/ChatSender.cs | Sends chat box entries (used for emote commands) |
| src/Aetherphone/Core/Shortcuts/ShortcutRunner.cs | Runs a shortcut's steps, gating command steps on game state |
| src/Aetherphone/Core/Contacts/FriendListReader.cs | Reads the in-game friend list into `FriendEntry` records |
| src/Aetherphone/Core/Contacts/FriendActions.cs | Adventurer plates, party invites, estate teleports |
| src/Aetherphone/Core/Collections/CollectionsCatalogService.cs | Local unlock state via `IUnlockState`, framework-thread gated |
| src/Aetherphone/Core/Dailies/DailiesReader.cs | Roulette, allowance, and Wondrous Tails completion reads |
| src/Aetherphone/Core/Maps/MapData.cs | Aetheryte and zone catalog from sheets |
| src/Aetherphone/Core/Maps/LocationShare.cs | Captures player position, opens map links |
| src/Aetherphone/Core/Venues/LifestreamBridge.cs | Teleports through the Lifestream plugin's IPC |
| src/Aetherphone/Core/PhoneVisibility.cs | "Is the phone on screen" probe consumed by services |
| src/Aetherphone/Core/PollCadence.cs | Slows background polling when the phone is hidden |

## Dalamud services and how they are injected

Dalamud is the plugin framework that loads Aetherphone inside FFXIV. It provides typed services for everything the game exposes. You never construct these: Dalamud injects them into static properties marked with `[PluginService]` before the plugin constructor runs. All of them live at the top of src/Aetherphone/Plugin.cs. The table lists the services in active use; `IKeyState` and `IGamepadState` are also injected there but currently have no call sites:

| Service | What Aetherphone uses it for |
| --- | --- |
| `IDalamudPluginInterface` | Config file, UI builder, assembly location, IPC to other plugins |
| `ICommandManager` | The `/phone` and `/aetherphone` chat commands, forwarding `/li` commands to Lifestream |
| `IDtrBar` | The server info bar entry with the unread badge |
| `IChatGui` | Reading game chat for the Linkpearl app |
| `IDataManager` | Lumina Excel sheet access (static game data) |
| `IObjectTable` | `LocalPlayer`: name, world, position, current class |
| `IClientState` | Login state, territory, client language, `Login`/`Logout`/`TerritoryChanged` events |
| `IFramework` | The per-frame `Update` event and thread marshalling |
| `ICondition` | Game state flags: combat, cutscene, between areas, mounted, and so on |
| `IDutyState` | Duty lifecycle events (`DutyCompleted` feeds the Activity app) |
| `ITextureProvider` | Loading game icons and textures |
| `ITextureSubstitutionProvider` | Texture replacement interop |
| `IGameGui` | Hovered item, opening map links |
| `INamePlateGui` | Nameplate interop |
| `IContextMenu` | The "Search the market" context menu entry |
| `IPluginLog` | Logging |
| `IGameConfig` | Game configuration values |
| `IUnlockState` | Mount, minion, emote, orchestrion, and other unlock checks |
| `IGameInteropProvider` | The plugin's only function hook: the DXGI Present hook in src/Aetherphone/Core/Video/DxHandler.cs that drives video playback (falls back to the UI render pump when the hook fails) |
| `IAetheryteList` | Which aetherytes the character is attuned to |

Core services get these handed to them once in `PhoneServices.Build` (src/Aetherphone/Core/PhoneServices.cs) instead of reaching for `Plugin.*` statics, which keeps them testable. Some leaf code (for example `LocationShare` and `HealthTracker`) does use the statics directly.

## The golden threading rule

Everything that reads or writes live game state must happen on the framework thread, the game's main thread where FFXIV runs its own update loop. Dalamud delivers both `IFramework.Update` and `UiBuilder.Draw` callbacks on that thread, so code running inside a tick handler or inside the phone's draw path is safe. Code running anywhere else (a `Task.Run` body, an HTTP continuation, a WebSocket callback) is not, and reading `ObjectTable.LocalPlayer` or dereferencing a ClientStructs pointer there can crash the game.

Three tools keep the plugin on the right thread:

**FrameworkTicker** (src/Aetherphone/Core/FrameworkTicker.cs) is the standard way to do periodic game reads. It subscribes to `IFramework.Update`, throttles with `Environment.TickCount64`, and only fires when its optional `AppGate` is open (the gate tracks whether the owning app is installed):

```csharp
ticker = new FrameworkTicker(framework, TickIntervalMilliseconds, OnTick, gate);
```

`ActivityTracker`, `HealthTracker`, `InventoryCaptureService`, and the reminder services all follow this pattern. `OnTick` runs on the framework thread, so it may touch game memory freely.

**RunOnFrameworkThread** hops back from background work. `Configuration.Save` (src/Aetherphone/Configuration.cs) shows the canonical shape:

```csharp
public void Save()
{
    if (Plugin.Framework.IsInFrameworkUpdateThread)
    {
        SaveNow();
        return;
    }

    _ = Plugin.Framework.RunOnFrameworkThread(SaveNow);
}
```

Both branches route through `SaveNow`, which wraps `SavePluginConfig` in a try/catch so a failed disk write logs an error instead of throwing into the frame.

`AethernetSession` (src/Aetherphone/Core/Aethernet/AethernetSession.cs) uses the same call so sign-in, sign-out, and auth-failure updates arriving from network callbacks mutate session state on the framework thread.

**IsInFrameworkUpdateThread guards** protect reads that silently produce garbage off-thread. `CollectionsCatalogService.EnsureLocalUnlocks` (src/Aetherphone/Core/Collections/CollectionsCatalogService.cs) refuses to collect unlock state off-thread and returns `null` so the caller retries on a later frame instead of crashing.

### The constructor is not the framework thread

Dalamud may run the plugin constructor on a loader thread. Never read `IObjectTable.LocalPlayer` or any character data inside it. The `Plugin` constructor follows this rule: when `OpenOnStartup` is set it only flags `autoOpenPending` and subscribes `OnAutoOpenTick` to `Framework.Update`. The actual `ObjectTable.LocalPlayer`, `Condition[ConditionFlag.BetweenAreas]`, and `Condition[ConditionFlag.BetweenAreas51]` checks happen inside that tick handler (src/Aetherphone/Plugin.cs, `QueueAutoOpen` and `OnAutoOpenTick`), which is guaranteed to be on the framework thread. Copy this deferral pattern for any startup logic that needs the character.

## Reading static game data: Lumina Excel sheets

Lumina is the library that parses FFXIV's data files. The game ships its databases as "Excel sheets" (nothing to do with Microsoft Excel): typed tables such as `ClassJob`, `World`, `TerritoryType`, `Item`. You access them through `IDataManager`:

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

That is the house pattern, from `GameData.WorldName`: guard against row 0, use `TryGetRow`, call `ExtractText()` on string columns. `GameData` (src/Aetherphone/Core/Game/GameData.cs) centralizes almost all sheet reads and caches derived arrays (mount ids, roulette ids, world region codes), so look there before adding a new reader.

`TryGetRow` takes a **RowId**, the sheet's stable primary key. It never takes a positional index. Some sheets also carry columns whose names end in `ArrayIndex`; those are indexes into raw in-memory arrays, not row keys. Keep the two worlds separate (see the landmine below).

### Worked example: ClassJob roles

The Jobs app sorts jobs into tank, healer, and DPS buckets using the `ClassJob` sheet. `GameData.TryGetClassJobDivision` reads four columns: `JobType`, `Role`, `UIPriority`, and `ClassJobCategory.RowId`. Base classes (gladiator, thaumaturge, and friends) have `JobType` 0, so `JobsReader.BucketFor` (src/Aetherphone/Core/Jobs/JobsReader.cs) matches on `JobType` first and falls back to the `Role` column when it gets 0:

```csharp
return jobType switch
{
    1 => (int)JobRole.Tank,
    2 or 6 => (int)JobRole.Healer,
    3 => (int)JobRole.Melee,
    4 => (int)JobRole.PhysicalRanged,
    5 => (int)JobRole.MagicalRanged,
    _ => role switch
    {
        1 => (int)JobRole.Tank,
        2 => (int)JobRole.Melee,
        3 => classJobCategoryId == WarCategoryId ? (int)JobRole.PhysicalRanged : (int)JobRole.MagicalRanged,
        4 => (int)JobRole.Healer,
        _ => -1,
    },
};
```

Crafters and gatherers are bucketed by `ClassJobCategory` instead (`HandCategoryId` 33, `LandCategoryId` 32). Job icons come from `GameData.JobIconId`, which is `FramedJobIconBaseId` (62100) plus the ClassJob RowId; that is the framed icon set the game itself uses in party lists.

### The RowId vs array index landmine

The most expensive recurring bug in this codebase is mixing up a sheet RowId with an array-index column. Both are small integers, so the wrong one compiles, runs, and returns plausible values for the wrong thing. The rules, each backed by shipped code:

- **ClientStructs accessors take the RowId.** `DailiesReader.ReadDutyRoulettes` passes `ContentRoulette` RowIds to `InstanceContent.IsRouletteComplete`; the native function does its own RowId-to-slot mapping. `GameData.DailyBonusRouletteRowIds` stores `(byte)row.RowId` and uses `CompletionArrayIndex < 0` only as a "this row is tracked at all" filter, never as the value it passes.
- **Raw game arrays take the array-index column.** `JobsReader.LevelFor` indexes `PlayerState.Instance()->ClassJobLevels` with `ClassJob.ExpArrayIndex` (via `GameData.JobExpArrayIndex`), because that array is laid out by experience slot, not by RowId.

Name your variables `...RowIds` or `...ArrayIndex` so the next reader cannot confuse them, and bounds-check array-index reads the way `LevelFor` does.

Sheets are read-only static data, so reading them is far less dangerous than game memory, but keep sheet access on the framework thread paths like the rest of the codebase does unless you have measured a reason not to.

## Reading live game memory: FFXIVClientStructs

FFXIVClientStructs (ClientStructs for short) is a community-maintained set of C# struct definitions laid over the game's own memory. It is raw pointers behind `unsafe`, which is why every file that uses it is `unsafe` and framework-thread only. The pattern is always the same: get the singleton, null-check it, read:

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

(from src/Aetherphone/Core/Inventory/InventoryReader.cs). `Instance()` can return null during login, logout, and zone transitions, so the null check is mandatory, not defensive decoration. Structs the plugin reads today include `PlayerState` (ContentId, job levels), `RaptureGearsetModule` (gearsets), `InventoryManager` (equipped items), `UIState` (emote unlocks), `InfoProxyFriendList` and `GameMain` (friend list), `QuestManager` and `InstanceContent` (dailies), `HousingManager` (ward and plot), `EnvManager` (weather), and `Framework.Instance()->ClientTime` (the Eorzea clock).

## Unlock and collection state

Dalamud's `IUnlockState` service answers "does this character own this mount/minion/emote?" without hand-rolled memory reads. `CollectionsCatalogService` (src/Aetherphone/Core/Collections/CollectionsCatalogService.cs) walks the relevant sheets and asks per row: `IsMountUnlocked`, `IsCompanionUnlocked`, `IsEmoteUnlocked`, `IsOrchestrionUnlocked`, `IsCharaMakeCustomizeUnlocked`, `IsGlassesUnlocked`, `IsTripleTriadCardUnlocked`. These are framework-thread calls; that is exactly why `EnsureLocalUnlocks` checks `framework.IsInFrameworkUpdateThread` and returns `null` off-thread. Emote unlocks are also checked directly through `UIState.Instance()->IsEmoteUnlocked(emoteId)` in `PhoneEmoteController`; that accessor takes the `Emote` sheet RowId.

## Acting on the game

Reading is mostly harmless; writing is where you can grief the player. Every write path in the plugin gates itself on game state first.

**Class switching** goes through gearsets only, never through raw job changes. src/Aetherphone/Core/Jobs/GearsetActions.cs is the whole story:

```csharp
public static bool Equip(int gearsetId)
{
    var module = RaptureGearsetModule.Instance();
    if (module is null || !module->IsValidGearset(gearsetId))
    {
        return false;
    }

    return module->EquipGearset(gearsetId) == 0;
}
```

Note the return contract: `EquipGearset` returns 0 on success, so the comparison is `== 0`, not a truthiness check.

**Emotes**: `PhoneEmoteController` plays a scroll-reading emote while the phone is open and the character stands still. The feature is gated on `Configuration.ScrollWhileIdle` (default true). It prefers the looping tomescroll emote (id 295) and falls back to the one-shot tomestone emote (id 191) when tomescroll is locked, checking each through `UIState`. It refuses to fire under any of its 37 `BlockingConditions` (including `InCombat`, `Gathering`, `Crafting`, `TradeOpen`, and `Performing`), and only while the character struct's mode is `CharacterModes.Normal`. It waits until the character has been still for 400 ms (`StillnessDelayMilliseconds`), recasts on a 2500 ms cooldown (`RecastCooldownMilliseconds`), and then sends the emote's text command plus `" motion"` through `ChatSender.TrySend`. `ChatSender` (src/Aetherphone/Core/GameChat/ChatSender.cs) pushes the string into the game's own chat box via `UIModule.Instance()->ProcessChatBoxEntry`, capped at 500 UTF-8 bytes, and rejects any message whose length changes under the game's `SanitizeString` (it compares the length before and after sanitizing). Treat `ChatSender` as a loaded weapon: whatever you pass it executes as if the player typed it.

**Shortcut command steps** are the other gated caller. `ShortcutRunner` (src/Aetherphone/Core/Shortcuts/ShortcutRunner.cs) sends user-authored command steps through `ChatSender.TrySendSanitised`, the sibling that lets the game sanitize rather than refusing. Only `Command` steps touch the game, so `ShortcutRunner.NeedsGame` gates them alone and a shortcut that merely opens a plugin or a link still runs at the title screen. Before each command step the runner refuses outright when `IClientState.IsLoggedIn` is false, and otherwise holds while any of `BetweenAreas`, `BetweenAreas51`, `OccupiedInCutSceneEvent`, `WatchingCutscene`, or `WatchingCutscene78` is set, because the game silently swallows chat entries in those states. The hold is bounded by `ShortcutHold` (15 seconds) so a long cutscene abandons the run with a reported reason instead of hanging. Combat and casting are deliberately not blocked: combat macros are a legitimate use of a shortcut, unlike the idle phone emote above.

Shortcuts can also be imported from other players as a text code (`ShortcutCode`, prefix `AEPS1.`), which makes them the one path where text authored by a stranger can reach `ChatSender`. Two rules hold that line, and any future import path must keep both. First, `ShortcutCode.TryDecode` treats the payload as hostile: it caps the code length, rejects unknown step kinds, over-long commands, an empty name, and more steps than the store allows, clamps waits, drops unrecognized glyphs, and refuses any `OpenUrl` step that is not http or https, so an imported link cannot reach `file://` or a local executable the way a hand-typed one already could not. Second, importing never runs anything: the import screen lists every step in full, including the whole URL rather than just its host, and the shortcut only reaches the library when the player accepts it. Running still takes a deliberate tap afterwards.

**Friend actions** (src/Aetherphone/Core/Contacts/FriendActions.cs) use game UI "agents" (the objects behind native windows): `AgentCharaCard.Instance()->OpenCharaCard` for adventurer plates, `InfoProxyPartyInvite.Instance()->InviteToPartyContentId` for party invites, `AgentFriendlist.Instance()->OpenFriendEstateTeleportation` for estate visits.

**Teleports** are delegated to the Lifestream plugin over Dalamud IPC (inter-plugin communication: typed call gates registered by name). src/Aetherphone/Core/Venues/LifestreamBridge.cs subscribes with `Plugin.PluginInterface.GetIpcSubscriber` to `Lifestream.IsBusy`, `Lifestream.Teleport`, `Lifestream.ChangeWorldById`, `Lifestream.CanVisitSameDC`, `Lifestream.CanVisitCrossDC`, and `Lifestream.GoToHousingAddress` (the gate behind `TravelToHousingPlot` below). `TeleportToAetheryte(uint aetheryteRowId)` layers its own checks before invoking anything: Lifestream installed and loaded, not busy, and the character actually attuned to that aetheryte (scanning `Plugin.AetheryteList` for a matching `AetheryteId` with `SubIndex` 0). The IPC teleport takes the `Aetheryte` sheet RowId directly. The `/li tp <name>` chat command is not used to teleport; `LifestreamBridge.AetheryteCommand` only builds that string as a clipboard fallback when Lifestream is missing (`MapsApp.Teleport`, the Muster app, and the chat transcript's `StartTravel` fallback in src/Aetherphone/Windows/Components/ChatTranscript.cs all copy it). Two travel paths still run a command through `ICommandManager.ProcessCommand`: `TravelToAethernet` sends `/li <shard>` for in-city aethernet shards, and `Travel` sends `/li <code>` for venue travel codes.

**Zone instance sync**: `LifestreamBridge` also subscribes to `Lifestream.GetCurrentInstance`, `Lifestream.CanChangeInstance`, and `Lifestream.ChangeInstance`. The hunts app's mark detail page (`HuntsApp.Detail.cs`) arms a pending watcher (`ArmPendingInstanceSync`, mirroring `ArmPendingWorldHop`) whenever a navigate action targets a mark with a known `ZoneInstance`, regardless of whether that navigation needs a world hop, an aetheryte teleport, or the player is already in the target territory. Each `Framework.Update` tick, once the player's territory and world match the target, it compares `GetCurrentInstance()` against the mark's tracked instance and calls `ChangeInstance` when they differ and `CanChangeInstance()` allows it, retrying until it succeeds or the same timeout used for pending flags and world hops elapses.

**Choosing a destination** is `TravelPlanner` (src/Aetherphone/Core/Maps/TravelPlanner.cs), the one resolver every travel button shares. From a territory and world id (or a whole `SharedLocation`) it returns a `TravelDestination`: same world and territory means `AlreadyThere`, a foreign world means world travel, otherwise the best aetheryte or aethernet shard for that territory, taken from a lookup built once over the `Aetheryte` sheet. Housing shares resolve further: territories whose `TerritoryIntendedUse` is 13 (a ward) or 14 (any interior, so private houses, chambers, workshops, apartments, and venue rooms) map to their district through the district's `Aetheryte` column, so a share sent from inside a house travels to its ward and plot through `LifestreamBridge.TravelToHousingPlot` instead of stopping at the city aetheryte. Attunement for those is checked against the district's city aetheryte.

**Map links**: `LocationShare.OpenMap` builds a `MapLinkPayload` and calls `Plugin.GameGui.OpenMapWithMapLink`, which opens the native map with a flag, the same as clicking a `<pos>` link in chat.

## Character identity

The stable identity of a character is its **ContentId**, a `ulong` the server assigns per character. `InventoryReader.ReadLocalContentId` reads it from `PlayerState`, and `CharacterWatch` (src/Aetherphone/Core/Game/CharacterWatch.cs) polls it every `Framework.Update`, exposing `CurrentContentId` and a `Changed` event. Per-character stores (messages, linkshells, health, activity) key off this event so switching alts swaps data cleanly.

Display identity (name, home world, current world) comes from `IObjectTable.LocalPlayer` via `GameData.LocalPlayer`, `LocalHomeWorldId`, and `LocalCurrentWorldId`. `GameData.IsLocalPlayer(name, world)` matches either home or current world, which matters for players visiting other worlds. `LocationShare.Capture` combines identity with position: territory from `IClientState.TerritoryType`, map coordinates converted with the `Map` sheet's `SizeFactor` and offsets, and housing ward/plot/room from `HousingManager`. Indoors `GetCurrentWard` and `GetCurrentPlot` come back empty, so the read falls back to `GetCurrentIndoorHouseId` for the ward, plot, and room number.

## Contacts from the friend list

The friend list lives server-side; the client only has whatever `InfoProxyFriendList` last fetched. `FriendListReader` (src/Aetherphone/Core/Contacts/FriendListReader.cs) therefore has two halves:

- `RequestServerData()` asks the proxy to refresh, and refuses inside instanced content (`GameMain.Instance()->CurrentContentFinderConditionId != 0`) because the request misbehaves there.
- `Read(into, gameData)` copies the proxy's entries into `FriendEntry` records (src/Aetherphone/Core/Contacts/FriendEntry.cs), resolving world, job, and territory names through `GameData` sheet lookups, with job and location left empty for offline friends.

The Linkpearl app drives this on a cadence (src/Aetherphone/Apps/Linkpearl/LinkpearlApp.Contacts.cs): re-read every 5 seconds while idle, poll faster for a 6 second window after a refresh request, and rate-limit refresh requests to one per 5 seconds.

## Weather and time

**Reading**: `WeatherService` computes the natural forecast entirely from sheets (`TerritoryType` to `WeatherRate` to `Weather`) plus the deterministic forecast hash in `ForecastTarget`, so the natural forecast needs no game memory at all (the `Forecast` list only touches game memory to swap `LiveRenderedWeather` into the current window). `LiveRenderedWeather` reads what is actually on screen from `EnvManager.Instance()->ActiveWeather`. `EorzeaTime.CurrentSeconds` reads `Framework.Instance()->ClientTime`, honoring `EorzeaTimeOverride` when set, and falls back to a pure real-time formula if the framework pointer is null.

**Overriding** (the Skywatcher app): `WeatherControl` (src/Aetherphone/Core/Game/WeatherControl.cs) is the only code allowed to write. It sets `EnvManager.Instance()->ActiveWeather` with a short `TransitionTime`, and Eorzea time via `ClientTime.IsEorzeaTimeOverridden` plus `EorzeaTimeOverride`. Because these are client-side visual overrides on shared engine state, `WeatherControl` is defensive in layers: `CanControl` requires logged in, not `InCombat`, not `BetweenAreas`, and not `BetweenAreas51`; the per-frame `OnUpdate` reverts everything if control is lost or the app is uninstalled (its `AppGate`); `TerritoryChanged` clears overrides; and `Dispose` calls `ClearAll` so unloading the plugin never leaves the world frozen at midnight. If you add any new game-state override, copy this revert-on-everything shape.

## Phone visibility and game states

`PhoneVisibility` (src/Aetherphone/Core/PhoneVisibility.cs) is deliberately tiny: a `Func<bool>` probe that `Plugin` binds to `phoneWindow is { IsOpen: true, IsMinimized: false }`. It answers "is the phone actually on screen right now" for services that should behave differently in the background: `PollCadence` stretches network polling intervals when hidden (used by `SocialNotificationService`, `MusterStore`, `YellowPagesStore`, and others), and `PhoneEmoteController` only animates while the probe returns true.

Hiding during cutscenes is not custom code: Dalamud hides plugin windows in those states by default. The plugin's one opt-out is Group Pose: `PluginInterface.UiBuilder.DisableGposeUiHide = Cfg.ShowInGpose` in the `Plugin` constructor keeps the phone usable during photo sessions when the setting is on.

Game-state awareness beyond window hiding is condition-driven per feature, always through `ICondition` flags:

| Feature | Gate |
| --- | --- |
| Auto-open on login (`Plugin.OnAutoOpenTick`) | Waits for `LocalPlayer` present and neither `BetweenAreas` nor `BetweenAreas51` |
| Phone emote (`PhoneEmoteController`) | Blocked by 37 condition flags: combat, cutscenes, crafting, gathering, trading, performing, zone transitions, and more |
| Weather/time override (`WeatherControl.CanControl`) | Blocked in combat and zone transitions; auto-reverts |
| Shortcut command steps (`ShortcutRunner`) | Refused when logged out; held while zoning or in a cutscene, then abandoned after 15 seconds |
| Health reminders (`HealthTracker.RemindersSuppressed`) | Optionally muted in combat, duties (`BoundByDuty`), cutscenes |
| Movement tracking (`HealthTracker.Classify`) | Uses `Diving`, `Swimming`, `InFlight`, `Mounted`, `WatchingCutscene`, and more |
| Duty counting (`ActivityTracker`) | `IDutyState.DutyCompleted` event |

## Which calls are dangerous when

| API | Dangerous when | Protection in code |
| --- | --- | --- |
| `IObjectTable.LocalPlayer` | Off the framework thread, or in the plugin constructor | Deferred to `Framework.Update` (`OnAutoOpenTick`) |
| Any `*.Instance()` ClientStructs pointer | Off-thread, or during login/logout/zone change (null) | Null-check every call site, framework-thread callers only |
| `IUnlockState.Is*Unlocked` | Off the framework thread | `IsInFrameworkUpdateThread` guard in `CollectionsCatalogService` |
| `ChatSender.TrySend` | Any time, if the text is not fully under your control | Length cap, sanitization length check, unlock and condition gates upstream |
| `RaptureGearsetModule.EquipGearset` | Invalid gearset id | `IsValidGearset` first, `== 0` success check |
| `EnvManager`/`ClientTime` writes | Left set after combat, zone change, or unload | `WeatherControl` reverts on all of those |
| `RaptureAtkModule.SetUiVisibility` | Left hidden when a caller never restores, or after unload | `GameUiVisibility` reverts on `Dispose`; callers pair every `Hide` with a `Restore` on their own timeout |
| `InfoProxyFriendList.RequestData` | Inside instanced content | `CurrentContentFinderConditionId != 0` bail-out |
| Lifestream IPC | Plugin missing, mid-travel, unattuned aetheryte | `IsAvailable`, `IsBusy`, `IsAttuned` ladder in `LifestreamBridge` |
| `PluginInterface.SavePluginConfig` | Off the framework thread | `Configuration.Save` marshals via `RunOnFrameworkThread` |

## Gotchas

- The plugin constructor can run on a Dalamud loader thread. Reading `ObjectTable.LocalPlayer` or ClientStructs there is a real crash, not a theoretical one. Follow the `QueueAutoOpen`/`OnAutoOpenTick` deferral in src/Aetherphone/Plugin.cs.
- RowId and array-index columns are both small integers, so passing the wrong one compiles and returns plausible wrong data. ClientStructs accessors like `InstanceContent.IsRouletteComplete` take the sheet RowId; raw arrays like `PlayerState->ClassJobLevels` take the `ExpArrayIndex` column. See `DailiesReader.ReadDutyRoulettes` and `JobsReader.LevelFor` for the correct pairing.
- `RaptureGearsetModule.EquipGearset` returns 0 on success. `GearsetActions.Equip` compares `== 0`; a truthiness check inverts the result.
- Base classes have `JobType` 0 in the `ClassJob` sheet. Bucketing by `JobType` alone drops every base class; `JobsReader.BucketFor` falls back to the `Role` column, where role 3 covers both physical and magical ranged base classes and the `WarCategoryId` check splits them.
- `CollectionsCatalogService.EnsureLocalUnlocks` returns `null` off the framework thread by design. Callers must treat null as "try again next frame", not as "no unlocks".
- `FriendListReader.RequestServerData` returns false inside duties and when proxies are null. The friend list you read afterward is only as fresh as the last successful request; the Linkpearl app polls, it never assumes.
- Aetheryte teleports must pass the `Aetheryte` sheet RowId to the Lifestream IPC. Do not build `/li tp <name>` commands to teleport; in this codebase that string exists only as a clipboard fallback for users without the IPC available (`MapsApp.Teleport`, the Muster app's copy fallback, and `ChatTranscript.StartTravel`).
- Weather and time overrides write shared engine state. Any new writer must revert on territory change, combat, gate close, and `Dispose`, exactly as `WeatherControl` does, or players get stuck weather after unloading the plugin.
- `GameUiVisibility` writes the same global flag as the game's own "Display/Hide UI" keybind, so it also has to opt the phone out of Dalamud's automatic UI hiding (`UiBuilder.DisableUserUiHide`) or the phone vanishes along with the HUD. It records the previous value and puts it back, and it refuses to hide when the player already has their UI hidden, so restoring never turns a hidden HUD back on.
- `EorzeaTime.CurrentSeconds` silently switches to a real-time formula when `Framework.Instance()` is null (early boot). Do not treat two consecutive reads as monotonic across that boundary.
- `ChatSender.TrySend` drops messages over 500 UTF-8 bytes and any message the game's sanitizer would shorten, returning false rather than sending an altered string. Check the return value.

## Related docs

- [Getting started](getting-started.md): Dalamud and ImGui primer, building and loading the dev plugin
- [Architecture](architecture.md): plugin boot, `PhoneServices`, and the frame loop this doc's threading rule lives in
- [App framework](app-framework.md): how apps receive these services and the `AppGate` install gates
- [State and persistence](state-and-persistence.md): the ContentId-keyed per-character stores fed by `CharacterWatch`
- [Notifications](notifications.md): how visibility and game state shape notification delivery
