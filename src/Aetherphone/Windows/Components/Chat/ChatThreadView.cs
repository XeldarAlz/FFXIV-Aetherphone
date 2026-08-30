using System.Collections.Concurrent;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Dalamud.Bindings.ImGui;
using Aetherphone.Core.Translation;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal abstract class ChatThreadView<TMessage, TThread> : IDisposable, IChatTranscriptMedia,
    IChatTranscriptInteractions, IChatTranscriptVoice, IChatTranscriptPaging, IChatTranscriptTranslation
    where TMessage : class, IIdentified
    where TThread : class, IIdentified
{
    protected const int MessageMax = 1000;
    private const float ReactorEmojiSize = 26f;

    protected readonly ChatThreadStoreBase<TMessage, TThread> store;
    protected readonly AppSkin ui;
    protected readonly RemoteImageCache images;
    protected readonly LodestoneService lodestone;
    protected readonly HttpService http;
    protected readonly PhotoLibrary library;
    protected readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly ReportService report;
    protected readonly TranslationService translation;
    private readonly WallpaperImageCache wallpaperImages;
    protected readonly ChatTranscript transcript = new();
    protected readonly ChatMenuController menuController = new();
    protected readonly ChatComposer composer = new();
    protected readonly ChatSearchController searchController = new();
    protected readonly VoiceNotePlayer voicePlayer = new();
    protected readonly EncryptionInfoPane encryptionPane;
    private readonly PhotoZoomView imageZoom = new();
    private readonly Dictionary<string, string> sessionDrafts = new(StringComparer.Ordinal);
    private volatile string? failedSendThreadId;
    private volatile string? failedSendText;
    private static readonly TimeSpan VoiceFailureRetryFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SyncBannerFloor = TimeSpan.FromSeconds(2);
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> voiceFailed = new(StringComparer.Ordinal);
    private readonly float threadPollSeconds;
    private readonly float typingSendSeconds;
    private readonly Action<string> pickImage;
    private readonly Action<string> shareLocation;
    private readonly Action<string, string, string?> sendText;
    private readonly Action<string, string, string> editText;
    private readonly Action<string, byte[], int> sendVoice;
    private readonly Func<int> resolveVoiceInput;
    private string? pendingPrefill;
    private readonly Func<string, bool> canRevealBody;

    private volatile string? pendingVoicePlay;
    private TMessage[] transcriptSource = Array.Empty<TMessage>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private TMessage[] sweptSource = Array.Empty<TMessage>();
    private bool sweptTranslated;
    private string scopeThreadId = string.Empty;
    private string conversationScope = string.Empty;
    private const float PushActivePollMultiplier = 3f;
    private const int ResumeFrameGap = 3;

    private float sinceThreadPoll;
    private float sinceTypingPoll;
    private int lastThreadDrawFrame;
    private float sinceTypingSend;
    private string lastTypingDraft = string.Empty;
    private string? imageViewId;
    private volatile int imageSaveOutcome;
    private volatile bool imageSaveBusy;
    private string[] pickerPaths = Array.Empty<string>();
    private string? pickerThreadId;
    private string? pendingPickedPath;
    private volatile ReactorDto[]? reactors;
    private string? reactorsFor;

    protected ChatThreadView(ChatThreadStoreBase<TMessage, TThread> store, AppSkin ui, RemoteImageCache images,
        LodestoneService lodestone, HttpService http, PhotoLibrary library, Configuration configuration,
        ConfirmService confirm, ReportService report, TranslationService translation,
        WallpaperImageCache wallpaperImages, EncryptionHelpService encryptionHelp,
        float threadPollSeconds, float typingSendSeconds)
    {
        this.store = store;
        this.ui = ui;
        this.images = images;
        this.lodestone = lodestone;
        this.http = http;
        this.library = library;
        this.configuration = configuration;
        this.confirm = confirm;
        this.report = report;
        this.translation = translation;
        this.wallpaperImages = wallpaperImages;
        this.threadPollSeconds = threadPollSeconds;
        this.typingSendSeconds = typingSendSeconds;
        encryptionPane = new EncryptionInfoPane(store.Vault, confirm, encryptionHelp);
        sinceTypingSend = typingSendSeconds;
        pickImage = OpenImagePicker;
        shareLocation = AskShareLocation;
        sendText = ComposerSendText;
        editText = ComposerEditText;
        sendVoice = ComposerSendVoice;
        resolveVoiceInput = ResolveVoiceInput;
        canRevealBody = CanRevealBody;
    }

    protected abstract PhoneTheme Theme { get; }

    protected abstract IPhoneApp Owner { get; }

    protected abstract INavigator Navigation { get; }

    protected abstract Action BackAction { get; }

    protected abstract string MyUserId { get; }

    protected abstract Vector4 Accent { get; }

    protected abstract string EmptyText { get; }

    protected abstract string LogTag { get; }

    protected abstract string PickerTitle { get; }

    protected abstract string ImportLabel { get; }

    protected abstract string NoPhotosLabel { get; }

    protected abstract string SaveLabel { get; }

    protected abstract string SavedLabel { get; }

    protected virtual bool IsGroupThread => false;

    protected virtual ChatComposerStyle ComposerStyle => ChatComposerStyle.Bar;

    protected virtual string ComposerHint => Loc.T(L.Velvet.MessageHint);

    protected virtual IChatTranscriptPostCards? PostCards => null;

    protected virtual IChatTranscriptStoryReplies? StoryReplies => null;

    protected abstract void DrawHeader(Rect area, string threadId);

    protected virtual void OpenEncryptionInfo(string threadId)
    {
    }

    protected virtual void DrawAboveTranscript(ref Rect listRect, string threadId)
    {
    }

    protected virtual void OnThreadSwitchingFrom(string previousThreadId)
    {
        if (composer.IsEditing)
        {
            return;
        }

        var draft = composer.Draft.Trim();
        if (draft.Length == 0)
        {
            sessionDrafts.Remove(previousThreadId);
            return;
        }

        sessionDrafts[previousThreadId] = composer.Draft;
    }

    protected virtual void OnThreadOpened(string threadId) =>
        composer.Draft = sessionDrafts.GetValueOrDefault(threadId, string.Empty);

    protected virtual void OnDraftConsumed(string threadId) => sessionDrafts.Remove(threadId);

    protected abstract TranscriptMessage[] MapTranscript(TMessage[] source);

    protected abstract ChatMenuModel BuildMenuModel();

    protected abstract void BeginReply(string messageId);

    protected abstract bool IsDeleted(TMessage message);

    protected abstract string SenderIdOf(TMessage message);

    protected abstract int KindOf(TMessage message);

    protected abstract string? BodyOf(TMessage message);

    protected abstract int EncVersionOf(TMessage message);

    protected abstract byte[]? DecryptSealed(TMessage message, string? threadId, byte[] sealedBytes);

    protected abstract void OpenImageView(string messageId);

    protected abstract void OpenReactions(string messageId);

    protected abstract void PushImagePickerScreen(string threadId);

    protected abstract void PopScreen();

    protected bool IsEncrypted(TMessage message) => EncVersionOf(message) == EnvelopeCodec.VersionEnvelope;

    protected TMessage? FindMessage(string messageId) => store.FindMessage(messageId);

    protected ReadOnlySpan<TranscriptMessage> TranscriptMessages => transcriptCache;

    public void GateMenus() => menuController.Gate();

    public void PrefillDraft(string body) => pendingPrefill = body;

    public void RequestScrollTo(string messageId) => transcript.RequestScrollTo(messageId);

    public void RequestSnapToBottom() => transcript.RequestSnapToBottom();

    public virtual void OnAppClosed()
    {
        if (store.CurrentThreadId is { } openThreadId)
        {
            OnThreadSwitchingFrom(openThreadId);
        }

        composer.CancelVoice();
        voicePlayer.Stop();
        searchController.Close();
        composer.Clear();
    }

    public void Draw(Rect area, string threadId)
    {
        var frame = ImGui.GetFrameCount();
        var resumed = frame - lastThreadDrawFrame > ResumeFrameGap;
        lastThreadDrawFrame = frame;
        if (store.CurrentThreadId != threadId)
        {
            if (store.CurrentThreadId is { } previousThreadId)
            {
                OnThreadSwitchingFrom(previousThreadId);
            }

            store.OpenThread(threadId);
            sinceThreadPoll = 0f;
            sinceTypingPoll = threadPollSeconds;
            lastTypingDraft = string.Empty;
            composer.ClearTargets();
            searchController.Close();
            composer.CancelVoice();
            voicePlayer.Stop();
            OnThreadOpened(threadId);
            transcript.RequestSnapToBottom();
        }
        else if (resumed)
        {
            store.RequestThreadRefresh(threadId);
            sinceThreadPoll = 0f;
        }

        if (pendingPrefill is { } prefill)
        {
            pendingPrefill = null;
            if (composer.Draft.Trim().Length == 0)
            {
                composer.Draft = prefill;
            }
        }

        RestoreFailedSend(threadId);

        store.NoteThreadViewed(threadId);
        TickThread(threadId);
        DrawHeader(area, threadId);
        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var composerStyle = ComposerStyle;
        var composerHeight = ChatComposer.Height(composerStyle);
        var accessoryHeight = composer.AccessoryHeight;
        var transcriptMessages = BuildTranscript(store.Messages);
        SweepTranslations(threadId, transcriptMessages);
        if (searchController.Open)
        {
            var searchHeight = 44f * scale;
            searchController.Draw(new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + searchHeight)),
                new ChatSearchModel(ui, transcriptMessages, transcript.RequestScrollTo));
            top += searchHeight;
        }

        var listRect = new Rect(new Vector2(area.Min.X, top),
            new Vector2(area.Max.X, area.Max.Y - composerHeight - accessoryHeight));
        DrawVaultBanner(ref listRect, threadId);
        DrawSyncBanner(ref listRect);
        DrawAboveTranscript(ref listRect, threadId);
        var model = new ChatTranscriptModel
        {
            ThreadId = threadId,
            Messages = transcriptMessages,
            MyUserId = MyUserId,
            Accent = Accent,
            Theme = Theme,
            MutedInk = ui.MutedInk,
            BodyInk = ui.BodyInk,
            EmptyText = EmptyText,
            LoadingText = Loc.T(L.Common.Loading),
            OtherTyping = store.OtherTyping,
            Loading = store.LoadingThread || store.ThreadOpenPending,
            IsGroup = IsGroupThread,
            Media = this,
            Interactions = this,
            Voice = this,
            Paging = this,
            PostCards = PostCards,
            StoryReplies = StoryReplies,
            Translation = this,
        };
        transcript.Draw(listRect, model);
        composer.Draw(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), new ChatComposerModel
        {
            Ui = ui,
            Style = composerStyle,
            Hint = ComposerHint,
            ConversationId = threadId,
            MaxLength = MessageMax,
            Sending = store.Sending,
            CanImage = true,
            CanVoice = true,
            CanLocation = true,
            CanHandleEscape = !searchController.Open,
            Blocked = store.SendWouldDowngrade,
            BlockedNotice = Loc.T(L.Encryption.ComposerBlocked),
            OnBlockedTap = () => OpenEncryptionInfo(threadId),
            ResolveVoiceInput = resolveVoiceInput,
            OnPickImage = pickImage,
            OnShareLocation = shareLocation,
            OnSendText = sendText,
            OnEditText = editText,
            OnSendVoice = sendVoice,
        });
        DrawMessageMenu(area);
    }

    private void DrawSyncBanner(ref Rect listRect)
    {
        var retryIn = store.SyncRetryIn;
        if (retryIn <= SyncBannerFloor)
        {
            return;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(retryIn.TotalSeconds));
        ChatHeaderControls.DrawBanner(ui, ref listRect, Loc.T(L.Common.ChatOutOfDate, seconds), ui.MutedInk,
            store.RetrySyncNow);
    }

    private void DrawVaultBanner(ref Rect listRect, string threadId)
    {
        var state = store.VaultState;
        if (state == KeyVaultState.Locked)
        {
            var banner = store.Vault.RecoveryConfigured
                ? L.Encryption.LockedBanner
                : L.Encryption.LockedNoRecoveryBanner;
            ChatHeaderControls.DrawBanner(ui, ref listRect, Loc.T(banner), ui.MutedInk,
                () => OpenEncryptionInfo(threadId));
            return;
        }

        if (state != KeyVaultState.Unlocked)
        {
            return;
        }

        if (store.Vault.UnsavedRecoveryCode is not null)
        {
            ChatHeaderControls.DrawBanner(ui, ref listRect, Loc.T(L.Encryption.SaveCodeBanner), ui.Accent,
                () => OpenEncryptionInfo(threadId));
            return;
        }

        if (store.HasOlderKeyMessages(ConversationScope(threadId)))
        {
            ChatHeaderControls.DrawBanner(ui, ref listRect, Loc.T(L.Encryption.OlderKeyBanner), ui.MutedInk,
                () => OpenEncryptionInfo(threadId));
            return;
        }

        if (store.Vault.RecoveryConfigured || !configuration.RecoveryNudgeDue())
        {
            return;
        }

        ChatHeaderControls.DrawPromptBanner(ui, ref listRect, Loc.T(L.Encryption.RecoveryNudgeBanner), ui.MutedInk,
            () => OpenEncryptionInfo(threadId),
            configuration.SnoozeRecoveryNudge);
    }

    public void DrawEncryptionScreen(Rect area)
    {
        var context = new PhoneContext(area, Theme, Navigation);
        AppHeader.Draw(context, Loc.T(L.Encryption.InfoTitle), BackAction);
        var scale = UiScale.Current;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            encryptionPane.DrawBody(ui, Theme, store.IsSignedIn, store.EncryptingCurrent);
            ImGui.Dummy(new Vector2(0f, 30f * scale));
        }
    }

    public void DrawEncryptionEmbedded()
    {
        encryptionPane.DrawEmbedded(ui, Theme);
    }

    private ReadOnlySpan<TranscriptMessage> BuildTranscript(TMessage[] source)
    {
        if (ReferenceEquals(source, transcriptSource))
        {
            return transcriptCache;
        }

        transcriptSource = source;
        transcriptCache = MapTranscript(source);
        return transcriptCache;
    }

    private void TickThread(string threadId)
    {
        PumpPendingVoice();
        var delta = ImGui.GetIO().DeltaTime;
        sinceTypingPoll += delta;
        if (sinceTypingPoll >= threadPollSeconds)
        {
            sinceTypingPoll = 0f;
            store.RefreshTyping(threadId);
        }

        sinceThreadPoll += delta;
        var messagePollSeconds = store.RealtimePushActive
            ? threadPollSeconds * PushActivePollMultiplier
            : threadPollSeconds;
        if (sinceThreadPoll >= messagePollSeconds)
        {
            sinceThreadPoll = 0f;
            store.RefreshThread();
        }

        sinceTypingSend += delta;
        var draft = composer.Draft;
        if (draft != lastTypingDraft)
        {
            lastTypingDraft = draft;
            if (draft.Trim().Length > 0 && sinceTypingSend >= typingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(threadId);
            }
        }
    }

    IDalamudTextureWrap? IChatTranscriptMedia.Texture(string messageId) => ResolveThreadImage(messageId);

    void IChatTranscriptMedia.OnImageClick(string messageId) => OpenImageView(messageId);

    void IChatTranscriptInteractions.OnMessageContext(string messageId) => OpenMessageMenu(messageId);

    void IChatTranscriptInteractions.OnQuoteClick(string messageId) => transcript.RequestScrollTo(messageId);

    void IChatTranscriptInteractions.OnReactionClick(string messageId, string token) => OpenReactions(messageId);

    VoiceNoteState IChatTranscriptVoice.StateFor(string messageId) => voicePlayer.StateFor(messageId);

    void IChatTranscriptVoice.Toggle(string messageId) => ToggleVoice(messageId);

    bool IChatTranscriptPaging.HasMoreOlder => store.HasMoreOlder;

    bool IChatTranscriptPaging.LoadingOlder => store.LoadingOlder;

    void IChatTranscriptPaging.LoadOlder() => store.LoadOlder();

    TranslationView IChatTranscriptTranslation.View(string messageId, string body) =>
        translation.View(new TranslationKey(TranslationSurface.Dm, messageId), body);

    void IChatTranscriptTranslation.Activate(string messageId, string body)
    {
        var key = new TranslationKey(TranslationSurface.Dm, messageId);
        TranslateLink.Activate(translation, confirm, key, body, translation.Peek(key));
    }

    protected string ConversationScope(string threadId)
    {
        if (!string.Equals(threadId, scopeThreadId, StringComparison.Ordinal))
        {
            scopeThreadId = threadId;
            conversationScope = LogTag + ":" + threadId;
        }

        return conversationScope;
    }

    protected void TranslateMessage(string messageId)
    {
        var messages = transcriptCache;
        for (var index = 0; index < messages.Length; index++)
        {
            if (!string.Equals(messages[index].Id, messageId, StringComparison.Ordinal))
            {
                continue;
            }

            var body = messages[index].Body;
            if (body.Length == 0 || !canRevealBody(messageId))
            {
                return;
            }

            var key = new TranslationKey(TranslationSurface.Dm, messageId);
            TranslateLink.Activate(translation, confirm, key, body, translation.Peek(key));
            return;
        }
    }

    protected void DrawTranslateToggle(Rect area, float rowCenterY, string threadId)
    {
        if (!translation.Enabled)
        {
            return;
        }

        var scope = ConversationScope(threadId);
        var translated = translation.IsConversationTranslated(scope);
        if (ChatHeaderControls.DrawTranslateToggle(ui, area, rowCenterY, translated))
        {
            SetConversationTranslated(scope, !translated);
        }
    }

    private void SetConversationTranslated(string scope, bool translated)
    {
        if (!translated)
        {
            translation.SetConversationTranslated(scope, false);
            return;
        }

        TranslateLink.WithDisclosure(translation, confirm, () => translation.SetConversationTranslated(scope, true));
    }

    private void SweepTranslations(string threadId, ReadOnlySpan<TranscriptMessage> messages)
    {
        var translated = translation.Enabled && translation.IsConversationTranslated(ConversationScope(threadId));
        if (ReferenceEquals(transcriptSource, sweptSource) && translated == sweptTranslated)
        {
            return;
        }

        sweptSource = transcriptSource;
        sweptTranslated = translated;
        if (!translated)
        {
            return;
        }

        var myId = MyUserId;
        for (var index = 0; index < messages.Length; index++)
        {
            ref readonly var message = ref messages[index];
            if (message.Kind != 0 || message.SenderId == myId || message.Body.Length == 0
                || (message.Flags & (TranscriptFlags.Deleted | TranscriptFlags.Placeholder)) != 0
                || ChatText.EffectiveKind(message.Body, 0) != 0 || translation.IsSameAsTarget(message.Body))
            {
                continue;
            }

            translation.EnsureRequested(new TranslationKey(TranslationSurface.Dm, message.Id), message.Body);
        }
    }

    protected void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || IsDeleted(message))
        {
            return;
        }

        var kind = ChatText.EffectiveKind(BodyOf(message), KindOf(message));
        menuController.Open(messageId, SenderIdOf(message) == MyUserId, kind);
    }

    private void DrawMessageMenu(Rect area)
    {
        if (!menuController.Active)
        {
            return;
        }

        menuController.Draw(area, BuildMenuModel());
    }

    protected void CopyMessage(string messageId)
    {
        ChatActions.CopyMessageText(transcriptCache, messageId, canRevealBody);
    }

    protected bool CanRevealBody(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null)
        {
            return false;
        }

        var revealState = store.DecryptionState(messageId).State;
        return EncVersionOf(message) != 1
            || revealState is DmBodyState.Decrypted or DmBodyState.Remembered;
    }

    protected void BeginEdit(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || IsDeleted(message)
            || ChatText.EffectiveKind(BodyOf(message), KindOf(message)) != 0)
        {
            return;
        }

        if (EncVersionOf(message) != 0 && store.DecryptionState(messageId).State != DmBodyState.Decrypted)
        {
            return;
        }

        composer.BeginEdit(messageId, BodyOf(message) ?? string.Empty);
    }

    protected void AskDeleteMessage(string messageId)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Message.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Message.DeleteAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            ConfirmAsync = done => store.DeleteMessage(messageId, done),
        });
    }

    protected void OpenReportMessage(string messageId)
    {
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Encryption.ReportMessageAction),
            Disclosure = Loc.T(L.Encryption.ReportDisclosure),
            Submit = (reason, done) => store.ReportMessage(messageId, reason, done),
        });
    }

    private void AskShareLocation(string threadId)
    {
        var captured = LocationShare.Capture();
        if (captured is not { } location)
        {
            confirm.Alert(null, Loc.T(L.Message.LocationUnavailable), Loc.T(L.Account.FailDismiss));
            return;
        }

        var summary = LocationShare.Summary(location);
        var prompt = Loc.T(L.Message.ShareLocationConfirm);
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Message.ShareLocation),
            Message = summary.Length > 0 ? $"{prompt}\n{summary}" : prompt,
            ConfirmLabel = Loc.T(L.Velvet.Send),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            FailedMessage = Loc.T(L.Message.LocationSendFailed),
            ConfirmAsync = done =>
            {
                if (store.Sending)
                {
                    done(false);
                    return;
                }

                store.SendMessage(threadId, LocationShare.Compose(location), sent =>
                {
                    if (sent)
                    {
                        transcript.RequestSnapToBottom();
                    }

                    done(sent);
                });
            },
        });
    }

    private void ComposerSendText(string threadId, string text, string? replyToId)
    {
        store.SendMessage(threadId, text, succeeded => NoteSendOutcome(succeeded, threadId, text), replyToId);
        transcript.RequestSnapToBottom();
        lastTypingDraft = string.Empty;
        OnDraftConsumed(threadId);
    }

    private void ComposerEditText(string threadId, string editId, string text)
    {
        store.EditMessage(threadId, editId, text, _ => { });
        lastTypingDraft = string.Empty;
        OnDraftConsumed(threadId);
    }

    private void NoteSendOutcome(bool succeeded, string threadId, string text)
    {
        if (succeeded)
        {
            return;
        }

        failedSendThreadId = threadId;
        failedSendText = text;
    }

    private void RestoreFailedSend(string threadId)
    {
        var text = failedSendText;
        if (text is null || failedSendThreadId != threadId)
        {
            return;
        }

        failedSendText = null;
        failedSendThreadId = null;
        if (composer.Draft.Length == 0)
        {
            composer.Draft = text;
        }
    }

    private void ComposerSendVoice(string threadId, byte[] wavBytes, int durationSecs)
    {
        store.SendVoiceMessage(threadId, wavBytes, durationSecs, _ => { });
        transcript.RequestSnapToBottom();
    }

    private int ResolveVoiceInput() => AudioDevices.ResolveInput(configuration.CallInputDevice);

    protected void ToggleVoice(string messageId)
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

    private void FetchVoice(string messageId)
    {
        if (voiceBytes.ContainsKey(messageId))
        {
            return;
        }

        if (voiceFailed.TryGetValue(messageId, out var failedAtUtc))
        {
            if (DateTime.UtcNow - failedAtUtc < VoiceFailureRetryFor)
            {
                return;
            }

            voiceFailed.TryRemove(messageId, out _);
        }

        var url = store.DmMediaUrl(messageId);
        if (url is null || !voiceFetching.TryAdd(messageId, 0))
        {
            return;
        }

        var message = FindMessage(messageId);
        var threadId = store.CurrentThreadId;
        _ = Task.Run(async () =>
        {
            try
            {
                var data = await http.GetBytesAsync(new Uri(url), CancellationToken.None).ConfigureAwait(false);
                var plain = data is null
                    ? null
                    : message is not null && IsEncrypted(message)
                        ? DecryptSealed(message, threadId, data)
                        : data;
                if (plain is not null)
                {
                    voiceBytes[messageId] = plain;
                }
                else
                {
                    MarkVoiceFailed(messageId);
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Voice note download failed");
                MarkVoiceFailed(messageId);
            }
            finally
            {
                voiceFetching.TryRemove(messageId, out _);
            }
        });
    }

    private void MarkVoiceFailed(string messageId)
    {
        voiceFailed[messageId] = DateTime.UtcNow;
        if (pendingVoicePlay == messageId)
        {
            pendingVoicePlay = null;
        }
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

    protected IDalamudTextureWrap? ResolveThreadImage(string messageId)
    {
        if (images.Resident(messageId) is { } resident)
        {
            return resident;
        }

        var message = FindMessage(messageId);
        if (message is null)
        {
            return null;
        }

        if (!IsEncrypted(message))
        {
            return images.Get(store.DmMediaUrl(messageId));
        }

        var threadId = store.CurrentThreadId;
        var url = store.DmMediaUrl(messageId);
        if (url is null)
        {
            return null;
        }

        return images.GetSealed(messageId, url, sealedBytes => DecryptSealed(message, threadId, sealedBytes));
    }

    public void DrawImageViewer(Rect area, string messageId)
    {
        if (imageViewId != messageId)
        {
            imageViewId = messageId;
            imageSaveOutcome = 0;
            imageZoom.Reset();
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(area.Min, area.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.94f)));
        var headerHeight = AppHeader.Height * scale;
        var footerHeight = 60f * scale;
        var controlsBottom = area.Max.Y - footerHeight;
        var fitMin = new Vector2(area.Min.X + 8f * scale, area.Min.Y + headerHeight);
        var fitMax = new Vector2(area.Max.X - 8f * scale,
            controlsBottom - PhotoZoomView.ControlBandUnits * scale);
        var texture = ResolveThreadImage(messageId);
        if (texture is null)
        {
            Typography.DrawCentered(new Vector2(area.Center.X, (fitMin.Y + fitMax.Y) * 0.5f), Loc.T(L.Common.Loading),
                ui.MutedInk);
        }
        else
        {
            var controls = new Rect(new Vector2(fitMin.X, fitMax.Y), new Vector2(fitMax.X, controlsBottom));
            if (imageZoom.Draw(new Rect(fitMin, fitMax), texture, Theme, 10f * scale, true, controls))
            {
                Plugin.PhotoWindow.Open(() => ResolveThreadImage(messageId), Owner);
            }
        }

        var context = new PhoneContext(area, Theme, Navigation);
        AppHeader.Draw(context, string.Empty, BackAction);
        var saved = imageSaveOutcome == 1;
        var label = saved ? SavedLabel : SaveLabel;
        var buttonWidth = MathF.Min(240f * scale, area.Width - 32f * scale);
        var buttonHeight = 42f * scale;
        var buttonTop = area.Max.Y - footerHeight + (footerHeight - buttonHeight) * 0.5f;
        var buttonRect = new Rect(new Vector2(area.Center.X - buttonWidth * 0.5f, buttonTop),
            new Vector2(area.Center.X + buttonWidth * 0.5f, buttonTop + buttonHeight));
        if (ui.PillButton(buttonRect, label, !saved) && !saved && !imageSaveBusy && texture is not null)
        {
            SaveImage(messageId);
        }
    }

    private void SaveImage(string messageId)
    {
        var url = store.DmMediaUrl(messageId);
        var message = FindMessage(messageId);
        if (string.IsNullOrEmpty(url) || imageSaveBusy || message is null)
        {
            return;
        }

        var encrypted = IsEncrypted(message);
        var threadId = store.CurrentThreadId;
        if (encrypted && threadId is null)
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
                    ? DecryptSealed(message, threadId, raw)
                    : raw;
                if (bytes is not null)
                {
                    var (pixels, width, height) = ImageProcessor.DecodeRgba32(bytes);
                    library.Save(pixels, width, height);
                    succeeded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"[{LogTag}] save image failed");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
    }

    protected void OpenImagePicker(string threadId)
    {
        pickerThreadId = null;
        pendingPickedPath = null;
        PushImagePickerScreen(threadId);
    }

    public void DrawImagePicker(Rect area, string threadId)
    {
        var context = new PhoneContext(area, Theme, Navigation);
        AppHeader.Draw(context, PickerTitle, BackAction);
        if (pickerThreadId != threadId)
        {
            pickerThreadId = threadId;
            pickerPaths = library.List();
            pendingPickedPath = null;
        }

        var picked = Interlocked.Exchange(ref pendingPickedPath, null);
        if (!string.IsNullOrEmpty(picked))
        {
            SendChatImage(threadId, picked);
            return;
        }

        var scale = UiScale.Current;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, ImportLabel, true))
        {
            FilePicker.PickImage(PickerTitle, path => Interlocked.Exchange(ref pendingPickedPath, path));
        }

        var gridRect = new Rect(new Vector2(area.Min.X, importRect.Max.Y + 12f * scale), area.Max);
        using (AppSurface.Begin(gridRect))
        {
            if (pickerPaths.Length == 0)
            {
                Typography.DrawCentered(new Vector2(gridRect.Center.X, gridRect.Min.Y + 60f * scale),
                    NoPhotosLabel, ui.MutedInk);
                return;
            }

            const int columns = 3;
            var gap = 6f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (columns - 1)) / columns;
            var origin = ImGui.GetCursorScreenPos();
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var margin = cell + 60f * scale;
            for (var index = 0; index < pickerPaths.Length; index++)
            {
                var column = index % columns;
                var rowIndex = index / columns;
                var rowTop = rowIndex * (cell + gap);
                if (rowTop + cell < scrollY - margin || rowTop > scrollY + viewHeight + margin)
                {
                    continue;
                }

                var min = new Vector2(origin.X + column * (cell + gap), origin.Y + rowTop);
                var max = new Vector2(min.X + cell, min.Y + cell);
                var hovered = UiInteract.Hover(min, max);
                DrawPickerThumbnail(pickerPaths[index], min, max, scale, hovered);
                if (UiInteract.Click(min, max, hovered))
                {
                    SendChatImage(threadId, pickerPaths[index]);
                }
            }

            var rows = (pickerPaths.Length + columns - 1) / columns;
            var totalHeight = rows * (cell + gap);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(avail, totalHeight));
        }
    }

    private void SendChatImage(string threadId, string path)
    {
        store.SendImageMessage(threadId, path, string.Empty, _ => { });
        transcript.RequestSnapToBottom();
        pickerThreadId = null;
        PopScreen();
    }

    private void DrawPickerThumbnail(string path, Vector2 min, Vector2 max, float scale, bool hovered)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 10f * scale;
        var texture = wallpaperImages.Get(path);
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
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    public void DrawReactions(Rect area, string messageId)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, Theme, Navigation);
        AppHeader.Draw(context, Loc.T(L.Message.ReactionsTitle), BackAction);
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
        var myId = MyUserId;
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
        AvatarView.DrawRemote(drawList, avatarCenter, radius, Theme, label, string.Empty, reactor.AvatarUrl, images,
            lodestone, 0.85f, 32);
        var textLeft = avatarCenter.X + radius + 12f * scale;
        var labelMaxWidth = MathF.Max(1f, origin.X + width - pad - 40f * scale - textLeft);
        var rowHovering = UiInteract.Hover(origin, rowMax);
        if (mine)
        {
            Marquee.DrawLeft(new MarqueeId("chatthread.reactor.", reactor.UserId), label, textLeft, origin.Y + 10f * scale,
                labelMaxWidth, new TextStyle(1f, FontWeight.SemiBold), Theme.TextStrong, rowHovering);
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale),
                Typography.FitText(Loc.T(L.Message.TapToRemove), labelMaxWidth, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            Marquee.DrawLeft(new MarqueeId("chatthread.reactor.", reactor.UserId), label, textLeft,
                origin.Y + rowHeight * 0.5f - 9f * scale, labelMaxWidth, new TextStyle(1f, FontWeight.SemiBold),
                Theme.TextStrong, rowHovering);
        }

        ReactionArt.Draw(drawList, reactor.Token,
            new Vector2(origin.X + width - pad - ReactorEmojiSize * 0.5f * scale, origin.Y + rowHeight * 0.5f),
            ReactorEmojiSize * scale, 1f, 1f);
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

    public void Dispose()
    {
        composer.Dispose();
        voicePlayer.Dispose();
        encryptionPane.Dispose();
    }
}
