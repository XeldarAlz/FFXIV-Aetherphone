using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Media;
using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Report;
using Aetherphone.Core.Telephony.Audio;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal abstract class ChatThreadView<TMessage, TThread> : IDisposable
    where TMessage : class, IIdentified
    where TThread : class, IIdentified
{
    protected const int MessageMax = 1000;

    protected readonly ChatThreadStoreBase<TMessage, TThread> store;
    protected readonly AppSkin ui;
    protected readonly RemoteImageCache images;
    protected readonly LodestoneService lodestone;
    protected readonly HttpService http;
    protected readonly PhotoLibrary library;
    protected readonly Configuration configuration;
    protected readonly ChatTranscript transcript = new();
    protected readonly ChatMenuController menuController = new();
    protected readonly ChatComposer composer = new();
    protected readonly ChatSearchController searchController = new();
    protected readonly VoiceNotePlayer voicePlayer = new();
    private readonly PhotoZoomView imageZoom = new();
    private readonly ConcurrentDictionary<string, byte[]> voiceBytes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> voiceFetching = new(StringComparer.Ordinal);
    private readonly float threadPollSeconds;
    private readonly float typingSendSeconds;
    private readonly Func<string, string?> threadMediaUrl;
    private readonly Func<string, IDalamudTextureWrap?> resolveImage;
    private readonly Action<string> onImageClick;
    private readonly Action<string> onMessageContext;
    private readonly Action<string> onQuoteClick;
    private readonly Action<string, string> onReactionClick;
    private readonly Func<string, VoiceNoteState> voiceStateFor;
    private readonly Action<string> onVoiceToggle;
    private readonly Action onLoadOlder;
    private readonly Action<string> pickImage;
    private readonly Action<string, string, string?> sendText;
    private readonly Action<string, string, string> editText;
    private readonly Action<string, byte[], int> sendVoice;
    private readonly Func<int> resolveVoiceInput;
    private readonly Func<string, bool> canRevealBody;

    private volatile string? pendingVoicePlay;
    private TMessage[] transcriptSource = Array.Empty<TMessage>();
    private TranscriptMessage[] transcriptCache = Array.Empty<TranscriptMessage>();
    private float sinceThreadPoll;
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
        float threadPollSeconds, float typingSendSeconds)
    {
        this.store = store;
        this.ui = ui;
        this.images = images;
        this.lodestone = lodestone;
        this.http = http;
        this.library = library;
        this.configuration = configuration;
        this.threadPollSeconds = threadPollSeconds;
        this.typingSendSeconds = typingSendSeconds;
        sinceTypingSend = typingSendSeconds;
        threadMediaUrl = store.DmMediaUrl;
        resolveImage = ResolveThreadImage;
        onImageClick = OpenImageView;
        onMessageContext = OpenMessageMenu;
        onQuoteClick = transcript.RequestScrollTo;
        onReactionClick = OnReactionClicked;
        voiceStateFor = voicePlayer.StateFor;
        onVoiceToggle = ToggleVoice;
        onLoadOlder = store.LoadOlder;
        pickImage = OpenImagePicker;
        sendText = ComposerSendText;
        editText = ComposerEditText;
        sendVoice = ComposerSendVoice;
        resolveVoiceInput = ResolveVoiceInput;
        canRevealBody = CanRevealBody;
    }

    protected abstract PhoneTheme Theme { get; }

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

    protected abstract void DrawHeader(Rect area, string threadId);

    protected virtual void DrawAboveTranscript(ref Rect listRect, string threadId)
    {
    }

    protected virtual void OnThreadSwitchingFrom(string previousThreadId)
    {
    }

    protected virtual void OnThreadOpened(string threadId)
    {
    }

    protected virtual void OnDraftConsumed(string threadId)
    {
    }

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

    protected abstract void OpenImagePicker(string threadId);

    protected abstract void PopScreen();

    protected bool IsEncrypted(TMessage message) => EncVersionOf(message) == EnvelopeCodec.VersionEnvelope;

    protected TMessage? FindMessage(string messageId) => store.FindMessage(messageId);

    protected ReadOnlySpan<TranscriptMessage> TranscriptMessages => transcriptCache;

    public void GateMenus() => menuController.Gate();

    public void RequestScrollTo(string messageId) => transcript.RequestScrollTo(messageId);

    public void RequestSnapToBottom() => transcript.RequestSnapToBottom();

    public virtual void OnAppClosed()
    {
        composer.CancelVoice();
        voicePlayer.Stop();
        searchController.Close();
        composer.Clear();
    }

    public void Draw(Rect area, string threadId)
    {
        if (store.CurrentThreadId != threadId)
        {
            if (store.CurrentThreadId is { } previousThreadId)
            {
                OnThreadSwitchingFrom(previousThreadId);
            }

            store.OpenThread(threadId);
            sinceThreadPoll = threadPollSeconds;
            lastTypingDraft = string.Empty;
            composer.ClearTargets();
            searchController.Close();
            composer.CancelVoice();
            voicePlayer.Stop();
            OnThreadOpened(threadId);
        }

        store.NoteThreadViewed(threadId);
        TickThread(threadId);
        DrawHeader(area, threadId);
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
        DrawAboveTranscript(ref listRect, threadId);
        var model = new ChatTranscriptModel(threadId, transcriptMessages, MyUserId, Accent, Theme,
            ui.MutedInk, ui.BodyInk, store.OtherTyping, store.LoadingThread,
            IsGroupThread, images, threadMediaUrl, onImageClick, EmptyText, Loc.T(L.Common.Loading),
            onMessageContext, onQuoteClick, onReactionClick, voiceStateFor, onVoiceToggle,
            store.HasMoreOlder, store.LoadingOlder, onLoadOlder, resolveImage);
        transcript.Draw(listRect, model);
        composer.Draw(new Rect(new Vector2(area.Min.X, area.Max.Y - composerHeight), area.Max), new ChatComposerModel
        {
            Ui = ui,
            ConversationId = threadId,
            MaxLength = MessageMax,
            Sending = store.Sending,
            CanImage = true,
            CanVoice = true,
            CanHandleEscape = !searchController.Open,
            ResolveVoiceInput = resolveVoiceInput,
            OnPickImage = pickImage,
            OnSendText = sendText,
            OnEditText = editText,
            OnSendVoice = sendVoice,
        });
        DrawMessageMenu(area);
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
        sinceThreadPoll += delta;
        if (sinceThreadPoll >= threadPollSeconds)
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
            if (draft.Trim().Length > 0 && sinceTypingSend >= typingSendSeconds)
            {
                sinceTypingSend = 0f;
                store.SendTyping(threadId);
            }
        }
    }

    private void OnReactionClicked(string messageId, string reactionToken) => OpenReactions(messageId);

    protected void OpenMessageMenu(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || IsDeleted(message))
        {
            return;
        }

        menuController.Open(messageId, SenderIdOf(message) == MyUserId, KindOf(message));
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

        return EncVersionOf(message) != 1 || store.DecryptionState(messageId).State == DmBodyState.Decrypted;
    }

    protected void BeginEdit(string messageId)
    {
        var message = FindMessage(messageId);
        if (message is null || KindOf(message) != 0 || IsDeleted(message))
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
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Message.DeleteConfirm),
            ConfirmLabel = Loc.T(L.Message.DeleteAction),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done => store.DeleteMessage(messageId, done),
        });
    }

    protected void OpenReportMessage(string messageId)
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
        OnDraftConsumed(threadId);
    }

    private void ComposerEditText(string threadId, string editId, string text)
    {
        store.EditMessage(threadId, editId, text, _ => { });
        lastTypingDraft = string.Empty;
        OnDraftConsumed(threadId);
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
                if (data is not null)
                {
                    var plain = message is not null && IsEncrypted(message)
                        ? DecryptSealed(message, threadId, data)
                        : data;
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

    protected IDalamudTextureWrap? ResolveThreadImage(string messageId)
    {
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

        return images.GetKeyed(messageId, async token =>
        {
            var data = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
            return data is null ? null : DecryptSealed(message, threadId, data);
        });
    }

    public void DrawImageViewer(Rect area, string messageId)
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
                ui.MutedInk);
        }
        else
        {
            imageZoom.Draw(new Rect(fitMin, fitMax), texture, Theme, 10f * scale);
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
                    using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(bytes);
                    var pixels = new byte[image.Width * image.Height * 4];
                    image.CopyPixelDataTo(pixels);
                    library.Save(pixels, image.Width, image.Height);
                    succeeded = true;
                }
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[{LogTag}] save image failed: {exception.Message}");
            }
            finally
            {
                imageSaveOutcome = succeeded ? 1 : 2;
                imageSaveBusy = false;
            }
        });
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

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var importHeight = 46f * scale;
        var importRect = new Rect(new Vector2(area.Min.X + 16f * scale, top + 8f * scale),
            new Vector2(area.Max.X - 16f * scale, top + 8f * scale + importHeight));
        if (ui.PillButton(importRect, ImportLabel, true))
        {
            NativeFileDialog.PickImage(PickerTitle, path => Interlocked.Exchange(ref pendingPickedPath, path));
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
            var cell = (ScrollLayout.StableContentWidth() - gap * (columns - 1)) / columns;
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap)))
            {
                for (var index = 0; index < pickerPaths.Length; index++)
                {
                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("chatpick", new Vector2(cell, cell));
                        DrawPickerThumbnail(pickerPaths[index], ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), scale);
                        if (clicked)
                        {
                            SendChatImage(threadId, pickerPaths[index]);
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
        pickerThreadId = null;
        PopScreen();
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

    public void DrawReactions(Rect area, string messageId)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
        if (mine)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 10f * scale), label, Theme.TextStrong, 1f,
                FontWeight.SemiBold);
            Typography.Draw(new Vector2(textLeft, origin.Y + 31f * scale), Loc.T(L.Message.TapToRemove), ui.MutedInk,
                TextStyles.Footnote);
        }
        else
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + rowHeight * 0.5f - 9f * scale), label, Theme.TextStrong,
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

    public void Dispose()
    {
        composer.Dispose();
        voicePlayer.Dispose();
    }
}
