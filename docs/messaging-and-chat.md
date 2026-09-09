# Messaging and chat

This page explains the shared chat stack: the reusable components that turn a list of messages into a full thread screen (bubbles, composer, menus, search, pagination), the stores that feed them, and the apps that consume them. Read it before you touch any conversation surface, whether that is the Message app, Velvet, Aethergram DMs, or the in-game Linkpearl app, and before you build a new one. Everything here is client side; the Aethernet backend is a separate ASP.NET repository, and this doc only describes what the plugin sends and expects. The realtime and HTTP plumbing is covered in [networking.md](networking.md), and the notification side in [notifications.md](notifications.md).

Two terms you will see throughout: Dalamud is the plugin framework that loads Aetherphone inside Final Fantasy XIV, and Dear ImGui is the immediate mode UI library it exposes, meaning every screen is redrawn from scratch every frame. There is no retained widget tree; a "component" here is a class that draws itself when you call its `Draw` method each frame.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Windows/Components/ChatThreadView.cs | Abstract base for a complete thread screen; wires all the pieces below together |
| src/Aetherphone/Core/Message/ChatThreadStoreBase.cs | Abstract base for a chat store: polling, paging, sending, reactions, edits, media |
| src/Aetherphone/Windows/Components/ChatTranscript.cs | Renders the scrolling bubble list; owns scroll, paging triggers, and read ticks |
| src/Aetherphone/Windows/Components/ChatComposer.cs | Input bar: draft, reply and edit bars, emoji picker, image, location, voice |
| src/Aetherphone/Windows/Components/ChatMenuController.cs | Right-click message menu plus the reaction strip |
| src/Aetherphone/Windows/Components/ChatSearchController.cs | In-thread text search bar with match stepping |
| src/Aetherphone/Windows/Components/ChatEntranceTracker.cs | Detects newly appended messages and drives their entrance animation |
| src/Aetherphone/Windows/Components/ChatText.cs | Message kind constants, preview text, and token-to-kind resolution |
| src/Aetherphone/Windows/Components/ChatActions.cs | Copy-message-to-clipboard helper |
| src/Aetherphone/Windows/Components/ChatBubble.cs | Lightweight standalone bubble, now used only by Yellow Pages inquiries |
| src/Aetherphone/Windows/Components/ChatHeaderControls.cs | Encryption lock, search toggle, and dismissible banners in thread headers |
| src/Aetherphone/Core/Aethernet/Clients/ChatClient.cs | HTTP endpoints for Message app conversations |
| src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs | `ChatMessageDto`, `ConversationDto`, and their page records |
| src/Aetherphone/Apps/Message/DirectMessagesStore.cs | The Message app's concrete store |
| src/Aetherphone/Core/GameChat/GameChannels.cs | The channel catalog: one row per game text channel |
| src/Aetherphone/Core/GameChat/ChatCapture.cs | One IChatGui subscriber for every channel, payload preserving |
| src/Aetherphone/Core/GameChat/ChatLog.cs | Capped buffers, one per conversation stream |
| src/Aetherphone/Core/GameChat/ChatArchive.cs | Per-character history on disk with retention |
| src/Aetherphone/Core/GameChat/ChatInbox.cs | The conversation list model, unread, read watermarks |
| src/Aetherphone/Windows/Components/GameChatThread.cs | The whole in-game thread screen, plus `GameChatTargets` |
| src/Aetherphone/Windows/Components/GameChatMenu.cs | Right-click menu for game chat lines and links, shared by the app and the pop-outs |
| src/Aetherphone/Windows/Components/InboxRowView.cs | Conversation row with hover quick actions (pin, mute, more) |
| src/Aetherphone/Core/GameChat/TellPreferences.cs | Pinned and muted tells |
| src/Aetherphone/Windows/LinkpearlPopouts.cs | The pop-out window pool: open, restore, persist, auto-pop tells |
| src/Aetherphone/Windows/LinkpearlPopoutWindow.cs | One floating chat window bound to one conversation |
| src/Aetherphone/Windows/PopoutTabs.cs | Tab list maths for a grouped pop-out, plus migration from the one-key state |
| src/Aetherphone/Core/GameChat/PopoutPresence.cs | Hides pop-outs in combat or a duty and brings them back |
| src/Aetherphone/Core/GameChat/LinkpearlHotkey.cs | The chord that opens and cycles recent conversations |
| src/Aetherphone/Core/GameChat/ChannelStyleStore.cs | Per channel colours and rules, with a no-override fast path |
| src/Aetherphone/Core/GameChat/MessageSplitter.cs | Splits a message past the byte budget into paced parts |
| src/Aetherphone/Windows/Components/Primitives/SoftWrapBuffer.cs | Maps the composer's wrapped display back onto the message the user typed |
| src/Aetherphone/Core/GameChat/ChatDrafts.cs | Draft per conversation, and the record of what was sent |
| src/Aetherphone/Core/Emoji/EmojiShortcodes.cs | Resolves :shortcode: spans for the game chat stack |
| src/Aetherphone/Core/Game/PlayerActions.cs | Friend, blacklist, adventurer plate and target, for a chat line |
| src/Aetherphone/Windows/Components/ChatLineView.cs | Compact one-line-per-message renderer |
| src/Aetherphone/Windows/Components/RunText.cs | Styled run layout with wrapping and hit testing |

## Two stacks, one bubble language

There are two chat stacks in the codebase:

- The **server-backed stack**: `ChatThreadStoreBase` plus `ChatThreadView`. Messages live on the Aethernet backend, arrive as DTOs (data transfer objects, the wire-format records) over HTTP, and support replies, reactions, edits, media, and encryption. Three apps use it.
- The **in-game stack**: the Linkpearl app renders real game chat captured through Dalamud's `IChatGui`. It has no server and no reactions, so it does not use `ChatThreadStoreBase` or `ChatThreadView`. It has its own screen, `GameChatThread`, which reuses `ChatTranscript` for bubble density and its own `ChatLineView` for compact density.

Both stacks share `ChatEntranceTracker` for the pop-in animation and the same visual language.

## Who consumes the stack

| App | In-app name | App class | Thread view | Store |
| --- | --- | --- | --- | --- |
| Message (id `message`) | "ChocoChat" (`L.Apps.Message`) | src/Aetherphone/Apps/Message/MessageApp.cs | `ThreadView : ChatThreadView<ChatMessageDto, ConversationDto>` in src/Aetherphone/Apps/Message/MessageApp.Thread.cs | `DirectMessagesStore` |

The Message app (ChocoChat to users, the name every surface shows) is the WhatsApp-shaped one: four icon-only tabs with hover tooltips (Chats, Calls, Contacts, Settings) drawn by `MessageApp.cs`, a Chats header whose search button slides a `SearchField` in above a centered filter `ChipRail`, one partial file per screen, and its own chrome helpers in `MessageApp.Chrome.cs` (header band, person rows, action rows, grouped card rows). Its colors come from the neutral `AppPalettes.Message` chrome plus a user-chosen chat theme (`MessageThemes`, stored as `Configuration.MessageChatTheme`) that recolors the accent, the sent bubbles, badges and buttons; the Settings tab hosts the theme and wallpaper pickers. Groups carry a photo and a description (`ConversationDto.AvatarUrl` and `Description`, edited through `PATCH /chats/{id}` with `UpdateConversationRequest`) and member roles (`ChatRoles`: member 0, owner 1, admin 2; `POST /chats/{id}/members/{userId}/role`); owners and admins can rename, describe, re-photo and manage members, and only the owner can remove or demote an admin. The group photo upload reuses the `avatar` media scope, so the server validates it exactly like a profile picture.
| Velvet (id `velvet`) | "Velvet" | src/Aetherphone/Apps/Velvet/VelvetShell.cs | `ThreadView : ChatThreadView<VelvetMessageDto, VelvetThreadDto>` in src/Aetherphone/Apps/Velvet/VelvetShell.Thread.cs | `VelvetStore` in src/Aetherphone/Apps/Velvet/VelvetStore.cs |
| Aethergram | "Aethergram" | src/Aetherphone/Apps/Aethergram/AethergramApp.cs | `ThreadView : ChatThreadView<GramMessageDto, GramThreadDto>` in src/Aetherphone/Apps/Aethergram/AethergramApp.Thread.cs | `GramDmStore` in src/Aetherphone/Apps/Aethergram/GramDmStore.cs |
| Linkpearl (id `messages`) | "Linkpearl" (`L.Apps.Linkpearl`) | src/Aetherphone/Apps/Linkpearl/LinkpearlApp.cs | `GameChatThread` in src/Aetherphone/Windows/Components/ | `ChatLog`, `ChatInbox` and `TabStore` in src/Aetherphone/Core/GameChat/ |

Naming note: the Message app is the one user-facing changelogs call "ChocoChat" (see `changelog.r0920.1` in src/Aetherphone/Localization/en.json). In code and localization keys it is always `message` / `MessageApp`.

Aethergram's `ThreadView` additionally implements `IChatTranscriptPostCards` and `IChatTranscriptStoryReplies`, the two optional transcript extensions for shared-post cards and story-reply context.

## Anatomy of a thread screen

`ChatThreadView<TMessage, TThread>.Draw(Rect area, string threadId)` composes everything, top to bottom:

1. **Thread switch.** If `store.CurrentThreadId` differs from `threadId` it calls `store.OpenThread`, clears the composer targets, closes search, and stops voice playback. Hooks `OnThreadSwitchingFrom` / `OnThreadOpened` let the app save and restore drafts (the Message app persists them in `Configuration.MessageDrafts`).
2. **Tick.** `TickThread` polls typing state and the open thread on the cadence passed to the constructor (`ThreadPollSeconds` is 3s for Message, 2.5s for Velvet and Aethergram), and sends a typing signal when the draft changes. When the store reports `RealtimePushActive`, the message poll stretches by `PushActivePollMultiplier` (3x) because pushes trigger refreshes instead.
3. **Header.** `DrawHeader` is abstract; apps draw their own title row and call `ChatHeaderControls.DrawLock` and `DrawSearchToggle` for the encryption lock and the search button.
4. **Search bar.** When `ChatSearchController.Open`, a 44px bar is inserted under the header. Search is a case-insensitive substring scan over the already loaded transcript (it does not query the server), skips system and deleted messages, and steps between matches via `transcript.RequestScrollTo`.
5. **Banners.** `DrawVaultBanner` shows the locked-vault or recovery-nudge banner; `DrawAboveTranscript` lets apps inject their own (the Message app shows a safety-number-changed banner).
6. **Backdrop.** `PaintTranscriptBackdrop(listRect)` is a virtual hook that paints behind the bubble list; the Message app paints the chat wallpaper there (`MessageWallpapers.Paint`: a solid color or a library photo, plus an optional glyph pattern), resolved per conversation from `Configuration.MessageChatWallpapers` with `MessageWallpaper` as the fallback.
7. **Transcript.** `ChatTranscript.Draw` renders the bubble list from a `ChatTranscriptModel`. The `Bubbles` field (a `ChatBubbleStyle`, supplied by the view's `BubbleStyle` property) overrides the outgoing and incoming fills and inks, the corner rounding, and whether the first bubble of a run gets a tail; a default style keeps the accent-on-wash look the other apps use.
8. **Composer.** `ChatComposer.Draw` renders the input bar plus an accessory bar when replying or editing. Three layouts exist: `Bar` (emoji inside the pill, image and location icons, round send), `Pill` (Aethergram's camera-first pill), and `Plus` (the Message app: a leading plus button that opens an attach menu for photo and location, the text pill with the emoji button inside it, and a round send button that becomes the microphone while the draft is empty).
8. **Message menu.** `ChatMenuController.Draw` renders the right-click menu over everything.

### ChatTranscript

`ChatTranscript` consumes a `ReadOnlySpan<TranscriptMessage>`. `TranscriptMessage` is a flat readonly struct (id, sender, body, kind, timestamps, reply snapshot, reactions, `TranscriptFlags` byte with `Encrypted`, `Placeholder`, `Unverified`, `Deleted`, `Forwarded`, `Edited`). It draws:

- day separator chips (`TimeText.DayLabel`) and sender grouping: consecutive messages from the same sender within `GroupWindowSeconds` (240s) are visually grouped, and in group threads the first bubble of a run carries the sender name. When the model supplies `IChatTranscriptSenders`, incoming runs in a group also get the sender's avatar in a gutter to the left (`SenderAvatarRadius`), with text, image and voice bubbles drawing the name inside the bubble the way WhatsApp does; card bubbles keep the name row above the run. Without that hook (Velvet, Aethergram) the name row stays above the run and nothing shifts,
- one bubble renderer per kind: text, image, voice note, shared post, story reply, plus card bubbles for location, muster invite, and Yellow Pages ad tokens detected inside text bodies,
- a typing indicator bubble driven by `model.OtherTyping`,
- follow-bottom scrolling (`SyncFollow`), scroll-to-message with a highlight flash (`RequestScrollTo`), and the load-older trigger described below.

Everything the transcript needs from the outside comes in through small interfaces on the model: `IChatTranscriptMedia` (image textures and clicks), `IChatTranscriptInteractions` (context menu, quote click, reaction click), `IChatTranscriptVoice` (playback state and toggle), `IChatTranscriptPaging` (older-page state), and the optional `IChatTranscriptPostCards` / `IChatTranscriptStoryReplies` / `IChatTranscriptSenders` (group sender avatars; ChocoChat resolves them from the member list, falling back to the avatar snapshot on the message). `ChatThreadView` implements the first four itself.

### ChatComposer

`ChatComposer` owns the draft string and the reply/edit targets. `BeginReply` and `BeginEdit` are mutually exclusive and show an accessory bar above the input. The bar exposes emoji (an `EmojiPicker` panel), image (`OnPickImage`), location (`OnShareLocation`), and voice buttons; the send button becomes a microphone when the draft is empty and `CanVoice` is set, which starts a `VoiceNoteRecorder`. Enter submits (`ImGuiInputTextFlags.EnterReturnsTrue`). The `ChatComposerModel` callbacks (`OnSendText`, `OnEditText`, `OnSendVoice`) route back into the store through `ChatThreadView`. Text length is capped by `ChatThreadView.MessageMax` (1000).

### ChatMenuController and ChatActions

Right-clicking a bubble calls `ChatThreadView.OpenMessageMenu`, which records the mouse anchor and defers the open to the menu pass drawn at the end of the same frame (an opened-frame guard keeps the triggering click from instantly dismissing it). The menu renders a reaction strip (six fixed tokens in `ReactionArt.Tokens`: `+1`, `heart`, `laugh`, `wow`, `sad`, `pray`) plus eight possible actions gated by `ChatMenuModel` capability flags: reply, forward, copy, star, edit, info, delete, report. Edit and info only appear on your own messages, and edit only on plain text (`kind == 0`). `ChatActions.CopyMessageText` copies the body (or a readable location summary) to the clipboard, refusing when an encrypted body is not yet decrypted.

The menu draws on ImGui's foreground draw list, so each app must call `threadView.GateMenus()` at the top of its `Draw` before other input handling; all three consuming apps do. `GateMenus` also gates the composer's attach menu (`ChatComposer.Gate`), which is a `DropdownMenu` anchored on the plus button of the `Plus` layout.

### ChatEntranceTracker

Tracks how many lines have "settled" per thread key. When the line count grows and the tail id changes, the new lines animate in for `TransitionTiming.BubbleSeconds`; `Progress(index)` returns 0..1 for the bubble renderers. A count jump with an unchanged tail id (an older page arriving) is treated as history and skips the animation.

## The message model

Server-backed messages are `ChatMessageDto` records (src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs). The important fields and behaviors:

| Concept | Where | Notes |
| --- | --- | --- |
| Kinds | `Kind` int; constants in `ChatThreadStoreBase`, `ChatTranscript`, and `ChatText` | 0 text, 1 image, 2 system row, 3 voice note, 4 shared post, 5 story reply |
| Derived kinds | `ChatText.EffectiveKind` | 6 location, 7 muster, 8 ad; these are kind 0 messages whose body is a token, resolved by `LocationShare.IsToken`, `MusterShare.IsToken`, `AdShare.IsToken` |
| Replies | `ReplyToId` plus `ReplySenderId/Name`, `ReplyBody`, `ReplyKind` | The server snapshots the quoted message onto the reply; the composer's reply bar and the in-bubble quote use `ChatText.QuotePreview`; clicking the quote scrolls to the original |
| Reactions | `Reactions: ReactionSummaryDto[]` | `SetReaction` applies an optimistic local update (`ApplyLocalReaction`) before the request; a reactor list screen loads via `LoadReactions` |
| Edits | `EditedAtUnix`; `EditMessage` | Only own plain-text messages; the stamp shows an "edited" label (`L.Message.EditedAt`) |
| Deletes | `Deleted`; `DeleteMessage` | The store tombstones locally (`Tombstone` strips the body, encryption fields, and reactions); the bubble renders a muted "deleted" row |
| Forwarding | `Forwarded` on the DTO, `ForwardOfId` on the send request; `DirectMessagesStore.ForwardMessage` | Bubbles show a "forwarded" label; encrypted text is decrypted and re-encrypted for the target thread, encrypted media cannot be forwarded |
| Voice notes | kind 3, `DurationSecs` | Recorded as WAV, uploaded via `MediaClient`; playback downloads, optionally decrypts, and caches bytes in `ChatThreadView`, played by `VoiceNotePlayer` |
| Images | kind 1, `MediaWidth/Height` | `SendImageMessage` re-encodes to JPEG capped at `DmImageMaxDimension` (1280), uploads, then creates the message with the media key |
| Location | token in body | `LocationShare.Compose` produces `[aep.loc.v1:territory;map;x;y;world;ward;plot;room]` (8 semicolon-separated fields); the transcript renders a card and clicking it calls `LocationShare.OpenMap`, and a "Go there" pill inside the card travels through `TravelPlanner` when the share resolves to a reachable destination |
| Starring | src/Aetherphone/Core/Message/StarredMessage.cs | Message app only; a local bookmark list in `Configuration.MessageStarredMessages`, not a server feature |

Encryption is out of scope here (see [networking.md](networking.md)); the short version is that `EncVersion == EnvelopeCodec.VersionEnvelope` marks an end-to-end encrypted body, `DecorateMessages` swaps in decrypted text via `MessageCipher`, and the transcript shows placeholder styling until decryption succeeds.

## History pagination

Paging is cursor based. Every page endpoint returns items plus an opaque `NextCursor` (`ChatMessagePage`, `ConversationPage`); the client echoes it back as a `?cursor=` query parameter (`ChatClient.MessagesAsync`). `null` means no more pages.

In `ChatThreadStoreBase`:

- `OpenThread` fetches the newest page and stores `olderCursor` / `hasMoreOlder`.
- `LoadOlder` fetches with the cursor and merges into the live array with `IdentifiedMerge.MergeById` under `messagesLock`: existing items not present in the page are kept, incoming items win on id collision, then everything is re-sorted by `CreatedAt` with the id as tiebreaker. Merging (rather than appending) makes older pages, refreshes, and optimistic sends commute.
- `RefreshThread` re-fetches only the newest page and merges the same way, so a refresh never throws away older pages you already loaded.
- Failed polls feed `NotePollResult`, which applies exponential backoff (up to roughly 40 seconds) shared by thread refresh, typing polls, and thread opens.
- The thread list pages the same way through `LoadMoreThreads` and `threadListCursor`.

Scroll restore lives in `ChatTranscript`:

- `MaybeLoadOlder` fires when the user scrolls within `LoadOlderThreshold` (48 logical px) of the top, the store has more, and the view is not following the bottom. Before calling `LoadOlder` it records the anchor `olderAnchorFromBottom = ScrollMaxY - ScrollY`, the distance from the bottom.
- While the anchor is active, `ApplyOlderRestore` pins `ScrollY = ScrollMaxY - anchor` every frame. Because the anchor is bottom-relative, prepending any number of messages leaves the viewport visually still.
- The anchor releases after the message count grows, `OlderSettleFrames` (2) frames pass, and the scroll has actually landed on the restored value (a mouse wheel tick in the next `NewFrame` overwrites a pending `SetScrollY`, so the pin is re-applied until it sticks), or after an `OlderRestoreTimeout` (20s) safety timeout if the load fails.
- A three-dot loading shimmer draws at the top while `LoadingOlder` is true.

## Read state and receipts

- **Ticks.** Outgoing bubbles get a stamp with the clock time plus one check mark (`PhoneIcons.Check`); when the message's `ReadAtUnix` is set, it becomes a double check (`PhoneIcons.Checks`) tinted `SeenTickColor` (`MeasureStamp` / `DrawStamp` in ChatTranscript.cs). Incoming bubbles never show ticks.
- **Per-member receipts.** The Message app's message info screen (src/Aetherphone/Apps/Message/MessageApp.MessageInfo.cs) shows group read state from `ConversationMemberDto.LastReadAtUnix`.
- **Unread counts.** Come from the server per thread (`ConversationDto.UnreadCount`). `ComputeUnread` sums them, skipping muted threads, and feeds the app icon badge.
- **Mark-read has two paths.** Fetching a conversation's message page is the implicit read acknowledgement (the server side of that watermark lives in the backend repo), and `ChatClient.MarkReadAsync` posts an explicit ack to `/chats/{id}/read`; `DirectMessagesStore.ApplyPushedMessage` calls it when a realtime-pushed message lands on a thread being viewed, since merging the push locally fetches no page. The implicit path has a sharp consequence on the client: any background code path that fetches an open thread will silently mark it read. That is why `ChatThreadStoreBase.RequestThreadRefresh` only sets a pending flag: `ConsumePendingThreadRefresh` calls `RefreshThread` only when `NoteThreadViewed` was called for that thread within the last `ViewingGrace` (4s), meaning the thread is actually on screen. The realtime ping handler (`DirectMessagesStore.OnChatPinged`) merges the pushed message or goes through `RequestThreadRefresh`, never straight to `RefreshThread`, for exactly this reason.
- **Notification suppression.** `NoteThreadViewed` also clears the thread's notification group, and `RaiseInboxNotifications` skips threads being viewed within the grace window, muted threads, and the first (priming) inbox poll after sign-in.

The inbox itself polls every 60 seconds in the foreground and 120 in the background (`PollCadence` with `PhoneVisibility`), and a realtime chat ping requests an immediate pass.

## In-game chat: Linkpearl

Linkpearl mirrors real game chat, so its data never touches Aethernet. A conversation there is either a person you tell or a tab the player built out of channels; both are one row in one list and both open the same thread.

- **The channel catalog.** `GameChannels` (src/Aetherphone/Core/GameChat/GameChannels.cs) is the single source of truth: one row per channel carrying its `XivChatType` values, send command, label, tint and category. Capture, the byte budget, the tints, the picker and localization all read that row, so adding a channel costs one entry. Battle and loot logs are deliberately absent, they are a log rather than a conversation.
- **Capture.** `ChatCapture` subscribes once to `IChatGui.ChatMessage` and resolves the channel through the catalog. It reads `ITextProvider`, which covers plain text and auto-translate together, and keeps payload data as `ChatChunk` values for items, map links, players, statuses, quests, party finder listings and other plugins' Dalamud links. Mentions of the local player are flagged here.
- **Streams.** `ChatLog` keys buffers by stream: a channel key for channels, or `tell:name@world` for one tell counterpart. That makes the `MaxLinesPerStream` cap and the history file per conversation rather than per channel. The log owns the sequence counter and has no Dalamud dependency.
- **Views.** A tab reads several streams at once through `ChatStreamView`, which appends live lines in place and only rebuilds when the stream set changes or history is restored. `ChatInbox` builds the conversation list on the same principle, plus unread counts and read watermarks.
- **Send.** `ChatSend` routes through the real chat box using the channel's command, measures the 500 byte cap against the actual prefix, and tracks pending sends. A sent line posts as a dimmed ghost and resolves when the game echoes it back; ten seconds without an echo becomes "Not delivered" with a retry.
- **History.** `ChatArchive` writes one file per conversation under `GameChat/<contentId>`, where the folder name is the character's ContentId as 16-digit lowercase hex (`ToString("x16")`), capped and flushed at most every 30 seconds. Retention is Off, Session, 30 days or Forever, resolved per channel with a global default, and `Configuration.ArchiveTellsToDisk` is the master switch that forces everything off. Legacy `MessageArchive` tell files import once per character.
- **Alerts and unread.** `ChatNotifier` honors the tab's `AlertPolicy`, per channel mutes, the global bell and the conversation on screen. `ChatInbox` counts unread by the same rule, so a channel that cannot notify you cannot badge you either.
- **Rendering.** `GameChatThread` is the whole screen below the header. Multi-channel tabs default to compact density (`ChatLineView`: one line per message, channel rail, collapsing sender rows); tells default to bubbles through the shared `ChatTranscript`. Both draw text through `RunText`, which lays out styled runs with wrapping and hit testing so every game link is tappable. `GameChatTargets.For(row)` turns an `InboxRow` into the `GameChatTarget` the thread opens, so the app screen and the pop-out windows open conversations the same way. A stream can hold `ChatLog.MaxLinesPerStream` (2000) lines, so the thread only draws a window over the tail: `WindowPageLines` (100) visible lines to start, growing by a page whenever the user scrolls within `LoadOlderThreshold` of the top. Bubbles get this through the transcript's `IChatTranscriptPaging` hook (the thread is its own synchronous pager); compact density grows the window in its own loop and keeps the viewport still by measuring where last frame's first line landed in content space, then shifting `ScrollY` by exactly that much in the same frame. Growth waits while the kinetic scroller is dragging, flinging, or rubber-banding, because `KineticScroller.SyncOffset` ignores external scroll changes in those states. `Reveal` (search hits, deep links) widens the window to include its target before either path draws.
- **Read state.** A watermark per conversation, persisted in `Configuration.LinkpearlSeen`, advanced only while the conversation is actually on screen. `ChatInbox.IsViewing` is true for the conversation open in the app (`Viewing`) and for any expanded pop-out that is hovered or focused (`SetAttended`); both the unread counter and `ChatNotifier` consult it. A collapsed pop-out never counts as viewing, so unread keeps climbing behind the badge on its bar even while the pointer rests on it. There are no receipts because the game has none.
- **Pin and mute.** Tabs pin through `TabStore.TogglePin` (capped at `MaxPinned`) and mute by setting `AlertPolicy.Off`; tells pin and mute through `TellPreferences` (`Configuration.LinkpearlPinnedTells` / `LinkpearlMutedTells`). `ChatInbox.TogglePinned` and `ToggleMuted` hide the difference. A muted row keeps counting `Unread` so the list can show a quiet dot, but it never adds to `TotalUnread` (the app badge) and never notifies; `InboxRow.HasBadge` is the one flag the row renderers read.
- **The list screen.** `LinkpearlApp` is a header (title, new chat, more), a search field, a paused-notifications banner, a filter `ChipRail` (All, Tells, Tabs, Unread, shown once there are four or more conversations), then pinned rows and the rest by activity. `InboxRowView` reveals pin, mute and more buttons on hover and returns an `InboxRowAction`; right-click opens the same `conversationMenu` the thread header uses. New tabs come from the `SheetSurface` behind the plus button (preset tiles for Free Company, Linkshells, Party and Local, a custom tab, or a tell via the People tab), so nothing lives at the bottom of the list any more.
- **Pop-out windows.** `LinkpearlPopouts` (src/Aetherphone/Windows/LinkpearlPopouts.cs) owns a pool of `LinkpearlPopoutWindow` Dalamud windows, one conversation each, registered in the plugin's `WindowSystem` next to the phone. They draw their own rounded surface, a title strip (avatar, title that opens a switch menu, unread pill, collapse, bell, open-in-phone, close) and a `GameChatThread`, and they keep working while the phone is closed or minimized. Placement is persisted in `Configuration.LinkpearlPopouts` and restored on load; opacity and text size come from `LinkpearlPopoutOpacity` / `LinkpearlPopoutTextScale`. With `LinkpearlPopoutTells` on, a new tell pops its own window automatically when the phone is not visible, unless notifications are paused, Do Not Disturb is on, or the tell is muted. A pop-out only marks its conversation read while it is hovered or focused, so an auto-opened window keeps its unread pill until the player looks at it. The context menu is the shared `GameChatMenu`; actions that need the phone (look up, market) hand off through `LinkpearlLauncher` and `Plugin.ShowPhoneApp`. Confirms are the exception: a window reserves a `ConfirmHosts` id, wraps its draw in `ConfirmHosts.Enter`, and draws its own `ConfirmOverlay` over its frame, so opening a link from a pop-out asks in that window rather than on a phone the player may have minimized. The frame is shielded while the card is up, Escape cancels it, and collapsing, unbinding, or suppressing the window drops the request through `ConfirmService.CancelHost`. Each window sets `UiScale.SetPhone` and `Plugin.Fonts.SetPhoneZoom` in `PreDraw` like `PhoneWindow` does, and disables `DragScrollHost` so the transcript scrolls with the wheel like a desktop window.
- **Grouping pop-outs as tabs.** A window holds up to six conversations: `LinkpearlPopoutState` carries `Keys` and `Active`, migrated lazily from the old single `Key` on restore, and `PopoutTabs` holds the pure list maths. A window with more than one conversation draws a `ChipRail` inside its title strip; a collapsed group hides the rail and shows the whole group's unread on the badge. Only the active tab is attended, so background tabs keep accruing unread. One `GameChatThread` per window is re-targeted on switch rather than one per tab, because each thread owns a `ChatStreamView` that subscribes to the log and merges hundreds of entries. When every window is full a new conversation joins the least recently active one instead of being dropped, and dragging a pop-out over another merges them.
- **Getting out of the way.** `PopoutPresence` ticks on `Framework.Update` and calls `LinkpearlPopouts.SetSuppressed`, which hides a window without unbinding it (`OnClose` ignores a close that a gate caused, so placement, size and collapsed state survive). It reads `TerritoryIntendedUse` for the field operation exemption and debounces both directions, and a message arriving for a hidden conversation brings the windows back. `LinkpearlHotkey` opens the most recent conversation and walks the list on repeat presses, refusing to fire while a text field has focus.
- **Channel styles.** `ChannelStyleStore` indexes `Configuration.LinkpearlChannelStyles` into an array sized by the channel catalog and answers from two branches when nothing is customized. Four inks per channel (sender and body, incoming and outgoing) are applied in `ChatLineView` and in `GameChatThread.Map`. Three rules ride along: never unread (which `ChatInbox` and `ChatNotifier` both consult, so a channel that cannot badge you cannot notify you either), hide outgoing (display only, capture and history keep the line), and hide from the game chat log. That last one calls `PreventOriginal` and is gated twice, by a master switch and a per channel flag, both off by default; the system and echo channels can never be hidden, and anything outside the catalog never reaches the check.
- **Writing.** `GameComposer` measures its own height so `GameChatThread` can lay the transcript out against it, wrapping to `LinkpearlComposerMaxLines` before it scrolls, with Enter sending and Shift+Enter breaking the line. The widget holds a soft wrapped copy of the draft, so `SoftWrapBuffer` remembers the breaks it inserted and merges every edit back onto the logical message, which is what keeps a wrap from reaching the wire as a line break. `MessageSplitter` cuts a message past `ChatSend.Budget` on whitespace, never mid word or mid rune, and `ChatSend.SendSplit` drains the parts from the framework tick at a configured interval rather than in one burst. `ChatDrafts` keeps a draft per conversation key, which is what makes a pop-out tab switch restore the right one, and records sent messages so one that never arrived can be copied back out of settings.
- **Emoji in game chat.** `EmojiShortcodes` mirrors `EmojiScanner`: a fast reject that allocates nothing when a line holds no colon, then spans and glyphs. `ChatRuns` splits plain text on those spans inside its per message cache, so a `TextRun` can be an emoji rather than a string and `RunText` gives it a square advance and hands it to `EmojiRender`. What travels the wire is still the shortcode text, because the game cannot carry emoji over its own chat.
- **Collapsing a pop-out.** ImGui cannot collapse a window that has no title bar, so the window collapses itself down to its own title strip: the chevron button, or a double-click on the bar away from the title and the buttons, flips `collapsed`, and a `Spring` drives the height between the expanded size and `TitleHeight`. Three rules make it hold together. `SizeConstraints` is rebuilt every frame in `PreDraw` because a minimum height of `MinHeight` would otherwise clamp the forced collapsed size straight back up, and `NoResize` joins the flags while the window is collapsed or moving so the grip cannot fight the animation. The window keeps its own `expandedSize`, updated only while it is fully expanded and at rest, and `Snapshot` persists that rather than the live frame, so a collapsed window comes back at the size it had. The top edge always stays still: only the height changes, `Position` is left alone for the whole animation, and the bar sits where it already was no matter where on screen the window is parked. Collapsing also closes the switch menu, the chat menu and the composer's channel menu, because an open `DropdownMenu` blocks input through `Gate` every frame whether or not anything draws it, and a menu clamped into a 44px frame has nowhere to go. Settings collapses and expands every open pop-out at once next to Close all pop-outs.

## Adopting the stack for a new surface

If your new surface talks to Aethernet, you write two subclasses and reuse everything else.

**1. The store.** Subclass `ChatThreadStoreBase<TMessage, TThread>`; both type parameters must implement `IIdentified` (an `Id` string). You implement the transport verbs and field accessors, and inherit polling, backoff, pagination, merge, optimistic reactions, tombstoning, media upload with optional encryption, inbox notifications, and report evidence collection:

```csharp
internal sealed class SupportStore : ChatThreadStoreBase<ChatMessageDto, ConversationDto>
{
    protected override string ImageUploadScope => "support-image";

    protected override async Task<MessagePage?> FetchMessagesPageAsync(
        string threadId, string? cursor, CancellationToken token)
    {
        var page = await client.MessagesAsync(threadId, cursor, token).ConfigureAwait(false);
        return page is null ? null : new MessagePage(page.Items, page.NextCursor);
    }

    protected override long MessageTimeOf(ChatMessageDto message) => message.CreatedAtUnix;
}
```

That is a sketch of three of roughly thirty abstract members; `DirectMessagesStore` is the reference implementation and most overrides are one-line delegations like these.

**2. The view.** Subclass `ChatThreadView<TMessage, TThread>`. You implement `MapTranscript` (DTO array to `TranscriptMessage[]`), `DrawHeader`, `BuildMenuModel` (which menu actions your surface offers), `BeginReply`, the field accessors (`KindOf`, `BodyOf`, `SenderIdOf`, ...), and the navigation pushes (`OpenImageView`, `OpenReactions`, `PushImagePickerScreen`, `PopScreen`) that route to your app's `ViewRouter`. You inherit the transcript, composer, search, menus, voice playback, the image viewer, the image picker grid, and the reactors screen; your routes just call back into `DrawImageViewer`, `DrawImagePicker`, and `DrawReactions`.

**3. Per-frame wiring.** In your app's `Draw`, call `threadView.GateMenus()` before drawing routes, and call `threadView.OnAppClosed()` from the app's close hook so recording and playback stop. Pass your poll cadences to the base constructor.

Copy-on-write is the contract between the two: the store publishes messages as an immutable `TMessage[]` snapshot and replaces the whole array on any change (`CopyOnWrite.Append`, cloned arrays in `ReplaceMessage` and `ApplyLocalReaction`). `ChatThreadView.BuildTranscript` re-runs `MapTranscript` only when the array reference changes, which is what keeps a full remap off the per-frame path.

If your surface is local-only like Linkpearl, skip both bases and compose `ChatTranscript` or `ChatLineView`, `ChatEntranceTracker`, and your own store instead. Linkpearl is the worked example.

## Gotchas

- **Never mutate a message in place.** The transcript cache keys on the array reference (`BuildTranscript` in ChatThreadView.cs). If a store edits a `TMessage` inside the existing array instead of publishing a new array, the UI will never repaint that change.
- **Background fetches mark threads read.** The message page fetch acks the thread as read; the explicit `ChatClient.MarkReadAsync` exists only for pushed messages that bypass the fetch. Route any push- or timer-driven refresh through `RequestThreadRefresh`, which `ConsumePendingThreadRefresh` gates on `IsBeingViewed`, never straight to `RefreshThread`, or you will silently clear unread state and suppress notifications for threads the user never saw. This regressed once and the gate in `ChatThreadStoreBase` is the fix; keep it.
- **`GateMenus` is mandatory.** `ChatMenuController` draws on the foreground draw list, above every widget. Apps must call `threadView.GateMenus()` at the top of their `Draw` (see `MessageApp.Draw`); skip it and clicks leak through the open menu into the UI underneath.
- **Two ids that read alike.** The server-backed Message app is app id `message`; the in-game Linkpearl app is app id `messages`. Notifications and gates use these keys (`PhoneNotification("message", ...)` vs `PhoneNotification("messages", ...)`), so grep for the exact string.
- **Short threads never auto-load older pages.** `MaybeLoadOlder` bails when `ImGui.GetScrollMaxY() <= 0f`, so a first page that does not fill the viewport has no scrollbar and therefore no trigger. Do not assume every thread eventually pulls its full history.
- **Layout caches sweep, they do not clear.** `RunText` and `RichTextCache` stamp each layout with the frame it was last used and, once past their threshold, drop only entries idle for more than a frame (at most one sweep per frame). The old clear-everything policy made any list longer than the cap re-wrap every row every frame, which is what dropped a 2000-line Linkpearl history from the high 80s to the low teens.
- **Location, muster, and ad shares are kind 0.** They are plain text messages carrying a token; the transcript detects them per frame with `TryParse` and `ChatText.EffectiveKind` resolves them for menus and previews. If you add a token type, wire all three spots (bubble, `EffectiveKind`, `QuotePreview`/`ListPreview`) or it will render as raw text somewhere.
- **The entrance tracker keys on the tail id.** `ChatEntranceTracker.Sync` animates only when the count grows and the tail id changed; prepended history is deliberately silent. Pass a stable tail id or older pages will pop like new messages.
- **Wrapped newlines are not part of the message.** The game composer types into a soft wrapped copy of the draft, so the ImGui buffer carries newlines the user never pressed. `SoftWrapBuffer` tracks the breaks it inserted and merges each edit back into the logical text, and only a Shift+Enter break survives into `MessageSplitter`. Never read the widget buffer as the message, and never try to tell a soft break from a typed one by re-measuring the text: that guess is what once baked a wrap into the draft and sent one party message as five.
- **Game chat history is a per channel promise.** Retention is resolved per channel, so a channel shared by two tabs has one history and one policy; editing Keep history in a tab writes through to its channels and reads back as Mixed when they disagree. `ChatArchive` drives the character switch order (flush, clear, switch, load), so nothing else should clear `ChatLog`.

## Related docs

- [UI toolkit](ui-toolkit.md): Typography, AppSkin, UiInteract, and the widget conventions the chat components are built on
- [App framework](app-framework.md): IPhoneApp, routing, and badges that host a thread screen
- [Networking](networking.md): the Aethernet client, realtime signals, and the encryption envelope
- [Notifications](notifications.md): how inbox notifications, groups, and deep links work
- [State and persistence](state-and-persistence.md): Configuration, per-character data, and media storage
- [Game integration](game-integration.md): IChatGui and the other Dalamud services Linkpearl relies on
