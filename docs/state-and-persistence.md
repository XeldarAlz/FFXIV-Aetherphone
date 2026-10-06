# State and persistence

This page explains where Aetherphone keeps every kind of data: plugin settings, per-character stores, session tokens, and media files on disk. Read it before you add any new piece of state to an app, so you know whether it belongs in the shared `Configuration` object, in a per-character file, or on the server. Everything here is client-side; the Aethernet backend is a separate service covered in [Networking](networking.md).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Configuration.cs | The single Dalamud plugin config object, plus its one-shot migration methods |
| src/Aetherphone/Core/Config/ConfigMigrations.cs | Raw JSON type-name rewrites that run before the config is deserialized |
| src/Aetherphone/Plugin.cs | Boot order: run `ConfigMigrations.Run`, load the config, run the `Migrate...` calls |
| src/Aetherphone/Core/PhoneServices.cs | Wires most stores and picks their disk folders; a few apps resolve their own folder (see [Media files on disk](#media-files-on-disk)) |
| src/Aetherphone/Core/Game/CharacterWatch.cs | Polls the local character's ContentId and raises `Changed` on switches |
| src/Aetherphone/Core/Aethernet/AethernetSession.cs | Active account state; stashes and loads per-character session snapshots |
| src/Aetherphone/Core/Aethernet/CharacterSessionManager.cs | Drives session switching every frame from the played character |
| src/Aetherphone/Core/Aethernet/CharacterSession.cs | The serialized per-character token and key-cache snapshot |
| src/Aetherphone/Core/Crypto/KeyVault.cs | End-to-end key handling; reads and writes `Configuration.EncryptionKeysByUserId` |
| src/Aetherphone/Core/Crypto/DecryptedHistoryStore.cs | Per-account sealed file of message bodies this device already decrypted |
| src/Aetherphone/Core/Photos/PhotoLibrary.cs | Photo storage, `AEP_` naming, import, edited copies, and delete |
| src/Aetherphone/Core/Media/PhotoEdit.cs, PhotoEditor.cs | The edit recipe (orientation, straighten, adjustments, looks) and the scalar pixel pipeline that applies it |
| src/Aetherphone/Core/Photos/ScreenshotImportService.cs | Watches screenshot folders and copies new captures into the library |
| src/Aetherphone/Core/GameChat/ChatArchive.cs | Per-character game chat history on disk, one file per conversation |
| src/Aetherphone/Core/GameChat/MessageArchive.cs | The legacy /tell archive, read once to migrate into ChatArchive |
| src/Aetherphone/Core/Songs/LibraryStore.cs | The Music library file, moved out of `Configuration` |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Bundled plus user custom ringtone and notification sounds |
| src/Aetherphone/Core/Wallpapers/WallpaperLibrary.cs | Built-in plus imported custom wallpapers |
| src/Aetherphone/Core/Net/DiskCache.cs | Size-budgeted disk cache, optionally DPAPI-sealed, for media, images, audio, lyrics, strats, and collections |

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
        SaveNow();
        return;
    }

    _ = Plugin.Framework.RunOnFrameworkThread(SaveNow);
}
```

The framework thread is the game's main update thread; the plugin funnels all config writes through it. If you call `Save()` from a background task it defers the write to the next framework tick instead of blocking. `SaveNow()` is the single actual writer: `Save()` calls it directly when already on the framework thread and defers it otherwise. It wraps `SavePluginConfig` in a try/catch and logs "Configuration save failed; settings changed this session may be lost" instead of throwing, so a failed write is swallowed and only visible in the log.

When a write must land before the caller continues, `await configuration.SaveNowAsync()`: it runs `SaveNow` on the framework thread (directly when already there) and returns a task that completes after the write. `KeyVault` uses it so a new encryption key is on disk before it is published. `Plugin.Dispose` calls `SaveNow()` directly as its last step.

What belongs in `Configuration`:

- Small, durable settings: toggles, volumes, ids of selected things, seen-timestamps.
- Small lists of small records (`Notes`, `Alarms`, `MarketFavorites`, `CustomWallpapers`).
- Nothing large. The entire object is deserialized at boot and rewritten in full on every `Save()`. Chat history, per-character snapshots, and binary media all live in files under `<config>` instead (see below).

The usual mutation pattern in app code is: change the property, then call `Plugin.Cfg.Save()` (or `configuration.Save()` where the instance was injected).

## Schema migrations

There are three tiers of migration, from cheapest to heaviest. Pick the lightest one that works.

**1. Add a property with a default.** New properties deserialize to their initializer when missing from old JSON. `public bool ShowAppNames { get; set; } = true;` needs no migration at all. This is the normal case.

**2. One-shot migrations.** When existing data must be transformed once (ids renamed, values moved, a layout repacked), add a `Migrate...()` method on `Configuration` with a guard that makes it run once, and call it from the `Plugin` constructor next to the existing `Cfg.Migrate...()` calls, which run right after `Cfg.NormalizeAethernetBaseUrl()`. That block in src/Aetherphone/Plugin.cs is the source of truth for which migrations run and in which order. Guards come in a few shapes:

- A `bool` flag property (`...Migrated`, `...Repacked`, `...Applied`, `...Initialized`, `...Cleared`), the common case, shown below.
- A value sentinel, when the pre-migration state is unmistakable: `MigrateChirperMediaFilters` and `MigratePhoneWidth` guard on the migrated property itself (`ChirperShowMediaPosts` still false, `PhoneWidth` still 0), and `MigrateHomeLooks` runs only while `Looks` is empty.
- No guard at all, for an idempotent rewrite that saves only when something changed: `MigrateRetiredSounds` and `MigrateRetiredWallpapers` map retired ids to their replacements (`RetiredSounds.Replace`, `BuiltInWallpapers.Replace`) on every boot.
- A flag plus an input computed before load: `MigrateUiSoundDefaults(freshInstall)` receives whether the config file was missing before `GetPluginConfig()`, so it can turn interface sounds on for fresh installs only.

The flag-guarded pattern, verbatim from src/Aetherphone/Configuration.cs:

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

**3. Type-name rewrites.** The saved JSON embeds assembly-qualified .NET type names for the record types stored in config lists (for example `Aetherphone.Core.Message.StarredMessage, Aetherphone`). If you move or rename such a `[Serializable]` type, old config files still reference the old name and fail to load those entries. `ConfigMigrations` (src/Aetherphone/Core/Config/ConfigMigrations.cs) fixes this by rewriting the raw JSON text before deserialization: the `Plugin` constructor calls `ConfigMigrations.Run(PluginInterface.ConfigFile)` before `GetPluginConfig()`. It backs the file up to `*.pre-migration.bak` once, writes to a temp file, then swaps it in.

When to add one: only when you move a type that is serialized inside `Configuration` to a new namespace. Add an `(Old, New)` pair to `ConfigMigrations.TypeRenames`. The existing entries (for example `Aetherphone.Apps.Notes.PhoneNote` to `Aetherphone.Core.Notes.PhoneNote`) exist because those types moved from `Apps` namespaces into `Core`.

Never rename or repurpose an existing property in place. Old installs will silently lose or misread the value. Add a new property, migrate the old one across with a tier-2 migration, and leave the old property to rot (or keep it as a `Legacy...` field like `LegacyUnclaimedToken`). To give a property a new C# name without losing what is saved under the old one, pin the old JSON key: `LegacyShowWalletBadge` carries `[JsonProperty("ShowWalletBadge")]` (and the Dailies and Activity twins do the same), so it still reads the old key, and `MigrateBadgeSettings` carries the value into `BadgeSettings`.

**Moving data out of `Configuration`.** When a list outgrows the config, give it its own file and import the old fields once from the new store. `LibraryStore` (src/Aetherphone/Core/Songs/LibraryStore.cs) is the reference: Music playlists and recents used to live in `Configuration.Playlists` and `SongRecents`. `LibraryStore` now owns `<config>/Music/library.json`, calls `MigrateLegacy(configuration)` only when that file does not exist yet, and clears the old config lists only after the new file was written.

## Per-character data and ContentId

A ContentId is the game's stable 64-bit id for one character. It survives renames and world transfers, which makes it the key for everything per-character. Two services observe it every frame:

- `CharacterWatch` (src/Aetherphone/Core/Game/CharacterWatch.cs) polls `InventoryReader.ReadLocalContentId()` on each framework update and raises `Changed(ulong)` when the logged-in character changes (0 means logged out).
- `CharacterSessionManager` does the same poll to drive account switching (next section).

Per-character state comes in two shapes:

**Dictionaries inside `Configuration`, keyed by `ulong` ContentId.** Used when the per-character payload is small:

- `JobsCategoriesByCharacter` (custom gearset categories)
- `LookByCharacter` (which saved Look, a bundle of home layout, theme, accent, case and wallpapers, each character uses; `HomeLookService` in src/Aetherphone/Core/Home/ swaps the flat appearance fields when the character changes, the same mirror pattern `AethernetSession` uses for account slots)
- `MutedLinkshellsByCharacter` (legacy per-character linkshell mutes; nothing writes it anymore, and its only reader is `TabStore.ReadLegacyMutes`, which seeds mute state for newly created Linkpearl tabs)
- `NameplateTitleByCharacter` (each character's nameplate title settings, `NameplateTitleService` in src/Aetherphone/Core/Honorific/)
- `PendingCasinoSittings`, `CasinoSittingSeenAtUnix`, and `PendingCasinoRounds` (casino sittings and rounds still open on the server, so they can be settled after a reload; `CasinoStore` and `CasinoPlayStore` key them by the active account slot, `AethernetSession.ActiveContentId`, not by the played character)
- `CharacterSessions` (account session snapshots, see below)

**Per-character files under `<config>`.** Used for anything that grows:

| Location | Contents | Owner |
| --- | --- | --- |
| `<config>/GameChat/<contentid>/` (lowercase hex, one SHA-256-named JSON per conversation stream) | Game chat history, capped at `ChatLog.MaxLinesPerStream` (2000) lines per stream. Written only while `Configuration.ArchiveTellsToDisk` is on (default on; despite the name it gates every channel) and the stream's `HistoryPolicy` (`LinkpearlHistory`, overridden per channel by `LinkpearlHistoryByChannel`, default `Days30`) is `Days30` or `Forever`. `Off` and `Session` streams stay in memory and their files are deleted on load; `Days30` drops lines older than 30 days on load | `ChatArchive` (src/Aetherphone/Core/GameChat/ChatArchive.cs) |
| `<config>/Messages/<contentid>/` (lowercase hex, same SHA-256 naming) | Legacy /tell history (500-line cap); read once per character by `ChatArchive.MigrateLegacyTells`, never written anymore | `MessageArchive` (legacy) |
| `<config>/Activity/<CONTENTID>.json` (uppercase hex) | Activity app tracking | `ActivityStore` (src/Aetherphone/Core/Activity/ActivityStore.cs) |
| `<config>/Health/<CONTENTID>.json` (uppercase hex) | Health tracker samples | `HealthStore` (src/Aetherphone/Core/Health/HealthStore.cs) |
| `<config>/Wallet/<CONTENTID>.json` (uppercase hex) | Currency balances, the change log, and daily closing balances | `WalletHistoryStore` (src/Aetherphone/Core/Wallet/WalletHistoryStore.cs) |
| `<config>/Collections/<CONTENTID>.json` (uppercase hex) | Owned-unlock baseline, recent unlocks, and pins | `CollectionLedgerStore` (src/Aetherphone/Core/Collections/CollectionLedgerStore.cs) |
| `<config>/cache/inventory/<contentid>.json` (lowercase hex) | Inventory snapshots | `InventoryStore` (src/Aetherphone/Core/Inventory/InventoryStore.cs) |

Subscribers to `CharacterWatch.Changed` include `ChatArchive` and `TabStore` (both in src/Aetherphone/Core/GameChat/), `HomeLookService` (src/Aetherphone/Core/Home/), and `HuntsService` (src/Aetherphone/Core/Hunts/). The others reach per-character state differently: `HealthTracker`, `WalletService`, `DailiesTracker`, `GameTimers`, and the Jobs app read `CharacterWatch.CurrentContentId` when they need it (Health swaps profiles on its own sample tick when the id changes), while `ActivityTracker`, `CollectionsJournal`, and the inventory capture path read the current ContentId straight from game state on their own ticks and receive no `CharacterWatch` at all. `ChatArchive.OnCharacterChanged` shows the reload pattern, verbatim from src/Aetherphone/Core/GameChat/ChatArchive.cs:

```csharp
private void OnCharacterChanged(ulong contentId)
{
    Flush();
    log.Clear();
    lock (sync)
    {
        dirty.Clear();
        activeRoot = null;
        if (contentId == 0)
        {
            return;
        }

        var directory = new DirectoryInfo(Path.Combine(baseDir.FullName, contentId.ToString("x16")));
        if (!directory.Exists)
        {
            directory.Create();
        }

        activeRoot = directory;
        lastFlushMilliseconds = Environment.TickCount64;
    }

    MigrateLegacyTells(contentId);
    Load();
}
```

The shape to copy: flush the outgoing character's dirty state first, drop the in-memory state, treat 0 as logged out (no active root, nothing loaded), then point at the new character's folder and reload. `MigrateLegacyTells` is the one-shot import half of the handler: guarded by the `Configuration.LinkpearlMigratedCharacters` list, it reads the legacy `MessageArchive` files once per character and rewrites them as ChatArchive streams. The character is marked done before the import runs, so if `ArchiveTellsToDisk` is off at that moment the import is skipped for good. `TabStore.ReadLegacyMutes` is the minimal version of the same event: it just re-reads `Configuration.MutedLinkshellsByCharacter` for the new character so newly created tabs inherit the old mutes.

## Accounts, sessions, and the reset contract

Aethernet accounts are also keyed by ContentId. The moving parts:

- `Configuration.CharacterSessions` is a `Dictionary<ulong, CharacterSession>`. Each `CharacterSession` snapshot stores that character's API token, a copy of its encryption key cache, and display metadata (handle, name, world, avatar URL).
- `Configuration.EncryptionKeysByUserId` is the authoritative store for end-to-end private keys: one protected blob per Aethernet account user id, sealed by `LocalKeyProtector` (Windows DPAPI for the current user, falling back to plain base64 where DPAPI is unavailable). `KeyVault` reads it first (falling back to the flat `EncryptionKeyCache` when its user id matches) and writes it on every key change. The flat `EncryptionKeyCache` fields and the session snapshots are mirrors, and a replaced key is kept in `EncryptionRetiredKeysByUserId` so chats sealed to it still open. `MigrateEncryptionKeyStore` seeded the dictionary once from the flat cache, the legacy unclaimed key, and every session snapshot.
- `AethernetSession` holds the *active* account in the flat config fields `AethernetToken`, `EncryptionKeyCache`, and `EncryptionKeyCacheUserId`. `SwitchTo(ulong)` stashes the current flat fields back into the dictionary (`StashActive`), then loads the target snapshot (`LoadFlat`) or clears them (`ClearFlat`), and fires the `Changed` event.
- `CharacterSessionManager.OnTick` notices character switches and calls `session.SwitchTo(session.ResolveTarget(contentId))`.
- The manual account switcher lives in `AethernetSession.PinAccount`, `UseCharacterAccount`, and `ForgetAccount`, backed by `Configuration.FollowCharacterAccount` and `PinnedAccountContentId`. `AccountSelection.Target` (src/Aetherphone/Core/Aethernet/AccountSelection.cs) resolves which slot wins: follow the played character by default, or stay pinned to one account as long as its stored token exists.

**The reset contract.** Any store that caches account-scoped server data must subscribe to `AethernetSession.Changed`, compare `session.CurrentUser?.Id` against a remembered `lastAccountId`, and clear every cached list, cursor, and id when it differs. See `SocialFeedStore.OnSessionChanged` (src/Aetherphone/Core/Social/SocialFeedStore.cs) and `ChatThreadStoreBase.OnSessionAccountChanged` (src/Aetherphone/Core/Message/ChatThreadStoreBase.cs) for reference implementations; `git grep -i "session.changed +="` lists every subscriber (`AdInquiryStore` and the other chat stores get it by inheriting `ChatThreadStoreBase`). If your new store skips this, switching alts shows the previous account's data.

Note the two different triggers: `CharacterWatch.Changed` fires on *character* switches (local, offline data), while `AethernetSession.Changed` fires on *account* switches, sign-in, and sign-out (server data). Pinning an account means the character can change while the account does not, so pick the right event for what you cache.

## Media files on disk

All user media lives under `<config>` next to the config file, along with the other files in the table below. Bundled read-only assets ship beside the plugin assembly (`Plugin.PluginInterface.AssemblyLocation.DirectoryName`) in `Wallpapers` and `Sounds`; user copies never overwrite them. Most folders are chosen in `PhoneServices.Build`; a few owners resolve their own from `Plugin.PluginInterface.ConfigDirectory` instead (`PhotoLibrary` in `AppRegistry`, Velvet's not-interested archive in `VelvetShell`, `CalendarEvents`, the Music catalog and `PlaylistCovers`, `DoomAssets`, and `MediaDependencies`).

| Location | Contents |
| --- | --- |
| `<config>/Photos/` | The photo library: camera saves and imported screenshots |
| `<config>/Photos/.thumbs/` | JPEG thumbnails, one per photo (`PhotoLibrary.ThumbnailPathFor`) |
| `<config>/Photos/.trash/` | Recently Deleted: `PhotoLibrary.Delete` moves photos here and stamps the file creation time; `PurgeExpired` removes them 30 days later, `Restore` moves them back |
| `<config>/Sounds/Ringtones/`, `<config>/Sounds/Notifications/` | User custom sounds (mp3, wav), copied in by `SoundLibrary.AddUserFile` |
| `<config>/Wallpapers/` | Imported wallpapers, named `custom-<guid>` by `WallpaperLibrary.AddCustom` |
| `<config>/ShortcutIcons/` | Custom Shortcuts icons (`ShortcutIconLibrary`), listed by `Configuration.CustomShortcutIconIds` |
| `<config>/Music/library.json`, `<config>/Music/downloads/`, `<config>/Music/covers/` | The Music library (`LibraryStore`), offline downloads (`DownloadStore`), and playlist cover art (`PlaylistCovers`); the Music catalog keeps its `catalog.json` in the same folder |
| `<config>/History/` | Readable chat history: one sealed `.dat` per account (`DecryptedHistoryStore`, see [Server-side vs local](#server-side-vs-local)) |
| `<config>/Velvet/` | Velvet's per-account not-interested list, one JSON per account (`VelvetNotInterestedArchive`) |
| `<config>/calendar_events.json` | The Calendar events cache (`CalendarEvents`) |
| `<config>/doom/` | Doom WADs and the soundfont, downloaded on demand (`DoomAssets`) |
| `<config>/aetherstream/` | AetherStream's downloaded media dependencies (`MediaDependencies`) |
| `<config>/cache/media/`, `.../images/`, `.../audio/`, `.../lyrics/`, `.../strats/`, `.../collections/` | `DiskCache` folders with byte budgets of 64, 128, 256, 16, 24, and 32 MB (set in `PhoneServices.Build`). `media` and `images` are built with `protect: true`, so their files are DPAPI-sealed for the current Windows user |
| `<config>/cache/housing/`, `<config>/cache/lodestone-ids.tsv`, `<config>/cache/audio/resolver` | Other caches: housing data, the Lodestone id index (`LodestoneService`), and the song link resolver (`SongLinkResolver`) |

**Photos and the `AEP_` prefix.** `PhotoLibrary.Save` names camera captures `AEP_yyyyMMdd_HHmmss_fff.png`. `PhotoLibrary.Import` *copies* (never moves) an external file into the library under the same `AEP_` pattern, stamping the name from the taken timestamp its caller passes in and probing up to 100 millisecond offsets to avoid collisions. `ScreenshotImportService` feeds it: while `Configuration.ImportScreenshots` is on, it watches the game's screenshot folder plus ReShade and GShade save paths, waits for each new file to finish writing, then imports it with the file's last write time as the taken timestamp. The Photos app derives a photo's taken-date by parsing that name (`ResolveTaken` in src/Aetherphone/Apps/Photos/PhotosApp.cs), falling back to the file's write time. Editing never rewrites a file: the editor in the Photos viewer renders the recipe (`PhotoEditor.Apply`, then the crop) over the full-resolution source and hands the result to `PhotoLibrary.SaveEdited`, which writes a new `AEP_` PNG stamped with the current time beside the untouched original, so an edit sorts as the newest photo and reverting is deleting the copy.

**Custom sounds.** `SoundLibrary` merges bundled and user folders; config properties like `RingtoneSound` store a token of the form `file:<name>` (`SoundTokens.FilePrefix`), never a path. `TryResolvePath` prefers the user folder over the bundled one for the same file name.

**Custom wallpapers.** The image file goes to `<config>/Wallpapers/` and a small `CustomWallpaper` record (id, file name, crop) is appended to `Configuration.CustomWallpapers`. Deleting removes both.

**Caches are disposable.** `DiskCache.Set` enforces the byte budget by deleting the oldest files. Anything under `<config>/cache/` can vanish at any time; never keep the only copy of something there.

## Server-side vs local

The Aethernet backend (a separate ASP.NET service in its own repository) is the source of truth for everything shared between players: accounts and profiles, social posts and comments, encrypted chat threads and messages, uploaded media, musters, ads, and moderation state. The client persists what it needs to reconnect, decrypt, and stay consistent across a restart: per-character tokens in `Configuration.CharacterSessions`, end-to-end keys in `Configuration.EncryptionKeysByUserId`, the social seen watermarks (`SocialActivitySeenUnix`) and read acks the server has not confirmed yet (`PendingNotificationAcks`), size-budgeted disk caches of downloaded media, and the account-scoped files below. Server-backed stores such as `ChatThreadStoreBase` and `SocialFeedStore` keep thread lists, feeds, and cursors in memory only and refetch after a restart or account switch. See [Networking](networking.md) for the API client, realtime signals, and encryption.

The exception that matters for chat is message bodies this device has already decrypted. Every chat store decrypts through `MessageCipher`, which hands each successful decrypt to `DecryptedHistoryStore.Remember` and falls back to `TryGet` when a body can no longer be decrypted (its key is missing or still pending, for example after a key reset), so chats the user has read stay readable. `DecryptedHistoryStore` (src/Aetherphone/Core/Crypto/DecryptedHistoryStore.cs) keeps one file per account, `<config>/History/<SHA-256 of the account id>.dat`, sealed with `LocalKeyProtector`. It loads the file on account change, flushes at most every 15 seconds from a framework tick, and keeps up to 20,000 bodies, trimming the oldest back to 16,000 beyond that. Deleting the account from Settings clears it.

Velvet's not-interested list is the other account-scoped file: `VelvetNotInterestedArchive` writes `<config>/Velvet/<SHA-256 of the account id>.json`.

## Rules of thumb for app authors

- New small setting or favorite list: add a property to `Configuration` with a sensible default, call `Save()` after mutating. No migration needed.
- New per-character setting, small: a `Dictionary<ulong, ...>` on `Configuration` keyed by ContentId, reloaded from a `CharacterWatch.Changed` handler.
- Growing or per-character bulky data: a JSON file per ContentId under a new `<config>` subfolder, following `ActivityStore` or `ChatArchive`. Write via a temp file and `File.Move(temp, path, true)` so a crash cannot truncate it.
- Binary media the user created: a `<config>` subfolder with a stable naming scheme, plus (if it needs settings) a small record list in `Configuration`, like wallpapers.
- Downloaded, refetchable bytes: a `DiskCache` under `<config>/cache/`.
- Shared or account-scoped data: it belongs on the server; the client store keeps it in memory and obeys the `AethernetSession.Changed` reset contract. If a slice of it must survive a restart per account, give it its own file named by a SHA-256 of the account id, following `DecryptedHistoryStore` or `VelvetNotInterestedArchive`.
- Keep `Configuration` compact: it is one JSON file rewritten in full on every save of any setting. When a list outgrows it, move it to its own file the way `LibraryStore` did.
- Migrate, never rename in place: new property plus a one-shot flag migration for value changes, a `ConfigMigrations.TypeRenames` entry for type moves.

## Gotchas

- The config JSON embeds assembly-qualified type names for serialized record types. Moving a `[Serializable]` type stored in `Configuration` to another namespace silently drops users' data unless you add a `ConfigMigrations.TypeRenames` pair. Every existing entry in that table is a scar from a real move.
- `Configuration.Save()` called off the framework thread is fire-and-forget: the write happens on a later framework tick. Do not assume the file is on disk when the call returns; use `await configuration.SaveNowAsync()` when you need the write to have landed.
- ContentId hex casing is inconsistent across stores: `ChatArchive`, `MessageArchive`, and `InventoryStore` format with `"x16"` (lowercase); `ActivityStore`, `HealthStore`, `WalletHistoryStore`, and `CollectionLedgerStore` with `"X16"` (uppercase). Account-scoped files are named by a SHA-256 of the account id instead, and even those differ: `DecryptedHistoryStore` uses uppercase hex of the id as given, `VelvetNotInterestedArchive` lowercase hex of the lowercased id. Copy the exact store you are following, and do not expect file names to match across features.
- `ChatArchive` names files with a SHA-256 hash of the stream key (the legacy `MessageArchive` did the same with the send target), so you cannot map a file back to a conversation by eye. Streams are capped at `ChatLog.MaxLinesPerStream` (2000) stored lines.
- The `AEP_` file name is load-bearing through `PhotosApp.ResolveTaken`, which parses the taken date out of it; the Photos app then sorts every entry by that resolved time, so `PhotoLibrary.List`'s name-descending order is only a pre-sort. Renaming files in the Photos folder makes `ResolveTaken` fall back to the file's write time, which breaks date grouping and shifts the photo's position. Place metadata is keyed by file name too: `Configuration.PhotoPlaces[fileName]` holds the territory a photo was taken in (stamped by the camera and by `ScreenshotImportService.StampPlace`, copied onto edited copies, pruned when the file is gone), so a renamed photo also loses its place.
- `PhotoLibrary.Save` encodes and writes the PNG on a background `Task.Run`. Callers can see the in-flight write through `AddPendingNames`, and `Version` bumps (`MarkChanged`) when it finishes or fails, but nothing awaits it at shutdown, so a capture taken right before quitting the game can be lost.
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
