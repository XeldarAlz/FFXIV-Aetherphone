using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const int MessageMax = 1000;
    private const float ThreadPollSeconds = 2.5f;
    private const float TypingSendSeconds = 3f;

    private readonly ChatTranscript transcript = new();
    private readonly ChatMenuController messageMenuController = new();
    private readonly ChatComposer composer = new();
    private readonly ChatSearchController searchController = new();
    private readonly VoiceNotePlayer voicePlayer = new();
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private readonly PhotoZoomView imageZoom = new();
    private volatile string? pendingVoicePlay;
    private VelvetMessageDto[] transcriptSource = Array.Empty<VelvetMessageDto>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private Func<string, string?>? threadMediaUrl;
    private Func<string, IDalamudTextureWrap?>? resolveThreadImage;
    private Action<string>? onThreadImageClick;
    private Action<string>? onMessageContext;
    private Action<string>? onThreadQuoteClick;
    private Action<string, string>? onThreadReactionClick;
    private Func<string, VoiceNoteState>? voiceStateFor;
    private Action<string>? onThreadVoiceToggle;
    private Action<string>? composerPickImage;
    private Action<string, string, string?>? composerSendText;
    private Action<string, string, string>? composerEditText;
    private Action<string, byte[], int>? composerSendVoice;
    private Func<int>? composerResolveVoice;
    private Action? onThreadLoadOlder;
    private float sinceThreadPoll;
    private float sinceTypingSend = TypingSendSeconds;
    private string lastTypingDraft = string.Empty;
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string[] chatPickerPaths = Array.Empty<string>();
    private string? chatPickerThreadId;
    private string? chatPendingPickedPath;
    private volatile ReactorDto[]? reactors;
    private string? reactorsFor;

    private void DrawThread(Rect area, string threadId)
    {
        if (store.ThreadId != threadId)
        {
            store.OpenThread(threadId);
            sinceThreadPoll = ThreadPollSeconds;
            lastTypingDraft = string.Empty;
            searchController.Close();
            composer.ClearTargets();
            composer.CancelVoice();
            voicePlayer.Stop();
        }

        store.NoteThreadViewed(threadId);
        TickThread(threadId);
        DrawThreadHeader(area, threadId);
        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerHeight = 52f * scale;
        var accessoryHeight = composer.AccessoryHeight;
        var transcriptMessages = BuildTranscript(store.Messages);
        if (searchController.Open)
        {
            var searchHeight = 44f * scale;
            searchController.Draw(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
                new ChatSearchModel(ui, transcriptMessages, transcript.RequestScrollTo));
            top += searchHeight;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - accessoryHeight));
        threadMediaUrl ??= store.DmMediaUrl;
        resolveThreadImage ??= ResolveThreadImage;
        onThreadImageClick ??= id => router.Push(VelvetView.ImageView(id));
        onMessageContext ??= OpenMessageMenu;
        onThreadQuoteClick ??= transcript.RequestScrollTo;
        onThreadReactionClick ??= (messageId, _) => router.Push(VelvetView.Reactions(messageId));
        voiceStateFor ??= voicePlayer.StateFor;
        onThreadVoiceToggle ??= ToggleVoice;
        onThreadLoadOlder ??= store.LoadOlder;
        var model = new ChatTranscriptModel(threadId, transcriptMessages, store.Me?.UserId ?? string.Empty, Accent,
            theme, VelvetTheme.MutedInk, VelvetTheme.BodyInk, store.OtherTyping, store.LoadingThread,
            false, images, threadMediaUrl, onThreadImageClick, Loc.T(L.Velvet.ThreadEmpty), Loc.T(L.Common.Loading),
            onMessageContext, onQuoteClick: onThreadQuoteClick, onReactionClick: onThreadReactionClick,
            voiceState: voiceStateFor, onVoiceToggle: onThreadVoiceToggle,
            onLoadOlder: onThreadLoadOlder, hasMoreOlder: store.HasMoreOlder, loadingOlder: store.LoadingOlder,
            resolveImage: resolveThreadImage);
        transcript.Draw(listRect, model);
        composerPickImage ??= id => router.Push(VelvetView.ChatImage(id));
        composerSendText ??= ComposerSendText;
        composerEditText ??= ComposerEditText;
        composerSendVoice ??= ComposerSendVoice;
        composerResolveVoice ??= ResolveVoiceInput;
        composer.Draw(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), new ChatComposerModel
        {
            Ui = ui,
            ConversationId = threadId,
            MaxLength = MessageMax,
            Sending = store.Sending,
            CanImage = true,
            CanVoice = true,
            CanHandleEscape = !searchController.Open,
            ResolveVoiceInput = composerResolveVoice,
            OnPickImage = composerPickImage,
            OnSendText = composerSendText,
            OnEditText = composerEditText,
            OnSendVoice = composerSendVoice,
        });
        DrawMessageMenu(area);
    }

    private void DrawThreadHeader(Rect area, string threadId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        ChatHeaderControls.DrawLock(ui, area, rowCenterY, store.EncryptingCurrent, store.VaultState, () => { });
        ChatHeaderControls.DrawSearchToggle(ui, area, rowCenterY, searchController.Open, searchController.Toggle);
        var name = ThreadTitle(threadId);
        var avatarHandle = ThreadAvatar(threadId, out var monogram, out var presence);
        var avatarRadius = 18f * scale;
        var nameSize = Typography.Measure(name, 1f, FontWeight.SemiBold);
        var gap = 9f * scale;
        var groupWidth = avatarRadius * 2f + gap + nameSize.X;
        var startX = MathF.Max(area.Center.X - groupWidth * 0.5f, area.Min.X + 48f * scale);
        var avatarCenter = new Vector2(startX + avatarRadius, rowCenterY);
        AvatarView.Draw(drawList, avatarCenter, avatarRadius, Accent, monogram, 0.95f, avatarHandle, 32);
        PresenceDot(drawList, new Vector2(avatarCenter.X + avatarRadius - 3f * scale,
            avatarCenter.Y + avatarRadius - 3f * scale), presence);
        var nameLeft = avatarCenter.X + avatarRadius + gap;
        var offset = ThreadOffset(threadId);
        var textWidth = nameSize.X;
        if (offset is { } minutes)
        {
            var timeText = Aetherphone.Core.Social.SocialTimeZone.Describe(minutes);
            var subSize = Typography.Measure(timeText, 0.72f, FontWeight.Regular);
            var gapY = 1f * scale;
            var stackTop = rowCenterY - (nameSize.Y + gapY + subSize.Y) * 0.5f;
            Typography.Draw(new Vector2(nameLeft, stackTop), name, theme.TextStrong, 1f, FontWeight.SemiBold);
            Typography.Draw(new Vector2(nameLeft, stackTop + nameSize.Y + gapY), timeText, VelvetTheme.MutedInk, 0.72f);
            textWidth = MathF.Max(nameSize.X, subSize.X);
        }
        else
        {
            Typography.Draw(new Vector2(nameLeft, rowCenterY - nameSize.Y * 0.5f), name, theme.TextStrong, 1f,
                FontWeight.SemiBold);
        }

        var hitMin = new Vector2(avatarCenter.X - avatarRadius, area.Min.Y);
        var hitMax = new Vector2(nameLeft + textWidth, area.Min.Y + AppHeader.Height * scale);
        if (UiInteract.HoverClick(hitMin, hitMax))
        {
            OpenProfile(threadId);
        }
    }

    private void PresenceDot(ImDrawListPtr drawList, Vector2 center, int presence)
    {
        if (presence > VelvetPresence.Offline)
        {
            VBadge.Dot(drawList, center, VelvetTheme.PresenceColor(presence));
        }
    }

    private int? ThreadOffset(string threadId)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return threads[index].UtcOffsetMinutes;
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                return connections[index].UtcOffsetMinutes;
            }
        }

        return null;
    }

    private string ThreadTitle(string threadId)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                return string.IsNullOrEmpty(threads[index].OtherDisplayName)
                    ? threads[index].OtherHandle
                    : threads[index].OtherDisplayName;
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                return string.IsNullOrEmpty(connections[index].DisplayName)
                    ? connections[index].Handle
                    : connections[index].DisplayName;
            }
        }

        return string.Empty;
    }

    private AvatarHandle ThreadAvatar(string threadId, out string monogram, out int presence)
    {
        var threads = store.Threads;
        for (var index = 0; index < threads.Length; index++)
        {
            if (threads[index].OtherUserId == threadId)
            {
                var thread = threads[index];
                monogram = Monogram(thread.OtherDisplayName, thread.OtherHandle);
                presence = thread.Presence;
                return lodestone.Remote(thread.OtherUserId, ToUri(thread.OtherAvatarUrl));
            }
        }

        var connections = store.Connections;
        for (var index = 0; index < connections.Length; index++)
        {
            if (connections[index].UserId == threadId)
            {
                var connection = connections[index];
                monogram = Monogram(connection.DisplayName, connection.Handle);
                presence = connection.Presence;
                return lodestone.Remote(connection.UserId, ToUri(connection.AvatarUrl));
            }
        }

        monogram = "?";
        presence = VelvetPresence.Offline;
        return AvatarHandle.Disabled;
    }

    private static string Monogram(string displayName, string handle)
    {
        var source = string.IsNullOrEmpty(displayName) ? handle : displayName;
        return source.Length > 0 ? source[..1].ToUpperInvariant() : "?";
    }

    private static Uri? ToUri(string? url) =>
        string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ? null : uri;

    private void TickThread(string threadId)
    {
        PumpPendingVoice();
        var delta = ImGui.GetIO().DeltaTime;
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= ThreadPollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
            store.RefreshTyping(threadId);
        }

        sinceTypingSend += delta;
        var draft = composer.Draft;
        if (draft != lastTypingDraft)
        {
            lastTypingDraft = draft;
            if (draft.Trim().Length > 0 && sinceTypingSend >= TypingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(threadId);
            }
        }
    }

    private ReadOnlySpan<TranscriptMessage> BuildTranscript(VelvetMessageDto[] source)
    {
        if (ReferenceEquals(source, transcriptSource))
        {
            return transcriptCache;
        }

        transcriptSource = source;
        var myId = store.Me?.UserId ?? string.Empty;
        var otherName = store.ThreadId is { } threadId ? ThreadTitle(threadId) : string.Empty;
        var mapped = new TranscriptMessage[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            var message = source[index];
            if (message.Deleted)
            {
                mapped[index] = new TranscriptMessage(message.Id, message.SenderId, Loc.T(L.Message.DeletedBody), 0,
                    message.CreatedAtUnix, 0, 0, null, string.Empty, default, TranscriptFlags.Deleted);
                continue;
            }

            var replySender = string.Empty;
            var replyBody = string.Empty;
            if (message.ReplyToId is not null)
            {
                replySender = message.ReplySenderId == myId ? Loc.T(L.Message.You) : otherName;
                replyBody = ChatText.QuotePreview(message.ReplyBody, message.ReplyKind);
            }

            TranscriptReaction[]? reactions = null;
            var summaries = message.Reactions;
            if (summaries is { Length: > 0 })
            {
                reactions = new TranscriptReaction[summaries.Length];
                for (var summaryIndex = 0; summaryIndex < summaries.Length; summaryIndex++)
                {
                    reactions[summaryIndex] = new TranscriptReaction(summaries[summaryIndex].Token,
                        summaries[summaryIndex].Count, summaries[summaryIndex].Mine);
                }
            }

            mapped[index] = new TranscriptMessage(message.Id, message.SenderId, message.Body, message.Kind,
                message.CreatedAtUnix, message.MediaWidth, message.MediaHeight, message.ReadAtUnix, string.Empty,
                default, MessageFlags(message), message.ReplyToId, replySender, replyBody, message.ReplyKind,
                message.DurationSecs, reactions);
        }

        transcriptCache = mapped;
        return transcriptCache;
    }

    private byte MessageFlags(VelvetMessageDto message)
    {
        byte flags = 0;
        if (message.EditedAtUnix is not null)
        {
            flags |= TranscriptFlags.Edited;
        }

        if (message.EncVersion == 0)
        {
            return flags;
        }

        var state = store.DecryptionState(message.Id);
        flags |= TranscriptFlags.Encrypted;
        if (state.IsPlaceholder)
        {
            flags |= TranscriptFlags.Placeholder;
        }
        else if (state.State == DmBodyState.Decrypted && !state.Verified)
        {
            flags |= TranscriptFlags.Unverified;
        }

        return flags;
    }

    private void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        messageMenuController.Open(messageId, message.SenderId == (store.Me?.UserId ?? string.Empty), message.Kind);
    }

    private void DrawMessageMenu(Rect area)
    {
        if (!messageMenuController.Active)
        {
            return;
        }

        var model = new ChatMenuModel
        {
            Ui = ui,
            ShowReactions = true,
            CanReply = true,
            CanForward = false,
            CanCopy = true,
            CanStar = false,
            CanEdit = true,
            CanInfo = false,
            CanDelete = true,
            CanReport = true,
            IsStarred = _ => false,
            MyReactionTo = store.MyReactionTo,
            OnReply = BeginReply,
            OnForward = _ => { },
            OnCopy = id => ChatActions.CopyMessageText(transcriptCache, id, CanRevealBody),
            OnStar = _ => { },
            OnEdit = BeginEdit,
            OnInfo = _ => { },
            OnDelete = AskDeleteMessage,
            OnReport = OpenReportMessage,
            OnReact = store.SetReaction,
        };
        messageMenuController.Draw(area, model);
    }

    private VelvetMessageDto? FindMessage(string messageId)
    {
        var snapshot = store.Messages;
        for (var index = 0; index < snapshot.Length; index++)
        {
            if (snapshot[index].Id == messageId)
            {
                return snapshot[index];
            }
        }

        return null;
    }

    private bool CanRevealBody(string id)
    {
        var message = FindMessage(id);
        if (message is null)
        {
            return false;
        }

        return message.EncVersion != 1 || store.DecryptionState(id).State == DmBodyState.Decrypted;
    }

    private void BeginReply(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Deleted)
        {
            return;
        }

        var myId = store.Me?.UserId ?? string.Empty;
        var senderName = message.SenderId == myId ? Loc.T(L.Message.You) : ThreadTitle(store.ThreadId ?? messageId);
        composer.BeginReply(messageId, senderName, ChatText.QuotePreview(message.Body, message.Kind));
    }

    private void BeginEdit(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || message.Kind != 0 || message.Deleted)
        {
            return;
        }

        if (message.EncVersion != 0 && store.DecryptionState(messageId).State != DmBodyState.Decrypted)
        {
            return;
        }

        composer.BeginEdit(messageId, message.Body ?? string.Empty);
    }

    private void AskDeleteMessage(string messageId)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Message.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Message.DeleteAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => store.DeleteMessage(messageId, done),
        });
    }

    private void OpenReportMessage(string messageId)
    {
        Plugin.Report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Encryption.ReportMessageAction),
            Disclosure = Loc.T(L.Encryption.ReportDisclosure),
            Submit = (reason, done) => store.ReportMessage(messageId, reason, done),
        });
    }

    private void ComposerSendText(string threadId, string text, string? replyToId)
    {
        store.SendMessage(threadId, text, _ => { }, replyToId);
        transcript.RequestSnapToBottom();
        lastTypingDraft = string.Empty;
    }

    private void ComposerEditText(string threadId, string editId, string text)
    {
        store.EditMessage(threadId, editId, text, _ => { });
        lastTypingDraft = string.Empty;
    }

    private void ComposerSendVoice(string threadId, byte[] wavBytes, int durationSecs)
    {
        store.SendVoiceMessage(threadId, wavBytes, durationSecs, _ => { });
        transcript.RequestSnapToBottom();
    }

    private int ResolveVoiceInput() => AudioDevices.ResolveInput(configuration.CallInputDevice);

    private void ToggleVoice(string messageId)
    {
        if (voiceBytes.TryGetValue(messageId, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(messageId, bytes);
            return;
        }

        pendingVoicePlay = messageId;
        FetchVoice(messageId);
    }

    private IDalamudTextureWrap? ResolveThreadImage(string messageId)
    {
        var message = store.FindMessage(messageId);
        if (message is null)
        {
            return null;
        }

        if (message.EncVersion != EnvelopeCodec.VersionEnvelope)
        {
            return images.Get(store.DmMediaUrl(messageId));
        }

        if (store.ThreadId is not { } partner)
        {
            return null;
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null)
        {
            return null;
        }

        return images.GetKeyed(messageId, async token =>
        {
            var data = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
            return data is null ? null : store.DecryptMedia(message, data, partner);
        });
    }

    private void FetchVoice(string messageId)
    {
        if (voiceBytes.ContainsKey(messageId))
        {
            return;
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null || !voiceFetching.TryAdd(messageId, 0))
        {
            return;
        }

        var message = store.FindMessage(messageId);
        var partner = store.ThreadId;
        _ = Task.Run(async () =>
        {
            try
            {
                var data = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                if (data is not null)
                {
                    byte[]? plain;
                    if (message is { EncVersion: EnvelopeCodec.VersionEnvelope })
                    {
                        plain = partner is not null ? store.DecryptMedia(message, data, partner) : null;
                    }
                    else
                    {
                        plain = data;
                    }

                    if (plain is not null)
                    {
                        voiceBytes[messageId] = plain;
                    }
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Voice note download failed: {exception.Message}");
            }
            finally
            {
                voiceFetching.TryRemove(messageId, out _);
            }
        });
    }

    private void PumpPendingVoice()
    {
        if (pendingVoicePlay is not { } id)
        {
            return;
        }

        if (voiceBytes.TryGetValue(id, out var bytes))
        {
            pendingVoicePlay = null;
            voicePlayer.Toggle(id, bytes);
            return;
        }

        FetchVoice(id);
    }

    private void DrawReactions(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Message.ReactionsTitle), back);
        if (reactorsFor != messageId)
        {
            reactorsFor = messageId;
            reactors = null;
            store.LoadReactions(messageId, result => reactors = result);
        }

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var snapshot = reactors;
        if (snapshot is null)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 60f * scale), Loc.T(L.Common.Loading),
                ui.MutedInk);
            return;
        }

        if (snapshot.Length == 0)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.ThumbsUp, Loc.T(L.Message.ReactionsTitle), string.Empty);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            for (var index = 0; index < snapshot.Length; index++)
            {
                DrawReactorRow(messageId, snapshot[index], scale);
            }

            ImGui.Dummy(new Vector2(0f, 24f * scale));
        }
    }

    private void DrawReactorRow(string messageId, ReactorDto reactor, float scale)
    {
        var myId = store.Me?.UserId ?? string.Empty;
        var mine = reactor.UserId == myId;
        var rowHeight = 54f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var drawList = ImGui.GetWindowDrawList();
        var rowMax = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, rowMax, 14f * scale);
        var pad = 12f * scale;
        var radius = 17f * scale;
        var avatarCenter = new Vector2(origin.X + pad + radius, origin.Y + rowHeight * 0.5f);
        var label = mine
            ? Loc.T(L.Message.You)
            : reactor.DisplayName.Length > 0 ? reactor.DisplayName : reactor.Handle;
        AvatarView.DrawRemote(drawList, avatarCenter, radius, theme, label, string.Empty, reactor.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        if (mine)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 10f * scale), label, theme.TextStrong, 1f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Message.TapToRemove), ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label, theme.TextStrong,
                1f, FontWeight.SemiBold);
        }

        var tokenColor = ReactionArt.Color(reactor.Token);
        AppSkin.Icon(new Vector2(origin.X + width - pad - 10f * scale, origin.Y + rowHeight * 0.5f),
            ReactionArt.Glyph(reactor.Token), tokenColor, 1f);
        if (mine && UiInteract.HoverClick(origin, rowMax))
        {
            store.SetReaction(messageId, string.Empty);
            var current = reactors;
            if (current is not null)
            {
                var next = new List<ReactorDto>(current.Length);
                for (var index = 0; index < current.Length; index++)
                {
                    if (current[index].UserId != myId)
                    {
                        next.Add(current[index]);
                    }
                }

                reactors = next.ToArray();
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight + 8f * scale));
    }

    private void DrawImageView(Rect area, string messageId)
    {
        if (imageViewId != messageId)
        {
            imageViewId = messageId;
            imageSaveOutcome = 0;
            imageZoom.Reset();
        }

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.94f)));
        var headerHeight = AppHeader.Height * scale;
        var footerHeight = 60f * scale;
        var fitMin = new Vector2(area.Min.X + 8f * scale, area.Min.Y + headerHeight);
        var fitMax = new Vector2(area.Max.X - 8f * scale, area.Max.Y - footerHeight);
        var texture = ResolveThreadImage(messageId);
        if (texture is null)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, (fitMin.Y + fitMax.Y) * 0.5f), Loc.T(L.Common.Loading),
                VelvetTheme.MutedInk);
        }
        else
        {
            imageZoom.Draw(new Rect(fitMin, fitMax), texture, theme, 10f * scale);
        }

        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, string.Empty, back);
        var saved = imageSaveOutcome == 1;
        var label = saved ? Loc.T(L.Velvet.SavedToGallery) : Loc.T(L.Velvet.SaveToGallery);
        var buttonWidth = MathF.Min(240f * scale, area.Width - 32f * scale);
        var buttonHeight = 42f * scale;
        var buttonTop = area.Max.Y - footerHeight + (footerHeight - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(area.Center.X - buttonWidth * 0.5f, buttonTop),
            new Vector2(area.Center.X + buttonWidth * 0.5f, buttonTop + buttonHeight));
        if (ui.PillButton(buttonRect, label, !saved) && !saved && !imageSaveBusy && texture is not null)
        {
            SaveDmImage(messageId);
        }
    }

    private void SaveDmImage(string messageId)
    {
        var url = store.DmMediaUrl(messageId);
        var message = store.FindMessage(messageId);
        if (string.IsNullOrEmpty(url) || imageSaveBusy || message is null)
        {
            return;
        }

        var encrypted = message.EncVersion == EnvelopeCodec.VersionEnvelope;
        var partner = store.ThreadId;
        if (encrypted && partner is null)
        {
            return;
        }

        imageSaveBusy = true;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var raw = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                var bytes = encrypted && raw is not null
                    ? store.DecryptMedia(message, raw, partner!)
                    : raw;
                if (bytes is not null)
                {
                    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                    var pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);
                    library.Save(pixels, image.Width, image.Height);
                    succeeded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Velvet] save image failed: {exception.Message}");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
    }

    private void DrawChatImage(Rect area, string threadId)
    {
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Velvet.ChangePhoto), back);
        if (chatPickerThreadId != threadId)
        {
            chatPickerThreadId = threadId;
            chatPickerPaths = library.List();
            chatPendingPickedPath = null;
        }

        var picked = Interlocked.Exchange(ref chatPendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            SendChatImage(threadId, picked);
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, Loc.T(L.Velvet.ImportFromPc), true))
        {
            LaunchChatImageDialog();
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (chatPickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    Loc.T(L.Velvet.NoPhotos), VelvetTheme.MutedInk);
                return;
            }

            const int columns = 3;
            var gap = 6f * scale;
            var cell = (ScrollLayout.StableContentWidth() - gap * (columns - 1)) / columns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < chatPickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("chatpick", new Vector2(cell, cell));
                        DrawPickerThumbnail(chatPickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                            scale);
                        if (clicked)
                        {
                            SendChatImage(threadId, chatPickerPaths[index]);
                        }
                    }

                    if (index % columns != columns - 1)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void SendChatImage(string threadId, string path)
    {
        store.SendImageMessage(threadId, path, string.Empty, _ => { });
        transcript.RequestSnapToBottom();
        chatPickerThreadId = null;
        router.Pop();
    }

    private void LaunchChatImageDialog()
    {
        NativeFileDialog.PickImage(Loc.T(L.Velvet.ChangePhoto),
            path => Interlocked.Exchange(ref chatPendingPickedPath, path));
    }

    private static void DrawPickerThumbnail(string path, Vector2 min, Vector2 max, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = Plugin.WallpaperImages.Get(path);
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            return;
        }

        var size = texture.Size;
        var uv0 = Vector2.Zero;
        var uv1 = Vector2.One;
        if (size.X > 0f && size.Y > 0f)
        {
            var aspect = size.X / size.Y;
            if (aspect > 1f)
            {
                var inset = (1f - 1f / aspect) * 0.5f;
                uv0 = new Vector2(inset, 0f);
                uv1 = new Vector2(1f - inset, 1f);
            }
            else if (aspect < 1f)
            {
                var inset = (1f - aspect) * 0.5f;
                uv0 = new Vector2(0f, inset);
                uv1 = new Vector2(1f, 1f - inset);
            }
        }

        drawList.AddImageRounded(texture.Handle, min, max, uv0, uv1, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }
}
