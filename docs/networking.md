# Networking and the Aethernet backend

This doc covers the client side of everything online in Aetherphone: the HTTP layer, sessions and sign-in, the install-source gate, the realtime websocket, voice calls, rate limiting, end-to-end encryption, media transfer, and how to point a dev build at a non-production backend. Read it before touching any feature that talks to a server. The backend itself ("Aethernet") is a separate ASP.NET service in its own repository; only its client-visible contract is described here.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Net/HttpService.cs | The shared `HttpClient` wrapper: JSON calls, GET 429 pauses, edge-shed retry, ETag cache, retried byte downloads |
| src/Aetherphone/Core/Net/AethernetClientIdentity.cs | Aethernet host match, `X-Aep-Source` and `X-Aep-Build` headers, and the `X-Aep-Source-Status` reply |
| src/Aetherphone/Core/Net/EtagCache.cs | In-memory ETag store for conditional GETs |
| src/Aetherphone/Core/Net/DiskCache.cs | Size-capped on-disk byte cache used by media caches, optionally DPAPI-sealed |
| src/Aetherphone/Core/Media/MediaCache.cs | Bytes to GPU texture cache with failure cooldowns |
| src/Aetherphone/Core/Net/RequestThrottle.cs | Concurrency plus minimum-interval gate (Lodestone, Universalis, radio-browser, housing, collections, song search) |
| src/Aetherphone/Core/Runtime/PollCadence.cs | Poll intervals for the stores, stretched while the websocket is up |
| src/Aetherphone/Core/Aethernet/AethernetSession.cs | Token, base URL, sign-in state, install-source status, per-character session slots |
| src/Aetherphone/Core/Aethernet/AethernetTransport.cs | Session-aware request builder over `HttpService` |
| src/Aetherphone/Core/Aethernet/AethernetApi.cs | Facade that constructs the typed domain clients for one app scope |
| src/Aetherphone/Core/Aethernet/Clients/ | One typed client per API domain (auth, chats, media, keys, ...) |
| src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs | Request and response records for the wire contract |
| src/Aetherphone/Core/Aethernet/SignInFlow.cs | Lodestone and Rising Stones challenge plus XIVAuth device-flow state machines |
| src/Aetherphone/Core/RealtimeSignalBus.cs | In-process event bus fed by the websocket |
| src/Aetherphone/Core/Telephony/RealtimeConnection.cs | The websocket itself: connect, receive loop, reconnect |
| src/Aetherphone/Core/Telephony/CallSignalRouter.cs | Routes websocket messages to the bus and to `CallHub` |
| src/Aetherphone/Core/Telephony/CallHub.cs | The call state machine: lifecycle, timeouts, call log, reconnect grace |
| src/Aetherphone/Core/Telephony/CallSession.cs | Per-call glue between capture, the mixer, and the websocket |
| src/Aetherphone/Core/Telephony/Audio/ | Opus codec settings, microphone capture, and the playback mixer |
| src/Aetherphone/Core/Telephony/MediaFrame.cs | Binary framing for audio packets on the websocket |
| src/Aetherphone/Core/Crypto/CryptoBox.cs | ECDH identities, `EC1.` key wrapping, AES-GCM seal and open |
| src/Aetherphone/Core/Crypto/EnvelopeCodec.cs | `AE1.` message envelopes with commitment tags |
| src/Aetherphone/Core/Crypto/KeyVault.cs | Device key lifecycle, local key cache, recovery codes |
| src/Aetherphone/Core/Shell/RateLimitPill.cs | The "Too many requests" pill in the phone shell |

## What the Aethernet backend is

Aethernet is the hosted service behind every social feature: accounts, chats, feeds, calls, media storage, and moderation. It is an ASP.NET application maintained in a separate repository, so nothing in this repo builds or runs it. The plugin talks to it two ways:

- HTTPS requests against a base URL (`Configuration.DefaultAethernetBaseUrl` in src/Aetherphone/Configuration.cs): `https://api.aetherphone.net` in stable Release builds, the development instance in Debug and Beta builds (see [Dev vs prod endpoints](#dev-vs-prod-endpoints)).
- One websocket per signed-in session at the same host, path `/rt` (built in `RealtimeConnection.BuildUri`).

Features that are purely local (notes, calculator, most mini-games) never touch the network. Many apps call third-party services directly: Universalis (market), Faloop (hunts, plus its own socket), LRCLIB (lyrics), lodestonenews.com (news), FFXIV Venues and Partake (venues), radio-browser and the stations themselves (radio), FFXIV Collect (collections), the housing APIs, Rolladeck, YouTube (songs), the Lodestone through NetStone, GitHub for the plugin catalog and the mpv, yt-dlp, and deno downloads, and the plugin's own repository manifest for update checks. None of them get the Aethernet token or the `X-Aep-*` headers. Most ride `HttpService`; `RadioPlayer`, `MediaDependencies`, the Doom asset fetch, NetStone, and YoutubeExplode bring their own HTTP clients.

## The HTTP layer

`HttpService` (src/Aetherphone/Core/Net/HttpService.cs) owns the pooled `HttpClient` that the Aethernet clients and most third-party services share. It uses a 20 second timeout per request (60 seconds for uploads) and caps response bodies at 32 MB. It sends no default `User-Agent`: `GetJsonAsync` adds one only when the caller passes `userAgent`, and only `LrcLibClient` does (name, version, and repository URL), on purpose, because LRCLIB asks clients to identify themselves. The separate clients in `RadioPlayer`, `MediaDependencies`, and `DoomAssets` set a bare product name of their own. All JSON goes through source-generated `System.Text.Json` type infos (`AethernetJsonContext` in src/Aetherphone/Core/Aethernet/AethernetJsonContext.cs), so serialization never uses runtime reflection.

Every Aethernet-specific header is gated on the request host. `AethernetClientIdentity.Matches` (src/Aetherphone/Core/Net/AethernetClientIdentity.cs) compares the request host with the host of the live `AethernetSession.BaseUrl`, re-resolved whenever the base URL changes, so the Settings > About test server toggle takes effect without a reload. Third-party hosts and presigned storage URLs never see these headers. The headers that matter:

- `Authorization: Bearer <token>` when the caller passes a session token. `AethernetTransport` passes it for session calls; uploads get it only on the API host (see [Media upload and download](#media-upload-and-download)).
- `X-Aep-App: <scope>` when the request comes from an app-scoped `AethernetApi` instance and targets the Aethernet host. `AppRegistry` (src/Aetherphone/Core/Apps/AppRegistry.cs) and `PhoneServices` build separate instances with scopes like `"chirper"`, `"velvet"`, `"yellowpages"`, or `"casino"`. The server uses it to pick which per-app identity (profile, handle, badges) to render in the response, falls back to the account identity for scopes it does not know (such as `"dm"`), and marks responses `Vary: X-Aep-App`. A wrong scope returns the wrong identity.
- `X-Aep-Request-Id`: a fresh random id stamped by `ApplyHeaders` on every request to the Aethernet host (JSON calls, and upload PUTs when they target that host; the raw byte downloads of `GetBytesAsync` skip it). The server echoes it back, error responses carry it into `AepFailure.RequestId`, and it is the string to quote when matching a client failure to a server-side log line.
- `X-Aep-Source` and `X-Aep-Build`: the install source and build, added by `AethernetIdentityHandler`, a `DelegatingHandler` in `HttpService`'s pipeline, to every request to the Aethernet host (byte downloads included), and by `RealtimeConnection` on the websocket handshake. See [The install-source gate](#the-install-source-gate).

`AethernetTransport` sits between typed clients and `HttpService`. It prefixes `AethernetSession.BaseUrl` onto relative paths, attaches the session token, and short-circuits to `default` when the user is not signed in. Every session request's status code is funneled into `AethernetSession.ReportAuthStatus` (anonymous calls skip that sink), which is how a 401 anywhere flips the session into the `TokenRejected` state.

A typed client method looks like this (real code from src/Aetherphone/Core/Aethernet/Clients/KeysClient.cs):

```csharp
public async Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token,
    Action<AepFailure>? onFailure = null)
{
    var status = 0;
    var keys = await net.GetAsync("/keys/me", AethernetJsonContext.Default.MyKeysDto, token,
        statusCode => status = statusCode, onFailure).ConfigureAwait(false);
    return (keys, status);
}
```

Note the error model: typed clients do not throw on HTTP failures. A `null` result can mean a network error, a non-2xx status, a rate-limit pause, or simply "not signed in". Two callbacks let a caller distinguish. The primary channel is `onFailure`: every session-token method on `AethernetTransport` threads an optional `Action<AepFailure>` down into `HttpService`, and nearly every typed client method exposes it, as above. `AepFailure` (src/Aetherphone/Core/Net/AepFailure.cs) names the failure with an `AepFailureKind` (`Offline`, `Timeout`, `RateLimitPaused`, `SignedOut`, `Server`, `BadResponse`, `Cancelled`) and, for server failures, carries the status code, the error body's `Code` and message, and the echoed `RequestId`. The older `onStatus` callback still reports the raw HTTP status code when that is all a caller needs.

GET responses that carry an `ETag` header (a server-provided version stamp) are cached in `EtagCache`. The next identical GET sends `If-None-Match`, and a `304 Not Modified` answer is served from the cached body. Cache keys include the bearer token and the app scope, so different accounts and app scopes never share entries.

## Sessions, tokens, and sign-in

### The session

`AethernetSession` holds the signed-in state. The token itself lives in `Configuration.AethernetToken` and is persisted through Dalamud's plugin configuration. Because one player can own several characters, the session also keeps a `Configuration.CharacterSessions` dictionary keyed by the character's ContentId (the game's stable character identifier); switching characters stashes the active token and encryption key cache into a `CharacterSession` slot and loads the new character's slot. `SignOut` calls exist, and `AuthClient.RevokeTokenAsync` deletes the token server-side via `DELETE /auth/token`.

A token is valid for 90 days from sign-in, with no sliding renewal (backend `TokenFactory.TokenLifetime`). After that every session request answers 401 `token_expired`, the session flips to `TokenRejected`, and the user signs in again.

### Lodestone challenge flow

Sign-in proves character ownership through the Lodestone, Square Enix's public character site. `SignInFlow.StartLodestone` posts the character name and world to `/auth/challenge` (anonymous, no token) and receives a `ChallengeResponse` with a short code. The Settings Account page then walks the user through the steps you can read in src/Aetherphone/Core/Localization/L.cs: copy the code, paste it into your Lodestone profile, then verify. `SignInFlow.VerifyChallenge` posts the challenge id to `/auth/verify`; on success the server returns a token plus a `UserDto`, and `AethernetSession.SignIn` persists both. A challenge (Lodestone or Rising Stones) expires 30 minutes after it is issued.

### Rising Stones challenge flow

The Chinese game version has no Lodestone, so ownership is proven through Rising Stones (石之家), its counterpart. `SignInFlow.StartRisingStones` posts the profile UID to `/auth/risingstones/challenge` (`AuthClient.RisingStonesChallengeAsync`, anonymous) and receives the same `ChallengeResponse` shape; the user pastes the code into their Rising Stones personal signature instead of a Lodestone profile. Verification is shared: the same `SignInFlow.VerifyChallenge` posts the challenge id to `/auth/verify`. The flow differs only in copy and in three dedicated failure reasons (`risingstones_profile_not_found`, `risingstones_code_not_found`, and `risingstones_unavailable` in `VerifyFailure`).

### XIVAuth device flow

`SignInFlow.StartXivAuth` offers an alternative: `/auth/xivauth/start` returns a verification URL and user code, the client opens the browser, and `PollXivLoopAsync` polls `/auth/xivauth/poll` until the flow completes, times out, or fails.

### Typed verify reasons

The verify endpoints answer expected failures inside the body rather than with HTTP error codes. `VerifyResponse` (src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs) carries `Ok`, an optional `Reason` string, and on success the token and user. The reason strings are mirrored client-side as constants in `VerifyFailure` (src/Aetherphone/Core/Aethernet/VerifyResult.cs): `character_not_found`, `code_not_found`, `banned`, `rate_limited`, and so on. `AuthClient.VerifyAsync` wraps everything in a `VerifyResult`; when the transport itself returned nothing, it synthesizes `VerifyFailure.Network` (or `RateLimited` on a 429). `SignInFailureText.Resolve` maps each reason to localized title and body copy, and a `banned` reason routes through `AethernetSession.ReportBanned` with the optional `SuspensionDto` details.

### Companion device link

Settings > Linked Devices (src/Aetherphone/Apps/Settings/Pages/LinkedDevicesPage.cs, hidden until the version row in Settings > About is tapped enough times to set `Configuration.LinkedDevicesUnlocked`) signs a companion device into the same account with a token of its own. The signed-in plugin calls `AuthClient.NewDeviceLinkAsync` (`POST /auth/device-link/new`) and shows the returned code as a QR code, polls `GET /auth/device-link/status/{code}` until a device claims the code and reports its name, then finishes with `POST /auth/device-link/approve` once the user approves. The claim itself comes from the companion device, not the plugin. This links a session; moving the encryption key between PCs is a separate flow (see [Key storage and recovery](#key-storage-and-recovery)).

### The install-source gate

Aethernet knows where each copy of the plugin was installed from. `InstallSource.Initialize` (src/Aetherphone/Core/Updates/InstallSource.cs) records Dalamud's `SourceRepository` (or `DEV` for a dev plugin) and the assembly's informational version, and every Aethernet request and the websocket handshake carry them as `X-Aep-Source` (left off when Dalamud reports no source) and `X-Aep-Build`. The backend keeps a status per install source that its operators manage, and stamps `X-Aep-Source-Status: warned` or `blocked` on authenticated responses. `AethernetIdentityHandler` reads that header off every Aethernet response and hands it to `AethernetSession.ReportSourceStatus`:

- `warned` queues a notice, once per plugin session, that `InstallSourceNotice.Poll` (called from `PhoneShell.Draw`) shows with a copyable link to the official repository.
- `blocked` sets `IsSourceBlocked` and `TokenRejected`, which signs the user out: `IsSignedIn` turns false, `AethernetTransport` stops sending session requests, `CallHub.Reconcile` stops the websocket, and the blocked notice shows. The server refuses the token from that source too (401 `source_blocked`).
- Sign-in is refused up front from a blocked source: the challenge starts answer 403 `source_blocked`, and verify and XIVAuth start return `source_blocked` as the reason, which `SignInFailureText` turns into copy that links the official repository.

## The realtime layer

### The websocket

`RealtimeConnection` (src/Aetherphone/Core/Telephony/RealtimeConnection.cs) opens a `ClientWebSocket` to `<base url as wss>/rt` with the bearer token in an `Authorization` header plus `X-Aep-Source` and `X-Aep-Build`. Keepalive pings go every 15 seconds, and a peer that does not answer within 20 seconds (`KeepAliveTimeout`) drops the connection. Text frames are `CallControl` JSON messages; binary frames are call audio. The class lives under Core/Telephony for historical reasons, but it carries all realtime traffic, not only calls.

Limits on the client: an inbound message over 1 MB drops the connection, and a send that does not finish within 10 seconds aborts the socket so the reconnect loop takes over. Limits on the server (backend `RealtimeEndpoint` and `CallRegistry`): any inbound message over 64 KB ends the connection, binary audio frames over 4 KB are not relayed, a user holds at most four sockets (a fifth evicts the oldest), and a full global connection cap answers the handshake with 503.

### What flows over it

`CallSignalRouter` dispatches each `CallControl.Type` (constants in `SignalType`, src/Aetherphone/Core/Telephony/Contracts/Signals.cs). The families:

- Call signaling: `call.incoming`, `call.roster`, `call.declined`, `call.unavailable`, `call.ended`, `call.handled`, forwarded to `CallHub` (see [Calls](#calls) below).
- Notification pings, published onto `RealtimeSignalBus`: `chat.ping`, `velvet.ping`, `gram.ping`, `ads.ping`, `social.ping`, `muster.ping`, `announce.ping`, `poll.ping`, `feedback.ping`, `keys.stale`, `keys.linkPending`, and `content.removed`. `chat.ping` carries the conversation id and, when the server includes it, the pushed message itself. `social.ping` carries `App` and `ContentKind` (`notification` or `notice`) so each listener refreshes only the surface it names (`SocialSignal.CoversApp`, `CoversNotices`). `keys.linkPending` raises `DeviceLinkRequested` for `DeviceLinkWatcher` (see [Key storage and recovery](#key-storage-and-recovery)).
- Typing pushes: `chat.typing`, `velvet.typing`, `gram.typing`, and `ads.typing` carry the thread id and publish `TypingPinged`.
- Casino and game rooms: every type under the `casino.` prefix (attach, snapshot, event, private, ended, ...) is published as one `CasinoSignal` through `RealtimeSignalBus.PublishCasino`, and the casino stores (src/Aetherphone/Core/Casino/) drive live tables from it. The `game.` prefix works the same way through `PublishGame` as a `GameSignal`, consumed by `GameRoomsStore` (src/Aetherphone/Core/Games/GameRoomsStore.cs) for the Games app's online rooms.
- Radio rooms: every type under the `radio.` prefix is published raw through `PublishRadio`, and `RadioRoomRouter` (src/Aetherphone/Core/Radio/RadioRoomRouter.cs) drains it on the framework thread into `RadioRoomSession`, the live chat, reactions, and requests around a community radio station.
- Jam traffic: every type under the `jam.` prefix belongs to the Music app's listening parties (Jam). `JamSignalRouter` (src/Aetherphone/Core/Jam/JamSignalRouter.cs) subscribes to the connection directly and feeds `JamSession`.
- Stream traffic: every type under the `stream.` prefix belongs to AetherStream watch-along sessions. `StreamSignalRouter` (src/Aetherphone/Core/Telephony/StreamSignalRouter.cs) subscribes to the same connection and dispatches the join, roster, state, queue, and kick messages to `WatchAlongSession` (src/Aetherphone/Core/Video/WatchAlongSession.cs); `CallSignalRouter` itself only suppresses its unhandled-signal warning for the `stream.` and `jam.` prefixes. When the host plays a local file, the published `Url` is an `aep-local:` fingerprint token (sampled SHA-256, byte size, and file name; src/Aetherphone/Core/Video/LocalMediaIdentity.cs) instead of a filesystem path, and each viewer maps the token to their own copy of the file before playback syncs. The server projects the host's position and stamps every `stream.state` and `stream.joined` with its own clock in `StateAtUnixMs`; a viewer keeps a `ServerClock` (src/Aetherphone/Core/Video/ServerClock.cs) from those stamps (the largest of the last eight `stamp - received` samples, so network delay never inflates the estimate) and projects the host's position every framework tick from it. `PlaybackSyncController` (src/Aetherphone/Core/Video/PlaybackSyncController.cs) turns the drift into a correction: nothing inside 0.3 s, an mpv `speed` nudge of up to 5 % that ramps in by 1.5 s of drift, and a hard seek only past 3 s, followed by a 2 s settle. Speed goes back to 1 whenever the host pauses, the state is older than 30 s, or a new file loads. A viewer whose player fails on the room's link sends one `stream.playbackFailed` (url, cleaned reason) per link; the server relays it to the host only, and only while the room is still on that link, as `stream.viewerFailed`, which the host's player surfaces as a "N of M watching can't play this" card with Skip. Party features ride on the same prefix behind a capability bit: both sides send `Features` (`StreamFeature.Party`), and the client hides invite codes, host handover, co-host control, and reactions until the server reports the bit. A room can publish a six-character invite `Code`; `stream.join` with a `Code` skips the contact gate but never the block check, which is how players on other worlds and data centers get in. `stream.state` carries `GuestPermissions` for the whole room and per-member `Members` flags (`StreamPermission`: add to queue, control playback, can host). A viewer who holds the control grant sends `stream.control` (play, pause, seek, next); the server relays it to the host as `stream.controlRequest` and the host applies it to its own player, so the host stays the single source of playback truth. `stream.transfer` hands the room to a capable viewer and every member receives `stream.hostChanged`; the server sends the same message when it promotes a successor after the host's socket drops and the host does not come back within the server's 30 s reconnect grace. A dropped socket keeps its seat through that grace, so `WatchAlongSession` watches `StreamSignalRouter.ConnectedChanged` and resumes once the socket is back: a host republishes at once instead of waiting for its 8 s heartbeat, a viewer sends `stream.join` for the same host and takes its seat back without the contact gate, retrying every 3 s while the room is unavailable (a backend restart loses every room until its host republishes), and a viewer already playing the room's link resyncs instead of reloading it. A join still waiting for an answer is sent again. The room's rules (approval, discovery, invite code, guest permissions) travel in that message, and the new host keeps them as a session-only `PartyPolicy` (src/Aetherphone/Core/Video/PartyRules.cs) instead of publishing its own saved defaults; a host that opened its own party reads the same struct from `Configuration`. `stream.leave` names the host being left, so a viewer who is promoted while its leave is in flight passes the room on rather than closing it. `stream.react` fans out as `stream.reaction` for the emoji bursts over the screen, and the screen pose in `stream.state` includes pitch, roll, and curve. When the queue runs dry the host keeps the room open for a five-minute grace window (`PartyIdle` in src/Aetherphone/Core/Video/PartyRules.cs) with the screen on standby instead of leaving.

`RealtimeSignalBus` is a plain in-process event hub. Stores subscribe to the ping that concerns them (`ChatPinged`, `SocialPinged`, ...) and react by refreshing immediately. `ContentRemoved` carries a `ContentRemovalSignal` so every phone purges moderated content without waiting for a poll. The bus is not receive-only: `BindSender` and `TrySend` expose an outbound path onto the socket (bound to `CallSignalRouter.Send` while the router lives), which is how `CasinoRoomSession`, `GameRoomSession`, and `RadioRoomSession` send their attach, detach, resync, and room requests upstream.

The socket runs whenever a session is signed in. `CallHub.Reconcile` (src/Aetherphone/Core/Telephony/CallHub.cs) re-evaluates that on every session change: it calls `router.Start()` while signed in and `router.Stop()` when the session is no longer signed in (a sign-out, a 401, a ban, or a blocked install source), and when the session token changes it ends any active call, stops the socket, and starts it again with the new token. Turning calls off is different: when `Configuration.CallsEnabled` is false `Reconcile` ends the active call but leaves the socket running, so notification pings keep flowing.

### Reconnect behavior

`RealtimeConnection.RunAsync` reconnects forever until stopped. If a connection survived at least 60 seconds it counts as healthy and the next retry happens after 0.5 to 5 seconds; otherwise the base delay doubles per attempt toward a 15 second ceiling and is then multiplied by 0.5 to 1.5 of jitter. `RealtimeSignalBus.SetActive` broadcasts `ConnectedChanged`, and several stores answer the reconnect with `PollCadence.RequestAfterReconnect`, which schedules a resync poll at a random moment within the next 20 seconds. Nothing missed during the outage stays missed, and a server restart does not bring every client back in the same instant.

### The polling backstop

Polling is the fallback; pushes drive refreshes while the socket is up. Stores poll on a `PollCadence` (src/Aetherphone/Core/Runtime/PollCadence.cs), which picks a foreground or background interval based on `PhoneVisibility` and supports `RequestImmediate` for realtime pings. A cadence built with the signal bus stretches to a 15-minute safety poll while the socket is connected, unless the phone is visible and the surface called `NoteWatched` within the last second (each chat app does while its inbox is on screen). The intervals below are the ones that apply while realtime is down or the surface is watched; all three services pass the bus:

| Service | Foreground | Background |
| --- | --- | --- |
| `SocialNotificationService` (src/Aetherphone/Core/Notifications/SocialNotificationService.cs) | 60 s | 120 s |
| `ChatThreadStoreBase` inbox (src/Aetherphone/Core/Message/ChatThreadStoreBase.cs) | 60 s | 120 s |
| `AccountStateService` (src/Aetherphone/Core/Aethernet/AccountStateService.cs) | 120 s | 300 s |

So even with a dead websocket, notifications arrive within roughly two minutes.

## Calls

Voice calls are the reason Core/Telephony exists. They ride the same websocket as everything else: signaling travels as `CallControl` text frames, audio as binary frames, and the server forwards each participant's audio frames to everyone else in the call. The client never opens a peer-to-peer connection; the websocket is the whole transport. Everything below is client behavior unless it names the backend.

### Call lifecycle

`CallHub` (src/Aetherphone/Core/Telephony/CallHub.cs) is the state machine. `CallState` (src/Aetherphone/Core/Telephony/CallState.cs) is `Idle`, `Dialing`, `Ringing`, `Connecting`, `Active`, or `Ended`, though the hub never rests in `Ended`: every teardown path resets straight to `Idle`.

**Outgoing.** `StartCall` generates the call id client-side (`Guid.NewGuid()`), moves to `Dialing`, writes an outgoing entry to the call log, and sends `call.start` with the invitee's user id. The server answers with `call.roster` updates listing every participant with a `Slot`, a `State` (`ringing`, `active`, `left`), and identity fields. The moment a roster shows at least one other active participant, the hub starts audio and flips to `Active`. If the last pending invitee declines (`call.declined`), the server reports the callee unavailable (`call.unavailable`), or 60 seconds pass with no answer (`DialingTimeoutSeconds`), the call ends and `ConfirmService` shows a localized outcome alert.

**Incoming.** `call.incoming` carries the caller as a `ParticipantInfo`. The hub declines automatically, with reason `unavailable` and a missed-call log entry, when calls are disabled or the Message app is uninstalled (the `AppGate` built as `installer.Gate("message")` in src/Aetherphone/Core/PhoneServices.cs), and with reason `busy` when another call is in progress. Otherwise it enters `Ringing`, starts the ringtone loop, posts the incoming-call notification, and raises `IncomingCallPresented`, which `Plugin.BringPhoneForward` (shared with the alarm ringer) uses to maximize and open the phone window. `Accept` sends `call.accept` and moves to `Connecting`; the next roster drives it to `Active`. Ringing that nobody touches for 60 seconds auto-declines and logs a missed call, and `call.handled` (another of your sessions answered) silently stops the local ring.

**Group calls.** `AddParticipant` sends `call.invite` with the same call id and writes another outgoing log entry. The roster is the only truth about who is in the call; the UI renders whatever it says. The server caps a call at 16 participants (backend `CallRegistry`). Once audio is running, a roster with no other active participants left tears the call down locally.

**Ending.** `Hangup` sends `call.leave`; `call.ended` ends it from the server side. Every user- or server-initiated end (hang-up, decline, `call.ended`, the timeouts, connection loss, sign-out) funnels through the private `EndCall`, which resets to `Idle`, stops the ringtone, plays the call-end sound when audio had connected, disposes the audio session, and shows the outcome alert for declined, unavailable, no-answer, and dropped calls. The one exception is the roster-emptied case above: `HandleRoster` resets state directly, with no alert, no end sound, and no `call.leave`.

**The call log.** `CallLogStore` (src/Aetherphone/Core/Telephony/CallLogStore.cs) persists the last 50 entries in `Configuration.CallLog`, merging consecutive same-direction calls with the same peer into a single row with a count. Calls missed while the plugin was closed still appear: the server also delivers missed calls as social notification type 20, and `CallHub` folds those into the log with a 3-minute dedup window so a live miss and its server echo do not double up.

Ringing loops the ringtone chosen in `Configuration.RingtoneSound` (`SoundService.StartCallRing`) and posts a notification on the `phone` channel; full story: [Notifications](notifications.md).

### The audio pipeline

When a roster first shows another active participant, `CallAudioController` (src/Aetherphone/Core/Telephony/CallAudioController.cs) builds one `CallSession` (src/Aetherphone/Core/Telephony/CallSession.cs) and stops any music playing through `PlaybackHub`, so the call has the speakers to itself.

Capture: `AudioCapture` (src/Aetherphone/Core/Telephony/Audio/AudioCapture.cs) records 48 kHz mono 16-bit PCM in 20 ms buffers through NAudio's `WaveInEvent`, on the microphone picked in Settings (`Configuration.CallInputDevice`, resolved by device name in `AudioDevices`). Each 960-sample frame is first scaled by the microphone gain (`Configuration.CallInputGain`, 0 to 2, saturating), then passes an RMS noise gate (opens at 0.018, closes at 0.010, with 12 frames of hangover), so silence is never encoded. Mute simply stops encoding; nothing is sent while you are muted, and no mute signal goes to the server (the `call.mute` constant exists in `SignalType` but the client never sends it). Open frames are encoded with Opus through the managed Concentus codec; `OpusAudio` pins the settings (VOIP application, voice signal type, VBR, complexity 5, native library disabled) at the 28 kbps bitrate `AudioCapture` hands it.

Framing: `MediaFrame` (src/Aetherphone/Core/Telephony/MediaFrame.cs) prepends a 20-byte header made of a version byte, the 16-byte call id, the sender's slot byte, and a little-endian 16-bit sequence number, and the whole packet goes out as one binary websocket frame via `RealtimeConnection.SendMediaAsync`.

Playback: binary frames arrive on `RealtimeConnection.MediaReceived`. `CallSession` drops frames from other calls and from its own slot, then hands the payload to `VoiceMixer` (src/Aetherphone/Core/Telephony/Audio/VoiceMixer.cs), which keeps one Opus decoder and a 600 ms overflow-discarding jitter buffer per remote slot, mixes all slots into one float stream, tracks a per-slot RMS level (`CallHub.LevelOf` feeds the speaking indicator from it), scales each slot by its per-person gain (0 for a person muted for me), scales the sum by the master call volume (`Configuration.CallOutputVolume`, 0 to 2), clamps, and plays on shared-mode WASAPI with 140 ms latency (`AudioOutputFactory.Create`, falling back to waveOut). The speaker is `Configuration.CallOutputDevice`, a WASAPI endpoint friendly name that `AudioDevices.FindOutput` resolves to an `MMDevice` (the system default when unset or unplugged). Per-person volumes and mutes persist by user id in `Configuration.CallPeerVolumes` and `CallPeerMuted`; `CallAudioController.SyncRemotesLocked` reapplies them to slots on every roster. Switching either device mid-call restarts only that end (`CallSession.SwitchInput` and `SwitchOutput`, run off the UI thread) without touching the jitter buffers.

### Surviving websocket drops

A mid-call socket drop does not end the call. `CallHub` starts a 20 second grace window (`ReconnectGraceMs`), during which `CallView.Connected` reads false and the in-call UI shows a reconnecting state. If `RealtimeConnection` reconnects inside the window (its retry loop is described under [reconnect behavior](#reconnect-behavior) above), the hub sends `call.rejoin`, plus a fresh `call.accept` when the drop happened while `Connecting`, and the next `call.roster` restores everything, including slots. If the window expires, `CallHub.Advance` ends the call as connection-lost and alerts the user. `Plugin.OnCallsTick` pumps `Advance` on every framework tick, independent of the phone window, so this grace window and the 60 second ring and dial timeouts keep running while the phone is minimized or closed. The server keeps its own 20 second disconnect grace (backend `CallRegistry`), matching `ReconnectGraceMs`. The one exception is `Ringing`: an unanswered incoming call is abandoned the moment the socket drops and logged as missed.

### Where the UI lives

- The Message app owns the call surfaces: src/Aetherphone/Apps/Message/MessageApp.Calls.cs draws the Calls tab with the persisted log, the in-call screen (`MessageRoute.Call`), and the green return-to-call banner, and `MessageApp.SyncCallRoute` pushes and pops the call route as `CallState` changes.
- The full-screen incoming-call overlay is shell chrome, not app UI: `IncomingCallOverlay` (src/Aetherphone/Windows/Components/Sheets/IncomingCallOverlay.cs) draws over everything while the state is `Ringing`.
- The Dynamic Island (src/Aetherphone/Core/Shell/DynamicIsland.cs) shows the live call outside the app with one button (Accept while ringing, Hang up otherwise), and tapping the card jumps back into the call via `CallHub.RequestCallScreen`; the Message app consumes the request with `ConsumeCallScreenRequest`.
- Call audio controls live in one component, `CallAudioPanel` (src/Aetherphone/Windows/Components/Chat/CallAudioPanel.cs): call volume, a volume slider and Mute for me per person, microphone volume with a live level meter, and the speaker and microphone lists. The in-call Audio button and a tap on an avatar open it in a sheet; Settings shows the same panel.
- Settings and Control Center: `CallsPage` (src/Aetherphone/Apps/Settings/Pages/CallsPage.cs) holds the enable toggle above `CallAudioPanel`, and `ControlRegistry` (src/Aetherphone/Core/ControlCenter/ControlRegistry.cs) exposes the same enable toggle as a Control Center tile.

Every surface reads call state the same way: `CallHub.Snapshot()` returns an immutable `CallView` struct each frame, and nothing subscribes to per-field change events. The hub raises exactly one event, `IncomingCallPresented`, and it exists to open the phone window, not to push state.

## Rate limiting on the client

The server answers abusive traffic with HTTP 429. `HttpService` reacts by pausing polling toward the host, and only polling: a 429 on a GET calls `PauseHost`, which records a deadline from the `Retry-After` header (capped at 30 seconds, 10 seconds when the header is zero, negative, or a past date, plus 0.25 to 1.5 seconds of jitter), and until it passes every GET to that host short-circuits without touching the network, reporting status 429 to `onStatus` and `RateLimitPaused` to `onFailure` (`IsPollingPaused` checks the request method). Pauses are per host, so a media or third-party host can be paused while the API host is not. Writes stay outside the scheme: a 429 on a POST, PATCH, or DELETE fails only that call and pauses nothing (`PauseIfPolling` just logs a warning for non-GETs), and `PutBytesAsync` uploads neither honor nor set pauses.

A 429 with no `Retry-After` at all is an edge shed: the proxy in front of the API dropped one request, while Aethernet's own rate limiter always sets the header. `PauseHost` ignores it (`IsEdgeShed`), so nothing is paused. A JSON GET is retried once after 0.75 to 2 seconds, and `GetBytesAsync` retries it within its three attempts.

The user sees this as the `RateLimitPill` (src/Aetherphone/Core/Shell/RateLimitPill.cs), a small pill under the status bar that reads "Too many requests. Retrying in {n}s" (`L.Common.RateLimited`). It polls `HttpService.PauseRemaining` for the API host every frame and animates in only while a pause is active. `PhoneShell` wires it into the shell overlay stack.

Separately, `RequestThrottle` enforces polite pacing toward third parties, with a concurrency limit and minimum interval chosen by each service. `LodestoneService` runs at most one Lodestone request at a time with a 1200 ms minimum interval; the Universalis, radio-browser, housing, collections, and song search services set their own.

## End-to-end encryption

Direct messages are encrypted client-side whenever every member of the conversation has a published key, and then the server stores only ciphertext. A conversation where a member has no key yet stays plaintext (EncVersion 0) and is labeled Not encrypted. Once a conversation has a key generation and every member holds a key, the server refuses plaintext sends and edits with 409 `thread_encrypted` (Message chats, Velvet DMs, and Aethergram DMs; Yellow Pages inquiries rely on the client policy alone), and the client holds a send until it knows the conversation's key status (`EncryptedSendPolicy`), so a conversation never silently downgrades. The contract has three wire prefixes, all defined in Core/Crypto:

- `EC1.` marks a wrapped conversation key. `CryptoBox.WrapCek` generates an ephemeral P-256 ECDH key pair, derives a wrap key with HKDF-SHA256, and seals the 32-byte conversation encryption key (CEK) with AES-GCM for one recipient's public key. Each conversation member gets their own `EC1.` wrap.
- `AE1.` marks an encrypted message body. `EnvelopeCodec.Encode` produces `AE1.<generation>.<base64>`, where generation is the key version for that conversation. The AES-GCM additional authenticated data binds scope id, generation, and sender id, so a ciphertext cannot be replayed into another conversation or attributed to another sender. The envelope also carries a random franking key whose HMAC commitment tag lets a report prove what a message said without giving the server decryption ability.
- `EL1.` marks an identity private key wrapped for device linking. `CryptoBox.WrapSecret` and `UnwrapSecret` use the same ephemeral ECDH, HKDF, and AES-GCM construction with the HKDF info `aethernet-device-link-v1` (see Linking a new device below).

Scopes name a conversation across apps: `ConversationKeyStore.ChatScope`, `VelvetScope`, `GramScope`, and `AdScope` (src/Aetherphone/Core/Crypto/ConversationKeyStore.cs) cover Message-app chats, Velvet DMs, Aethergram DMs, and Yellow Pages inquiries. Attachments and voice notes are sealed too, via `MediaEnvelope` with its own AAD domain string `aep-media-v1`. Public content (posts, comments, profiles) is not end-to-end encrypted.

Two paths send readable text from an encrypted conversation on purpose. Translating a message decrypts it on the PC and sends the text to Aethernet `/translate/batch`; the one-time translation disclosure (`TranslateLink.WithDisclosure`) says so. A report reveals the reported message and the few before it (`ReportReveals.Limit`), decrypted, with their franking keys, after the report sheet's disclosure.

### Key storage and recovery

`KeyVault` manages the device identity key:

- The private key is exported as PKCS8 and stored per account in `Configuration.EncryptionKeysByUserId` (mirrored into `EncryptionKeyCache`), protected by Windows DPAPI (`LocalKeyProtector` uses `ProtectedData` with the account id as entropy). If DPAPI throws, the key is still stored, as plain base64 behind a `raw.` prefix with no at-rest protection, and `LocalCacheUnavailable` is set so the Settings encryption page shows a warning.
- The public key is published to the server via `KeysClient.PutMyKeysAsync` (`PUT /keys/me`), and peers fetch it through `PeerKeyDirectory`.
- Recovery codes: `KeyVault.CreateRecoveryCodeAsync` wraps the private key with a key derived from a 20-character code (PBKDF2-SHA256, 600,000 iterations, in src/Aetherphone/Core/Crypto/RecoveryKey.cs) and escrows the result server-side; `RecoverWithCodeAsync` reverses it on a new device. Escrows of keys from before a rotation stay archived (`GET /keys/me/escrows`), so the same code can also restore older keys (`RestorePreviousKeysAsync`).
- Linking a new device: a new PC calls `KeyVault.StartDeviceLinkAsync`, which posts an ephemeral public key to `POST /keys/link/requests`. The server pushes `keys.linkPending` to the account's other sessions, and `DeviceLinkWatcher` (src/Aetherphone/Core/Crypto/DeviceLinkWatcher.cs) on an unlocked PC lists `GET /keys/link/requests` and asks the user to approve, showing the request's verification code (without the socket it checks every 5 minutes). `KeyVault.ApproveDeviceLinkAsync` wraps the PKCS8 identity key as `EL1.` to the ephemeral key and posts it to `/keys/link/requests/{id}/approve`. The new PC polls `GET /keys/link/requests/{id}`, unwraps, and keeps the key only if its public half matches the account's published key. The server lets a request live 5 minutes and allows 3 pending per account.
- `KeyVault.State` drives the UI: `Unlocked`, `Provisioning`, `Locked` (an account key exists but this device cannot load it), `Unsupported`, or `Unavailable`.

### Getting a lost key's history back

Losing the device key does not lose the messages. Every conversation key is wrapped once per member, so anyone else in the chat still holds a copy that can be re-sealed to a replacement key. Three things make that happen without anyone opening a chat by hand:

- The server names who can be helped. The bulk key endpoints (`GET /keys/conversations` and the velvet, gram, and ads twins) return `HealTargets` per conversation: the members whose wrap is older than their published key version, listed with their current public key and the exact generations the caller can still read. `WrapHealing` (Aethernet.Api/Common/WrapHealing.cs, in the backend repo) computes it, and never names a member who has left.
- The client acts on it. `ConversationKeyStore.ScheduleHeal` runs after each hydrate: for every generation it holds a CEK for, it re-wraps to the target's new public key and posts `AddWraps`. A target is remembered as healed per key version, so the sweep does not repost on the next hydrate, and a partial failure retries.
- The server nudges peers. A key rotation through `PUT /keys/me` pushes `keys.stale` to everyone sharing a conversation with the rotating user, and `ConversationKeyStore` answers it by re-hydrating all four surfaces. Peers who are online repair within seconds; the rest do it at their next launch.

`POST /chats/{id}/keys/wraps` (and `POST /velvet`, `/gram`, or `/ads` `/threads/{otherId}/keys/wraps`) accepts wraps for any generation up to the current one and re-validates membership and key versions, so the heal-target list is only a hint and never a grant. Recovery codes still matter for the case this cannot fix: a conversation where every other member also lost their key, or where nobody comes back online.

### Platform support

The EC key math runs entirely in managed code through the BouncyCastle.Cryptography package (a `PackageReference` in src/Aetherphone/Aetherphone.csproj): `CryptoBox.TryGenerateIdentity` generates the P-256 pair with `ECKeyPairGenerator`, keys are exported and imported through `SubjectPublicKeyInfoFactory` and `PublicKeyFactory`, and the ECDH agreement uses `ECDHBasicAgreement`. That keeps the curve work off the OS bcrypt provider; the earlier `ECDiffieHellman.Create`-based implementation failed on some Wine setups whose bcrypt lacks P-256 support, and the managed library replaced it. The symmetric layer (AES-GCM seal and open, HKDF, random bytes) still uses `System.Security.Cryptography`. If key generation fails anyway, `TryGenerateIdentity` returns `null`, the vault ends in `KeyVaultState.Unsupported`, and encrypted conversations show placeholder text instead of bodies.

## Media upload and download

Uploads are a three-step dance, visible end to end in `AvatarUpload.RunAsync` (src/Aetherphone/Core/Aethernet/AvatarUpload.cs):

1. `MediaClient.UploadUrlAsync` posts content type and a scope string to `/media/upload-url` and receives an `UploadUrlResponse` with `Key`, `UploadUrl`, and `PublicUrl`.
2. `MediaClient.UploadImageAsync` PUTs the raw bytes to `UploadUrl`. `AethernetTransport.UploadBearerFor` attaches the session token only when the upload URL's host and port match the API base URL; a presigned URL on another host is sent without credentials by design. A server running on local storage instead of a bucket hands out `/media/{key}` on the API host itself, and that same-host PUT is the one that needs the token.
3. The caller references the media in a follow-up API call, for example `AccountClient.UpdateProfileAsync` with the returned `PublicUrl`, or a chat send with the media key.

Server limits (backend `MediaContentTypes` and `MediaEndpoints`): PNG, JPEG, WebP, and GIF images up to 8 MB, with an unencrypted GIF capped at 4 MB, and WAV audio up to 6 MB. An unknown upload scope fails with 400. The client uploads GIFs as-is and voice notes as `audio/wav`.

For end-to-end encrypted attachments, `MessageCipher.PrepareOutboundMedia` seals the bytes before step 2, so the storage layer only ever holds ciphertext.

Every JPEG the client uploads comes out of `ScalarJpegEncoder` (src/Aetherphone/Core/Media/ScalarJpegEncoder.cs), a baseline encoder written as plain loops; ImageSharp only decodes, crops, and resizes. It exists because ImageSharp's SIMD encoder produced red, blue, and white dashes (a skipped -128 level shift) on machines where an injected overlay drops the upper AVX lanes across a thread suspension, and the server stores whatever bytes it receives. Keep the encoder free of `Vector<T>` and intrinsics, or that guarantee goes away.

Downloads go through `HttpService.GetBytesAsync` (up to 3 attempts with backoff). Display-side caching is layered: `DiskCache` persists bytes under `cache/` in the plugin config directory with a size budget set in `PhoneServices.Build`, and the media and image caches there are DPAPI-sealed at rest (`protect: true`). `MediaCache` turns bytes into GPU textures with a 96 MB texture budget, 30-day disk age, and a 2-minute cooldown after a failed fetch. Encrypted attachments are fetched sealed and decrypted with `MessageCipher.TryDecryptMedia` before display.

## Dev vs prod endpoints

The base URL lives in `Configuration.AethernetBaseUrl`, and its default is decided at compile time: `Configuration.DefaultAethernetBaseUrl` is `TestAethernetBaseUrl` (the development Railway deployment) under `#if DEBUG || BETA` and `LiveAethernetBaseUrl` (`https://api.aetherphone.net`) otherwise (src/Aetherphone/Configuration.cs). A Debug or Beta build therefore already talks to the dev backend with no config editing, the workflow [Getting started](getting-started.md) describes.

Debug and Beta builds (`AepConstants.IsPrerelease`) also have a Test server toggle in Settings > About that switches between the test and live URLs, saves, and signs you out. The identity headers follow the new host at once, with no reload. Pointing anywhere else (a local backend, say) still means editing the saved plugin configuration JSON. That JSON is the file Dalamud hands the plugin as `PluginInterface.ConfigFile`, named after the plugin's internal name, in the `pluginConfigs` folder of the XIVLauncher data directory under `%AppData%`: `Aetherphone.json` for a stable Release build, `AetherphoneDev.json` for the Debug plugin, and `AetherphoneBeta.json` for a Beta build. src/Aetherphone/Aetherphone.csproj renames the target to `AetherphoneDev` in the Debug configuration and to `AetherphoneBeta` when built with `-p:AetherphoneBeta=true`, and Dalamud treats each as a separate plugin with its own config. Edit it only while the plugin is unloaded, or set the value from code instead: the running plugin rewrites the entire file on every `Configuration.Save()`, so a hand edit made while the plugin is loaded is clobbered by the next save.

Guard rails in `Configuration.NormalizeAethernetBaseUrl` (called at boot from src/Aetherphone/Plugin.cs):

- Invalid or empty URLs reset to `DefaultAethernetBaseUrl`.
- A known legacy production host is force-migrated to the current default.
- In stable Release builds, loopback URLs (localhost) also reset to the default. Only Debug and Beta builds may target a local backend, which keeps a stray dev config from shipping to users.

Because the backend is a separate repository, a local backend means running that service yourself; the shared dev deployment is what Debug and Beta builds target out of the box. Never test unreleased client changes against the production API; use a Debug build against a non-production base URL, and keep any tokens for it out of commits and screenshots.

## API client map

The main networking folders under Core/, Aethernet-backed first, third-party last. Other app-specific clients live beside their domain; grep for `AethernetApi` or `HttpService` to find the rest.

| Folder | One line |
| --- | --- |
| src/Aetherphone/Core/Net/ | Transport primitives shared by all remote features |
| src/Aetherphone/Core/Aethernet/ | Session, transport, sign-in, and the typed clients plus DTO (data transfer object, the wire-format record) contracts |
| src/Aetherphone/Core/Runtime/ | `PollCadence`, the polling backstop the stores share |
| src/Aetherphone/Core/Crypto/ | E2EE building blocks described above, plus device linking |
| src/Aetherphone/Core/Telephony/ | Calls: the websocket, signal routing, call state, and Opus audio |
| src/Aetherphone/Core/Message/ | `ChatThreadStoreBase`, the shared thread store behind every DM surface |
| src/Aetherphone/Core/Notifications/ | `SocialNotificationService`: social notification polling driven by `social.ping` |
| src/Aetherphone/Core/Social/ | Shared social domain types and stores (feeds, stories, identities) used by the social apps |
| src/Aetherphone/Core/Moderation/ | Moderation notice polling, presentation, and the suspension gate |
| src/Aetherphone/Core/Report/ | The central report popup; submissions travel through `SafetyClient` to `/reports` |
| src/Aetherphone/Core/Translation/ | `TranslationService` over `/translate` |
| src/Aetherphone/Core/Muster/ | Muster events store |
| src/Aetherphone/Core/YellowPages/ | Yellow Pages ads and their encrypted inquiry threads |
| src/Aetherphone/Core/Coins/ | Coin balance, quests, and shop over `CoinsClient` |
| src/Aetherphone/Core/Casino/ | Casino state store, the money endpoints, and the seat machine behind the Gamba app |
| src/Aetherphone/Core/Games/ | Online game rooms over `GamesClient` and `game.` signals; the global leaderboard over `ScoresClient` |
| src/Aetherphone/Core/Jam/ | Music listening parties over `jam.` signals |
| src/Aetherphone/Core/Video/ | AetherStream watch-along over `stream.` signals |
| src/Aetherphone/Core/Songs/ | Song search (YouTube, third-party) and listening presence over `MusicListeningClient` |
| src/Aetherphone/Core/Radio/ | radio-browser search and station streams (third-party) plus community radio and its `radio.` rooms on Aethernet |
| src/Aetherphone/Core/Lodestone/ | NetStone-based Lodestone lookups for avatars and portraits, throttled and disk-cached |
| src/Aetherphone/Core/Market/ | Universalis market client, a third-party API outside Aethernet |
| src/Aetherphone/Core/Hunts/, Lyrics/, News/, Venues/, Housing/, Collections/, Rolladeck/ | Third-party clients: Faloop, LRCLIB, lodestonenews.com, FFXIV Venues and Partake, the housing APIs, FFXIV Collect, Rolladeck |

The chat stores that consume these clients are covered in [Messaging and chat](messaging-and-chat.md), and how pings become banners and badges is covered in [Notifications](notifications.md).

### Game score routes

`ScoresClient` (src/Aetherphone/Core/Aethernet/Clients/ScoresClient.cs) covers the leaderboard behind the Games app; `LeaderboardStore` (src/Aetherphone/Core/Games/LeaderboardStore.cs) is its only caller, described in [Mini-games framework](games-framework.md#global-leaderboard). All four need a signed-in session, and the player routes answer domain refusals with HTTP 200 and a `reason` rather than a 4xx, so a `null` from the client is always a transport problem:

| Route | Client method | Shape |
| --- | --- | --- |
| `POST /games/scores` | `SubmitAsync(gameId, value, sessionId)` | `GameScoreSubmitRequest` to `GameScoreSubmitDto { accepted, reason, best, rank, total, friendsRank, weekRank }`; reasons are `""`, `unknown_game`, `implausible`, `too_soon`, `not_better`, `hidden` (mirrored in `ScoreReasons`) |
| `GET /games/scores/{gameId}?scope=global\|friends&span=all\|week&limit=50` | `BoardAsync(gameId, scope, span, limit)` | `GameLeaderboardDto { gameId, scope, span, entries[], me }`; `me` is null when the caller is not on the board; 404 for an unknown game id |
| `GET /games/scores/me` | `MyRanksAsync()` | `GameScoreRanksDto { ranks[] }`, one row per game with an all-time entry |
| `POST /me/games-privacy` | `SetShowOnLeaderboardsAsync(show)` | `UpdateGamesPrivacyRequest { showOnLeaderboards }` to the full `UserDto`, which carries `showOnLeaderboards` |

The `gameId` on the wire is the stat id (`tetris.modern`, `sudoku.easy`), and the plugin only ever sends the ids in `ScoreStatIds.All`. Nothing uploads until the account has joined the boards through `/me/games-privacy`: `showOnLeaderboards` is false by default on the server and on the wire.

### Game room routes

`GamesClient` (src/Aetherphone/Core/Aethernet/Clients/GamesClient.cs) carries the online rooms on the Games app's Together tab; `GameRoomsStore` (src/Aetherphone/Core/Games/GameRoomsStore.cs) is its only caller, described in [Mini-games framework](games-framework.md#play-with-friends-online-rooms). Like the score routes, every room route needs a signed-in session and answers a refused intent or action with HTTP 200, `granted: false` and a `reason`:

| Route | Client method | Shape |
| --- | --- | --- |
| `GET /games/rooms` | `RoomsAsync()` | `GameRoomListDto { rooms[], serverNowUnixMs }`, the rooms this account is in |
| `POST /games/rooms` | `CreateRoomAsync(clientRoomId, gameKind)` | `GameRoomCreateRequest` to `GameRoomResultDto { granted, reason, room }` |
| `POST /games/rooms/join` | `JoinByCodeAsync(code)` | `GameRoomJoinRequest` to `GameRoomResultDto` |
| `GET /games/rooms/{roomId}` | `RoomCardAsync(roomId)` | `GameRoomCardDto`, one room's card |
| `GET /games/rooms/{roomId}/state` | `RoomStateAsync(roomId)` | `GameRoomSnapshotDto`, the HTTP fallback while the socket is down; a 404 closes the room locally |
| `GET /games/rooms/{roomId}/you` | `YouAsync(roomId)` | `GameRoomYouDto`, the caller's private lane (an Uno hand, a Broadside fleet) |
| `POST /games/rooms/{roomId}/act` | `ActAsync(roomId, request)` | `GameRoomActionRequest { action, actionCount, card, color, clientActionId, ... }` to `GameRoomActionResultDto { granted, reason, actionCount }` |
| `POST /games/rooms/{roomId}/leave`, `/kick`, `/close` | `LeaveAsync`, `KickAsync(roomId, userId)`, `CloseAsync` | `GameRoomActionDto { granted, reason }`; kick posts a `GameRoomMemberRequest` |

The room kinds live in `GameRoomWire` (src/Aetherphone/Core/Games/GameRoomWire.cs). Every action names the roster's `actionCount` it was decided against, and the server refuses a mismatch as `stale_action` instead of applying it twice:

| Kind | Actions the client sends |
| --- | --- |
| `games.uno` | `start` (the card slot carries the rule set), `play`, `draw`, `pass` |
| `games.chess` | `start`, `move`, `resign` |
| `games.pool` | `start`, `shoot`, `place` (ball in hand), `resign` |
| `games.connectfour` | `start`, `drop`, `resign` |
| `games.broadside` | `start`, `place` (the fleet), `fire`, `resign` |
| `games.luckydraw` | `start`, `hit`, `stay`, `target` |
| `games.crater` | `start`, `shoot` (weapon, facing, elevation, power, fuse and the stepped walk), `pass` (a turn spent walking), `resign` |
| `games.minigolf` | `start` (the card slot carries 9 or 18 holes), `shoot` |

The live room rides the realtime socket under the `game.` prefix (`game.attach`, `game.snapshot`, `game.event`, `game.private`, `game.ended` and the rest in src/Aetherphone/Core/Telephony/Contracts/Signals.cs); see [The realtime layer](#the-realtime-layer) above.

`game.motion` is the one unsequenced room signal. During a Crater turn the walker sends its whole walk so far (tag, turn count, moogle, facing, aim in milliradians, then the signed tick runs) at most every 66 ms; opponents replay the runs through the same `CraterMotion` code the server uses, a few ticks behind to absorb jitter, so the walk they watch is the walk the server will play. The server relays each packet to the room's other sockets and keeps the newest one per member, so a turn that times out mid-walk keeps the walk instead of snapping the moogle back.

## Gotchas

- Typed clients never throw on HTTP failure. A `null` return can mean network error, non-2xx, an active rate-limit pause, or a signed-out session (`AethernetTransport` short-circuits to `default` when `Session.IsSignedIn` is false). Pass an `onFailure` callback and switch on `AepFailure.Kind` when the difference matters.
- One 401 from any endpoint marks the whole session `TokenRejected` (`AethernetSession.ReportAuthStatus`) and every subsequent request no-ops until the user signs in again. An `X-Aep-Source-Status: blocked` reply does the same (`AethernetSession.ReportSourceStatus`). "The API stopped responding" is often just this.
- A 429 on a GET pauses every GET to that host process-wide via `HttpService.PauseHost`, not only the endpoint that tripped it, so unrelated polling features sharing the host go quiet until the pause expires. The exception is a 429 with no `Retry-After` (an edge shed), which is retried and pauses nothing. Writes are outside the scheme in both directions: a 429 on a POST, PATCH, DELETE, or upload PUT fails just that call and sets no pause, and writes keep flowing while GETs are paused.
- `X-Aep-App`, `X-Aep-Request-Id`, `X-Aep-Source`, and `X-Aep-Build` go only to the Aethernet host (`AethernetClientIdentity.Matches`). A third-party call that passes `appScope` (HuntsClient passes `"hunts"` to Faloop) still sends no `X-Aep-App`; there the scope only feeds the ETag key. Keep new identifying headers behind the same host check.
- The realtime socket is not gated on `Configuration.CallsEnabled`. Do not "optimize" `CallHub.Reconcile` into skipping `router.Start()` when calls are off; notification pings ride the same socket.
- Stable Release builds reset loopback base URLs to production at boot (`Configuration.ShouldResetBaseUrl` compiles the loopback check only outside `#if DEBUG || BETA`). If your local backend config keeps disappearing, you are running a stable Release build.
- Upload PUTs only carry the bearer token when the upload URL's host and port match the API base URL (`AethernetTransport.UploadBearerFor`). Expecting `Authorization` on an external storage host will fail silently.
- `EtagCache` keys include the bearer token and the app scope, so two app-scoped `AethernetApi` instances requesting the same URL maintain separate cache entries. That is intentional; do not dedupe them.
- `HttpService` caps response bodies at 32 MB (`MaxResponseBytes`). Anything larger fails the request rather than streaming.
- Casino money endpoints never answer a denial with a non-2xx status. A refused buy-in, bet or top-up is HTTP 200 with `Granted: false` and a `Reason` string, because a typed client reads non-2xx as `null`, which would make a rule denial indistinguishable from a transport failure. Treat `Granted` as the verdict and `Reason` as the message key; a `null` return is a transport problem, never a rule.
- Loss limits are opt-in. Since 2026-08-14 the backend ships with no house loss limit: a `LossLimit` of `0` on `/casino/` means **no limit is set**, and the server sends zero unless the player set their own or an operator set one. Every limit value on the wire is in chips: a self limit must sit between `CasinoEconomy.MinSelfLossLimit` (5,000 chips) and `MaxDailyLossLimit` (the daily buy-in cap, 2,500,000 chips), mirrored client-side by `CasinoLimits` and `CasinoLimitPicker.CeilingFor(state.DailyBuyInCap)`. The client reads 0 as "no limit" everywhere (`CasinoTonight.HasLimit`), so a zero headroom from `CasinoStore.MergeLimits` never renders as "0 room left". Lowering a self limit applies at once; raising or removing it (`SelfLossLimit: null`) waits for the next coin day and comes back as `PendingRaiseAtUnix`.

## Related docs

- [Getting started](getting-started.md): build the plugin and load a Debug dev build
- [Architecture](architecture.md): where `PhoneServices.Build` wires all of these services together
- [Messaging and chat](messaging-and-chat.md): the chat stores built on `ChatClient`, `MessageCipher`, and the inbox cadence
- [Notifications](notifications.md): how realtime pings and polls become banners, sounds, and badges
- [State and persistence](state-and-persistence.md): `Configuration`, per-character data, and media storage on disk
- [App framework](app-framework.md): how apps get their app-scoped `AethernetApi` instances
