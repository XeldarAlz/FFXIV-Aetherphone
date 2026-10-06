# Notifications

This doc walks the full notification pipeline on the client: how a notification is created, filtered, stacked in the notification center, shown as a banner, played as a sound, and routed back into an app when the user taps it. Read it before you make an app post notifications, before you add a deep link target, or when a badge or banner does not behave the way you expect. Server-side delivery (the Aethernet backend, a separate ASP.NET service in its own repo) is out of scope here; this doc covers only what the plugin does with the data it receives. It pairs with [App framework](app-framework.md).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Notifications/PhoneNotification.cs | The notification record every producer creates |
| src/Aetherphone/Core/Notifications/NotificationService.cs | Queue, filters, retention, unread count, timed mute check, sound trigger |
| src/Aetherphone/Core/Notifications/NotificationRouter.cs | Turns a tapped notification into app navigation; forwards read acks |
| src/Aetherphone/Core/Notifications/NotificationChannels.cs | Catalog of per-app settings channels |
| src/Aetherphone/Core/Notifications/AppNotificationSetting.cs | Per-channel enable, banner, sound on/off, sound override, and timed mute |
| src/Aetherphone/Core/Notifications/NotificationMutes.cs | Timed per-app mute math (one hour, until tomorrow) |
| src/Aetherphone/Core/Notifications/SocialNotificationService.cs | Polls the backend, converts DTOs to phone notifications, sends read acks |
| src/Aetherphone/Core/Notifications/NotificationAckQueue.cs | Persisted per-account queue of read acks waiting for the server |
| src/Aetherphone/Core/Notifications/UnreadCounts.cs | The server's unread breakdown per app and type from the poll |
| src/Aetherphone/Core/Notifications/SoundService.cs | Resolves and plays notification sounds, ringtones, ringback, and alarm tones |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Bundled plus user sound files, token resolution |
| src/Aetherphone/Core/Notifications/SoundTokens.cs | `file:` and `silent` token format |
| src/Aetherphone/Core/Notifications/SoundEffectPlayer.cs | NAudio playback, one-shots and the ringtone loop |
| src/Aetherphone/Core/Notifications/UiSound.cs, UiSoundService.cs, UiSoundPlayer.cs | Interface sounds: the cue catalog, the gates, and the mixer |
| src/Aetherphone/Core/Social/SocialActivity.cs | Numbered social type catalog and body text |
| src/Aetherphone/Core/Notifications/NotificationGroups.cs | Pure per-app grouping, stack math, summary counts |
| src/Aetherphone/Core/Notifications/NotificationSections.cs | Today / Yesterday / Earlier bucketing and the per-app tally for the Notifications app |
| src/Aetherphone/Windows/Components/Notify/NotificationDeck.cs | Shared stack, expand, swipe, tap, and clear engine for both hosts |
| src/Aetherphone/Windows/Components/Notify/NotificationDeckStyle.cs | Glass or solid card style for the deck |
| src/Aetherphone/Windows/Components/Notify/NotificationCenter.cs | Control center panel: summary pill, scrolling, hosts the deck |
| src/Aetherphone/Windows/Components/Notify/NotificationCard.cs | Card content, glass fill, and the one app icon method |
| src/Aetherphone/Windows/Components/Notify/NotificationBanner.cs | Drop-down glass banner over the screen |
| src/Aetherphone/Apps/Notifications/NotificationsApp.cs (plus `.Filters.cs`, `.Status.cs`) | The Notifications app: sectioned inbox, app filter and mute card, Do Not Disturb status |
| src/Aetherphone/Apps/Settings/Pages/NotificationsPage.cs | Settings: quiet while busy, global banner switch, per-app list |
| src/Aetherphone/Apps/Settings/Pages/AppSettingsPage.cs | Settings: one app's Allow Notifications, Banners, Sounds, Badges, sound picker, Open and Remove App |
| src/Aetherphone/Core/Apps/NavigationStack.cs | `OnOpened` / `OnResumed` re-fire that deep links depend on |
| src/Aetherphone/Core/Apps/IResumableApp.cs | Opt-in `OnResumed` for apps that keep their screen across a short close |

## Pipeline overview

Every notification travels the same path, no matter who produced it:

1. A producer calls `NotificationService.Notify(PhoneNotification)`. The call is safe from any thread: it only enqueues into a `ConcurrentQueue`.
2. On the next framework tick (Dalamud's `IFramework.Update` event, which runs on the game's main thread once per frame), `NotificationService.OnFrameworkUpdate` drains the queue and calls `Present` for each item.
3. `Present` drops the notification if the app is not installed, if the user disabled that channel in Settings, or if the app is unavailable (the server kill switch, wired as `notifications.AppAvailability = navigation.IsAvailable` in src/Aetherphone/Core/Shell/PhoneShell.cs).
4. Survivors get a sequence `Id`, land in the `Recent` list (capped at `MaxRetained` = 50, oldest dropped), and bump `UnreadCount`. The `Added` event fires.
5. Alerts (banner, shake, sound) then pass four shared gates: the player must be logged in, `Configuration.DoNotDisturb` must be off, the app must not be under a timed mute (`NotificationService.IsMuted`, which checks `AppNotificationSetting.MutedUntilUnix` for the notification's `AppId` through `NotificationMutes`), and when `Configuration.QuietWhileBusy` is on (it defaults to true) `PlayerBusy.Now` must be false. `PlayerBusy.Now` (src/Aetherphone/Core/Game/PlayerBusy.cs) is true in combat, inside a duty, during a cutscene, and while zoning, so a fresh install is silent in all of those states by design. Behind the shared gates, three things happen independently: the `Presented` event fires only when the global `Configuration.ShowNotificationBanner` and the channel's `ShowNotificationBanner` setting are both on (`NotificationBanner` and `MinimizedPhone` listen to it); the `Vibration` event fires when `Configuration.Vibration` is on (`MinimizedPhone` and `PhoneShell` listen to it and shake the minimized puck or the open phone, even when banners are off); and a sound plays only when the notification is not `Muted`, the channel's Sounds switch is on (`Configuration.ShouldPlayNotificationSound`, backed by `AppNotificationSetting.PlaySound`), the per-stack throttle allows it, and `SoundService.PlayNotification` finds `Configuration.SilentMode` off and `Configuration.NotificationSoundsEnabled` on.
6. The notification now sits in the notification center until the user taps it (routed by `NotificationRouter`), swipes or right-clicks it away, clears it, or the 50-item cap pushes it out. Nothing expires by age.

Clock alarms and the Clock timer also ring: `ClockAlarmService` posts a muted notification (`PhoneNotification.Muted` skips the notification sound) and calls `AlarmRinger` (src/Aetherphone/Core/Clock/AlarmRinger.cs), which loops `Sounds/Ui/alarm.wav` or `timer.wav` at the ringtone volume, even in Silent mode, raises `AlarmRinger.Presented` (the `Plugin` handler `BringPhoneForward` brings the phone up like an incoming call), and shows `AlarmOverlay` with Stop and, for alarms, Snooze for the alarm's own `AlarmEntry.SnoozeMinutes` (default 9, at most 15; 0 hides Snooze). Ringing stops by itself after five minutes.

Producers are spread across the codebase. Local ones include `TimerNotifier`, `ClockAlarmService`, `ReminderService`, and `CalendarReminderService` in src/Aetherphone/Core/Notifications/, plus `ChatNotifier` in src/Aetherphone/Core/GameChat/ for in-game chat. Networked ones include `SocialNotificationService` (social activity, which also carries missed calls as type 20), `CallHub` (incoming calls), and the chat stores built on `ChatThreadStoreBase`.

## The notification model

`PhoneNotification` (src/Aetherphone/Core/Notifications/PhoneNotification.cs) is a record:

| Member | Meaning |
| --- | --- |
| `AppId` | The app the notification belongs to and opens |
| `Title`, `Body` | Text shown on the card and banner |
| `SingleLineBody` | `Body` with line breaks flattened; the banner renders this |
| `ReceivedAt` | Local timestamp shown on the card |
| `Accent` | Tile color, normally `AppAccents.For(appId)` |
| `GroupKey` | Optional stacking key (a conversation, a linkshell, a post) |
| `Id` | Sequence number stamped by `NotificationService.Present` |
| `ActorId`, `PostId` | Deep link targets for social notifications |
| `SocialType` | Social type number, `-1` for non-social notifications |
| `CreatedAtUnix` | Server timestamp, used for the read watermark |
| `ChannelId` | Optional settings channel that overrides `AppId` |
| `Read` | Mutable read flag behind `UnreadCount` |
| `Muted` | Skips the notification sound only; the Clock alarm notification sets it because `AlarmRinger` rings instead |

Two derived properties drive everything else:

- `StackKey` is `GroupKey` when set, otherwise `AppId`. The center stacks by it, the banner replaces by it, and the sound throttle keys on it.
- `SettingsKey` is `ChannelId` when set, otherwise `AppId`. The enable filter and the sound override look up per-app settings by it.

## Posting a notification

The real API is one call. `TimerNotifier` (src/Aetherphone/Core/Notifications/TimerNotifier.cs) is a small producer:

```csharp
private void Notify(string title, string body, string group)
{
    notifications.Notify(new PhoneNotification(AppId, title, body, DateTime.Now, Accent, group));
}
```

Timers passes one group key per category (`timers:resets`, `timers:ventures`, `timers:voyages`, `timers:events`, `timers:map`), so each category stacks separately. A producer that passes no `GroupKey`, such as `ReminderService` for Notes reminders, stacks everything under its app id. Chat-style producers pass a group key so each conversation stacks separately; `ChatNotifier` uses the conversation key, `tab:<id>` for a tab or the tell stream key for a person, and stamps the chat entry's own timestamp rather than `DateTime.Now`:

```csharp
private void Raise(string title, string body, string groupKey, DateTime at) =>
    notifications.Notify(new PhoneNotification(AppId, title, body, at, AppAccents.For(AppId), groupKey));
```

`CallHub` (src/Aetherphone/Core/Telephony/CallHub.cs) also sets `ChannelId` so call notifications resolve their settings under the `phone` channel instead of the messaging app:

```csharp
notifications.Notify(new PhoneNotification("message", NameOf(message.From), Loc.T(L.Phone.IncomingCallBody),
    DateTime.Now, Accent, "call:" + message.From.UserId)
{
    ChannelId = NotificationChannels.PhoneChannel,
});
```

Get the `NotificationService` through constructor injection like every other service (see [Architecture](architecture.md)); it is constructed in src/Aetherphone/Core/PhoneServices.cs.

## The notification center

Two hosts draw the center, and both run the same engine, `NotificationDeck` (src/Aetherphone/Windows/Components/Notify/NotificationDeck.cs):

- `ControlCenter` (src/Aetherphone/Core/Shell/ControlCenter.cs) hosts `NotificationCenter` (src/Aetherphone/Windows/Components/Notify/NotificationCenter.cs) as an overlay panel via `DrawOverlay` inside the pull-down control center. `NotificationCenter` adds the summary pill and kinetic scrolling around the deck.
- `NotificationsApp` (src/Aetherphone/Apps/Notifications/NotificationsApp.cs), app id `notifications`, builds its own `NotificationDeck` and draws a large-title inbox: a status card while Do Not Disturb is on (with a Turn Off pill) or while Quiet while busy is holding alerts back, per-app filter chips with counts in one `ChipRail` (`NotificationSections.Tally`), a card for the filtered app with Mute 1 Hour and Mute Today (or Unmute while muted) plus a Settings button, Today / Yesterday / Earlier sections (`NotificationSections.Of`), and a link to Settings. Its nav bar carries a moon button that toggles Do Not Disturb and a Clear button that asks for confirmation and clears only what the active filter shows.

Both call `SocialNotificationService.AcknowledgeAll()` and then `NotificationService.MarkAllRead()` on open (`NotificationsApp.OnOpened`, and `ControlCenter.Open` through `NotificationRouter.AcknowledgeAll`). Opening either view zeroes the center's unread count and also drops every social app's server-backed activity badge (`SocialNotificationService.UnseenCount`): the cached server unread counts are cleared and a global read ack is queued for the backend.

Behavior, all in `NotificationDeck`:

- **Grouping**: `NotificationGroups.Rebuild` (src/Aetherphone/Core/Notifications/NotificationGroups.cs) walks `Recent` newest-first and buckets by `StackKey`; groups run newest arrival first and items inside a group run newest first. Each host rebuilds only when `NotificationService.Version` or the UI language changes (the Notifications app also on a new day or a filter change), then hands the groups to `NotificationDeck.Sync`, so no grouping work happens per frame.
- **Stacking**: a collapsed group shows the newest card as a full card with up to two more layers stacked behind it (`NotificationGroups.MaxVisibleLayers` = 3), each layer 6 units lower and scaled by 0.94 per step, and an "n more" caption under the stack (`NotificationGroups.HiddenCount` = count minus one).
- **Expanding**: tapping a collapsed multi-item stack expands it under a header that carries the sender title, a glass "Clear All" pill for that group, and a "Show Less" action; tapping the header collapses it again. Groups with fewer than two items are forced collapsed by `NotificationDeck.Sync`.
- **Swipe to clear**: dragging a card left reveals a "Clear" action (`RevealWidth` = 84 units); releasing past half of it parks the card open, and a tap on the action clears that one card. Releasing past `SwipeCommitFraction` (42% of the card width) clears it straight away. Right-clicking a card slides it out immediately. Swiping a collapsed stack acts on the whole group (`NotificationService.RemoveGroup`); swiping an expanded row acts on one item (`NotificationService.Remove`). Tapping anywhere else closes the revealed action. Vertical drags scroll instead; the axis lock decides after 6 logical pixels of movement.
- **Clearing acknowledges**: dismissing a card or a group, and a group's Clear All pill, call `NotificationRouter.Acknowledge`, which for a social notification advances that app's read watermark on the server up to the dismissed item's timestamp (`AcknowledgeUpTo`).
- **Tap**: a tap (small total movement) on a single card calls `NotificationRouter.Open`. A tap on a collapsed stack expands it first.
- **Summary pill** (control center only): a glass capsule above the list shows how many notifications are waiting and how old the oldest one is, with a "Clear All" action that calls `NotificationRouter.AcknowledgeAll()` and then `NotificationService.Clear()`.
- **Surfaces**: in the control center the deck uses `NotificationDeckStyle.ForGlass`, so cards draw through `Material.LiquidGlass` in `GlassTone.Dark`. The Notifications app uses `NotificationDeckStyle.ForCards`: solid cards mixed from the app's backdrop. Pills are glass in both, and the banner is glass too (it picks `GlassTone.Light` over a bright app background). The app icon is drawn in exactly one place, `NotificationCard.DrawAppIcon`.

## Banners

`NotificationBanner` (src/Aetherphone/Windows/Components/Notify/NotificationBanner.cs) subscribes to `NotificationService.Presented` and shows an iOS-style card that drops from the top of the screen on the Dear ImGui foreground draw list (ImGui is an immediate mode UI library: everything is redrawn every frame, so the banner is a state machine advanced each frame).

Rules, all in `OnPresented` and the stage machine:

- Skipped entirely when the phone window is hidden (`phoneVisible`) or when the notification's app is the one currently on screen (`currentAppId`).
- A new notification with the same `StackKey` as the active banner replaces its content in place and restarts the hold timer.
- Any queued banner with the same `StackKey` is removed before the new one is queued (`RemoveQueuedGroup`), so a burst in one stack waits in line as its newest item only.
- At most `MaxQueued` = 4 banners wait in line; extras are dropped.
- A banner holds for `HoldSeconds` = 4 (paused while hovered or dragged), then animates out.
- Tap opens the notification through `NotificationRouter`; dragging up past a distance or velocity threshold dismisses it.
- The banner is a 68 unit glass card with a 24 unit radius that springs in from above the status bar and back out over 0.14 seconds; it shows the 36 unit app icon, the title, one line of body and the clock time through `TimeText.Clock`.

The minimized phone has its own alert surface. `MinimizedPhone` (src/Aetherphone/Windows/Components/Chrome/MinimizedPhone.cs) also subscribes to `Presented` and, while it is showing and its `MinimizedPart.Alerts` layout part is on, shows a small alert card with the same replace-in-place and replace-in-queue rules by `StackKey`, a 4.5 second hold, and a queue of 3.

## Channels and per-app settings

`NotificationChannels.All` (src/Aetherphone/Core/Notifications/NotificationChannels.cs) is the catalog of settings channels, each a `NotificationChannel(AppId, Name, Accent)`. It covers the messaging and social apps plus most apps that post local alerts; the array itself is the source of truth for which apps have a channel, so read it instead of trusting a list in a doc. There are two extra constants: `NotificationChannels.PhoneChannel` (`"phone"`), used as a `ChannelId` by call notifications and not part of `All`, and `NotificationChannels.NotificationsAppId` (`"notifications"`), the Notifications app's id, whose badge preference also drives the minimized phone and the server info bar.

Settings storage is `Configuration.NotificationSettings`, a `Dictionary<string, AppNotificationSetting>` keyed by `SettingsKey` (the timed mute is the exception, see below). `Configuration.NotificationSettingFor(appId)` creates an entry on demand. `AppNotificationSetting` has five fields:

- `Enabled` (default true): `Configuration.IsAppNotificationEnabled` returns true when no entry exists, so every channel is on until the user turns it off. `NotificationService.Present` checks this and drops disabled notifications before they reach the center.
- `ShowNotificationBanner` (default true): the per-channel banner switch. The `Presented` event requires this and the global `Configuration.ShowNotificationBanner` together.
- `PlaySound` (default true): the per-channel Sounds switch, read by `Configuration.ShouldPlayNotificationSound` (true when no entry exists). Off means the channel never plays a sound; banner and shake still happen.
- `Sound` (default null): a per-channel sound token. `Configuration.ResolveNotificationToken` falls back to the global `Configuration.NotificationSound` when no override is set.
- `MutedUntilUnix` (default 0): a timed mute, set from the Notifications app's per-app card. Mute 1 Hour stores now plus an hour (`NotificationMutes.ForAnHour`), Mute Today stores the next local midnight (`NotificationMutes.UntilTomorrow`), and Unmute stores 0. While it lies in the future, `NotificationService.IsMuted` holds back banner, shake and sound; the notification still lands in the center and counts as unread. The check uses the notification's `AppId`, not its `SettingsKey`.

The UI is Settings > Notifications and Badges (`NotificationsPage`), which owns the `QuietWhileBusy` and global banner switches and lists every installed app that has a channel, `HasBadge`, or both. Each row opens that app's `AppSettingsPage`; see [Hiding a badge](#hiding-a-badge) below for what it shows. The Settings button on the Notifications app's per-app card opens the same page through `SettingsLauncher.RequestAppNotifications`.

## Sounds

There are two sound kinds (`SoundKind`): `Ringtone` for calls and `Notification` for everything else. Each kind has its own `SoundLibrary` built in src/Aetherphone/Core/PhoneServices.cs:

- Bundled files ship next to the plugin assembly under `Sounds/Ringtones` and `Sounds/Notifications` (source assets in src/Aetherphone/Sounds/). Two more bundled folders sit beside them and never appear in a picker: `Sounds/Ui` (interface sounds plus `ringback.wav`, `alarm.wav` and `timer.wav`) and `Sounds/Games` (the mini-game palette). See [Assets and media: sounds](assets-and-media.md#sounds) for the files themselves.
- User files live under the plugin config directory in `Sounds/Ringtones` and `Sounds/Notifications`. `SoundService.AddUserFile` copies a picked file there; a user file with the same name as a bundled one wins (`SoundLibrary.TryResolvePath` checks the user directory first).
- Only `*.mp3` and `*.wav` are scanned.

Sounds are identified by tokens (`SoundTokens`): `file:<name>` for a file, `silent` for none, and empty string for the library default. An empty token, and any `file:` token whose file no longer exists, resolves to `SoundLibrary.DefaultToken`: the first bundled file in ordinal file-name order (silent when the bundled folder is empty). Legacy `game:` tokens from old versions are migrated by `Configuration.MigrateSoundSettings`. The `Configuration` defaults are `SoundLibrary.BundledRingtoneToken` (`file:Signal.mp3`) and `SoundLibrary.BundledNotificationToken` (`file:Chime.mp3`). Choices that point at a removed bundled file are rewritten to its replacement by `Configuration.MigrateRetiredSounds`.

Playback goes through `SoundService` on top of `SoundEffectPlayer`, which dispatches NAudio readers by file extension (`SoundEffectPlayer.OpenReader`): `.mp3` plays through the managed `Mp3FileReaderBase` with an `Mp3FrameDecompressor`, `.wav` through `WaveFileReader`, and `MediaFoundationReader` (Windows Media Foundation) is only the fallback, for other extensions and for files the managed readers reject. The managed-first order is what keeps sounds Wine-safe; src/Aetherphone/Sounds/README.md documents the dispatch. `PlayNotification(settingsKey)` plays a one-shot at `Configuration.NotificationVolume`, and `StartCallRing`/`StopCallRing` loop the ringtone at `Configuration.RingtoneVolume`. Both volumes are set with the continuous slider in the sound pages under Settings > Sounds, which links to Ringtone and Notification Sound (`VolumeSlider` in src/Aetherphone/Windows/Components/Primitives/VolumeSlider.cs); it commits and previews on release so a drag does not save the config every frame. `NotificationService.ShouldPlaySound` throttles to one sound per `StackKey` per 3 seconds (`SoundRepeatSeconds`), so a burst in one conversation dings once.

`SoundService` applies its own switches on top of the notification gates. `PlayNotification` returns early when `Configuration.SilentMode` is on or `NotificationSoundsEnabled` (the Notification Sound switch on Settings > Sounds) is off. `StartCallRing` returns early when `SilentMode` is on or `RingtoneEnabled` is off. `StartRingback` loops `Sounds/Ui/ringback.wav` at a fixed 0.5 for outgoing calls, unless `SilentMode` is on. Alarm and timer tones play on a second `SoundEffectPlayer` and ignore `SilentMode`.

### Interface sounds

`UiSoundService` (src/Aetherphone/Core/Notifications/UiSoundService.cs) plays short cues from the `UiSound` enum: taps, toggles, app open and close, sheets, the island, keyboard clicks, message send and receive, call connect and end, and the mini-game and casino palette. Code fires them through the static `UiFeedback.Play(UiSound)`, bound once in `PhoneServices.Build`. `UiSoundCatalog` (src/Aetherphone/Core/Notifications/UiSound.cs) maps each cue to its files under `Sounds/Ui` or `Sounds/Games`, a gain, a minimum repeat interval, a channel, and an optional pitch variance; `UiSoundPlayer` mixes them on its own NAudio output and closes it when idle.

A cue plays only when `Configuration.SilentMode` is off, the Interface Sounds master (`Configuration.UiSounds`) is on, and its channel switch is on: `UiSoundTaps`, `UiSoundTransitions`, `UiSoundToggles`, `UiSoundKeyboard`, or `GameSounds` for game cues (event cues have no switch of their own). Game cues need the master on too. Volume is `UiSoundVolume`, or `GameSoundVolume` for game cues. All of these live on Settings > Sounds. `Configuration.MigrateUiSoundDefaults(freshInstall)` runs once: it turns interface sounds on for a fresh install and off for an existing config, so upgrading users opt in.

## Deep links: what happens on tap

Tapping a card or banner calls `NotificationRouter.Open(PhoneNotification)` (src/Aetherphone/Core/Notifications/NotificationRouter.cs), which does four things:

1. For social notifications (`SocialType >= 0`), advances the read watermark via `SocialNotificationService.AcknowledgeUpTo`.
2. Removes the whole stack from the center (`NotificationService.RemoveGroup`). If the app is unavailable it stops here.
3. Parks the destination in the right launcher. The if-chain in `NotificationRouter.Open` is the source of truth; today it covers the Linkpearl conversation key (`LinkpearlLauncher`), `call:` group keys (`DmLauncher.RequestCalls`), conversation ids (`DmLauncher`, `VelvetLauncher`, `GramDmLauncher`), Muster, Yellow Pages (the ad detail, or the inquiry thread for type 19) and Announcements detail ids, Jam invite codes (`JamLauncher.RequestLobby`), live station ids (`RadioLauncher.RequestStation`, music app, type 21), `casino:<tableId>` group keys (`CasinoLauncher.RequestTable`), the AetherStream up-next suggestion (`AetherStreamLauncher.RequestUpNext`), hunt group keys (`HuntsLauncher.RequestDetail`), feedback ids (`FeedbackLauncher.RequestDetail`), the encryption guide (`EncryptionSetupLauncher.Request`), every other `settings` notification, which is how moderation notices open the Safety page (`SafetyLauncher.Request`), and post or profile links (`SocialLauncher`, via `SocialDeepLink` in src/Aetherphone/Core/Apps/SocialLauncher.cs).
4. Calls `INavigator.Open(appId)`.

The contract that makes this land on the right screen: **opening an app always runs its open hook, even when the app is already on screen.** `NavigationStack.OpenApp` (src/Aetherphone/Core/Apps/NavigationStack.cs) calls `NotifyOpened(app)` when the requested app is already the current one, instead of returning early. `NotifyOpened` runs `app.OnOpened()`, unless the app implements `IResumableApp` (src/Aetherphone/Core/Apps/IResumableApp.cs) and was closed less than 10 minutes ago (`ResumeWindowMilliseconds`); then it runs `OnResumed()` instead and the app keeps its current screen. Going Back onto a resumable app that sits underneath always runs `OnResumed()`. `git grep IResumableApp` lists the apps that opt in. The one case with no hook at all: `OpenApp` returns early when a moderation suspension blocks the app (`SuspensionGate.Blocks`).

For your app to support deep links you need:

- A launcher service with park-and-consume semantics. `DmLauncher` (src/Aetherphone/Core/Apps/DmLauncher.cs) is the template: `RequestConversation(id)` stores pending state, `TryConsumeConversation(out id)` returns it exactly once.
- A branch in `NotificationRouter.Open` that fills your launcher from the notification's `GroupKey`, `PostId`, or `ActorId`.
- Consumption in your app's `OnOpened`, after resetting navigation state, and, if the app implements `IResumableApp`, in `OnResumed` as well. `MessageApp` (src/Aetherphone/Apps/Message/MessageApp.cs) shows the pattern, abridged here: both hooks end in one shared consumer. The full `OnOpened` also resets search, the call audio sheet, the avatar lightbox, and group compose state, and `ConsumeLaunchRequests` has a third branch for `TryConsumeUser`.

```csharp
public void OnOpened()
{
    router.Reset();
    activeTab = MessageTab.Chats;
    RefreshAndConsumeLaunch(forceContacts: true);
}

public void OnResumed()
{
    RefreshAndConsumeLaunch(forceContacts: false);
}

private void RefreshAndConsumeLaunch(bool forceContacts)
{
    contacts.Refresh(force: forceContacts);
    store.RefreshConversations();
    ConsumeLaunchRequests();
}

private void ConsumeLaunchRequests()
{
    if (launcher.TryConsumeCalls())
    {
        activeTab = MessageTab.Calls;
        return;
    }

    if (launcher.TryConsumeConversation(out var conversationId))
    {
        ShowLaunchedThread(conversationId);
    }
}
```

`ShowLaunchedThread` resets the router and pushes the thread only when that thread is not already showing. Because the hooks re-fire, treat them as re-entrant: `OnOpened` resets first, then consumes; `OnResumed` keeps the current screen and navigates only when a launcher has something parked. Consume-once launchers guarantee a plain open (home screen tap) does not replay the last deep link.

## Badges

Badges on home screen tiles come from `IPhoneApp.BadgeCount` (src/Aetherphone/Core/Apps/IPhoneApp.cs), drawn by `HomeTileView` (src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs). `BadgeAsDot` swaps the number for a dot (Settings, Games, Housing and Music use it for unseen markers). Folder tiles combine the badges of the apps inside.

Badges are **not** driven by the notification center. Each app still computes its own unread number. A few examples (`git grep "HasBadge => true"` lists every app with a user-toggleable badge):

| App | BadgeCount source |
| --- | --- |
| NotificationsApp | `NotificationService.UnreadCount` |
| MessageApp | `store.UnreadTotal + calls.UnseenMissed` |
| ChirperApp | `social.UnseenCount(Id)` |
| AethergramApp | `dmStore.UnreadCount + social.UnseenCount(Id)` |
| AnnouncementsApp | `store.UnreadCount` |

For social apps, `SocialNotificationService.UnseenCount` prefers the server's `UnreadByApp` counts from the notification poll, with one override: while an acknowledgement is still queued for flush, the pending ack watermark wins over the server count, so the badge does not bounce back up between the ack and the next poll. With no server counts at all it falls back to counting items newer than the per-account watermark stored in `Configuration.SocialActivitySeenUnix`. Opening an app's activity screen calls `MarkSeen(appId)`, which clears the local count, removes that app's social notifications from the center, and sends a read acknowledgement to the backend when the watermark actually advanced, or, when the server still reported unread for that app, an acknowledgement up to the current time even though the local watermark stayed put. Tapping a single notification acknowledges only up to that item (`AcknowledgeUpTo`).

Read acks survive a flaky network and a restart. Each one is queued in `NotificationAckQueue`, persisted in `Configuration.PendingNotificationAcks` keyed `<accountId>:<app>` (or `<accountId>:*` for a global ack from `AcknowledgeAll`), and flushed to the backend one at a time for the signed-in account, retried every 15 seconds until the server confirms it. A queued ack only ever moves its watermark forward, and a global ack drops the per-app acks it covers.

### Hiding a badge

Whether the count actually reaches the tile is a separate, generic on/off switch: `IPhoneApp.HasBadge` (default `false`) opts an app into it, and the enabled state lives in `Configuration.BadgeSettings`, a `Dictionary<string, bool>` keyed by app id with the same missing-entry-means-on default as `NotificationSettings` (`Configuration.IsAppBadgeEnabled`/`SetAppBadgeEnabled`). `HomeTileView` checks it centrally before drawing, so an app with `HasBadge` never needs to gate its own `BadgeCount` getter.

The toggle is not a separate screen. Settings > Notifications and Badges (`NotificationsPage`) builds one row per installed app from `InstalledAppList` (src/Aetherphone/Apps/Settings/InstalledAppList.cs, sorted by name), showing any app that has a notification channel (`NotificationChannels.Contains`) or `HasBadge` (`AppSettingsEntry.Notifies`). Each row opens that app's `AppSettingsPage` (src/Aetherphone/Apps/Settings/Pages/AppSettingsPage.cs), the same page Settings > Apps opens. Its Alerts card holds Allow Notifications, Banners and Sounds when the app has a channel, then a Badges row when `HasBadge` is true. The Sound picker follows only when the app has a channel, notifications are allowed, and Sounds is on. The page ends with Open and, for removable apps, Remove App. An app with neither a channel nor `HasBadge` gets no Alerts card and no row on the Notifications and Badges page.

The minimized phone also shows `NotificationService.UnreadCount` as a badge (prepared by `MinimizedPhone.RefreshBadge` in src/Aetherphone/Windows/Components/Chrome/MinimizedPhone.cs, capped at "99+"), and so does the server info bar entry (`ServerBarEntry` in src/Aetherphone/Core/Platform/ServerBarEntry.cs). The entry is plain native text: the game's Aethernet bitmap icon (`BitmapFontIcon.Aethernet`), the Dev or Beta tag on prerelease builds (`AepConstants.ServerBarTag`), and the count when it is above zero. Its tooltip carries the plugin name, an unread line, and a click hint; clicking the entry toggles the phone. `Refresh` rewrites text and tooltip and also re-runs on `Plugin.OnLanguageChanged`. Both read `NotificationsApp`'s badge preference, so turning that app's badge off clears all three surfaces. Neither recomputes per frame: they refresh on `NotificationService.Changed` and on `Configuration.BadgeSettingsChanged`, which `SetAppBadgeEnabled` raises.

## Social notification types

Social notifications arrive as `NotificationDto` (src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs) from the backend, polled by `SocialNotificationService` every 60 seconds in the foreground and 120 in the background, with realtime pings requesting an immediate poll. The `Type` field is a numbered catalog shared with the backend; the client-side source of truth is `SocialActivity` (src/Aetherphone/Core/Social/SocialActivity.cs). It currently runs 0 through 21:

| Type | Constant | Tap opens |
| --- | --- | --- |
| 0 | `TypeLike` | Post |
| 1 | `TypeComment` | Post |
| 2 | `TypeFollow` | Profile |
| 3 | `TypeConnectRequest` | Profile |
| 4 | `TypeConnectAccept` | Profile |
| 5 | `TypePostRemoved` | Moderation notice, never a phone notification here |
| 6 | `TypeCommentLike` | Post |
| 7 | `TypeMention` | Post |
| 8 | `TypeCommentMention` | Post |
| 9 | `TypePhotoTag` | Post |
| 10 | `TypeWarning` | Moderation notice, never a phone notification here |
| 11 | `TypeReportUpdate` | Moderation notice, never a phone notification here |
| 12 | `TypeRepost` | Post |
| 13 | `TypeQuote` | Post |
| 14 | `TypeFollowRequest` | Follow requests list |
| 15 | `TypeFollowAccept` | Profile |
| 16 | `TypeAdExpiring` | Yellow Pages ad detail |
| 17 | `TypeAdHidden` | Yellow Pages ad detail |
| 18 | `TypeAdOpened` | Yellow Pages ad detail |
| 19 | `TypeAdInquiry` | Yellow Pages inquiry thread |
| 20 | `TypeMissedCall` | Calls tab, grouped under `call:<actorId>` |
| 21 | `TypeRadioLive` | The live station in the music app (`RadioLauncher.RequestStation`) |

`SocialActivity.IsModerationNotice` covers 5, 10, and 11; `SocialNotificationService.Ingest` skips those, because moderation content flows through `ModerationNoticeService` and `ModerationNoticePresenter` (src/Aetherphone/Core/Moderation/ModerationNoticePresenter.cs) instead. The presenter posts every notice as a `settings` notification; a non-blocking notice is acknowledged right away, and a blocking one also opens an alert (`ConfirmService.Alert`) and is acknowledged when that alert is dismissed.

Type 19 never reaches the center from the social poll either: `SocialNotificationService.Present` skips `TypeAdInquiry`, and `AdInquiryStore` posts inquiry notifications itself with `SocialType = 19`.

Group keys for social notifications come from `SocialNotificationService.GroupKeyFor`: post-scoped items stack per post (`app:post:<postId>`), actor-scoped items stack per actor and type, Yellow Pages stacks per ad, and missed calls stack per caller.

When you add a type: the constants exist in `SocialActivity` and again as private constants in `NotificationRouter`. Update both, plus `SocialActivity.Body` for the card text and `NotificationRouter.SocialLinkFor` for the tap target, in lockstep with the backend enum.

## Muting and suppression

Everything that can stop a notification, in pipeline order:

| Gate | Where | Effect |
| --- | --- | --- |
| App not installed | `NotificationService.Present` | Dropped, warning logged |
| Channel disabled in Settings | `NotificationService.Present` | Dropped silently |
| App unavailable (server kill switch) | `NotificationService.Present` via `AppAvailability` | Dropped, warning logged |
| Logged out | `NotificationService.Present` | Added to center and unread count, but no banner, no shake, no sound |
| Do not disturb | `NotificationService.Present` | Added to center and unread count, but no banner, no shake, no sound |
| Timed app mute | `NotificationService.Present` via `IsMuted` (`MutedUntilUnix`, keyed by `AppId`) | Same silencing until the mute expires |
| Quiet while busy (default on) | `NotificationService.Present` via `PlayerBusy.Now` | Same silencing while in combat, in a duty, in a cutscene, or zoning |
| Banners off, globally or per channel | `NotificationService.Present` via `ShowNotificationBanner` | No banner (`Presented` never fires); shake and sound still happen |
| Muted notification | `NotificationService.Present` via `PhoneNotification.Muted` | No notification sound (the Clock alarm rings through `AlarmRinger` instead) |
| Sounds off for the channel | `NotificationService.Present` via `ShouldPlayNotificationSound` (`PlaySound`) | No sound; banner and shake still happen |
| Sound throttle | `NotificationService.ShouldPlaySound` | Sound skipped within 3 s per stack |
| Silent mode, or Notification Sound off | `SoundService.PlayNotification` via `SilentMode` and `NotificationSoundsEnabled` | No notification sound |
| Phone hidden | `NotificationBanner.OnPresented` | No banner; center still gets it |
| App already on screen | `NotificationBanner.OnPresented` | No banner; center still gets it |
| Minimized alert card off | `MinimizedPhone.OnPresented` via `MinimizedPart.Alerts` | No alert card on the minimized phone |
| Linkpearl (`messages`) uninstalled | `ChatNotifier.OnAppended` via its `AppGate` | No game chat notifications at all |
| Linkpearl pause | `ChatNotifier.OnAppended` via `LinkpearlNotificationGate.Paused` | History yes, notification no |
| Your own chat line | `ChatNotifier.OnAppended` via `entry.IsSelf` | No notification for messages you sent |
| Channel that never counts as unread | `ChatNotifier.OnAppended` via `ChannelStyles.Shared.NeverUnread` | No notification for that channel |
| Muted tell partner | `ChatNotifier.OnAppended` via `TellPreferences.IsMuted` | No notification for that tell stream |
| Channel muted in a tab | `ChatNotifier.Alerts` via `ChatTab.IsMuted` | Message still appended to history, no notification at all |
| Tab alerts set to Mentions or Off | `ChatNotifier.Alerts` | Only mentions notify, or nothing does |
| Conversation on screen | `ChatNotifier.OnAppended` via `ChatInbox.IsViewing` | No notification for what you are already reading |
| Muted or pending chat thread | `ChatThreadStoreBase` via `IsThreadMuted` (muted ChocoChat conversations, pending Aethergram requests) | No inbox notification for that thread |
| Thread being viewed | `ChatThreadStoreBase` viewing grace | No inbox notification for the open thread |
| Moderation dedup | `ModerationNoticePresenter.presented` set | Each pending notice id presented once |

The quiet-while-busy gate deserves emphasis because `Configuration.QuietWhileBusy` defaults to true: out of the box, alerts are silent in combat, duties, cutscenes, and while zoning (the `PlayerBusy.Now` states), with the notification still landing in the center and counting as unread. Users regularly report that silence as a bug; it is the shipped default, toggled in Settings > Notifications and Badges.

Do not disturb is toggled from several places, all writing `Configuration.DoNotDisturb`: the Action key on the phone chassis (`PhoneShell` via `DeviceChrome.KeyRect(..., HardwareKey.Action, ...)`, which also announces the change on the island), the switch on the root Settings page (`RootSettingsPage`; the Notifications and Badges page does not host it, it only dims its two alert rows while it is on), the `dnd` tile in the control center (`ControlRegistry` in src/Aetherphone/Core/ControlCenter/ControlRegistry.cs), the moon button in the Notifications app's nav bar and the Turn Off pill on its status card (`NotificationsApp`), the Quick Toggles home widget (`QuickTogglesWidget`), and the Do Not Disturb action in Spotlight (`SpotlightActions`). While it is on, a moon shows in the status bar (`StatusBar` in src/Aetherphone/Core/Shell/StatusBar.cs) and the coin earn pill holds its pending toasts (`CoinEarnPill` in src/Aetherphone/Core/Shell/CoinEarnPill.cs).

Muting is per tab and per channel (`ChatTab.MutedChannels`), and a tab's `AlertPolicy` decides whether anything notifies at all. The same rule drives the unread badge in `ChatInbox`, so a channel that cannot notify you also cannot badge you. Legacy per-character linkshell mutes (`Configuration.MutedLinkshellsByCharacter`) are carried into any tab that later includes the channel.

The viewing grace deserves detail because it protects a correctness invariant. `ViewingMark` (src/Aetherphone/Core/Message/ViewingMark.cs) holds one key plus the time it was last noted and covers that key for `Grace` (4 seconds); `ChatThreadStoreBase` notes it through `NoteThreadViewed(threadKey)` while a thread view draws, and `ChatInbox` notes it through `NoteViewing` while Linkpearl's thread screen draws. Because the mark lapses on its own, closing the phone with a thread open stops counting as viewing in every chat app. Three things key off it:

- The inbox scan skips notifying for the thread the user is looking at, and `ChatNotifier` does the same for Linkpearl.
- Clear-on-view: every frame a thread is on screen, the store removes its notification group and zeroes its local unread count (Linkpearl marks the row read instead). Any unread or card that accrued while the app was backgrounded clears on resume; a trip back to the list is never required.
- Realtime chat pings call `RequestThreadRefresh`, which flags a pending refresh; `ConsumePendingThreadRefresh` only executes it while the open thread is being viewed (`IsBeingViewed`, inside the grace window). Refreshing a thread makes the server mark it read, so an ungated background refresh would silently mark threads read, suppress the sender's notification, and break seen ticks. The chat stores (`DirectMessagesStore`, `GramDmStore`, `VelvetStore`) all route pings through `RequestThreadRefresh`; keep it that way for any new chat surface.

## What persists

The notification center lives in memory only. `NotificationService` keeps `Recent` in a plain list, so a plugin reload or game restart starts with an empty center and an unread count of zero. Uninstalling an app also removes its notifications (`NotificationService.RemoveApp`). What does survive lives in `Configuration`: the per-channel settings and timed mutes (`NotificationSettings`), the badge switches (`BadgeSettings`), the per-account social seen watermarks (`SocialActivitySeenUnix`, keyed `<accountId>:<app>`), and read acks the server has not confirmed yet (`PendingNotificationAcks`). See [State and persistence](state-and-persistence.md) for how `Configuration` is saved.

## Gotchas

- `NotificationService.Notify` is enqueue-only; presentation happens on the next framework tick. Never assume the notification exists in `Recent` right after the call.
- Opening an app that is already on screen runs its open hook again (`NavigationStack.OpenApp`): `OnOpened`, or `OnResumed` for an `IResumableApp` closed within the last 10 minutes. Apps that do heavy work or reset scroll state in `OnOpened` will do it again on every deep link; keep it cheap and idempotent, and make launcher consumption one-shot. A resumable app must consume in `OnResumed` too, or a deep link waits in its launcher until some later full open.
- The timed app mute is keyed by the notification's `AppId`, not its `SettingsKey`. Muting ChocoChat (`message`) from the Notifications app also silences the call notifications posted under it, even though their other settings resolve under `phone`.
- An empty `GroupKey` stacks everything by app id. If your app can produce parallel streams (conversations, posts), pass a real group key or every stream collapses into one card and one swipe deletes all of it.
- `SettingsKey` is the channel id when `ChannelId` is set. Missed and incoming call notifications resolve under `phone`, which is not in `NotificationChannels.All`, so no Settings row toggles them; do not "fix" a call notification bug by editing the `message` channel.
- Opening the Notifications app or the control center marks everything read instantly (`MarkAllRead` in `NotificationsApp.OnOpened` and `ControlCenter.Open`) and sends a global social read ack (`AcknowledgeAll`), so every social app's server-backed activity badge clears too. Do not rely on `UnreadCount` or a social badge surviving a peek at the pull-down.
- Only the newest 50 notifications are retained (`MaxRetained`); the oldest is silently dropped, unread or not. Nothing survives a reload (see [What persists](#what-persists)).
- The banner queue caps at 4 (`MaxQueued`); bursts beyond that never show a banner but still land in the center.
- A background refresh of a chat thread marks it read on the server. Use `ChatThreadStoreBase.RequestThreadRefresh` for ping-driven refreshes, never `RefreshThread` directly, or you will suppress your own notifications (this was a real regression, fixed by gating on the viewing grace).
- `SocialNotificationService.MarkSeen` clears the local count immediately and sends the read acknowledgement when the newest item advances the stored watermark, or, when the server still reported unread for that app, an acknowledgement up to the current time; the backend badge and the local badge converge on the next poll.
- Social type numbers are a wire contract with the backend and are duplicated between `SocialActivity` and `NotificationRouter`; a new type added in only one place will render but route nowhere (`SocialLinkFor` returns null and the tap just opens the app root).
- A user sound file with the same name as a bundled one shadows it for everyone selecting that token (`SoundLibrary.TryResolvePath` prefers the user directory).
- The `Vibration` toggle does not vibrate anything; it enables the `NotificationService.Vibration` event, which shakes the minimized phone (`MinimizedPhone.OnVibration`) and the open phone (`PhoneShell.OnVibration`). `Vibration` is a separate event from `Presented` and fires even with banners off, so turning banners off does not stop the shake.

## Related docs

- [App framework](app-framework.md): the `IPhoneApp` contract, `BadgeCount`, and navigation this doc builds on
- [Creating an app](creating-an-app.md): the step-by-step tutorial for building a phone app, including posting your first notification and wiring a badge
- [UI toolkit](ui-toolkit.md): `UiInteract`, `Typography`, and the drawing helpers the center and banner use
- [Networking](networking.md): the Aethernet client, realtime pings, and poll cadence
- [Messaging and chat](messaging-and-chat.md): `ChatThreadStoreBase` and the shared chat layer
- [State and persistence](state-and-persistence.md): `Configuration` storage and migrations
- [Assets and media](assets-and-media.md): bundled sound assets and other media folders
