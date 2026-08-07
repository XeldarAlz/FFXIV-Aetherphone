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
| src/Aetherphone/Windows/Components/ChatBubble.cs | Lightweight standalone bubble used by Linkpearl |
| src/Aetherphone/Windows/Components/ChatHeaderControls.cs | Encryption lock, search toggle, and dismissible banners in thread headers |
| src/Aetherphone/Core/Aethernet/Clients/ChatClient.cs | HTTP endpoints for Message app conversations |
| src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs | `ChatMessageDto`, `ConversationDto`, and their page records |
| src/Aetherphone/Apps/Message/DirectMessagesStore.cs | The Message app's concrete store |
| src/Aetherphone/Core/Linkpearl/ | In-game chat capture, tell and linkshell stores, disk archive |

## Two stacks, one bubble language

There are two chat stacks in the codebase:

- The **server-backed stack**: `ChatThreadStoreBase` plus `ChatThreadView`. Messages live on the Aethernet backend, arrive as DTOs (data transfer objects, the wire-format records) over HTTP, and support replies, reactions, edits, media, and encryption. Three apps use it.
- The **in-game stack**: the Linkpearl app renders real game chat (tells and linkshells) captured through Dalamud's `IChatGui`. It has no server, no message ids, and no reactions, so it uses the lightweight `ChatBubble` component directly instead of `ChatThreadView`.

Both stacks share `ChatEntranceTracker` for the pop-in animation and the same visual language.

## Who consumes the stack

| App | In-app name | App class | Thread view | Store |
| --- | --- | --- | --- | --- |
| Message (id `message`) | "Message" (`L.Apps.Message`) | src/Aetherphone/Apps/Message/MessageApp.cs | `ThreadView : ChatThreadView<ChatMessageDto, ConversationDto>` in src/Aetherphone/Apps/Message/MessageApp.Thread.cs | `DirectMessagesStore` |
| Velvet (id `velvet`) | "Velvet" | src/Aetherphone/Apps/Velvet/VelvetShell.cs | `ThreadView : ChatThreadView<VelvetMessageDto, VelvetThreadDto>` in src/Aetherphone/Apps/Velvet/VelvetShell.Thread.cs | `VelvetStore` in src/Aetherphone/Apps/Velvet/VelvetStore.cs |
| Aethergram | "Aethergram" | src/Aetherphone/Apps/Aethergram/AethergramApp.cs | `ThreadView : ChatThreadView<GramMessageDto, GramThreadDto>` in src/Aetherphone/Apps/Aethergram/AethergramApp.Thread.cs | `GramDmStore` in src/Aetherphone/Apps/Aethergram/GramDmStore.cs |
| Linkpearl (id `messages`) | "Linkpearl" (`L.Apps.Linkpearl`) | src/Aetherphone/Apps/Linkpearl/LinkpearlApp.cs | none; draws `ChatBubble` per line in LinkpearlApp.Chats.cs | `MessageStore` and `LinkshellStore` in src/Aetherphone/Core/Linkpearl/ |

Naming note: the Message app is the one user-facing changelogs call "ChocoChat" (see `changelog.r0920.1` in src/Aetherphone/Localization/en.json). In code and localization keys it is always `message` / `MessageApp`.

Aethergram's `ThreadView` additionally implements `IChatTranscriptPostCards` and `IChatTranscriptStoryReplies`, the two optional transcript extensions for shared-post cards and story-reply context.

## Anatomy of a thread screen

`ChatThreadView<TMessage, TThread>.Draw(Rect area, string threadId)` composes everything, top to bottom:

1. **Thread switch.** If `store.CurrentThreadId` differs from `threadId` it calls `store.OpenThread`, clears the composer targets, closes search, and stops voice playback. Hooks `OnThreadSwitchingFrom` / `OnThreadOpened` let the app save and restore drafts (the Message app persists them in `Configuration.MessageDrafts`).
2. **Tick.** `TickThread` polls typing state and the open thread on the cadence passed to the constructor (`ThreadPollSeconds` is 3s for Message, 2.5s for Velvet and Aethergram), and sends a typing signal when the draft changes. When the store reports `RealtimePushActive`, the message poll stretches by `PushActivePollMultiplier` (3x) because pushes trigger refreshes instead.
3. **Header.** `DrawHeader` is abstract; apps draw their own title row and call `ChatHeaderControls.DrawLock` and `DrawSearchToggle` for the encryption lock and the search button.
4. **Search bar.** When `ChatSearchController.Open`, a 44px bar is inserted under the header. Search is a case-insensitive substring scan over the already loaded transcript (it does not query the server), skips system and deleted messages, and steps between matches via `transcript.RequestScrollTo`.
5. **Banners.** `DrawVaultBanner` shows the locked-vault or recovery-nudge banner; `DrawAboveTranscript` lets apps inject their own (the Message app shows a safety-number-changed banner).
6. **Transcript.** `ChatTranscript.Draw` renders the bubble list from a `ChatTranscriptModel`.
7. **Composer.** `ChatComposer.Draw` renders the input bar (56px) plus an accessory bar when replying or editing.
8. **Message menu.** `ChatMenuController.Draw` renders the right-click menu over everything.

### ChatTranscript

`ChatTranscript` consumes a `ReadOnlySpan<TranscriptMessage>`. `TranscriptMessage` is a flat readonly struct (id, sender, body, kind, timestamps, reply snapshot, reactions, `TranscriptFlags` byte with `Encrypted`, `Placeholder`, `Unverified`, `Deleted`, `Forwarded`, `Edited`). It draws:

- day separator chips (`TimeText.DayLabel`) and sender grouping: consecutive messages from the same sender within `GroupWindowSeconds` (240s) are visually grouped, and in group threads a sender name row appears above each run,
- one bubble renderer per kind: text, image, voice note, shared post, story reply, plus card bubbles for location, muster invite, and Yellow Pages ad tokens detected inside text bodies,
- a typing indicator bubble driven by `model.OtherTyping`,
- follow-bottom scrolling (`SyncFollow`), scroll-to-message with a highlight flash (`RequestScrollTo`), and the load-older trigger described below.

Everything the transcript needs from the outside comes in through small interfaces on the model: `IChatTranscriptMedia` (image textures and clicks), `IChatTranscriptInteractions` (context menu, quote click, reaction click), `IChatTranscriptVoice` (playback state and toggle), `IChatTranscriptPaging` (older-page state), and the optional `IChatTranscriptPostCards` / `IChatTranscriptStoryReplies`. `ChatThreadView` implements the first four itself.

### ChatComposer

`ChatComposer` owns the draft string and the reply/edit targets. `BeginReply` and `BeginEdit` are mutually exclusive and show an accessory bar above the input. The bar exposes emoji (an `EmojiPicker` panel), image (`OnPickImage`), location (`OnShareLocation`), and voice buttons; the send button becomes a microphone when the draft is empty and `CanVoice` is set, which starts a `VoiceNoteRecorder`. Enter submits (`ImGuiInputTextFlags.EnterReturnsTrue`). The `ChatComposerModel` callbacks (`OnSendText`, `OnEditText`, `OnSendVoice`) route back into the store through `ChatThreadView`. Text length is capped by `ChatThreadView.MessageMax` (1000).

### ChatMenuController and ChatActions

Right-clicking a bubble calls `ChatThreadView.OpenMessageMenu`, which records the mouse anchor and defers the open to the menu pass drawn at the end of the same frame (an opened-frame guard keeps the triggering click from instantly dismissing it). The menu renders a reaction strip (six fixed tokens in `ReactionArt.Tokens`: `+1`, `heart`, `laugh`, `wow`, `sad`, `pray`) plus eight possible actions gated by `ChatMenuModel` capability flags: reply, forward, copy, star, edit, info, delete, report. Edit and info only appear on your own messages, and edit only on plain text (`kind == 0`). `ChatActions.CopyMessageText` copies the body (or a readable location summary) to the clipboard, refusing when an encrypted body is not yet decrypted.

The menu draws on ImGui's foreground draw list, so each app must call `threadView.GateMenus()` at the top of its `Draw` before other input handling; all three consuming apps do.

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
| Location | token in body | `LocationShare.Compose` produces `[aep.loc.v1:territory;map;x;y;world;ward;plot;room]` (8 semicolon-separated fields); the transcript renders a card and clicking it calls `LocationShare.OpenMap` |
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
- The anchor releases after the message count grows and `OlderSettleFrames` (2) frames pass, or after an `OlderRestoreTimeout` (20s) safety timeout if the load fails.
- A three-dot loading shimmer draws at the top while `LoadingOlder` is true.

## Read state and receipts

- **Ticks.** Outgoing bubbles get a stamp with the clock time plus one check mark; when the message's `ReadAtUnix` is set, it becomes a double check tinted `SeenTickColor` (`MeasureStamp` / `DrawStamp` in ChatTranscript.cs). Incoming bubbles never show ticks.
- **Per-member receipts.** The Message app's message info screen (src/Aetherphone/Apps/Message/MessageApp.MessageInfo.cs) shows group read state from `ConversationMemberDto.LastReadAtUnix`.
- **Unread counts.** Come from the server per thread (`ConversationDto.UnreadCount`). `ComputeUnread` sums them, skipping muted threads, and feeds the app icon badge.
- **Mark-read is implicit.** `ChatClient` has no mark-read endpoint. Fetching a conversation's message page is the read acknowledgement; the server side of that watermark lives in the backend repo. This has a sharp consequence on the client: any background code path that fetches an open thread will silently mark it read. That is why `RefreshThreadIfVisible` exists; it only refreshes when `NoteThreadViewed` was called for that thread within the last `ViewingGrace` (4s), meaning the thread is actually on screen. The realtime ping handler (`DirectMessagesStore.OnChatPinged`) uses it instead of `RefreshThread` for exactly this reason.
- **Notification suppression.** `NoteThreadViewed` also clears the thread's notification group, and `RaiseInboxNotifications` skips threads being viewed within the grace window, muted threads, and the first (priming) inbox poll after sign-in.

The inbox itself polls every 60 seconds in the foreground and 120 in the background (`PollCadence` with `PhoneVisibility`), and a realtime chat ping requests an immediate pass.

## In-game chat: Linkpearl

Linkpearl mirrors real game chat, so its data never touches Aethernet:

- **Capture.** `ChatBridge` (src/Aetherphone/Core/Linkpearl/ChatBridge.cs) subscribes to Dalamud's `IChatGui.ChatMessage` and keeps only `XivChatType.TellIncoming` and `TellOutgoing`. It resolves the sender's `PlayerPayload` to a `Name@World` send target and appends a `ChatLine` (direction, text, timestamp) to `MessageStore`. `LinkshellBridge` does the same for `Ls1`..`Ls8` and `CrossLinkShell1`..`CrossLinkShell8` via `LinkshellChannels.TryResolve`, tagging incoming lines with a `MessageAuthor` so group bubbles can show avatar and name.
- **Send.** Outgoing text goes through the real chat box: `ChatSender.TrySend("/tell Name@World ...")` for tells, and the channel's command (`LinkshellChannel.Command`, `/linkshell1`..`/cwlinkshell8`) for linkshells. The echoed game message is what lands in the store, so there is no optimistic append.
- **History.** Tells persist per conversation: `MessageArchive` writes one JSON file per conversation (named by a SHA-256 hash of the lowercased send target) under a per-character folder keyed by ContentId, keeping the last `MaxStoredLines` (500) lines, gated on `Configuration.ArchiveTellsToDisk`. The trash button on a thread calls `MessageStore.Remove`, which also deletes the archive file. Linkshell history is memory only and clears on character switch.
- **Mutes.** `LinkshellMuteStore` keeps a per-character muted set in `Configuration.MutedLinkshellsByCharacter`. Muting a channel suppresses its notifications and removes it from `LinkshellStore.TotalUnread`, but lines still append, so history keeps flowing. Separately, `LinkpearlNotificationGate` is a single bell toggle that pauses all tell and linkshell notifications at once.
- **Rendering.** Threads draw `ChatBubble.Draw(line, theme, entrance, group)` per line with a `ChatEntranceTracker` and a simple follow-bottom loop; the composer is a plain `InputTextWithHint` pill (LinkpearlApp.Chats.cs). No pagination: the whole loaded history renders every frame.
- **Read state.** `Conversation.MarkRead()` zeroes a local unread counter when a thread opens; there are no receipts because the game has none.

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

If your surface is local-only like Linkpearl, skip both bases and compose `ChatBubble`, `ChatEntranceTracker`, and your own store instead.

## Gotchas

- **Never mutate a message in place.** The transcript cache keys on the array reference (`BuildTranscript` in ChatThreadView.cs). If a store edits a `TMessage` inside the existing array instead of publishing a new array, the UI will never repaint that change.
- **Background fetches mark threads read.** There is no explicit read-ack request; the message page fetch is the ack. Route any push- or timer-driven refresh through `RefreshThreadIfVisible`, not `RefreshThread`, or you will silently clear unread state and suppress notifications for threads the user never saw. This regressed once and the gate in `ChatThreadStoreBase` is the fix; keep it.
- **`GateMenus` is mandatory.** `ChatMenuController` draws on the foreground draw list, above every widget. Apps must call `threadView.GateMenus()` at the top of their `Draw` (see `MessageApp.Draw`); skip it and clicks leak through the open menu into the UI underneath.
- **Two ids that read alike.** The server-backed Message app is app id `message`; the in-game Linkpearl app is app id `messages`. Notifications and gates use these keys (`PhoneNotification("message", ...)` vs `PhoneNotification("messages", ...)`), so grep for the exact string.
- **Short threads never auto-load older pages.** `MaybeLoadOlder` bails when `ImGui.GetScrollMaxY() <= 0f`, so a first page that does not fill the viewport has no scrollbar and therefore no trigger. Do not assume every thread eventually pulls its full history.
- **Location, muster, and ad shares are kind 0.** They are plain text messages carrying a token; the transcript detects them per frame with `TryParse` and `ChatText.EffectiveKind` resolves them for menus and previews. If you add a token type, wire all three spots (bubble, `EffectiveKind`, `QuotePreview`/`ListPreview`) or it will render as raw text somewhere.
- **The entrance tracker keys on the tail id.** `ChatEntranceTracker.Sync` animates only when the count grows and the tail id changed; prepended history is deliberately silent. Pass a stable tail id or older pages will pop like new messages.
- **Linkshell history is volatile.** Only tells archive to disk, and only when `Configuration.ArchiveTellsToDisk` is on; linkshell threads clear on every character switch (`LinkshellStore` subscribes to `CharacterWatch.Changed`). Do not build features that assume linkshell scrollback survives.

## Related docs

- [UI toolkit](ui-toolkit.md): Typography, AppSkin, UiInteract, and the widget conventions the chat components are built on
- [App framework](app-framework.md): IPhoneApp, routing, and badges that host a thread screen
- [Networking](networking.md): the Aethernet client, realtime signals, and the encryption envelope
- [Notifications](notifications.md): how inbox notifications, groups, and deep links work
- [State and persistence](state-and-persistence.md): Configuration, per-character data, and media storage
- [Game integration](game-integration.md): IChatGui and the other Dalamud services Linkpearl relies on
