# State and persistence

This page explains where Aetherphone keeps every kind of data: plugin settings, per-character stores, session tokens, and media files on disk. Read it before you add any new piece of state to an app, so you know whether it belongs in the shared `Configuration` object, in a per-character file, or on the server. Everything here is client-side; the Aethernet backend is a separate service covered in [Networking](networking.md).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Configuration.cs | The single Dalamud plugin config object, plus its one-shot migration methods |
| src/Aetherphone/Core/ConfigMigrations.cs | Raw JSON type-name rewrites that run before the config is deserialized |
| src/Aetherphone/Plugin.cs | Boot order: run `ConfigMigrations.Run`, load the config, run the flag migrations |
| src/Aetherphone/Core/PhoneServices.cs | Wires every store and decides which disk folder each one uses |
| src/Aetherphone/Core/Game/CharacterWatch.cs | Polls the local character's ContentId and raises `Changed` on switches |
| src/Aetherphone/Core/Aethernet/AethernetSession.cs | Active account state; stashes and loads per-character session snapshots |
| src/Aetherphone/Core/Aethernet/CharacterSessionManager.cs | Drives session switching every frame from the played character |
| src/Aetherphone/Core/Aethernet/CharacterSession.cs | The serialized per-character token and key-cache snapshot |
| src/Aetherphone/Core/Photos/PhotoLibrary.cs | Photo storage, `AEP_` naming, import and delete |
| src/Aetherphone/Core/Photos/ScreenshotImportService.cs | Watches screenshot folders and copies new captures into the library |
| src/Aetherphone/Core/Linkpearl/MessageArchive.cs | Per-character /tell history files on disk |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Bundled plus user custom ringtone and notification sounds |
| src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs | Built-in plus imported custom wallpapers |
| src/Aetherphone/Core/Net/DiskCache.cs | Size-budgeted disk cache used for media, images, audio, and collections |

## The Dalamud config model

Dalamud (the plugin framework that hosts Aetherphone inside FFXIV) gives every plugin one config object and one config directory:

- `Configuration` in src/Aetherphone/Configuration.cs implements Dalamud's `IPluginConfiguration`. It is a single flat class of properties: booleans, numbers, strings, lists, and dictionaries.
- Dalamud serializes the whole object to a single JSON file (`PluginInterface.ConfigFile`) and gives the plugin a private folder for everything else (`PluginInterface.ConfigDirectory`). This doc calls that folder `<config>`.
- The plugin loads it once at boot in the `Plugin` constructor (src/Aetherphone/Plugin.cs): `PluginInterface.GetPluginConfig() as Configuration ?? new Configuration()`. The loaded instance is exposed as the static `Plugin.Cfg`.

Saving goes through `Configuration.Save()`:

```csharp
public void Save()
{
    if (Plugin.Framework.IsInFrameworkUpdateThread)
    {
        Plugin.PluginInterface.SavePluginConfig(this);
        return;
    }

    _ = Plugin.Framework.RunOnFrameworkThread(() => Plugin.PluginInterface.SavePluginConfig(this));
}
```

The framework thread is the game's main update thread; the plugin funnels all config writes through it. If you call `Save()` from a background task it defers the write to the next framework tick instead of blocking. `SaveNow()` writes immediately on the current thread and is the exception, not the rule.

What belongs in `Configuration`:

- Small, durable settings: toggles, volumes, ids of selected things, seen-timestamps.
- Small lists of small records (`Notes`, `Alarms`, `MarketFavorites`, `CustomWallpapers`).
- Nothing large. The entire object is deserialized at boot and rewritten in full on every `Save()`. Chat history, per-character snapshots, and binary media all live in files under `<config>` instead (see below).

The usual mutation pattern in app code is: change the property, then call `Plugin.Cfg.Save()` (or `configuration.Save()` where the instance was injected).

## Schema migrations

There are three tiers of migration, from cheapest to heaviest. Pick the lightest one that works.

**1. Add a property with a default.** New properties deserialize to their initializer when missing from old JSON. `public bool ShowAppNames { get; set; } = true;` needs no migration at all. This is the normal case.

**2. One-shot flag migrations.** When existing data must be transformed once (ids renamed, values moved, a layout repacked), add a `bool ...Migrated` (or `...Initialized`) property plus a `Migrate...()` method on `Configuration`, and call it from the `Plugin` constructor next to the existing calls (`MigrateSoundSettings`, `MigrateChangelogSeen`, `MigrateMessage`, `MigrateMessagesMerge`, `MigrateSetupCompleted`, `MigrateControlPanelRepack`, `MigrateCharacterSessions`). The pattern, verbatim from src/Aetherphone/Configuration.cs:

```csharp
public void MigrateControlPanelRepack()
{
    if (ControlPanelRepacked)
    {
        return;
    }

    ControlPanel = null;
    ControlPanelRepacked = true;
    Save();
}
```

The guard flag makes the migration idempotent: it runs once per install, ever, and every later boot returns early. Note that `Configuration` has an `int Version` property because `IPluginConfiguration` requires one, but no code branches on it; the flags above are the actual mechanism.

**3. Type-name rewrites.** The saved JSON embeds assembly-qualified .NET type names for the record types stored in config lists (for example `Aetherphone.Core.Message.StarredMessage, Aetherphone`). If you move or rename such a `[Serializable]` type, old config files still reference the old name and fail to load those entries. `ConfigMigrations` (src/Aetherphone/Core/ConfigMigrations.cs) fixes this by rewriting the raw JSON text before deserialization: the `Plugin` constructor calls `ConfigMigrations.Run(PluginInterface.ConfigFile)` before `GetPluginConfig()`. It backs the file up to `*.pre-migration.bak` once, writes to a temp file, then swaps it in.

When to add one: only when you move a type that is serialized inside `Configuration` to a new namespace. Add an `(Old, New)` pair to `ConfigMigrations.TypeRenames`. The existing entries (for example `Aetherphone.Apps.Notes.PhoneNote` to `Aetherphone.Core.Notes.PhoneNote`) exist because those types moved from `Apps` namespaces into `Core`.

Never rename or repurpose an existing property in place. Old installs will silently lose or misread the value. Add a new property, migrate the old one across with a tier-2 migration, and leave the old property to rot (or keep it as a `Legacy...` field like `LegacyUnclaimedToken`).

## Per-character data and ContentId

A ContentId is the game's stable 64-bit id for one character. It survives renames and world transfers, which makes it the key for everything per-character. Two services observe it every frame:

- `CharacterWatch` (src/Aetherphone/Core/Game/CharacterWatch.cs) polls `InventoryReader.ReadLocalContentId()` on each framework update and raises `Changed(ulong)` when the logged-in character changes (0 means logged out).
- `CharacterSessionManager` does the same poll to drive account switching (next section).

Per-character state comes in two shapes:

**Dictionaries inside `Configuration`, keyed by `ulong` ContentId.** Used when the per-character payload is small:

- `JobsCategoriesByCharacter` (custom gearset categories)
- `MutedLinkshellsByCharacter` (per-character linkshell mutes, loaded by `LinkshellMuteStore` on every `CharacterWatch.Changed`)
- `CharacterSessions` (account session snapshots, see below)

**Per-character files under `<config>`.** Used for anything that grows:

| Location | Contents | Owner |
| --- | --- | --- |
| `<config>/Messages/<contentid>/` (lowercase hex, one SHA-256-named JSON per conversation) | /tell history, capped at 500 lines per conversation | `MessageArchive` |
| `<config>/Activity/<CONTENTID>.json` (uppercase hex) | Activity app tracking | `ActivityStore` (src/Aetherphone/Core/Activity/ActivityStore.cs) |
| `<config>/Health/<CONTENTID>.json` (uppercase hex) | Health tracker samples | `HealthStore` (src/Aetherphone/Core/Health/HealthStore.cs) |
| `<config>/cache/inventory/<contentid>.json` (lowercase hex) | Inventory snapshots | `InventoryStore` (src/Aetherphone/Core/Inventory/InventoryStore.cs) |

Stores that hold per-character data subscribe to `CharacterWatch.Changed`, drop their in-memory state, and reload from the new character's slot. `LinkshellMuteStore.OnCharacterChanged` shows the full pattern, including a one-shot migration from the old global list to the per-character dictionary:

```csharp
private void OnCharacterChanged(ulong id)
{
    contentId = id;
    if (id != 0 && !configuration.LinkshellMutesPerCharacterMigrated)
    {
        if (configuration.MutedLinkshells.Count > 0)
        {
            configuration.MutedLinkshellsByCharacter[id] = new List<string>(configuration.MutedLinkshells);
            configuration.MutedLinkshells = new List<string>();
        }

        configuration.LinkshellMutesPerCharacterMigrated = true;
        configuration.Save();
    }

    muted = configuration.MutedLinkshellsByCharacter.TryGetValue(id, out var list)
        ? new HashSet<string>(list, StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    Changed?.Invoke();
}
```

`MessageStore` plus `MessagesPerCharacterMigrated` follows the same shape for /tell history.

## Accounts, sessions, and the reset contract

Aethernet accounts are also keyed by ContentId. The moving parts:

- `Configuration.CharacterSessions` is a `Dictionary<ulong, CharacterSession>`. Each `CharacterSession` snapshot stores that character's API token, encryption key cache, and display metadata (handle, name, world, avatar URL).
- `AethernetSession` holds the *active* account in the flat config fields `AethernetToken`, `EncryptionKeyCache`, and `EncryptionKeyCacheUserId`. `SwitchTo(ulong)` stashes the current flat fields back into the dictionary (`StashActive`), then loads the target snapshot (`LoadFlat`) or clears them (`ClearFlat`), and fires the `Changed` event.
- `CharacterSessionManager.OnTick` notices character switches and calls `session.SwitchTo(session.ResolveTarget(contentId))`.
- The manual account switcher lives in `AethernetSession.PinAccount`, `UseCharacterAccount`, and `ForgetAccount`, backed by `Configuration.FollowCharacterAccount` and `PinnedAccountContentId`. `AccountSelection.Target` (src/Aetherphone/Core/Aethernet/AccountSelection.cs) resolves which slot wins: follow the played character by default, or stay pinned to one account as long as its stored token exists.

**The reset contract.** Any store that caches account-scoped server data must subscribe to `AethernetSession.Changed`, compare `session.CurrentUser?.Id` against a remembered `lastAccountId`, and clear every cached list, cursor, and id when it differs. See `SocialFeedStore.OnSessionChanged` (src/Aetherphone/Core/Social/SocialFeedStore.cs) and `ChatThreadStoreBase.OnSessionAccountChanged` (src/Aetherphone/Core/Message/ChatThreadStoreBase.cs) for reference implementations, and the subscriber list in `StoryStore`, `MusterStore`, `YellowPagesStore`, `AdInquiryStore`, `ContactBook`, `KeyVault`, and `SocialNotificationService`. If your new store skips this, switching alts shows the previous account's data.

Note the two different triggers: `CharacterWatch.Changed` fires on *character* switches (local, offline data), while `AethernetSession.Changed` fires on *account* switches, sign-in, and sign-out (server data). Pinning an account means the character can change while the account does not, so pick the right event for what you cache.

## Media files on disk

All user media lives under `<config>` next to the config file. Bundled read-only assets ship beside the plugin assembly (`Plugin.PluginInterface.AssemblyLocation.DirectoryName`) in `Wallpapers` and `Sounds`; user copies never overwrite them.

| Location | Contents |
| --- | --- |
| `<config>/Photos/` | The photo library: camera saves and imported screenshots |
| `<config>/Photos/.thumbs/` | JPEG thumbnails, one per photo (`PhotoLibrary.ThumbnailPathFor`) |
| `<config>/Sounds/Ringtones/`, `<config>/Sounds/Notifications/` | User custom sounds (mp3, wav), copied in by `SoundLibrary.AddUserFile` |
| `<config>/Wallpapers/` | Imported wallpapers, named `custom-<guid>` by `WallpaperLibrary.AddCustom` |
| `<config>/cache/media/`, `.../images/`, `.../audio/`, `.../collections/` | `DiskCache` folders with byte budgets of 64, 128, 256, and 32 MB (set in `PhoneServices.Build`) |

**Photos and the `AEP_` prefix.** `PhotoLibrary.Save` names camera captures `AEP_yyyyMMdd_HHmmss_fff.png`. `PhotoLibrary.Import` *copies* (never moves) an external file into the library under the same `AEP_` pattern, stamping the name from the taken timestamp its caller passes in and probing up to 100 millisecond offsets to avoid collisions. `ScreenshotImportService` feeds it: while `Configuration.ImportScreenshots` is on, it watches the game's screenshot folder plus ReShade and GShade save paths, waits for each new file to finish writing, then imports it with the file's last write time as the taken timestamp. The Photos app derives a photo's taken-date by parsing that name (`ResolveTaken` in src/Aetherphone/Apps/Photos/PhotosApp.cs), falling back to the file's write time.

**Custom sounds.** `SoundLibrary` merges bundled and user folders; config properties like `RingtoneSound` store a token of the form `file:<name>` (`SoundTokens.FilePrefix`), never a path. `TryResolvePath` prefers the user folder over the bundled one for the same file name.

**Custom wallpapers.** The image file goes to `<config>/Wallpapers/` and a small `CustomWallpaper` record (id, file name, crop) is appended to `Configuration.CustomWallpapers`. Deleting removes both.

**Caches are disposable.** `DiskCache.Set` enforces the byte budget by deleting the oldest files. Anything under `<config>/cache/` can vanish at any time; never keep the only copy of something there.

## Server-side vs local

The Aethernet backend (a separate ASP.NET service in its own repository) is the source of truth for everything shared between players: accounts and profiles, social posts and comments, encrypted chat threads and messages, uploaded media, musters, ads, and moderation state. The client persists only what it needs to reconnect and decrypt: per-character tokens and encryption key caches in `Configuration.CharacterSessions`, plus size-budgeted disk caches of downloaded media. Server-backed stores such as `ChatThreadStoreBase` and `SocialFeedStore` keep their data in memory only and refetch after a restart or account switch; they write nothing to disk. See [Networking](networking.md) for the API client, realtime signals, and encryption.

## Rules of thumb for app authors

- New small setting or favorite list: add a property to `Configuration` with a sensible default, call `Save()` after mutating. No migration needed.
- New per-character setting, small: a `Dictionary<ulong, ...>` on `Configuration` keyed by ContentId, reloaded from a `CharacterWatch.Changed` handler.
- Growing or per-character bulky data: a JSON file per ContentId under a new `<config>` subfolder, following `ActivityStore` or `MessageArchive`. Write via a temp file and `File.Move(temp, path, true)` so a crash cannot truncate it.
- Binary media the user created: a `<config>` subfolder with a stable naming scheme, plus (if it needs settings) a small record list in `Configuration`, like wallpapers.
- Downloaded, refetchable bytes: a `DiskCache` under `<config>/cache/`.
- Shared or account-scoped data: it belongs on the server; the client store keeps it in memory and obeys the `AethernetSession.Changed` reset contract.
- Keep `Configuration` compact: it is one JSON file rewritten in full on every save of any setting.
- Migrate, never rename in place: new property plus a one-shot flag migration for value changes, a `ConfigMigrations.TypeRenames` entry for type moves.

## Gotchas

- The config JSON embeds assembly-qualified type names for serialized record types. Moving a `[Serializable]` type stored in `Configuration` to another namespace silently drops users' data unless you add a `ConfigMigrations.TypeRenames` pair. Every existing entry in that table is a scar from a real move.
- `Configuration.Save()` called off the framework thread is fire-and-forget: the write happens on a later framework tick. Do not assume the file is on disk when the call returns.
- ContentId hex casing is inconsistent across stores: `MessageArchive` and `InventoryStore` format with `"x16"` (lowercase), `ActivityStore` and `HealthStore` with `"X16"` (uppercase). Copy the exact store you are following, and do not expect folder names to match across features.
- `MessageArchive` caps each conversation at 500 stored lines (`MaxStoredLines`) and names files with a SHA-256 hash of the send target, so you cannot map a file back to a conversation by eye.
- The `AEP_` file name is load-bearing twice: `PhotoLibrary.List` sorts by name descending (newest first) and `PhotosApp.ResolveTaken` parses the date out of it. Renaming files in the Photos folder breaks both ordering and date grouping.
- `PhotoLibrary.Save` encodes and writes the PNG on a background `Task.Run` with no completion signal; a screenshot taken immediately before quitting the game can be lost.
- `ChatThreadStoreBase.OnSessionAccountChanged` returns early when the new account id is null, so a plain sign-out does not clear thread caches; only a *different* signed-in account does. `SocialFeedStore.OnSessionChanged` has no null guard and clears on sign-out too. Know which behavior you are copying.
- `AethernetSession.StashActive` skips creating a snapshot when there is no token and no key cache, so an anonymous character leaves no `CharacterSessions` entry at all. Do not treat a missing dictionary entry as an error.

## Related docs

- [Architecture](architecture.md): plugin boot order and the frame loop that `Save()` defers to
- [App framework](app-framework.md): the `IPhoneApp` contract your persisted state hangs off
- [Creating an app](creating-an-app.md): where a new app's settings slot into `Configuration`
- [Networking](networking.md): the Aethernet client, auth tokens, and encryption key handling
- [Messaging and chat](messaging-and-chat.md): the chat stores that follow the session reset contract
- [Notifications](notifications.md): `AppNotificationSetting` and per-app sound overrides stored in config
- [Assets and media](assets-and-media.md): bundled fonts, sounds, wallpapers, and cases that ship with the plugin
- [Game integration](game-integration.md): the framework thread and other Dalamud services
- [Conventions](conventions.md): code style rules the samples here follow
