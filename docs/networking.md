# Networking and the Aethernet backend

This doc covers the client side of everything online in Aetherphone: the HTTP layer, sessions and sign-in, the realtime websocket, voice calls, rate limiting, end-to-end encryption, media transfer, and how to point a dev build at a non-production backend. Read it before touching any feature that talks to a server. The backend itself ("Aethernet") is a separate ASP.NET service in its own repository; only its client-visible contract is described here.

## Key files

| Path | Role |
| --- | --- |
| src/Aetherphone/Core/Net/HttpService.cs | The one `HttpClient` wrapper: JSON calls, retries, 429 pauses, ETag cache |
| src/Aetherphone/Core/Net/EtagCache.cs | In-memory ETag store for conditional GETs |
| src/Aetherphone/Core/Net/DiskCache.cs | Size-capped on-disk byte cache used by media caches |
| src/Aetherphone/Core/Net/MediaCache.cs | Bytes to GPU texture cache with failure cooldowns |
| src/Aetherphone/Core/Net/RequestThrottle.cs | Concurrency plus minimum-interval gate (used for Lodestone) |
| src/Aetherphone/Core/Aethernet/AethernetSession.cs | Token, base URL, sign-in state, per-character session slots |
| src/Aetherphone/Core/Aethernet/AethernetTransport.cs | Session-aware request builder over `HttpService` |
| src/Aetherphone/Core/Aethernet/AethernetApi.cs | Facade that constructs the 16 typed domain clients |
| src/Aetherphone/Core/Aethernet/Clients/ | One typed client per API domain (auth, chats, media, keys, ...) |
| src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs | Request and response records for the wire contract |
| src/Aetherphone/Core/Aethernet/SignInFlow.cs | Lodestone challenge and XIVAuth device-flow state machines |
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

- HTTPS requests against a base URL, `https://api.aetherphone.net` by default (`Configuration.DefaultAethernetBaseUrl` in src/Aetherphone/Configuration.cs).
- One websocket per signed-in session at the same host, path `/rt` (built in `RealtimeConnection.BuildUri`).

Features that are purely local (notes, calculator, most mini-games) never touch the network. A few apps call third-party services directly (Universalis for market data, YouTube for songs, NetStone for Lodestone lookups); none of them attach the Aethernet session, and only some ride `HttpService` (Universalis and Lodestone image downloads do; NetStone page fetches and YoutubeExplode bring their own HTTP clients).

## The HTTP layer

`HttpService` (src/Aetherphone/Core/Net/HttpService.cs) owns a single pooled `HttpClient` for the whole plugin. It sets a `User-Agent` of `Aetherphone/{version} (+https://github.com/XeldarAlz/FFXIV-Aetherphone)`, uses a 20 second timeout per request (60 seconds for uploads), and caps response bodies at 32 MB. All JSON goes through source-generated `System.Text.Json` type infos (`AethernetJsonContext` in src/Aetherphone/Core/Aethernet/AethernetJsonContext.cs), so serialization never uses runtime reflection.

Two headers matter:

- `Authorization: Bearer <token>` when a session token is available.
- `X-Aep-App: <scope>` when the request comes from an app-scoped `AethernetApi` instance. `AppRegistry` (src/Aetherphone/Core/Apps/AppRegistry.cs) builds separate instances with scopes like `"chirper"`, `"dm"`, or `"yellowpages"` so the server can attribute traffic per app.

`AethernetTransport` sits between typed clients and `HttpService`. It prefixes `AethernetSession.BaseUrl` onto relative paths, attaches the session token, and short-circuits to `default` when the user is not signed in. Every session request's status code is funneled into `AethernetSession.ReportAuthStatus` (anonymous calls skip that sink), which is how a 401 anywhere flips the session into the `TokenRejected` state.

A typed client method looks like this (real code from src/Aetherphone/Core/Aethernet/Clients/KeysClient.cs):

```csharp
public async Task<(MyKeysDto? Keys, int Status)> MyKeysAsync(CancellationToken token)
{
    var status = 0;
    var keys = await net.GetAsync("/keys/me", AethernetJsonContext.Default.MyKeysDto, token,
        statusCode => status = statusCode).ConfigureAwait(false);
    return (keys, status);
}
```

Note the error model: typed clients do not throw on HTTP failures. A `null` result can mean a network error, a non-2xx status, a rate-limit pause, or simply "not signed in". When a caller needs to distinguish, it passes an `onStatus` callback as above.

GET responses that carry an `ETag` header (a server-provided version stamp) are cached in `EtagCache`. The next identical GET sends `If-None-Match`, and a `304 Not Modified` answer is served from the cached body. Cache keys include the bearer token and the app scope, so different accounts and app scopes never share entries.

## Sessions, tokens, and sign-in

### The session

`AethernetSession` holds the signed-in state. The token itself lives in `Configuration.AethernetToken` and is persisted through Dalamud's plugin configuration. Because one player can own several characters, the session also keeps a `Configuration.CharacterSessions` dictionary keyed by the character's ContentId (the game's stable character identifier); switching characters stashes the active token and encryption key cache into a `CharacterSession` slot and loads the new character's slot. `SignOut` calls exist, and `AuthClient.RevokeTokenAsync` deletes the token server-side via `DELETE /auth/token`.

### Lodestone challenge flow

Sign-in proves character ownership through the Lodestone, Square Enix's public character site. `SignInFlow.StartLodestone` posts the character name and world to `/auth/challenge` (anonymous, no token) and receives a `ChallengeResponse` with a short code. The Settings Account page then walks the user through the steps you can read in src/Aetherphone/Core/Localization/L.cs: copy the code, paste it into your Lodestone profile, then verify. `SignInFlow.VerifyLodestone` posts the challenge id to `/auth/verify`; on success the server returns a token plus a `UserDto`, and `AethernetSession.SignIn` persists both.

### XIVAuth device flow

`SignInFlow.StartXivAuth` offers an alternative: `/auth/xivauth/start` returns a verification URL and user code, the client opens the browser, and `PollXivLoopAsync` polls `/auth/xivauth/poll` until the flow completes, times out, or fails.

### Typed verify reasons

The verify endpoints answer expected failures inside the body rather than with HTTP error codes. `VerifyResponse` (src/Aetherphone/Core/Aethernet/Contracts/Dtos.cs) carries `Ok`, an optional `Reason` string, and on success the token and user. The reason strings are mirrored client-side as constants in `VerifyFailure` (src/Aetherphone/Core/Aethernet/VerifyResult.cs): `character_not_found`, `code_not_found`, `banned`, `rate_limited`, and so on. `AuthClient.VerifyAsync` wraps everything in a `VerifyResult`; when the transport itself returned nothing, it synthesizes `VerifyFailure.Network` (or `RateLimited` on a 429). `SignInFailureText.Resolve` maps each reason to localized title and body copy, and a `banned` reason routes through `AethernetSession.ReportBanned` with the optional `SuspensionDto` details.

## The realtime layer

### The websocket

`RealtimeConnection` (src/Aetherphone/Core/Telephony/RealtimeConnection.cs) opens a `ClientWebSocket` to `<base url as wss>/rt` with the bearer token in an `Authorization` header. Keepalive pings go every 15 seconds. Text frames are `CallControl` JSON messages; binary frames are call audio. The class lives under Core/Telephony for historical reasons, but it carries all realtime traffic, not only calls.

### What flows over it

`CallSignalRouter` dispatches each `CallControl.Type` (constants in src/Aetherphone/Core/Telephony/Contracts/Signals.cs). Two families exist:

- Call signaling: `call.incoming`, `call.roster`, `call.declined`, `call.unavailable`, `call.ended`, `call.handled`, forwarded to `CallHub` (see [Calls](#calls) below).
- Notification pings: `chat.ping`, `velvet.ping`, `gram.ping`, `social.ping`, `muster.ping`, `announce.ping`, and `content.removed`, published onto `RealtimeSignalBus`.

`RealtimeSignalBus` is a plain in-process event hub. Stores subscribe to the ping that concerns them (`ChatPinged`, `SocialPinged`, ...) and react by refreshing immediately. `ContentRemoved` carries a `ContentRemovalSignal` so every phone purges moderated content without waiting for a poll.

The socket runs whenever a session is signed in. `CallHub.Reconcile` (src/Aetherphone/Core/Telephony/CallHub.cs) re-evaluates that on every session change: it calls `router.Start()` while signed in, and `router.Stop()` when the session signs out or the account id changes (ending any active call first). Turning calls off is different: when `Configuration.CallsEnabled` is false `Reconcile` ends the active call but leaves the socket running, so notification pings keep flowing.

### Reconnect behavior

`RealtimeConnection.RunAsync` reconnects forever until stopped. If a connection survived at least 60 seconds it counts as healthy and the next retry happens after 0.5 to 5 seconds; otherwise the base delay doubles per attempt toward a 15 second ceiling and is then multiplied by 0.5 to 1.5 of jitter. `RealtimeSignalBus.SetActive` broadcasts `ConnectedChanged`, and several stores request an immediate poll when the socket comes back so nothing missed during the outage stays missed.

### The polling backstop

Realtime is an optimization, never the only path. Stores poll on a `PollCadence` (src/Aetherphone/Core/PollCadence.cs), which picks a foreground or background interval based on `PhoneVisibility` and supports `RequestImmediate` for realtime pings. Verified examples:

| Service | Foreground | Background |
| --- | --- | --- |
| `SocialNotificationService` (src/Aetherphone/Core/Notifications/SocialNotificationService.cs) | 60 s | 120 s |
| `ChatThreadStoreBase` inbox (src/Aetherphone/Core/Message/ChatThreadStoreBase.cs) | 60 s | 120 s |
| `AccountStateService` (src/Aetherphone/Core/Aethernet/AccountStateService.cs) | 120 s | 300 s |

So even with a dead websocket, notifications arrive within roughly two minutes.

## Calls

Voice calls are the reason Core/Telephony exists. They ride the same websocket as everything else: signaling travels as `CallControl` text frames, audio as binary frames, and the server forwards each participant's audio frames to everyone else in the call. The client never opens a peer-to-peer connection; the websocket is the whole transport. Everything below is client behavior, and the server appears only as the messages it sends.

### Call lifecycle

`CallHub` (src/Aetherphone/Core/Telephony/CallHub.cs) is the state machine. `CallState` (src/Aetherphone/Core/Telephony/CallState.cs) is `Idle`, `Dialing`, `Ringing`, `Connecting`, `Active`, or `Ended`, though the hub never rests in `Ended`: every teardown path resets straight to `Idle`.

**Outgoing.** `StartCall` generates the call id client-side (`Guid.NewGuid()`), moves to `Dialing`, writes an outgoing entry to the call log, and sends `call.start` with the invitee's user id. The server answers with `call.roster` updates listing every participant with a `Slot`, a `State` (`ringing`, `active`, `left`), and identity fields. The moment a roster shows at least one other active participant, the hub starts audio and flips to `Active`. If the last pending invitee declines (`call.declined`), the server reports the callee unavailable (`call.unavailable`), or 60 seconds pass with no answer (`DialingTimeoutSeconds`), the call ends and `ConfirmService` shows a localized outcome alert.

**Incoming.** `call.incoming` carries the caller as a `ParticipantInfo`. The hub declines automatically, with reason `unavailable` and a missed-call log entry, when calls are disabled or the Message app is uninstalled (the `AppGate` built as `installer.Gate("message")` in src/Aetherphone/Core/PhoneServices.cs), and with reason `busy` when another call is in progress. Otherwise it enters `Ringing`, starts the ringtone loop, posts the incoming-call notification, and raises `IncomingCallPresented`, which `Plugin.OnIncomingCall` uses to maximize and open the phone window. `Accept` sends `call.accept` and moves to `Connecting`; the next roster drives it to `Active`. Ringing that nobody touches for 60 seconds auto-declines and logs a missed call, and `call.handled` (another of your sessions answered) silently stops the local ring.

**Group calls.** `AddParticipant` sends `call.invite` with the same call id and writes another outgoing log entry. The roster is the only truth about who is in the call; the UI renders whatever it says. Once audio is running, a roster with no other active participants left tears the call down locally.

**Ending.** `Hangup` sends `call.leave`; `call.ended` ends it from the server side. Every path funnels through one private teardown that resets to `Idle`, stops the ringtone, and disposes the audio session.

**The call log.** `CallLogStore` (src/Aetherphone/Core/Telephony/CallLogStore.cs) persists the last 50 entries in `Configuration.CallLog`, merging consecutive same-direction calls with the same peer into a single row with a count. Calls missed while the plugin was closed still appear: the server also delivers missed calls as social notification type 20, and `CallHub` folds those into the log with a 3-minute dedup window so a live miss and its server echo do not double up.

Ringing loops the ringtone chosen in `Configuration.RingtoneSound` (`SoundService.StartCallRing`) and posts a notification on the `phone` channel; full story: [Notifications](notifications.md).

### The audio pipeline

When a roster first shows another active participant, `CallAudioController` (src/Aetherphone/Core/Telephony/CallAudioController.cs) builds one `CallSession` (src/Aetherphone/Core/Telephony/CallSession.cs) and stops any music playing through `PlaybackHub`, so the call has the speakers to itself.

Capture: `AudioCapture` (src/Aetherphone/Core/Telephony/Audio/AudioCapture.cs) records 48 kHz mono 16-bit PCM in 20 ms buffers through NAudio's `WaveInEvent`, on the microphone picked in Settings (`Configuration.CallInputDevice`, resolved by device name in `AudioDevices`). Each 960-sample frame passes an RMS noise gate (opens at 0.018, closes at 0.010, with 12 frames of hangover), so silence is never encoded. Mute simply stops encoding; nothing is sent while you are muted, and no mute signal goes to the server (the `call.mute` constant exists in `SignalType` but the client never sends it). Open frames are encoded with Opus through the managed Concentus codec; `OpusAudio` pins the settings (VOIP application, voice signal type, VBR, complexity 5, native library disabled) at the 28 kbps bitrate `AudioCapture` hands it.

Framing: `MediaFrame` (src/Aetherphone/Core/Telephony/MediaFrame.cs) prepends a 20-byte header made of a version byte, the 16-byte call id, the sender's slot byte, and a little-endian 16-bit sequence number, and the whole packet goes out as one binary websocket frame via `RealtimeConnection.SendMediaAsync`.

Playback: binary frames arrive on `RealtimeConnection.MediaReceived`. `CallSession` drops frames from other calls and from its own slot, then hands the payload to `VoiceMixer` (src/Aetherphone/Core/Telephony/Audio/VoiceMixer.cs), which keeps one Opus decoder and a 600 ms overflow-discarding jitter buffer per remote slot, mixes all slots into one float stream, tracks a per-slot RMS level (`CallHub.LevelOf` feeds the speaking indicator from it), and plays through a `VolumeSampleProvider` on shared-mode WASAPI with 140 ms latency (`AudioOutputFactory.Create`, falling back to waveOut). The output device is always the system default: `AudioDevices.ResolveOutput` ignores `Configuration.CallOutputDevice` and returns -1, which is why the Settings Calls page only offers a microphone picker.

### Surviving websocket drops

A mid-call socket drop does not end the call. `CallHub` starts a 20 second grace window (`ReconnectGraceMs`), during which `CallView.Connected` reads false and the in-call UI shows a reconnecting state. If `RealtimeConnection` reconnects inside the window (its retry loop is described under [reconnect behavior](#reconnect-behavior) above), the hub sends `call.rejoin`, plus a fresh `call.accept` when the drop happened while `Connecting`, and the next `call.roster` restores everything, including slots. If the window expires, `Advance`, pumped every frame by `PhoneShell`, ends the call as connection-lost and alerts the user. The one exception is `Ringing`: an unanswered incoming call is abandoned the moment the socket drops and logged as missed.

### Where the UI lives

- The Message app owns the call surfaces: src/Aetherphone/Apps/Message/MessageApp.Calls.cs draws the Calls tab with the persisted log, the in-call screen (`MessageRoute.Call`), and the green return-to-call banner, and `MessageApp.SyncCallRoute` pushes and pops the call route as `CallState` changes.
- The full-screen incoming-call overlay is shell chrome, not app UI: `IncomingCallOverlay` (src/Aetherphone/Windows/Components/IncomingCallOverlay.cs) draws over everything while the state is `Ringing`.
- The Dynamic Island (src/Aetherphone/Core/Shell/DynamicIsland.cs) shows the live call outside the app with mute and hang-up buttons and jumps back into it via `CallHub.RequestCallScreen`; the Message app consumes the request with `ConsumeCallScreenRequest`.
- Settings and Control Center: `CallsPage` (src/Aetherphone/Apps/Settings/Pages/CallsPage.cs) holds the enable toggle and the microphone picker, and `ControlRegistry` (src/Aetherphone/Core/ControlCenter/ControlRegistry.cs) exposes the same enable toggle as a Control Center tile.

Every surface reads call state the same way: `CallHub.Snapshot()` returns an immutable `CallView` struct each frame, and nothing subscribes to per-field change events. The hub raises exactly one event, `IncomingCallPresented`, and it exists to open the phone window, not to push state.

## Rate limiting on the client

The server answers abusive traffic with HTTP 429. `HttpService` reacts by pausing the entire host: `PauseHost` records a deadline from the `Retry-After` header (10 seconds if absent, capped at 30, plus jitter), and every request to that host short-circuits until the deadline passes, reporting status 429 to its `onStatus` callback without touching the network.

The user sees this as the `RateLimitPill` (src/Aetherphone/Core/Shell/RateLimitPill.cs), a small pill under the status bar that reads "Too many requests. Retrying in {n}s" (`L.Common.RateLimited`). It polls `HttpService.PauseRemaining` for the API host every frame and animates in only while a pause is active. `PhoneShell` wires it into the shell overlay stack.

Separately, `RequestThrottle` enforces polite pacing toward third parties: `LodestoneService` runs at most one Lodestone request at a time with a 1200 ms minimum interval.

## End-to-end encryption

Direct messages are encrypted client-side so the server stores only ciphertext. The contract has two wire prefixes, both defined in Core/Crypto:

- `EC1.` marks a wrapped conversation key. `CryptoBox.WrapCek` generates an ephemeral P-256 ECDH key pair, derives a wrap key with HKDF-SHA256, and seals the 32-byte conversation encryption key (CEK) with AES-GCM for one recipient's public key. Each conversation member gets their own `EC1.` wrap.
- `AE1.` marks an encrypted message body. `EnvelopeCodec.Encode` produces `AE1.<generation>.<base64>`, where generation is the key version for that conversation. The AES-GCM additional authenticated data binds scope id, generation, and sender id, so a ciphertext cannot be replayed into another conversation or attributed to another sender. The envelope also carries a random franking key whose HMAC commitment tag lets a report prove what a message said without giving the server decryption ability.

Scopes name a conversation across apps: `ConversationKeyStore.ChatScope`, `VelvetScope`, `GramScope`, and `AdScope` (src/Aetherphone/Core/Crypto/ConversationKeyStore.cs) cover Message-app chats, Velvet DMs, Aethergram DMs, and Yellow Pages inquiries. Attachments and voice notes are sealed too, via `MediaEnvelope` with its own AAD domain string `aep-media-v1`. Public content (posts, comments, profiles) is not end-to-end encrypted.

### Key storage and recovery

`KeyVault` manages the device identity key:

- The private key is exported as PKCS8 and stored in `Configuration.EncryptionKeyCache`, protected by Windows DPAPI (`LocalKeyProtector` uses `ProtectedData` with the account id as entropy). If DPAPI is unavailable the key is simply not persisted and `LocalCacheUnavailable` is set.
- The public key is published to the server via `KeysClient.PutMyKeysAsync` (`PUT /keys/me`), and peers fetch it through `PeerKeyDirectory`.
- Recovery codes: `KeyVault.CreateRecoveryCodeAsync` wraps the private key with a key derived from a 20-character code (PBKDF2-SHA256, 600,000 iterations, in src/Aetherphone/Core/Crypto/RecoveryKey.cs) and escrows the result server-side; `RecoverWithCodeAsync` reverses it on a new device.
- `KeyVault.State` drives the UI: `Unlocked`, `Provisioning`, `Locked` (an account key exists but this device cannot load it), `Unsupported`, or `Unavailable`.

### Platform support

`CryptoBox.TryGenerateIdentity` tries `ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)` first and, on Windows, falls back to a CNG named-curve key. If both fail (observed on some Wine setups whose bcrypt lacks P-256 support), the vault ends in `KeyVaultState.Unsupported` and encrypted conversations show placeholder text instead of bodies. As of this writing, master carries no third-party crypto library; a BouncyCastle-based fallback for those environments has been proposed in a pull request but is not merged (check `PackageReference` entries in src/Aetherphone/Aetherphone.csproj for current truth).

## Media upload and download

Uploads are a three-step dance, visible end to end in `AvatarUpload.RunAsync` (src/Aetherphone/Core/Aethernet/AvatarUpload.cs):

1. `MediaClient.UploadUrlAsync` posts content type and a scope string to `/media/upload-url` and receives an `UploadUrlResponse` with `Key`, `UploadUrl`, and `PublicUrl`.
2. `MediaClient.UploadImageAsync` PUTs the raw bytes to `UploadUrl`. `AethernetTransport.UploadBearerFor` attaches the session token only when the upload URL's host and port match the API base URL; a presigned URL on another host is sent without credentials by design.
3. The caller references the media in a follow-up API call, for example `AccountClient.UpdateProfileAsync` with the returned `PublicUrl`, or a chat send with the media key.

For end-to-end encrypted attachments, `MessageCipher.PrepareOutboundMedia` seals the bytes before step 2, so the storage layer only ever holds ciphertext.

Downloads go through `HttpService.GetBytesAsync` (up to 3 attempts with backoff). Display-side caching is layered: `DiskCache` persists bytes under the plugin config directory with a size budget, and `MediaCache` turns bytes into GPU textures with a 96 MB texture budget, 30-day disk age, and a 2-minute cooldown after a failed fetch. Encrypted attachments are fetched sealed and decrypted with `MessageCipher.TryDecryptMedia` before display.

## Dev vs prod endpoints

The base URL lives in `Configuration.AethernetBaseUrl`. There is no in-app editor for it; a developer edits the saved plugin configuration JSON (or sets the property from code) and reloads. That JSON is the file Dalamud hands the plugin as `PluginInterface.ConfigFile`: `Aetherphone.json` (named after the plugin's internal name in src/Aetherphone/Aetherphone.json) in the `pluginConfigs` folder of the XIVLauncher data directory under `%AppData%`. Edit it only while the plugin is unloaded, or set the value from code instead: the running plugin rewrites the entire file on every `Configuration.Save()`, so a hand edit made while the plugin is loaded is clobbered by the next save.

Guard rails in `Configuration.NormalizeAethernetBaseUrl` (called at boot from src/Aetherphone/Plugin.cs):

- Invalid or empty URLs reset to `DefaultAethernetBaseUrl`.
- A known legacy production host is force-migrated to the current default.
- In Release builds, loopback URLs (localhost) also reset to the default. Only Debug builds may target a local backend, which keeps a stray dev config from shipping to users.

Because the backend is a separate repository, running against dev means running that service yourself or pointing at a dev deployment. Never test unreleased client changes against the production API; use a Debug build against a non-production base URL, and keep any tokens for it out of commits and screenshots.

## API client map

Everything Aethernet-flavored under Core/ in one pass:

| Folder | One line |
| --- | --- |
| src/Aetherphone/Core/Net/ | Transport primitives shared by all remote features |
| src/Aetherphone/Core/Aethernet/ | Session, transport, sign-in, and the typed clients plus DTO (data transfer object, the wire-format record) contracts |
| src/Aetherphone/Core/Crypto/ | E2EE building blocks described above |
| src/Aetherphone/Core/Lodestone/ | NetStone-based Lodestone lookups for avatars and portraits, throttled and disk-cached |
| src/Aetherphone/Core/Social/ | Shared social domain types and stores (feeds, stories, identities) used by the social apps |
| src/Aetherphone/Core/Moderation/ | Moderation notice polling, presentation, and the suspension gate |
| src/Aetherphone/Core/Report/ | The central report popup; submissions travel through `SafetyClient` to `/reports` |
| src/Aetherphone/Core/Telephony/ | Calls: the websocket, signal routing, call state, and Opus audio |
| src/Aetherphone/Core/Market/ | Universalis market client, a third-party API outside Aethernet |

The chat stores that consume these clients are covered in [Messaging and chat](messaging-and-chat.md), and how pings become banners and badges is covered in [Notifications](notifications.md).

## Gotchas

- Typed clients never throw on HTTP failure. A `null` return can mean network error, non-2xx, an active rate-limit pause, or a signed-out session (`AethernetTransport` short-circuits to `default` when `Session.IsSignedIn` is false). Pass an `onStatus` callback when the difference matters.
- One 401 from any endpoint marks the whole session `TokenRejected` (`AethernetSession.ReportAuthStatus`) and every subsequent request no-ops until the user signs in again. "The API stopped responding" is often just this.
- One 429 pauses every request to that host process-wide via `HttpService.PauseHost`, not only the endpoint that tripped it. Unrelated features sharing the host go quiet until the pause expires.
- The realtime socket is not gated on `Configuration.CallsEnabled`. Do not "optimize" `CallHub.Reconcile` into skipping `router.Start()` when calls are off; notification pings ride the same socket.
- Release builds reset loopback base URLs to production at boot (`Configuration.ShouldResetBaseUrl` compiles the loopback check only outside `#if DEBUG`). If your local backend config keeps disappearing, you are running a Release build.
- Upload PUTs only carry the bearer token when the upload URL's host and port match the API base URL (`AethernetTransport.UploadBearerFor`). Expecting `Authorization` on an external storage host will fail silently.
- `EtagCache` keys include the bearer token and `X-Aep-App` scope, so two app-scoped `AethernetApi` instances requesting the same URL maintain separate cache entries. That is intentional; do not dedupe them.
- `HttpService` caps response bodies at 32 MB (`MaxResponseBytes`). Anything larger fails the request rather than streaming.

## Related docs

- [Getting started](getting-started.md): build the plugin and load a Debug dev build
- [Architecture](architecture.md): where `PhoneServices.Build` wires all of these services together
- [Messaging and chat](messaging-and-chat.md): the chat stores built on `ChatClient`, `MessageCipher`, and the inbox cadence
- [Notifications](notifications.md): how realtime pings and polls become banners, sounds, and badges
- [State and persistence](state-and-persistence.md): `Configuration`, per-character data, and media storage on disk
- [App framework](app-framework.md): how apps get their app-scoped `AethernetApi` instances
