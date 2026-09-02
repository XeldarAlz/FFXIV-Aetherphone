# Notifications

This doc walks the full notification pipeline on the client: how a notification is created, filtered, stacked in the notification center, shown as a banner, played as a sound, and routed back into an app when the user taps it. Read it before you make an app post notifications, before you add a deep link target, or when a badge or banner does not behave the way you expect. Server-side delivery (the Aethernet backend, a separate ASP.NET service in its own repo) is out of scope here; this doc covers only what the plugin does with the data it receives. It pairs with [App framework](app-framework.md).

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Notifications/PhoneNotification.cs | The notification record every producer creates |
| src/Aetherphone/Core/Notifications/NotificationService.cs | Queue, filters, retention, unread count, sound trigger |
| src/Aetherphone/Core/Notifications/NotificationRouter.cs | Turns a tapped notification into app navigation |
| src/Aetherphone/Core/Notifications/NotificationChannels.cs | Catalog of per-app settings channels |
| src/Aetherphone/Core/Notifications/AppNotificationSetting.cs | Per-channel enable flag, banner flag, and sound override |
| src/Aetherphone/Core/Notifications/SocialNotificationService.cs | Polls the backend, converts DTOs to phone notifications |
| src/Aetherphone/Core/Notifications/SoundService.cs | Resolves and plays notification sounds and ringtones |
| src/Aetherphone/Core/Notifications/SoundLibrary.cs | Bundled plus user sound files, token resolution |
| src/Aetherphone/Core/Notifications/SoundTokens.cs | `file:` and `silent` token format |
| src/Aetherphone/Core/Notifications/SoundEffectPlayer.cs | NAudio playback, one-shots and the ringtone loop |
| src/Aetherphone/Core/Social/SocialActivity.cs | Numbered social type catalog and body text |
| src/Aetherphone/Windows/Components/NotificationCenter.cs | Stacked list, expand, swipe to delete |
| src/Aetherphone/Windows/Components/NotificationCard.cs | Single card rendering |
| src/Aetherphone/Windows/Components/NotificationBanner.cs | Drop-down banner over the screen |
| src/Aetherphone/Apps/Notifications/NotificationsApp.cs | The Notifications app that hosts the center |
| src/Aetherphone/Apps/Settings/Pages/NotificationsPage.cs | Settings: quiet while busy, global banner switch, per-app list |
| src/Aetherphone/Apps/Settings/Pages/AppNotificationPage.cs | Settings: one channel's enable, banner, and sound |
| src/Aetherphone/Core/Apps/NavigationStack.cs | `OnOpened` re-fire that deep links depend on |

## Pipeline overview

Every notification travels the same path, no matter who produced it:

1. A producer calls `NotificationService.Notify(PhoneNotification)`. The call is safe from any thread: it only enqueues into a `ConcurrentQueue`.
2. On the next framework tick (Dalamud's `IFramework.Update` event, which runs on the game's main thread once per frame), `NotificationService.OnFrameworkUpdate` drains the queue and calls `Present` for each item.
3. `Present` drops the notification if the app is not installed, if the user disabled that channel in Settings, or if the app is unavailable (the server kill switch, wired as `notifications.AppAvailability = navigation.IsAvailable` in src/Aetherphone/Core/Shell/PhoneShell.cs).
4. Survivors get a sequence `Id`, land in the `Recent` list (capped at `MaxRetained` = 50, oldest dropped), and bump `UnreadCount`. The `Added` event fires.
5. Alerts (banner, shake, sound) then pass three shared gates: the player must be logged in, `Configuration.DoNotDisturb` must be off, and when `Configuration.QuietWhileBusy` is on (it defaults to true) `PlayerBusy.Now` must be false. `PlayerBusy.Now` (src/Aetherphone/Core/Game/PlayerBusy.cs) is true in combat, inside a duty, during a cutscene, and while zoning, so a fresh install is silent in all of those states by design. Behind the shared gates, three things happen independently: the `Presented` event fires only when the global `Configuration.ShowNotificationBanner` and the channel's `ShowNotificationBanner` setting are both on (`NotificationBanner` listens to it and shows the drop-down card); the `Vibration` event fires when `Configuration.Vibration` is on (`MinimizedPhone` and `PhoneShell` listen to it and shake the minimized puck or the open phone, even when banners are off); and a sound may play.
6. The notification now sits in the notification center until the user taps it (routed by `NotificationRouter`), swipes it away, clears all, or it ages out.

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

Two derived properties drive everything else:

- `StackKey` is `GroupKey` when set, otherwise `AppId`. The center stacks by it, the banner replaces by it, and the sound throttle keys on it.
- `SettingsKey` is `ChannelId` when set, otherwise `AppId`. The enable filter and the sound override look up per-app settings by it.

## Posting a notification

The real API is one call. `TimerNotifier` (src/Aetherphone/Core/Notifications/TimerNotifier.cs) is the smallest producer:

```csharp
private void Notify(string title, string body)
{
    notifications.Notify(new PhoneNotification("timers", title, body, DateTime.Now, Accent));
}
```

With no `GroupKey`, all "timers" notifications stack into a single group. Chat-style producers pass a group key so each conversation stacks separately; `ChatNotifier` uses the conversation key, `tab:<id>` for a tab or the tell stream key for a person, and stamps the chat entry's own timestamp rather than `DateTime.Now`:

```csharp
private void Raise(string title, string body, string groupKey, DateTime at) =>
    notifications.Notify(new PhoneNotification(AppId, title, body, at, AppAccents.For(AppId), groupKey));
```

`CallHub` (src/Aetherphone/Core/Telephony/CallHub.cs) also sets `ChannelId` so call notifications resolve their settings under the `phone` channel instead of the messaging app:

```csharp
notifications.Notify(new PhoneNotification("message", message.From.DisplayName, Loc.T(L.Phone.IncomingCallBody),
    DateTime.Now, Accent, "call:" + message.From.UserId)
{
    ChannelId = NotificationChannels.PhoneChannel,
});
```

Get the `NotificationService` through constructor injection like every other service (see [Architecture](architecture.md)); it is constructed in src/Aetherphone/Core/PhoneServices.cs.

## The notification center

The center UI lives in `NotificationCenter` (src/Aetherphone/Windows/Components/NotificationCenter.cs) and is hosted in two places:

- `NotificationsApp` (src/Aetherphone/Apps/Notifications/NotificationsApp.cs), app id `notifications`, draws it as a full app screen.
- `ControlCenter` (src/Aetherphone/Core/Shell/ControlCenter.cs) draws the same component as an overlay panel via `DrawOverlay` inside the pull-down control center.

Both call `NotificationService.MarkAllRead()` on open (`NotificationsApp.OnOpened` and `ControlCenter.Open`), so opening either view zeroes the unread count immediately.

Behavior, all in `NotificationCenter`:

- **Stacking**: `BuildGroups` walks `Recent` newest-first and buckets by `StackKey`. A collapsed group shows the newest card on top with up to `MaxPeek` = 2 peeked card edges behind it and a count badge (`NotificationCard.DrawCountBadge`).
- **Expanding**: tapping a collapsed multi-item group expands it under a header with a "Show less" action. Groups with fewer than two items are forced collapsed by `SyncStates`.
- **Swipe to delete**: dragging a card left reveals a delete affordance; releasing past `SwipeCommitFraction` (42% of the card width) commits the removal. Swiping a collapsed group card removes the whole group (`NotificationService.RemoveGroup`); swiping an expanded row removes one item (`NotificationService.Remove`). Vertical drags scroll instead; the axis lock decides after 6 logical pixels of movement.
- **Tap**: a tap (small total movement) on a single card calls `NotificationRouter.Open`. A tap on a collapsed group expands it first.
- **Clear all**: the pill above the list calls `NotificationService.Clear()`.

## Banners

`NotificationBanner` (src/Aetherphone/Windows/Components/NotificationBanner.cs) subscribes to `NotificationService.Presented` and shows an iOS-style card that drops from the top of the screen on the Dear ImGui foreground draw list (ImGui is an immediate mode UI library: everything is redrawn every frame, so the banner is a state machine advanced each frame).

Rules, all in `OnPresented` and the stage machine:

- Skipped entirely when the phone window is hidden (`phoneVisible`) or when the notification's app is the one currently on screen (`currentAppId`).
- A new notification with the same `StackKey` as the active banner replaces its content in place and restarts the hold timer.
- At most `MaxQueued` = 4 banners wait in line; extras are dropped.
- A banner holds for `HoldSeconds` = 4 (paused while hovered or dragged), then animates out.
- Tap opens the notification through `NotificationRouter`; dragging up past a distance or velocity threshold dismisses it.

## Channels and per-app settings

`NotificationChannels.All` (src/Aetherphone/Core/Notifications/NotificationChannels.cs) is the catalog of settings channels: 21 entries, each a `NotificationChannel(AppId, Name, Accent)`, covering the messaging and social apps plus market, venues, muster, yellow pages, announcements, music, AetherStream, timers, character, health, housing, calendar, clock, notes, coin, and casino. There is one extra constant, `NotificationChannels.PhoneChannel` (`"phone"`), used as a `ChannelId` by call notifications; it is not part of `All`.

Settings storage is `Configuration.NotificationSettings`, a `Dictionary<string, AppNotificationSetting>` keyed by `SettingsKey`. `AppNotificationSetting` has exactly three knobs:

- `Enabled` (default true): `Configuration.IsAppNotificationEnabled` returns true when no entry exists, so every channel is on until the user turns it off. `NotificationService.Present` checks this and drops disabled notifications before they reach the center.
- `Sound` (default null): a per-channel sound token. `Configuration.ResolveNotificationToken` falls back to the global `Configuration.NotificationSound` when no override is set.
- `ShowNotificationBanner` (default true): the per-channel banner switch. The `Presented` event requires this and the global `Configuration.ShowNotificationBanner` together.

The UI is Settings > Notifications and Badges (`NotificationsPage`), which owns the `QuietWhileBusy` and global banner switches and lists every installed app that has a channel, `HasBadge`, or both, linking each to `AppNotificationPage` for whichever of the enable toggle, banner toggle, sound picker, and badge toggle apply. See [Hiding a badge](#hiding-a-badge) below.

## Sounds

There are two sound kinds (`SoundKind`): `Ringtone` for calls and `Notification` for everything else. Each kind has its own `SoundLibrary` built in src/Aetherphone/Core/PhoneServices.cs:

- Bundled files ship next to the plugin assembly under `Sounds/Ringtones` and `Sounds/Notifications` (source assets in src/Aetherphone/Sounds/).
- User files live under the plugin config directory in `Sounds/Ringtones` and `Sounds/Notifications`. `SoundService.AddUserFile` copies a picked file there; a user file with the same name as a bundled one wins (`SoundLibrary.TryResolvePath` checks the user directory first).
- Only `*.mp3` and `*.wav` are scanned.

Sounds are identified by tokens (`SoundTokens`): `file:<name>` for a file, `silent` for none, and empty string for the library default. Legacy `game:` tokens from old versions are migrated by `Configuration.MigrateSoundSettings`. Defaults are `SoundLibrary.BundledRingtoneToken` (`file:Ringtone_1.mp3`) and `SoundLibrary.BundledNotificationToken` (`file:Notification_1.mp3`).

Playback goes through `SoundService` on top of `SoundEffectPlayer`, which dispatches NAudio readers by file extension (`SoundEffectPlayer.OpenReader`): `.mp3` plays through the managed `Mp3FileReaderBase` with an `Mp3FrameDecompressor`, `.wav` through `WaveFileReader`, and `MediaFoundationReader` (Windows Media Foundation) is only the fallback, for other extensions and for files the managed readers reject. The managed-first order is what keeps sounds Wine-safe; src/Aetherphone/Sounds/README.md documents the dispatch. `PlayNotification(settingsKey)` plays a one-shot at `Configuration.NotificationVolume`, and `StartCallRing`/`StopCallRing` loop the ringtone at `Configuration.RingtoneVolume`. Both volumes are set with the continuous slider in the sound pages under Settings > Sounds, which links to Ringtone and Notification Sound (`VolumeSlider` in src/Aetherphone/Windows/Components/VolumeSlider.cs); it commits and previews on release so a drag does not save the config every frame. `NotificationService.ShouldPlaySound` throttles to one sound per `StackKey` per 3 seconds (`SoundRepeatSeconds`), so a burst in one conversation dings once.

## Deep links: what happens on tap

Tapping a card or banner calls `NotificationRouter.Open(PhoneNotification)` (src/Aetherphone/Core/Notifications/NotificationRouter.cs), which does four things:

1. For social notifications (`SocialType >= 0`), advances the read watermark via `SocialNotificationService.AcknowledgeUpTo`.
2. Removes the whole stack from the center (`NotificationService.RemoveGroup`). If the app is unavailable it stops here.
3. Parks the destination in the right launcher: the conversation key into `LinkpearlLauncher`, conversation ids into `DmLauncher`, `VelvetLauncher`, or `GramDmLauncher`, post or profile links into `SocialLauncher` (via `SocialDeepLink` in src/Aetherphone/Core/Apps/SocialLauncher.cs), live station ids into `RadioLauncher.RequestStation` (music app, type 21), `casino:<tableId>` group keys into `CasinoLauncher.RequestTable`, the AetherStream up-next suggestion into `AetherStreamLauncher.RequestUpNext`, and so on for Muster, Yellow Pages, Announcements, and moderation notices.
4. Calls `INavigator.Open(appId)`.

The contract that makes this land on the right screen: **`OnOpened` re-fires even when the app is already open.** `NavigationStack.OpenApp` (src/Aetherphone/Core/Apps/NavigationStack.cs) calls `NotifyOpened(app)`, and therefore `app.OnOpened()`, when the requested app is already the current one, instead of returning early. So an app never misses a deep link just because the user was already inside it.

For your app to support deep links you need:

- A launcher service with park-and-consume semantics. `DmLauncher` (src/Aetherphone/Core/Apps/DmLauncher.cs) is the template: `RequestConversation(id)` stores pending state, `TryConsumeConversation(out id)` returns it exactly once.
- A branch in `NotificationRouter.Open` that fills your launcher from the notification's `GroupKey`, `PostId`, or `ActorId`.
- Consumption at the top of your app's `OnOpened`, after resetting navigation state. `MessageApp.OnOpened` (src/Aetherphone/Apps/Message/MessageApp.cs) shows the pattern, abridged here; the full method also resets search state, refreshes contacts and conversations, and has a third branch for `TryConsumeUser`:

```csharp
public void OnOpened()
{
    router.Reset();
    activeTab = MessageTab.Chats;
    if (launcher.TryConsumeCalls())
    {
        activeTab = MessageTab.Calls;
    }
    else if (launcher.TryConsumeConversation(out var conversationId))
    {
        router.Push(MessageRoute.Thread(conversationId), false);
    }
}
```

Because `OnOpened` re-fires, treat it as re-entrant: reset first, then consume. Consume-once launchers guarantee a plain open (home screen tap) does not replay the last deep link.

## Badges

Badges on home screen tiles come from `IPhoneApp.BadgeCount` (src/Aetherphone/Core/Apps/IPhoneApp.cs), drawn by `HomeTileView` (src/Aetherphone/Windows/Components/Chrome/HomeTileView.cs). `BadgeAsDot` swaps the number for a dot (Settings uses it for the unseen changelog marker). Folder tiles combine the badges of the apps inside.

Badges are **not** driven by the notification center. Each app still computes its own unread number:

| App | BadgeCount source |
| --- | --- |
| NotificationsApp | `NotificationService.UnreadCount` |
| MessageApp | `store.UnreadTotal + calls.UnseenMissed` |
| ChirperApp | `social.UnseenCount(Id)` |
| AethergramApp | `dmStore.UnreadCount + social.UnseenCount(Id)` |
| AnnouncementsApp | `store.UnreadCount` |

For social apps, `SocialNotificationService.UnseenCount` prefers the server's `UnreadByApp` counts from the notification poll, with one override: while an acknowledgement is still queued for flush, the pending ack watermark wins over the server count, so the badge does not bounce back up between the ack and the next poll. With no server counts at all it falls back to counting items newer than the per-account watermark stored in `Configuration.SocialActivitySeenUnix`. Opening an app's activity screen calls `MarkSeen(appId)`, which clears the local count, removes that app's social notifications from the center, and sends a read acknowledgement to the backend when the watermark actually advanced, or, when the server still reported unread for that app, an acknowledgement up to the current time even though the local watermark stayed put. Tapping a single notification acknowledges only up to that item (`AcknowledgeUpTo`).

### Hiding a badge

Whether the count actually reaches the tile is a separate, generic on/off switch: `IPhoneApp.HasBadge` (default `false`) opts an app into it, and the enabled state lives in `Configuration.BadgeSettings`, a `Dictionary<string, bool>` keyed by app id with the same missing-entry-means-on default as `NotificationSettings` (`Configuration.IsAppBadgeEnabled`/`SetAppBadgeEnabled`). `HomeTileView` checks it centrally before drawing, so an app with `HasBadge` never needs to gate its own `BadgeCount` getter.

The toggle is not a separate screen: Settings > Notifications and Badges (`NotificationsPage`) builds one row per app from the live app list (`AppBundle.Apps`, threaded into `SettingsApp`/`NotificationsPage` the same way it already reaches `AppStoreApp`), showing any app that either has a notification channel (`NotificationChannels.Contains`) or `HasBadge`. `AppNotificationPage` then draws whichever sections apply to that app, top to bottom: Alerts (only when it has a channel), a "Show badge" row under Home Screen (only when `HasBadge` is true), then Sound (only when it has a channel and notifications are enabled). An app can have either, both, or (for most apps, which set neither) no row at all.

The minimized phone also shows `NotificationService.UnreadCount` as a badge (`MinimizedPhone.DrawBadge` in src/Aetherphone/Windows/Components/MinimizedPhone.cs).

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

`SocialActivity.IsModerationNotice` covers 5, 10, and 11; `SocialNotificationService.Ingest` skips those, because moderation content flows through `ModerationNoticeService` and `ModerationNoticePresenter` (src/Aetherphone/Core/Moderation/ModerationNoticePresenter.cs) instead, which posts non-blocking notices under the `settings` app id and shows blocking ones as alerts.

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
| Quiet while busy (default on) | `NotificationService.Present` via `PlayerBusy.Now` | Same silencing while in combat, in a duty, in a cutscene, or zoning |
| Banners off, globally or per channel | `NotificationService.Present` via `ShowNotificationBanner` | No banner (`Presented` never fires); shake and sound still happen |
| Sound throttle | `NotificationService.ShouldPlaySound` | Sound skipped within 3 s per stack |
| Phone hidden | `NotificationBanner.OnPresented` | No banner; center still gets it |
| App already on screen | `NotificationBanner.OnPresented` | No banner; center still gets it |
| Messages app uninstalled | `ChatNotifier.OnAppended` via its `AppGate` | No game chat notifications at all |
| Your own chat line | `ChatNotifier.OnAppended` via `entry.IsSelf` | No notification for messages you sent |
| Channel muted in a tab | `ChatNotifier.OnAppended` via `ChatTab.IsMuted` | Message still appended to history, no notification at all |
| Tab alerts set to Mentions or Off | `ChatNotifier.Alerts` | Only mentions notify, or nothing does |
| Conversation on screen | `ChatInbox.Viewing` | No notification for what you are already reading |
| Linkpearl pause | `LinkpearlNotificationGate.Paused` | Same: history yes, notification no |
| Thread being viewed | `ChatThreadStoreBase` viewing grace | No inbox notification for the open thread |
| Moderation dedup | `ModerationNoticePresenter.presented` set | Each pending notice id presented once |

The quiet-while-busy gate deserves emphasis because `Configuration.QuietWhileBusy` defaults to true: out of the box, alerts are silent in combat, duties, cutscenes, and while zoning (the `PlayerBusy.Now` states), with the notification still landing in the center and counting as unread. Users regularly report that silence as a bug; it is the shipped default, toggled in Settings > Notifications and Badges.

Do not disturb is toggled three ways: the switch on the phone chassis (`PhoneShell` via `DeviceChrome.MuteButtonRect`), the switch on the root Settings page (`RootSettingsPage`; the Notifications page does not host it, it only dims its two alert rows while it is on), and the `dnd` tile in the control center (`ControlRegistry` in src/Aetherphone/Core/ControlCenter/ControlRegistry.cs). While it is on, a moon shows in the status bar (`StatusBar` in src/Aetherphone/Core/Shell/StatusBar.cs) and the coin earn pill holds its pending toasts (`CoinEarnPill` in src/Aetherphone/Core/Shell/CoinEarnPill.cs).

Muting is per tab and per channel (`ChatTab.MutedChannels`), and a tab's `AlertPolicy` decides whether anything notifies at all. The same rule drives the unread badge in `ChatInbox`, so a channel that cannot notify you also cannot badge you. Legacy per-character linkshell mutes (`Configuration.MutedLinkshellsByCharacter`) are carried into any tab that later includes the channel.

The viewing grace deserves detail because it protects a correctness invariant. `ChatThreadStoreBase` (src/Aetherphone/Core/Message/ChatThreadStoreBase.cs) records `NoteThreadViewed(threadKey)` while a thread view draws, with a 4 second `ViewingGrace`. Two things key off it:

- The inbox scan skips notifying for the thread the user is looking at.
- Realtime chat pings call `RequestThreadRefresh`, which flags a pending refresh; `ConsumePendingThreadRefresh` only executes it while the open thread is being viewed (`IsBeingViewed`, inside the grace window). Refreshing a thread makes the server mark it read, so an ungated background refresh would silently mark threads read, suppress the sender's notification, and break seen ticks. The chat stores (`DirectMessagesStore`, `GramDmStore`, `VelvetStore`) all route pings through `RequestThreadRefresh`; keep it that way for any new chat surface.

## Gotchas

- `NotificationService.Notify` is enqueue-only; presentation happens on the next framework tick. Never assume the notification exists in `Recent` right after the call.
- `OnOpened` re-fires on an already-open app (`NavigationStack.OpenApp`). Apps that do heavy work or reset scroll state in `OnOpened` will do it again on every deep link; keep it cheap and idempotent, and make launcher consumption one-shot.
- An empty `GroupKey` stacks everything by app id. If your app can produce parallel streams (conversations, posts), pass a real group key or every stream collapses into one card and one swipe deletes all of it.
- `SettingsKey` is the channel id when `ChannelId` is set. Missed and incoming call notifications resolve under `phone`, which is not in `NotificationChannels.All`, so no Settings row toggles them; do not "fix" a call notification bug by editing the `message` channel.
- Opening the notification center or the control center marks everything read instantly (`MarkAllRead` in `NotificationsApp.OnOpened` and `ControlCenter.Open`). Do not rely on `UnreadCount` surviving a peek at the pull-down.
- Only the newest 50 notifications are retained (`MaxRetained`); the oldest is silently dropped, unread or not.
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
