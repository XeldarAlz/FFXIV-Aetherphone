using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Core.YellowPages;
using Aetherphone.Core.Translation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class TranscriptFlags
{
    public const byte Encrypted = 1;
    public const byte Placeholder = 2;
    public const byte Unverified = 4;
    public const byte Deleted = 8;
    public const byte Forwarded = 16;
    public const byte Edited = 32;
}

internal readonly struct TranscriptReaction
{
    public readonly string Token;
    public readonly int Count;
    public readonly bool Mine;

    public TranscriptReaction(string token, int count, bool mine)
    {
        Token = token;
        Count = count;
        Mine = mine;
    }
}

internal readonly struct TranscriptMessage
{
    public readonly string Id;
    public readonly string SenderId;
    public readonly string Body;
    public readonly int Kind;
    public readonly long CreatedAtUnix;
    public readonly int MediaWidth;
    public readonly int MediaHeight;
    public readonly long? ReadAtUnix;
    public readonly string SenderName;
    public readonly Vector4 SenderTint;
    public readonly byte Flags;
    public readonly string? ReplyToId;
    public readonly string ReplySenderName;
    public readonly string ReplyBody;
    public readonly int ReplyKind;
    public readonly int DurationSecs;
    public readonly TranscriptReaction[] Reactions;
    public readonly int SenderBadges;
    public readonly string[]? SenderBadgeIds;
    public readonly string ChannelTag;
    public readonly Vector4 ChannelTint;
    public readonly Vector4 BodyInk;
    public readonly TextRun[]? Runs;

    public TranscriptMessage(string id, string senderId, string body, int kind, long createdAtUnix, int mediaWidth,
        int mediaHeight, long? readAtUnix, string senderName, Vector4 senderTint, byte flags = 0,
        string? replyToId = null, string replySenderName = "", string replyBody = "", int replyKind = 0,
        int durationSecs = 0, TranscriptReaction[]? reactions = null, int senderBadges = 0,
        string[]? senderBadgeIds = null, string channelTag = "", Vector4 channelTint = default,
        TextRun[]? runs = null, Vector4 bodyInk = default)
    {
        ChannelTag = channelTag;
        ChannelTint = channelTint;
        BodyInk = bodyInk;
        Runs = runs;
        SenderBadges = senderBadges;
        SenderBadgeIds = senderBadgeIds;
        Id = id;
        SenderId = senderId;
        Body = body;
        Kind = kind;
        CreatedAtUnix = createdAtUnix;
        MediaWidth = mediaWidth;
        MediaHeight = mediaHeight;
        ReadAtUnix = readAtUnix;
        SenderName = senderName;
        SenderTint = senderTint;
        Flags = flags;
        ReplyToId = replyToId;
        ReplySenderName = replySenderName;
        ReplyBody = replyBody;
        ReplyKind = replyKind;
        DurationSecs = durationSecs;
        Reactions = reactions ?? Array.Empty<TranscriptReaction>();
    }
}

internal readonly record struct ChatPostCard(
    string PostId,
    string AuthorName,
    string Snippet,
    string? ThumbnailUrl,
    bool Available,
    bool Sensitive = false);

internal interface IChatTranscriptPostCards
{
    bool TryResolve(string messageId, string body, out ChatPostCard card);

    void Open(string postId);

    IDalamudTextureWrap? Thumbnail(string url);
}

internal readonly record struct ChatStoryReplyContext(string ContextText, string? ThumbnailUrl, bool Unavailable);

internal interface IChatTranscriptStoryReplies
{
    bool TryResolve(string messageId, out ChatStoryReplyContext context);

    IDalamudTextureWrap? Thumbnail(string url);
}

internal interface IChatTranscriptMedia
{
    IDalamudTextureWrap? Texture(string messageId);

    void OnImageClick(string messageId);
}

internal interface IChatTranscriptInteractions
{
    void OnMessageContext(string messageId);

    void OnLinkClick(string messageId, int target)
    {
    }

    void OnQuoteClick(string messageId);

    void OnReactionClick(string messageId, string token);
}

internal interface IChatTranscriptVoice
{
    VoiceNoteState StateFor(string messageId);

    void Toggle(string messageId);
}

internal interface IChatTranscriptPaging
{
    bool HasMoreOlder { get; }

    bool LoadingOlder { get; }

    void LoadOlder();
}

internal interface IChatTranscriptTranslation
{
    TranslationView View(string messageId, string body);

    void Activate(string messageId, string body);
}

internal readonly ref struct ChatTranscriptModel
{
    public required string ThreadId { get; init; }
    public required ReadOnlySpan<TranscriptMessage> Messages { get; init; }
    public required string MyUserId { get; init; }
    public required Vector4 Accent { get; init; }
    public required PhoneTheme Theme { get; init; }
    public required Vector4 MutedInk { get; init; }
    public required Vector4 BodyInk { get; init; }
    public required string EmptyText { get; init; }
    public required string LoadingText { get; init; }
    public bool OtherTyping { get; init; }
    public bool Loading { get; init; }
    public bool IsGroup { get; init; }
    public bool LabelsOwnMessages { get; init; }
    public IChatTranscriptMedia? Media { get; init; }
    public IChatTranscriptInteractions? Interactions { get; init; }
    public IChatTranscriptVoice? Voice { get; init; }
    public IChatTranscriptPaging? Paging { get; init; }
    public IChatTranscriptPostCards? PostCards { get; init; }
    public IChatTranscriptStoryReplies? StoryReplies { get; init; }
    public IChatTranscriptTranslation? Translation { get; init; }
}

internal sealed class ChatTranscript
{
    private const long GroupWindowSeconds = 240;
    private const int KindText = 0;
    private const int KindImage = 1;
    private const int KindSystem = 2;
    private const int KindVoice = 3;
    private const int KindPost = 4;
    private const int KindStoryReply = 5;
    private const int KindLocation = ChatText.LocationKind;
    private const float StampTextScale = 0.70f;
    private const float StampTickScale = 0.58f;
    private const float BubbleGap = 3f;
    private const float QuoteSenderScale = 0.75f;
    private const float QuotePreviewScale = 0.80f;
    private const float TravelPillHeight = 26f;
    private const float TravelIconSpace = 17f;
    private const float CaptionTextScale = 0.9f;
    private const float ReactionChipHeight = 26f;
    private const float ReactionChipGap = 4f;
    private const float ReactionChipEmoji = 17f;
    private const float ReactionChipPadX = 5f;
    private const float ReactionChipCountGap = 3f;
    private const float ReactionChipOverlap = 9f;
    private const float ReactionChipEdgeInset = 6f;
    private const float ReactionChipBelowGap = 4f;
    private const float ReactionChipRing = 1.5f;
    private const float ReactionChipFallbackScale = 0.72f;
    private static readonly Vector4 ReactionChipFill = new(0.11f, 0.11f, 0.14f, 0.97f);
    private static readonly Vector4 ReactionChipStroke = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 ReactionCountInk = new(0.94f, 0.94f, 0.97f, 1f);
    private static readonly TextStyle ReactionCountStyle = TextStyles.FootnoteEmphasized;
    private static readonly Vector4 SeenTickColor = new(0.45f, 0.83f, 1f, 1f);

    private const float FlashSeconds = 1.6f;
    private const float LoadOlderThreshold = 48f;
    private const int OlderSettleFrames = 2;
    private const float OlderRestoreTimeout = 20f;

    private readonly ChatEntranceTracker entrances = new();
    private string? followThreadId;
    private bool followBottom;
    private bool snapToBottom;
    private float olderAnchorFromBottom = -1f;
    private int olderBaselineCount;
    private int olderSettleFrames;
    private float olderElapsed;
    private float olderSpinnerPhase;
    private float typingReveal;
    private float typingPhase;
    private string? scrollTargetId;
    private int scrollRequestFrame;
    private string? flashMessageId;
    private float flashElapsed;

    public void RequestSnapToBottom() => snapToBottom = true;

    public void RequestScrollTo(string messageId)
    {
        scrollTargetId = messageId;
        scrollRequestFrame = ImGui.GetFrameCount();
        flashMessageId = messageId;
        flashElapsed = 0f;
    }

    public void Draw(Rect listRect, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        var tailId = model.Messages.Length > 0 ? model.Messages[model.Messages.Length - 1].Id : null;
        entrances.Sync(model.ThreadId, model.Messages.Length, tailId, delta, model.Loading);
        var loadingOlder = model.Paging is { LoadingOlder: true };
        if (loadingOlder)
        {
            olderSpinnerPhase += delta;
        }

        if (flashMessageId is not null)
        {
            flashElapsed += delta;
            if (flashElapsed >= FlashSeconds)
            {
                flashMessageId = null;
            }
        }

        var typingTarget = model.OtherTyping ? 1f : 0f;
        typingReveal += (typingTarget - typingReveal) * MathF.Min(1f, delta * 12f);

        using (var surface = AppSurface.Begin(listRect))
        {
            if (model.Messages.Length == 0 && typingReveal < 0.01f)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    model.Loading ? model.LoadingText : model.EmptyText, model.MutedInk);
                return;
            }

            SyncFollow(model.ThreadId, surface.FreshVisit);
            MaybeLoadOlder(model);
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            var messages = model.Messages;
            for (var index = 0; index < messages.Length; index++)
            {
                var message = messages[index];
                var hasPrevious = index > 0;
                var previous = hasPrevious ? messages[index - 1] : default;
                var newDay = !hasPrevious || !TimeText.SameLocalDay(previous.CreatedAtUnix, message.CreatedAtUnix);
                if (newDay)
                {
                    DrawDaySeparator(message.CreatedAtUnix, model);
                }

                var grouped = hasPrevious && !newDay && previous.Kind != KindSystem &&
                              previous.SenderId == message.SenderId &&
                              message.CreatedAtUnix - previous.CreatedAtUnix <= GroupWindowSeconds;
                if (hasPrevious && !newDay && !grouped)
                {
                    var cursor = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + 5f * scale));
                }

                if (message.Kind == KindSystem)
                {
                    DrawSystemMessage(message, model);
                    continue;
                }

                var ownMessage = message.SenderId == model.MyUserId;
                if (!grouped && message.SenderName.Length > 0 &&
                    (ownMessage ? model.LabelsOwnMessages : model.IsGroup))
                {
                    DrawSenderLabel(message, model.Theme, ownMessage);
                }

                if (message.Kind == KindImage)
                {
                    DrawImageBubble(message, index, model);
                }
                else if (message.Kind == KindVoice)
                {
                    DrawVoiceBubble(message, index, model);
                }
                else if (message.Kind == KindPost)
                {
                    DrawPostBubble(message, index, model);
                }
                else if (message.Kind == KindStoryReply)
                {
                    DrawStoryReplyBubble(message, index, model);
                }
                else if ((message.Flags & TranscriptFlags.Deleted) == 0
                         && LocationShare.TryParse(message.Body, out var location))
                {
                    DrawLocationBubble(message, index, location, model);
                }
                else if ((message.Flags & TranscriptFlags.Deleted) == 0
                         && MusterShare.TryParse(message.Body, out var musterId))
                {
                    DrawMusterBubble(message, index, musterId, model);
                }
                else if ((message.Flags & TranscriptFlags.Deleted) == 0
                         && AdShare.TryParse(message.Body, out var adId))
                {
                    DrawAdBubble(message, index, adId, model);
                }
                else
                {
                    DrawTextBubble(message, index, model);
                }
            }

            if (scrollTargetId is not null && ImGui.GetFrameCount() > scrollRequestFrame)
            {
                scrollTargetId = null;
            }

            if (typingReveal > 0.01f)
            {
                DrawTypingBubble(typingReveal, model);
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            if (followBottom)
            {
                ImGui.SetScrollHereY(1f);
            }

            ApplyOlderRestore(model, delta);
            if (loadingOlder)
            {
                DrawOlderLoading(listRect, model);
            }
        }
    }

    private void MaybeLoadOlder(in ChatTranscriptModel model)
    {
        if (olderAnchorFromBottom >= 0f || model.Paging is not { } paging
            || !paging.HasMoreOlder || paging.LoadingOlder || followBottom)
        {
            return;
        }

        var scale = UiScale.Current;
        if (ImGui.GetScrollMaxY() <= 0f || ImGui.GetScrollY() > LoadOlderThreshold * scale)
        {
            return;
        }

        olderAnchorFromBottom = ImGui.GetScrollMaxY() - ImGui.GetScrollY();
        olderBaselineCount = model.Messages.Length;
        olderSettleFrames = 0;
        olderElapsed = 0f;
        paging.LoadOlder();
    }

    private void ApplyOlderRestore(in ChatTranscriptModel model, float delta)
    {
        if (olderAnchorFromBottom < 0f)
        {
            return;
        }

        ImGui.SetScrollY(MathF.Max(0f, ImGui.GetScrollMaxY() - olderAnchorFromBottom));
        olderElapsed += delta;
        if (model.Messages.Length > olderBaselineCount)
        {
            if (++olderSettleFrames >= OlderSettleFrames)
            {
                olderAnchorFromBottom = -1f;
            }
        }
        else if (olderElapsed >= OlderRestoreTimeout)
        {
            olderAnchorFromBottom = -1f;
        }
    }

    private void DrawOlderLoading(Rect listRect, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var dotRadius = 2.6f * scale;
        var dotGap = 6f * scale;
        var baseX = listRect.Center.X - (dotRadius * 2f + dotGap);
        var baseY = listRect.Min.Y + 12f * scale;
        for (var dot = 0; dot < 3; dot++)
        {
            var wave = MathF.Max(0f, MathF.Sin(olderSpinnerPhase * 6f - dot * 0.9f));
            var alpha = 0.30f + 0.55f * wave;
            var center = new Vector2(baseX + dot * (dotRadius * 2f + dotGap), baseY);
            drawList.AddCircleFilled(center, dotRadius,
                ImGui.GetColorU32(Palette.WithAlpha(model.MutedInk, alpha)), 16);
        }
    }

    private void SyncFollow(string threadId, bool freshVisit)
    {
        var scale = UiScale.Current;
        if (followThreadId == threadId)
        {
            followBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 4f * scale;
        }
        else
        {
            followThreadId = threadId;
            followBottom = true;
            olderAnchorFromBottom = -1f;
        }

        if (freshVisit && scrollTargetId is null)
        {
            followBottom = true;
            olderAnchorFromBottom = -1f;
        }

        if (snapToBottom)
        {
            followBottom = true;
            snapToBottom = false;
        }
    }

    private static void DrawSenderLabel(TranscriptMessage message, PhoneTheme theme, bool mine)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var available = ScrollLayout.StableContentWidth();
        var maxWidth = available - 4f * scale;
        var name = FirstName(message.SenderName);
        var nameStyle = new TextStyle(0.78f, FontWeight.SemiBold);
        var nameWidth = MathF.Min(maxWidth, Typography.Measure(name, nameStyle).X);
        var tagWidth = message.ChannelTag.Length > 0
            ? 16f * scale + Typography.Measure(message.ChannelTag, TextStyles.Caption2).X
            : 0f;
        var blockWidth = mine ? MathF.Min(maxWidth, nameWidth + tagWidth) : maxWidth;
        var textLeft = mine ? origin.X + available - blockWidth : origin.X + 4f * scale;
        var rect = new Vector2(textLeft, origin.Y);
        var hovering = UiInteract.Hover(rect, new Vector2(rect.X + blockWidth, rect.Y + 16f * scale));
        UserName.Draw("chattranscript.sender." + message.Id, name, message.SenderBadges, message.SenderBadgeIds,
            textLeft, origin.Y, mine ? nameWidth : maxWidth, nameStyle, message.SenderTint, hovering, theme);
        if (message.ChannelTag.Length > 0)
        {
            DrawChannelTag(message, textLeft + nameWidth + 6f * scale, origin.Y, origin.X + available, scale);
        }

        if (!string.Equals(name, message.SenderName, StringComparison.Ordinal))
        {
            HoverTooltip.Show("chattranscript.senderfull." + message.Id,
                new Rect(rect, new Vector2(rect.X + nameWidth, rect.Y + 16f * scale)), message.SenderName,
                HoverLabelSide.Above);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + 16f * scale));
    }

    private static void DrawChannelTag(TranscriptMessage message, float left, float top, float limit, float scale)
    {
        var label = Typography.FitText(message.ChannelTag, MathF.Max(0f, limit - left) - 10f * scale,
            TextStyles.Caption2);
        if (label.Length == 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var size = Typography.Measure(label, TextStyles.Caption2);
        var min = new Vector2(left, top + 1f * scale);
        var max = min + size + new Vector2(10f * scale, 3f * scale);
        Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(message.ChannelTint, 0.18f)));
        var labelMin = new Vector2(min.X + (max.X - min.X - size.X) * 0.5f, min.Y + (max.Y - min.Y - size.Y) * 0.5f);
        Typography.Draw(drawList, labelMin, label, message.ChannelTint, TextStyles.Caption2);
    }

    private void DrawSystemMessage(TranscriptMessage message, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var available = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var textSize = Typography.Measure(message.Body, 0.74f, FontWeight.Medium);
        var center = new Vector2(origin.X + available * 0.5f, origin.Y + 6f * scale + textSize.Y * 0.5f);
        Typography.DrawCentered(center, message.Body, model.MutedInk, 0.74f, FontWeight.Medium);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + textSize.Y + 14f * scale));
    }

    private void DrawDaySeparator(long unixSeconds, in ChatTranscriptModel model)
    {
        var label = TimeText.DayLabel(unixSeconds);
        if (label.Length == 0)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var textSize = Typography.Measure(label, 0.72f, FontWeight.Medium);
        var chipWidth = textSize.X + 20f * scale;
        var chipHeight = textSize.Y + 8f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var chipMin = new Vector2(origin.X + (available - chipWidth) * 0.5f, origin.Y + 4f * scale);
        var chipMax = chipMin + new Vector2(chipWidth, chipHeight);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.08f)));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, model.MutedInk, 0.72f, FontWeight.Medium);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, chipMax.Y + 10f * scale));
    }

    private void DrawTextBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var deleted = (message.Flags & TranscriptFlags.Deleted) != 0;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 7f * scale;
        var wrap = available * 0.74f - paddingX * 2f;
        var translation = model.Translation is { } lookup && !deleted && !mine
            ? lookup.View(message.Id, message.Body)
            : default;
        var body = translation.Entry is null ? message.Body : translation.Text;
        var runs = deleted ? null : message.Runs;
        var runLayout = runs is null ? null : RunText.Layout(message.Id, runs, wrap);
        var linkLayout = deleted || runs is not null ? null : LinkText.LayoutFor(body, wrap);
        var textSize = runLayout is not null
            ? runLayout.Size
            : linkLayout is null ? ImGui.CalcTextSize(body, false, wrap) : linkLayout.Size;
        var footer = TranslationFooter.Measure(translation.Entry, wrap, scale);
        var deletedIconWidth = deleted ? 17f * scale : 0f;
        var stamp = MeasureStamp(message, mine, scale);
        var stampGap = 7f * scale;
        var inline = footer.Height <= 0f && textSize.Y <= ImGui.GetTextLineHeight() * 1.5f &&
                     deletedIconWidth + textSize.X + stampGap + stamp.Width <= wrap;
        var contentWidth = inline
            ? deletedIconWidth + textSize.X + stampGap + stamp.Width
            : MathF.Max(MathF.Max(deletedIconWidth + textSize.X, stamp.Width), footer.Width);
        var quote = MeasureQuote(message, wrap, scale);
        if (quote.Height > 0f)
        {
            contentWidth = MathF.Max(contentWidth, quote.MinWidth);
        }

        var forwardLabel = MeasureForwardLabel(message, scale);
        if (forwardLabel.Y > 0f)
        {
            contentWidth = MathF.Max(contentWidth, forwardLabel.X);
        }

        var quoteBlock = quote.Height > 0f ? quote.Height + 6f * scale : 0f;
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 3f * scale : 0f;
        var contentHeight = (inline ? textSize.Y : textSize.Y + footer.Height + stamp.Height + 2f * scale)
            + quoteBlock + forwardBlock;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = contentHeight + paddingY * 2f;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var placeholder = (message.Flags & TranscriptFlags.Placeholder) != 0;
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = message.BodyInk.W > 0f
            ? message.BodyInk
            : mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        if (placeholder || deleted)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), fx, mine, model, scale);
            contentTop += forwardBlock;
        }

        if (quote.Height > 0f)
        {
            DrawQuote(drawList, message, quote, new Vector2(bubbleMin.X + paddingX, contentTop),
                contentWidth, fx, mine, model);
            contentTop += quoteBlock;
        }

        if (deleted)
        {
            var iconCenter = new Vector2(bubbleMin.X + paddingX + 6f * scale, contentTop + textSize.Y * 0.5f);
            AppSkin.Icon(drawList, fx.Apply(iconCenter), IconGlyph.Of(FontAwesomeIcon.Ban),
                Palette.WithAlpha(ink, ink.W * fx.Alpha * 0.9f), 0.68f * fx.Pop);
        }

        var textPos = fx.Apply(new Vector2(bubbleMin.X + paddingX + deletedIconWidth, contentTop));
        if (runLayout is not null && runs is not null)
        {
            var runInk = Palette.WithAlpha(ink, ink.W);
            var tapped = RunText.Draw(drawList, runLayout, runs, textPos, runInk, fx.Alpha, entrance >= 1f);
            if (tapped >= 0 && model.Interactions is { } linkTarget)
            {
                linkTarget.OnLinkClick(message.Id, tapped);
            }
        }
        else if (linkLayout is null)
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * fx.Pop, textPos,
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * fx.Alpha)), body, wrap * fx.Pop);
        }
        else
        {
            var linkInk = mine || placeholder ? ink : model.Accent;
            LinkText.Draw(drawList, linkLayout, textPos, fx.Pop, ink, linkInk, fx.Alpha, entrance >= 1f);
        }

        if (footer.Height > 0f && model.Translation is { } target)
        {
            var footerPos = fx.Apply(new Vector2(bubbleMin.X + paddingX, contentTop + textSize.Y));
            var footerInk = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
            var actionInk = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Accent;
            if (TranslationFooter.Draw(drawList, footer, footerPos, footerInk, actionInk, fx.Alpha, scale))
            {
                target.Activate(message.Id, message.Body);
            }
        }

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        if (entrance >= 1f && !deleted && model.Interactions is { } interactions && message.Kind != KindSystem
            && Hovering(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            interactions.OnMessageContext(message.Id);
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawPostBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        if ((message.Flags & TranscriptFlags.Placeholder) != 0)
        {
            DrawTextBubble(message, index, model);
            return;
        }

        if (model.PostCards is { } cards && cards.TryResolve(message.Id, message.Body, out var card))
        {
            DrawPostCardBubble(message, index, card, cards, model);
            return;
        }

        DrawTextBubble(WithBodyText(message, Loc.T(L.Aethergram.SharedPost)), index, model);
    }

    private static TranscriptMessage WithBodyText(in TranscriptMessage message, string body)
    {
        return new TranscriptMessage(message.Id, message.SenderId, body, KindText, message.CreatedAtUnix,
            message.MediaWidth, message.MediaHeight, message.ReadAtUnix, message.SenderName, message.SenderTint,
            message.Flags, message.ReplyToId, message.ReplySenderName, message.ReplyBody, message.ReplyKind,
            message.DurationSecs, message.Reactions);
    }

    private void DrawPostCardBubble(TranscriptMessage message, int index, in ChatPostCard card,
        IChatTranscriptPostCards cards, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 7f * scale;
        var paddingY = 7f * scale;
        var innerWidth = MathF.Min(available * 0.62f, 210f * scale);
        var stamp = MeasureStamp(message, mine, scale);
        var snippet = card.Available ? card.Snippet : string.Empty;
        var snippetScale = TextStyles.Footnote.Scale;
        var snippetHeight = 0f;
        RichTextLayout? snippetLayout = null;
        if (snippet.Length > 0)
        {
            var lineHeight = Typography.Measure("Ag", TextStyles.Footnote).Y;
            snippetLayout = LinkText.LayoutFor(snippet, innerWidth / snippetScale);
            var naturalHeight = snippetLayout is not null
                ? snippetLayout.Size.Y * snippetScale
                : Typography.MeasureWrappedBlock(snippet, TextStyles.Footnote, innerWidth).Y;
            snippetHeight = MathF.Min(naturalHeight, lineHeight * 2f);
        }

        var unavailableLabel = Loc.T(L.Aethergram.PostUnavailable);
        var unavailableSize = Typography.Measure(unavailableLabel, TextStyles.FootnoteEmphasized);
        var authorHeight = card.Available
            ? Typography.Measure(card.AuthorName, TextStyles.SubheadlineEmphasized).Y
            : 0f;
        float bubbleWidth;
        float bubbleHeight;
        if (card.Available)
        {
            bubbleWidth = innerWidth + paddingX * 2f;
            bubbleHeight = paddingY + authorHeight + 6f * scale + innerWidth
                + (snippetHeight > 0f ? 5f * scale + snippetHeight : 0f)
                + 4f * scale + stamp.Height + paddingY;
        }
        else
        {
            var compactWidth = MathF.Max(19f * scale + unavailableSize.X, stamp.Width + 2f * scale);
            bubbleWidth = MathF.Min(innerWidth + paddingX * 2f, paddingX * 2f + compactWidth);
            bubbleHeight = paddingY + unavailableSize.Y + 4f * scale + stamp.Height + paddingY;
        }

        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        var mutedInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        if (!card.Available)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (card.Available)
        {
            var authorPos = fx.Apply(new Vector2(bubbleMin.X + paddingX + 2f * scale, contentTop));
            Typography.Draw(drawList, authorPos,
                Typography.FitText(card.AuthorName, innerWidth - 4f * scale, TextStyles.SubheadlineEmphasized),
                Palette.WithAlpha(ink, ink.W * fx.Alpha), TextStyles.SubheadlineEmphasized.Scale * fx.Pop,
                TextStyles.SubheadlineEmphasized.Weight);
            contentTop += authorHeight + 6f * scale;
            var thumbMin = fx.Apply(new Vector2(bubbleMin.X + paddingX, contentTop));
            var thumbMax = fx.Apply(new Vector2(bubbleMin.X + paddingX + innerWidth, contentTop + innerWidth));
            var rounding = 10f * scale * fx.Pop;
            var texture = card.Sensitive || card.ThumbnailUrl is null ? null : cards.Thumbnail(card.ThumbnailUrl);
            if (card.Sensitive)
            {
                SensitiveVeil.Draw(drawList, thumbMin, thumbMax, rounding);
            }
            else if (texture is null)
            {
                Squircle.Fill(drawList, thumbMin, thumbMax, rounding,
                    ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.08f * fx.Alpha)));
                AppSkin.Icon((thumbMin + thumbMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Image),
                    Palette.WithAlpha(model.MutedInk, fx.Alpha), 1.2f);
            }
            else
            {
                var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
                drawList.AddImageRounded(texture.Handle, thumbMin, thumbMax, uv0, uv1,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fx.Alpha)), rounding, ImDrawFlags.RoundCornersAll);
            }

            contentTop += innerWidth;
            if (snippetHeight > 0f)
            {
                contentTop += 5f * scale;
                var snippetMin = new Vector2(bubbleMin.X + paddingX, contentTop);
                var snippetMax = new Vector2(bubbleMin.X + paddingX + innerWidth, contentTop + snippetHeight);
                drawList.PushClipRect(fx.Apply(snippetMin), fx.Apply(snippetMax), true);
                if (snippetLayout is not null)
                {
                    LinkText.Draw(drawList, snippetLayout, fx.Apply(snippetMin), snippetScale * fx.Pop, mutedInk,
                        mutedInk, fx.Alpha, false);
                }
                else
                {
                    Typography.DrawWrappedLeft(fx.Apply(snippetMin), snippet,
                        Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha), TextStyles.Footnote, innerWidth);
                }

                drawList.PopClipRect();
                contentTop += snippetHeight;
            }
        }
        else
        {
            var iconCenter = new Vector2(bubbleMin.X + paddingX + 6f * scale,
                contentTop + unavailableSize.Y * 0.5f);
            AppSkin.Icon(drawList, fx.Apply(iconCenter), IconGlyph.Of(FontAwesomeIcon.EyeSlash),
                Palette.WithAlpha(ink, ink.W * fx.Alpha * 0.9f), 0.62f * fx.Pop);
            var labelPos = fx.Apply(new Vector2(bubbleMin.X + paddingX + 16f * scale, contentTop));
            Typography.Draw(drawList, labelPos, unavailableLabel, Palette.WithAlpha(ink, ink.W * fx.Alpha),
                TextStyles.FootnoteEmphasized.Scale * fx.Pop, TextStyles.FootnoteEmphasized.Weight);
        }

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX - 2f * scale, bubbleMax.Y - paddingY), fx,
            timeColor);
        if (entrance >= 1f && Hovering(bubbleMin, bubbleMax))
        {
            if (card.Available)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    cards.Open(card.PostId);
                }
            }

            if (model.Interactions is { } interactions && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                interactions.OnMessageContext(message.Id);
            }
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawLocationBubble(TranscriptMessage message, int index, in SharedLocation location,
        in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var placeholder = (message.Flags & TranscriptFlags.Placeholder) != 0;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 9f * scale;
        var badgeRadius = 16f * scale;
        var badgeColumn = badgeRadius * 2f + 10f * scale;

        var eyebrow = Loc.T(L.DirectMessages.LocationShared);
        var zone = LocationShare.ZoneName(location.TerritoryId);
        if (zone.Length == 0)
        {
            zone = Loc.T(L.DirectMessages.LocationPreview);
        }

        var worldLine = LocationShare.WorldLine(location);
        var detailLine = location.Ward > 0 ? LocationShare.HousingLine(location) : LocationShare.CoordinateText(location);
        var destination = TravelPlanner.Resolve(in location);
        var canTravel = TravelPlanner.CanGo(in destination);
        var travelLabel = canTravel ? Loc.T(L.Travel.GoThere) : string.Empty;
        var travelLabelSize = canTravel
            ? Typography.Measure(travelLabel, TextStyles.FootnoteEmphasized)
            : Vector2.Zero;
        var travelHeight = canTravel ? TravelPillHeight * scale : 0f;
        var stamp = MeasureStamp(message, mine, scale);
        var maxTextWidth = available * 0.74f - paddingX * 2f - badgeColumn;
        var eyebrowSize = Typography.Measure(eyebrow, TextStyles.FootnoteEmphasized);
        var zoneSize = Typography.Measure(zone, TextStyles.SubheadlineEmphasized);
        var worldSize = worldLine.Length > 0 ? Typography.Measure(worldLine, TextStyles.Footnote) : Vector2.Zero;
        var detailSize = detailLine.Length > 0 ? Typography.Measure(detailLine, TextStyles.Footnote) : Vector2.Zero;
        var textWidth = MathF.Min(maxTextWidth,
            MathF.Max(MathF.Max(eyebrowSize.X, zoneSize.X), MathF.Max(worldSize.X, detailSize.X)));
        var forwardLabel = MeasureForwardLabel(message, scale);
        var contentWidth = MathF.Max(badgeColumn + textWidth, stamp.Width);
        if (forwardLabel.Y > 0f)
        {
            contentWidth = MathF.Max(contentWidth, forwardLabel.X);
        }

        if (canTravel)
        {
            contentWidth = MathF.Max(contentWidth,
                travelLabelSize.X + (TravelIconSpace + 24f) * scale);
        }

        var textHeight = eyebrowSize.Y + 3f * scale + zoneSize.Y
                         + (worldSize.Y > 0f ? 2f * scale + worldSize.Y : 0f)
                         + (detailSize.Y > 0f ? 2f * scale + detailSize.Y : 0f);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 3f * scale : 0f;
        var travelBlock = canTravel ? 7f * scale + travelHeight : 0f;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = paddingY + forwardBlock + textHeight + travelBlock + 4f * scale + stamp.Height + paddingY;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        var mutedInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var accentInk = mine ? new Vector4(1f, 1f, 1f, 0.88f) : model.Accent;
        if (placeholder)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), fx, mine, model, scale);
            contentTop += forwardBlock;
        }

        var badgeCenter = new Vector2(bubbleMin.X + paddingX + badgeRadius, contentTop + textHeight * 0.5f);
        var badgeFill = mine ? new Vector4(1f, 1f, 1f, 0.20f) : Palette.WithAlpha(model.Accent, 0.18f);
        drawList.AddCircleFilled(fx.Apply(badgeCenter), badgeRadius * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(badgeFill, badgeFill.W * fx.Alpha)), 32);
        AppSkin.Icon(drawList, fx.Apply(badgeCenter), IconGlyph.Of(FontAwesomeIcon.MapMarkerAlt),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha), 1.05f * fx.Pop);

        var textLeft = bubbleMin.X + paddingX + badgeColumn;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(eyebrow, textWidth, TextStyles.FootnoteEmphasized),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha),
            TextStyles.FootnoteEmphasized.Scale * fx.Pop, TextStyles.FootnoteEmphasized.Weight);
        contentTop += eyebrowSize.Y + 3f * scale;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(zone, textWidth, TextStyles.SubheadlineEmphasized),
            Palette.WithAlpha(ink, ink.W * fx.Alpha),
            TextStyles.SubheadlineEmphasized.Scale * fx.Pop, TextStyles.SubheadlineEmphasized.Weight);
        contentTop += zoneSize.Y;
        if (worldSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(worldLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
            contentTop += worldSize.Y;
        }

        if (detailSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(detailLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
        }

        var travelHovered = false;
        if (canTravel)
        {
            var travelTop = bubbleMin.Y + paddingY + forwardBlock + textHeight + 7f * scale;
            var travelMin = new Vector2(bubbleMin.X + paddingX, travelTop);
            var travelMax = new Vector2(bubbleMax.X - paddingX, travelTop + travelHeight);
            travelHovered = entrance >= 1f && Hovering(travelMin, travelMax);
            var travelFill = mine
                ? new Vector4(1f, 1f, 1f, travelHovered ? 0.30f : 0.20f)
                : Palette.WithAlpha(model.Accent, travelHovered ? 0.34f : 0.22f);
            Squircle.Fill(drawList, fx.Apply(travelMin), fx.Apply(travelMax), travelHeight * 0.5f * fx.Pop,
                ImGui.GetColorU32(Palette.WithAlpha(travelFill, travelFill.W * fx.Alpha)));
            var travelCenterY = travelTop + travelHeight * 0.5f;
            var iconSpace = TravelIconSpace * scale;
            var contentLeft = (travelMin.X + travelMax.X) * 0.5f - (travelLabelSize.X + iconSpace) * 0.5f;
            AppSkin.Icon(drawList, fx.Apply(new Vector2(contentLeft + iconSpace * 0.4f, travelCenterY)),
                IconGlyph.Of(FontAwesomeIcon.LocationArrow),
                Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha), 0.62f * fx.Pop);
            Typography.Draw(drawList,
                fx.Apply(new Vector2(contentLeft + iconSpace, travelCenterY - travelLabelSize.Y * 0.5f)),
                travelLabel, Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha),
                TextStyles.FootnoteEmphasized.Scale * fx.Pop, TextStyles.FootnoteEmphasized.Weight);
            if (travelHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                HoverTooltip.Show(new Rect(travelMin, travelMax), TravelPlanner.Label(in destination),
                    HoverLabelSide.Above);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    StartTravel(in destination);
                }
            }
        }

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        if (entrance >= 1f && Hovering(bubbleMin, bubbleMax))
        {
            if (location.MapId != 0 && !travelHovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                HoverTooltip.Show(new Rect(bubbleMin, bubbleMax), Loc.T(L.DirectMessages.LocationOpenMap),
                    HoverLabelSide.Above);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    LocationShare.OpenMap(location);
                }
            }

            if (model.Interactions is { } interactions && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                interactions.OnMessageContext(message.Id);
            }
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private static void StartTravel(in TravelDestination destination)
    {
        var outcome = TravelPlanner.Go(in destination);
        if (outcome == LifestreamOutcome.Started)
        {
            return;
        }

        if (outcome == LifestreamOutcome.NotInstalled)
        {
            ImGui.SetClipboardText(TravelPlanner.Command(in destination));
            ShellToast.Show();
            return;
        }

        ShellToast.Show(TravelPlanner.Notice(outcome, in destination));
    }

    private void DrawMusterBubble(TranscriptMessage message, int index, string musterId, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var placeholder = (message.Flags & TranscriptFlags.Placeholder) != 0;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 9f * scale;
        var badgeRadius = 16f * scale;
        var badgeColumn = badgeRadius * 2f + 10f * scale;

        var resolution = MusterChatBridge.Resolve(musterId);
        var muster = resolution.Muster;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var over = muster is not null && muster.EndsAtUnix <= nowUnix;
        var eyebrow = Loc.T(L.Muster.InvitePreview);
        string title;
        var hostLine = string.Empty;
        var detailLine = string.Empty;
        if (muster is not null && !over)
        {
            title = Loc.T(MusterCategories.Label(muster.Category));
            hostLine = MusterText.Identity(muster);
            detailLine = muster.StartsAtUnix <= nowUnix
                ? $"{Loc.T(L.Common.Live)} · {Loc.T(L.Muster.EndsIn, MusterText.Span(muster.EndsAtUnix - nowUnix))}"
                : Loc.T(L.Muster.StartsIn, MusterText.Span(muster.StartsAtUnix - nowUnix));
        }
        else if (resolution.Missed || over)
        {
            title = Loc.T(L.Muster.InviteUnavailable);
        }
        else
        {
            title = Loc.T(L.Common.Loading);
        }

        var stamp = MeasureStamp(message, mine, scale);
        var maxTextWidth = available * 0.74f - paddingX * 2f - badgeColumn;
        var eyebrowSize = Typography.Measure(eyebrow, TextStyles.FootnoteEmphasized);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var hostSize = hostLine.Length > 0 ? Typography.Measure(hostLine, TextStyles.Footnote) : Vector2.Zero;
        var detailSize = detailLine.Length > 0 ? Typography.Measure(detailLine, TextStyles.Footnote) : Vector2.Zero;
        var textWidth = MathF.Min(maxTextWidth,
            MathF.Max(MathF.Max(eyebrowSize.X, titleSize.X), MathF.Max(hostSize.X, detailSize.X)));
        var forwardLabel = MeasureForwardLabel(message, scale);
        var contentWidth = MathF.Max(badgeColumn + textWidth, stamp.Width);
        if (forwardLabel.Y > 0f)
        {
            contentWidth = MathF.Max(contentWidth, forwardLabel.X);
        }

        var textHeight = eyebrowSize.Y + 3f * scale + titleSize.Y
                         + (hostSize.Y > 0f ? 2f * scale + hostSize.Y : 0f)
                         + (detailSize.Y > 0f ? 2f * scale + detailSize.Y : 0f);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 3f * scale : 0f;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = paddingY + forwardBlock + textHeight + 4f * scale + stamp.Height + paddingY;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        var mutedInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var accentInk = mine ? new Vector4(1f, 1f, 1f, 0.88f) : model.Accent;
        if (placeholder)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), fx, mine, model, scale);
            contentTop += forwardBlock;
        }

        var badgeCenter = new Vector2(bubbleMin.X + paddingX + badgeRadius, contentTop + textHeight * 0.5f);
        var badgeFill = mine ? new Vector4(1f, 1f, 1f, 0.20f) : Palette.WithAlpha(model.Accent, 0.18f);
        drawList.AddCircleFilled(fx.Apply(badgeCenter), badgeRadius * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(badgeFill, badgeFill.W * fx.Alpha)), 32);
        AppSkin.Icon(drawList, fx.Apply(badgeCenter), IconGlyph.Of(FontAwesomeIcon.Bullhorn),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha), 1.0f * fx.Pop);

        var textLeft = bubbleMin.X + paddingX + badgeColumn;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(eyebrow, textWidth, TextStyles.FootnoteEmphasized),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha),
            TextStyles.FootnoteEmphasized.Scale * fx.Pop, TextStyles.FootnoteEmphasized.Weight);
        contentTop += eyebrowSize.Y + 3f * scale;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized),
            Palette.WithAlpha(ink, ink.W * fx.Alpha),
            TextStyles.SubheadlineEmphasized.Scale * fx.Pop, TextStyles.SubheadlineEmphasized.Weight);
        contentTop += titleSize.Y;
        if (hostSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(hostLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
            contentTop += hostSize.Y;
        }

        if (detailSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(detailLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
        }

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        if (entrance >= 1f && Hovering(bubbleMin, bubbleMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(bubbleMin, bubbleMax), Loc.T(L.Muster.InviteOpen), HoverLabelSide.Above);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                MusterChatBridge.Open(musterId);
            }

            if (model.Interactions is { } interactions && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                interactions.OnMessageContext(message.Id);
            }
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawAdBubble(TranscriptMessage message, int index, string adId, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var placeholder = (message.Flags & TranscriptFlags.Placeholder) != 0;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 9f * scale;
        var badgeRadius = 16f * scale;
        var badgeColumn = badgeRadius * 2f + 10f * scale;

        var resolution = AdChatBridge.Resolve(adId);
        var ad = resolution.Ad;
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var over = ad is not null && (ad.ExpiresAtUnix <= nowUnix
            || !string.Equals(ad.Status, Core.Aethernet.Contracts.AdStatuses.Live, StringComparison.Ordinal));
        var eyebrow = Loc.T(L.YellowPages.AdPreview);
        string title;
        var ownerLine = string.Empty;
        var detailLine = string.Empty;
        if (ad is not null && !over)
        {
            title = ad.Title;
            ownerLine = AdText.Identity(ad);
            detailLine = ad.Archetype switch
            {
                Core.YellowPages.AdArchetypes.Place => AdText.OpenLine(ad, nowUnix),
                Core.YellowPages.AdArchetypes.Service => AdText.PriceLine(ad),
                _ => ad.SlotsLine,
            };
            if (detailLine.Length == 0)
            {
                detailLine = Loc.T(Core.YellowPages.AdCategories.Label(ad.Category));
            }
        }
        else if (resolution.Missed || over)
        {
            title = Loc.T(L.YellowPages.AdUnavailable);
        }
        else
        {
            title = Loc.T(L.Common.Loading);
        }

        var stamp = MeasureStamp(message, mine, scale);
        var maxTextWidth = available * 0.74f - paddingX * 2f - badgeColumn;
        var eyebrowSize = Typography.Measure(eyebrow, TextStyles.FootnoteEmphasized);
        var titleSize = Typography.Measure(title, TextStyles.SubheadlineEmphasized);
        var ownerSize = ownerLine.Length > 0 ? Typography.Measure(ownerLine, TextStyles.Footnote) : Vector2.Zero;
        var detailSize = detailLine.Length > 0 ? Typography.Measure(detailLine, TextStyles.Footnote) : Vector2.Zero;
        var textWidth = MathF.Min(maxTextWidth,
            MathF.Max(MathF.Max(eyebrowSize.X, titleSize.X), MathF.Max(ownerSize.X, detailSize.X)));
        var forwardLabel = MeasureForwardLabel(message, scale);
        var contentWidth = MathF.Max(badgeColumn + textWidth, stamp.Width);
        if (forwardLabel.Y > 0f)
        {
            contentWidth = MathF.Max(contentWidth, forwardLabel.X);
        }

        var textHeight = eyebrowSize.Y + 3f * scale + titleSize.Y
                         + (ownerSize.Y > 0f ? 2f * scale + ownerSize.Y : 0f)
                         + (detailSize.Y > 0f ? 2f * scale + detailSize.Y : 0f);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 3f * scale : 0f;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = paddingY + forwardBlock + textHeight + 4f * scale + stamp.Height + paddingY;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        var mutedInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var accentInk = mine ? new Vector4(1f, 1f, 1f, 0.88f) : model.Accent;
        if (placeholder)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), fx, mine, model, scale);
            contentTop += forwardBlock;
        }

        var badgeCenter = new Vector2(bubbleMin.X + paddingX + badgeRadius, contentTop + textHeight * 0.5f);
        var badgeFill = mine ? new Vector4(1f, 1f, 1f, 0.20f) : Palette.WithAlpha(model.Accent, 0.18f);
        drawList.AddCircleFilled(fx.Apply(badgeCenter), badgeRadius * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(badgeFill, badgeFill.W * fx.Alpha)), 32);
        AppSkin.Icon(drawList, fx.Apply(badgeCenter), IconGlyph.Of(FontAwesomeIcon.AddressBook),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha), 1.0f * fx.Pop);

        var textLeft = bubbleMin.X + paddingX + badgeColumn;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(eyebrow, textWidth, TextStyles.FootnoteEmphasized),
            Palette.WithAlpha(accentInk, accentInk.W * fx.Alpha),
            TextStyles.FootnoteEmphasized.Scale * fx.Pop, TextStyles.FootnoteEmphasized.Weight);
        contentTop += eyebrowSize.Y + 3f * scale;
        Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
            Typography.FitText(title, textWidth, TextStyles.SubheadlineEmphasized),
            Palette.WithAlpha(ink, ink.W * fx.Alpha),
            TextStyles.SubheadlineEmphasized.Scale * fx.Pop, TextStyles.SubheadlineEmphasized.Weight);
        contentTop += titleSize.Y;
        if (ownerSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(ownerLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
            contentTop += ownerSize.Y;
        }

        if (detailSize.Y > 0f)
        {
            contentTop += 2f * scale;
            Typography.Draw(drawList, fx.Apply(new Vector2(textLeft, contentTop)),
                Typography.FitText(detailLine, textWidth, TextStyles.Footnote),
                Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha),
                TextStyles.Footnote.Scale * fx.Pop, TextStyles.Footnote.Weight);
        }

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        if (entrance >= 1f && Hovering(bubbleMin, bubbleMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(new Rect(bubbleMin, bubbleMax), Loc.T(L.YellowPages.AdOpen), HoverLabelSide.Above);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                AdChatBridge.Open(adId);
            }

            if (model.Interactions is { } interactions && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                interactions.OnMessageContext(message.Id);
            }
        }

        var adChipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + adChipRow + BubbleGap * scale));
    }

    private void DrawStoryReplyBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        if ((message.Flags & TranscriptFlags.Placeholder) != 0 || (message.Flags & TranscriptFlags.Deleted) != 0)
        {
            DrawTextBubble(message, index, model);
            return;
        }

        if (model.StoryReplies is not { } replies || !replies.TryResolve(message.Id, out var context))
        {
            DrawTextBubble(message, index, model);
            return;
        }

        DrawStoryReplyContext(message, context, replies, model);
        DrawTextBubble(message, index, model);
    }

    private void DrawStoryReplyContext(in TranscriptMessage message, in ChatStoryReplyContext context,
        IChatTranscriptStoryReplies replies, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var labelSize = Typography.Measure(context.ContextText, 0.74f);
        var labelX = mine ? origin.X + available - labelSize.X - 4f * scale : origin.X + 4f * scale;
        Typography.Draw(new Vector2(labelX, origin.Y), context.ContextText,
            Palette.WithAlpha(model.MutedInk, 0.95f), 0.74f);
        var top = origin.Y + labelSize.Y + 5f * scale;
        float bottom;
        if (context.Unavailable || context.ThumbnailUrl is null)
        {
            var chipLabel = Loc.T(L.Aethergram.StoryUnavailable);
            var chipTextSize = Typography.Measure(chipLabel, TextStyles.FootnoteEmphasized);
            var chipHeight = chipTextSize.Y + 12f * scale;
            var chipWidth = 12f * scale + 15f * scale + chipTextSize.X + 12f * scale;
            var chipMin = new Vector2(mine ? origin.X + available - chipWidth : origin.X, top);
            var chipMax = chipMin + new Vector2(chipWidth, chipHeight);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.07f)));
            AppSkin.Icon(drawList, new Vector2(chipMin.X + 12f * scale + 5f * scale, chipMin.Y + chipHeight * 0.5f),
                IconGlyph.Of(FontAwesomeIcon.EyeSlash), Palette.WithAlpha(model.MutedInk, 0.9f), 0.62f);
            Typography.Draw(drawList, new Vector2(chipMin.X + 12f * scale + 15f * scale,
                chipMin.Y + (chipHeight - chipTextSize.Y) * 0.5f), chipLabel,
                Palette.WithAlpha(model.MutedInk, 0.95f), TextStyles.FootnoteEmphasized.Scale,
                TextStyles.FootnoteEmphasized.Weight);
            bottom = chipMax.Y;
        }
        else
        {
            var thumbWidth = 74f * scale;
            var thumbHeight = 132f * scale;
            var thumbMin = new Vector2(mine ? origin.X + available - thumbWidth : origin.X, top);
            var thumbMax = thumbMin + new Vector2(thumbWidth, thumbHeight);
            var rounding = 10f * scale;
            var texture = replies.Thumbnail(context.ThumbnailUrl);
            if (texture is null)
            {
                Squircle.Fill(drawList, thumbMin, thumbMax, rounding,
                    ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.08f)));
                AppSkin.Icon((thumbMin + thumbMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Image), model.MutedInk,
                    1.1f);
            }
            else
            {
                var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, thumbWidth, thumbHeight);
                drawList.AddImageRounded(texture.Handle, thumbMin, thumbMax, uv0, uv1, 0xFFFFFFFFu, rounding,
                    ImDrawFlags.RoundCornersAll);
            }

            bottom = thumbMax.Y;
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom + 5f * scale));
    }

    private static Vector2 MeasureForwardLabel(in TranscriptMessage message, float scale)
    {
        if ((message.Flags & TranscriptFlags.Forwarded) == 0 || (message.Flags & TranscriptFlags.Deleted) != 0)
        {
            return Vector2.Zero;
        }

        var size = Typography.Measure(Loc.T(L.Message.ForwardedLabel), 0.72f);
        return new Vector2(15f * scale + size.X, size.Y);
    }

    private void DrawForwardLabel(ImDrawListPtr drawList, Vector2 origin, in BubblePop fx, bool mine,
        in ChatTranscriptModel model, float scale)
    {
        var ink = mine ? new Vector4(1f, 1f, 1f, 0.70f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var label = Loc.T(L.Message.ForwardedLabel);
        var size = Typography.Measure(label, 0.72f);
        var iconCenter = new Vector2(origin.X + 5f * scale, origin.Y + size.Y * 0.5f);
        AppSkin.Icon(drawList, fx.Apply(iconCenter), IconGlyph.Of(FontAwesomeIcon.Share),
            Palette.WithAlpha(ink, ink.W * fx.Alpha), 0.58f * fx.Pop);
        var textPos = fx.Apply(new Vector2(origin.X + 15f * scale, origin.Y));
        Typography.Draw(drawList, textPos, label, Palette.WithAlpha(ink, ink.W * fx.Alpha), 0.72f * fx.Pop);
    }

    private float DrawReactionChips(ImDrawListPtr drawList, in TranscriptMessage message, bool mine,
        Vector2 bubbleMin, Vector2 bubbleMax, float alpha, in ChatTranscriptModel model, float scale)
    {
        var reactions = message.Reactions;
        if (reactions.Length == 0 || (message.Flags & TranscriptFlags.Deleted) != 0)
        {
            return 0f;
        }

        var chipHeight = ReactionChipHeight * scale;
        var chipGap = ReactionChipGap * scale;
        var emojiSize = ReactionChipEmoji * scale;
        var padX = ReactionChipPadX * scale;
        var countGap = ReactionChipCountGap * scale;
        var top = bubbleMax.Y - ReactionChipOverlap * scale;
        var totalWidth = 0f;
        Span<float> widths = stackalloc float[reactions.Length];
        for (var index = 0; index < reactions.Length; index++)
        {
            var width = padX * 2f + emojiSize;
            if (reactions[index].Count > 1)
            {
                width += countGap + Typography.Measure(reactions[index].Count.ToString(Loc.Culture),
                    ReactionCountStyle).X;
            }

            widths[index] = width;
            totalWidth += width + (index > 0 ? chipGap : 0f);
        }

        var edgeInset = ReactionChipEdgeInset * scale;
        var cursor = mine ? bubbleMax.X - edgeInset - totalWidth : bubbleMin.X + edgeInset;
        var fill = ReactionChipFill;
        var stroke = ReactionChipStroke;
        for (var index = 0; index < reactions.Length; index++)
        {
            var reaction = reactions[index];
            var chipMin = new Vector2(cursor, top);
            var chipMax = new Vector2(cursor + widths[index], top + chipHeight);
            var hovered = model.Interactions is not null && Hovering(chipMin, chipMax);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
            var ring = reaction.Mine ? Palette.WithAlpha(model.Accent, 0.95f * alpha)
                : hovered ? Palette.WithAlpha(stroke, stroke.W * 2f * alpha)
                : Palette.WithAlpha(stroke, stroke.W * alpha);
            Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f, ImGui.GetColorU32(ring),
                ReactionChipRing * scale);
            var emojiCenter = new Vector2(chipMin.X + padX + emojiSize * 0.5f, top + chipHeight * 0.5f);
            ReactionArt.Draw(drawList, reaction.Token, emojiCenter, emojiSize, alpha, ReactionChipFallbackScale);
            if (reaction.Count > 1)
            {
                var label = reaction.Count.ToString(Loc.Culture);
                var labelSize = Typography.Measure(label, ReactionCountStyle);
                Typography.Draw(drawList, new Vector2(emojiCenter.X + emojiSize * 0.5f + countGap,
                    top + chipHeight * 0.5f - labelSize.Y * 0.5f), label,
                    Palette.WithAlpha(ReactionCountInk, alpha), ReactionCountStyle);
            }

            if (hovered && model.Interactions is { } interactions)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                HoverTooltip.Show(new Rect(chipMin, chipMax),
                    Loc.T(reaction.Mine ? L.Message.ReactionRemove : L.Message.ReactionAdd), HoverLabelSide.Above);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    interactions.OnReactionClick(message.Id, reaction.Mine ? string.Empty : reaction.Token);
                }
            }

            cursor = chipMax.X + chipGap;
        }

        return chipHeight - ReactionChipOverlap * scale + ReactionChipBelowGap * scale;
    }

    private void DrawVoiceBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 10f * scale;
        var paddingY = 8f * scale;
        var contentWidth = MathF.Min(available * 0.62f, 210f * scale);
        var forwardLabel = MeasureForwardLabel(message, scale);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 3f * scale : 0f;
        var playRadius = 13f * scale;
        var rowHeight = playRadius * 2f;
        var stamp = MeasureStamp(message, mine, scale);
        var bottomRow = stamp.Height + 4f * scale;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = paddingY * 2f + forwardBlock + rowHeight + bottomRow;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var entrance = entrances.Progress(index);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), fx, mine, model, scale);
            contentTop += forwardBlock;
        }

        var state = model.Voice?.StateFor(message.Id) ?? default;
        var playCenter = new Vector2(bubbleMin.X + paddingX + playRadius, contentTop + playRadius);
        var playFill = mine ? new Vector4(1f, 1f, 1f, 0.22f) : Palette.WithAlpha(model.Accent, 0.9f);
        drawList.AddCircleFilled(playCenter, playRadius, ImGui.GetColorU32(Palette.WithAlpha(playFill,
            playFill.W * fx.Alpha)), 28);
        AppSkin.Icon(drawList, playCenter, IconGlyph.Of((state.Playing ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play)), new Vector4(1f, 1f, 1f, fx.Alpha), 0.7f);
        var trackLeft = playCenter.X + playRadius + 9f * scale;
        var trackRight = bubbleMax.X - paddingX;
        var trackY = playCenter.Y;
        var trackColor = mine ? new Vector4(1f, 1f, 1f, 0.35f) : Palette.WithAlpha(model.MutedInk, 0.55f);
        drawList.AddRectFilled(new Vector2(trackLeft, trackY - 2f * scale), new Vector2(trackRight, trackY + 2f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(trackColor, trackColor.W * fx.Alpha)), 2f * scale);
        var progress = state.Current ? state.Progress : 0f;
        if (progress > 0f)
        {
            var fillRight = trackLeft + (trackRight - trackLeft) * progress;
            drawList.AddRectFilled(new Vector2(trackLeft, trackY - 2f * scale), new Vector2(fillRight, trackY + 2f * scale),
                ImGui.GetColorU32(Palette.WithAlpha(ink, 0.95f * fx.Alpha)), 2f * scale);
            drawList.AddCircleFilled(new Vector2(fillRight, trackY), 4.5f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ink, fx.Alpha)), 16);
        }

        var duration = state.Current && state.Playing
            ? (int)MathF.Round(progress * message.DurationSecs)
            : message.DurationSecs;
        var durationText = TimeText.MinutesSeconds(duration);
        Typography.Draw(drawList, new Vector2(trackLeft, bubbleMax.Y - paddingY - stamp.Height),
            durationText, Palette.WithAlpha(mine ? new Vector4(1f, 1f, 1f, 0.72f) : model.MutedInk, fx.Alpha),
            StampTextScale);
        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        var playHitMin = playCenter - new Vector2(playRadius, playRadius);
        var playHitMax = playCenter + new Vector2(playRadius, playRadius);
        if (entrance >= 1f && model.Voice is { } voice && Hovering(playHitMin, playHitMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                voice.Toggle(message.Id);
            }
        }

        if (entrance >= 1f && model.Interactions is { } interactions
            && Hovering(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            interactions.OnMessageContext(message.Id);
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawImageBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var padding = 5f * scale;
        var aspect = message.MediaWidth > 0 && message.MediaHeight > 0
            ? (float)message.MediaHeight / message.MediaWidth
            : 1f;
        var imageWidth = available * 0.62f;
        var imageHeight = imageWidth * aspect;
        var maxHeight = 280f * scale;
        if (imageHeight > maxHeight)
        {
            imageHeight = maxHeight;
            imageWidth = imageHeight / aspect;
        }

        var caption = message.Body ?? string.Empty;
        var stamp = MeasureStamp(message, mine, scale);
        var captionLayout = caption.Length > 0 ? LinkText.LayoutFor(caption, imageWidth / CaptionTextScale) : null;
        var captionTextHeight = captionLayout is not null
            ? captionLayout.Size.Y * CaptionTextScale
            : Typography.Measure(caption, CaptionTextScale).Y;
        var captionHeight = caption.Length > 0 ? captionTextHeight + 6f * scale : 0f;
        var stampRowHeight = caption.Length > 0 ? stamp.Height + 3f * scale : 0f;
        var forwardLabel = MeasureForwardLabel(message, scale);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 4f * scale : 0f;
        var bubbleWidth = imageWidth + padding * 2f;
        var bubbleHeight = imageHeight + padding * 2f + captionHeight + stampRowHeight + forwardBlock;
        var start = ImGui.GetCursorScreenPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? model.Accent : ChatInk.IncomingBubble(model.Theme);
        var entrance = entrances.Progress(index);
        var bubbleMin = start + new Vector2(offsetX, 0f);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var fx = BubblePop.For(entrance, scale, new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y));
        var scaledMin = fx.Apply(bubbleMin);
        var scaledMax = fx.Apply(bubbleMax);
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + padding, bubbleMin.Y + padding), fx, mine, model,
                scale);
        }

        var imageMin = scaledMin + new Vector2(padding * fx.Pop, (padding + forwardBlock) * fx.Pop);
        var imageMax = imageMin + new Vector2(imageWidth * fx.Pop, imageHeight * fx.Pop);
        var rounding = 10f * scale * fx.Pop;
        var texture = model.Media?.Texture(message.Id);
        if (texture is null)
        {
            Squircle.Fill(drawList, imageMin, imageMax, rounding,
                ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.08f * fx.Alpha)));
            AppSkin.Icon((imageMin + imageMax) * 0.5f, IconGlyph.Of(FontAwesomeIcon.Image),
                Palette.WithAlpha(model.MutedInk, fx.Alpha), 1.2f);
        }
        else
        {
            drawList.AddImageRounded(texture.Handle, imageMin, imageMax, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fx.Alpha)), rounding, ImDrawFlags.RoundCornersAll);
            if (entrance >= 1f && model.Media is { } media && Hovering(imageMin, imageMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    media.OnImageClick(message.Id);
                }
            }
        }

        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * fx.Pop, mine, model);
        if (entrance >= 1f && model.Interactions is { } interactions
            && Hovering(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            interactions.OnMessageContext(message.Id);
        }

        if (caption.Length > 0)
        {
            var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
            var captionTop = imageMax.Y + 4f * scale * fx.Pop;
            var captionMaxWidth = imageMax.X - imageMin.X;
            if (captionLayout is not null)
            {
                LinkText.Draw(drawList, captionLayout, new Vector2(imageMin.X, captionTop), CaptionTextScale * fx.Pop,
                    ink, mine ? ink : model.Accent, fx.Alpha, entrance >= 1f);
            }
            else
            {
                Marquee.DrawLeftAuto(new MarqueeId("chattranscript.caption.", message.Id), caption, imageMin.X,
                    captionTop, captionMaxWidth, new TextStyle(CaptionTextScale * fx.Pop, FontWeight.Regular),
                    Palette.WithAlpha(ink, fx.Alpha));
            }
            var timeColor = mine
                ? new Vector4(1f, 1f, 1f, 0.72f)
                : Palette.WithAlpha(model.MutedInk, 0.95f);
            DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - padding - 4f * scale, bubbleMax.Y - padding),
                fx, timeColor);
        }
        else
        {
            var stampPad = new Vector2(7f * scale, 3f * scale);
            var pillMax = bubbleMin + new Vector2(padding + imageWidth, padding + forwardBlock + imageHeight) -
                          new Vector2(6f * scale, 6f * scale);
            var pillMin = pillMax - new Vector2(stamp.Width + stampPad.X * 2f, stamp.Height + stampPad.Y * 2f);
            Squircle.Fill(drawList, fx.Apply(pillMin), fx.Apply(pillMax),
                (pillMax.Y - pillMin.Y) * 0.5f * fx.Pop, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f * fx.Alpha)));
            DrawStamp(drawList, stamp, pillMax - stampPad, fx, new Vector4(1f, 1f, 1f, 0.92f));
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, fx.Alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawTypingBubble(float reveal, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        typingPhase += ImGui.GetIO().DeltaTime;
        if (typingPhase > 1000f)
        {
            typingPhase -= 1000f;
        }

        var eased = Easing.EaseOutCubic(Math.Clamp(reveal, 0f, 1f));
        var drawList = ImGui.GetWindowDrawList();
        var paddingX = 14f * scale;
        var dotRadius = 3.2f * scale;
        var dotGap = 7f * scale;
        var bubbleWidth = paddingX * 2f + dotRadius * 6f + dotGap * 2f;
        var bubbleHeight = 28f * scale;
        var start = ImGui.GetCursorPos();
        var origin = ImGui.GetCursorScreenPos() + new Vector2(0f, (1f - eased) * 6f * scale);
        var bubbleMax = new Vector2(origin.X + bubbleWidth, origin.Y + bubbleHeight);
        Squircle.Fill(drawList, origin, bubbleMax, bubbleHeight * 0.5f,
            ImGui.GetColorU32(ChatInk.Wash(model.Theme, 0.10f * eased)));
        var baseY = (origin.Y + bubbleMax.Y) * 0.5f;
        var firstDotX = origin.X + paddingX + dotRadius;
        for (var dot = 0; dot < 3; dot++)
        {
            var wave = MathF.Max(0f, MathF.Sin(typingPhase * 6f - dot * 0.9f));
            var offsetY = -wave * 4f * scale;
            var dotAlpha = (0.35f + 0.5f * wave) * eased;
            var center = new Vector2(firstDotX + dot * (dotRadius * 2f + dotGap), baseY + offsetY);
            drawList.AddCircleFilled(center, dotRadius,
                ImGui.GetColorU32(Palette.WithAlpha(model.BodyInk, dotAlpha)), 16);
        }

        ImGui.SetCursorPos(start);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, (bubbleHeight + 8f * scale) * eased));
    }

    private static QuoteMeasure MeasureQuote(in TranscriptMessage message, float wrap, float scale)
    {
        if (message.ReplyToId is null)
        {
            return default;
        }

        var senderSize = Typography.Measure(message.ReplySenderName, QuoteSenderScale, FontWeight.SemiBold);
        var previewSize = Typography.Measure(message.ReplyBody, QuotePreviewScale);
        var iconWidth = message.ReplyKind is KindImage or KindVoice or KindLocation ? 15f * scale : 0f;
        var innerWidth = MathF.Max(senderSize.X, iconWidth + previewSize.X);
        var desired = 3f * scale + 7f * scale + innerWidth + 8f * scale;
        var height = 5f * scale * 2f + senderSize.Y + 1f * scale + previewSize.Y;
        return new QuoteMeasure(height, MathF.Min(desired, wrap), senderSize.Y);
    }

    private void DrawQuote(ImDrawListPtr drawList, in TranscriptMessage message, in QuoteMeasure quote,
        Vector2 origin, float width, in BubblePop fx, bool mine, in ChatTranscriptModel model)
    {
        var scale = UiScale.Current;
        var quoteMin = origin;
        var quoteMax = origin + new Vector2(width, quote.Height);
        var scaledMin = fx.Apply(quoteMin);
        var scaledMax = fx.Apply(quoteMax);
        var fill = mine ? new Vector4(0f, 0f, 0f, 0.20f) : ChatInk.Wash(model.Theme, 0.07f);
        Squircle.Fill(drawList, scaledMin, scaledMax, 8f * scale * fx.Pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * fx.Alpha)));
        var barColor = mine ? new Vector4(1f, 1f, 1f, 0.92f) : model.Accent;
        var barMax = new Vector2(quoteMin.X + 3f * scale, quoteMax.Y);
        Squircle.Fill(drawList, fx.Apply(quoteMin), fx.Apply(barMax),
            1.5f * scale * fx.Pop, ImGui.GetColorU32(Palette.WithAlpha(barColor, barColor.W * fx.Alpha)));
        var textLeft = quoteMin.X + 3f * scale + 7f * scale;
        var textWidth = quoteMax.X - 8f * scale - textLeft;
        var senderInk = mine ? new Vector4(1f, 1f, 1f, 0.95f) : model.Accent;
        var senderPos = fx.Apply(new Vector2(textLeft, quoteMin.Y + 5f * scale));
        Typography.Draw(drawList, senderPos, Typography.FitText(message.ReplySenderName, textWidth,
            QuoteSenderScale, FontWeight.SemiBold), Palette.WithAlpha(senderInk, senderInk.W * fx.Alpha),
            QuoteSenderScale * fx.Pop, FontWeight.SemiBold);
        var previewInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var previewTop = quoteMin.Y + 5f * scale + quote.SenderHeight + 1f * scale;
        var previewLeft = textLeft;
        if (message.ReplyKind is KindImage or KindVoice or KindLocation)
        {
            var iconCenter = new Vector2(textLeft + 5f * scale, previewTop + 7f * scale);
            var glyph = message.ReplyKind switch
            {
                KindVoice => IconGlyph.Of(FontAwesomeIcon.Microphone),
                KindLocation => IconGlyph.Of(FontAwesomeIcon.MapMarkerAlt),
                _ => IconGlyph.Of(FontAwesomeIcon.Camera),
            };
            AppSkin.Icon(drawList, fx.Apply(iconCenter), glyph,
                Palette.WithAlpha(previewInk, previewInk.W * fx.Alpha), 0.62f * fx.Pop);
            previewLeft += 15f * scale;
        }

        var previewPos = fx.Apply(new Vector2(previewLeft, previewTop));
        Typography.Draw(drawList, previewPos, Typography.FitText(message.ReplyBody,
            textWidth - (previewLeft - textLeft), QuotePreviewScale, FontWeight.Regular),
            Palette.WithAlpha(previewInk, previewInk.W * fx.Alpha), QuotePreviewScale * fx.Pop);
        if (model.Interactions is { } interactions && Hovering(quoteMin, quoteMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                interactions.OnQuoteClick(message.ReplyToId!);
            }
        }
    }

    private void ConsumeScrollTarget(string messageId, float bubbleTop)
    {
        if (scrollTargetId != messageId)
        {
            return;
        }

        ImGui.SetScrollFromPosY(bubbleTop - ImGui.GetWindowPos().Y, 0.30f);
        scrollTargetId = null;
        followBottom = false;
    }

    private void DrawFlash(ImDrawListPtr drawList, string messageId, Vector2 min, Vector2 max, float rounding,
        bool mine, in ChatTranscriptModel model)
    {
        if (flashMessageId != messageId)
        {
            return;
        }

        var fade = 1f - flashElapsed / FlashSeconds;
        var color = mine ? new Vector4(1f, 1f, 1f, 0.20f * fade) : Palette.WithAlpha(model.Accent, 0.26f * fade);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(color));
    }

    private readonly struct QuoteMeasure
    {
        public readonly float Height;
        public readonly float MinWidth;
        public readonly float SenderHeight;

        public QuoteMeasure(float height, float minWidth, float senderHeight)
        {
            Height = height;
            MinWidth = minWidth;
            SenderHeight = senderHeight;
        }
    }

    private static BubbleStamp MeasureStamp(TranscriptMessage message, bool mine, float scale)
    {
        var time = TimeText.Clock(message.CreatedAtUnix);
        if ((message.Flags & TranscriptFlags.Edited) != 0)
        {
            time = Loc.T(L.Message.EditedAt, time);
        }

        var timeSize = Typography.Measure(time, StampTextScale);
        if ((message.Flags & TranscriptFlags.Deleted) != 0)
        {
            return new BubbleStamp(time, null, false, timeSize.X, timeSize.Y, 0f);
        }

        if (!mine)
        {
            return new BubbleStamp(time, null, false, timeSize.X, timeSize.Y, 0f);
        }

        var seen = message.ReadAtUnix is not null;
        var glyph = IconGlyph.Of((seen ? FontAwesomeIcon.CheckDouble : FontAwesomeIcon.Check));
        float tickWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            tickWidth = ImGui.CalcTextSize(glyph).X * StampTickScale;
        }

        return new BubbleStamp(time, glyph, seen, timeSize.X + 4f * scale + tickWidth, timeSize.Y, tickWidth);
    }

    private static void DrawStamp(ImDrawListPtr drawList, in BubbleStamp stamp, Vector2 bottomRight, in BubblePop fx,
        Vector4 timeColor)
    {
        var topLeft = new Vector2(bottomRight.X - stamp.Width, bottomRight.Y - stamp.Height);
        Typography.Draw(drawList, fx.Apply(topLeft), stamp.Time, Palette.WithAlpha(timeColor, timeColor.W * fx.Alpha),
            StampTextScale * fx.Pop);
        if (stamp.TickGlyph is null)
        {
            return;
        }

        var tickCenter = new Vector2(bottomRight.X - stamp.TickWidth * 0.5f, bottomRight.Y - stamp.Height * 0.45f);
        var tickColor = stamp.Seen ? SeenTickColor : timeColor;
        AppSkin.Icon(drawList, fx.Apply(tickCenter), stamp.TickGlyph,
            Palette.WithAlpha(tickColor, tickColor.W * fx.Alpha), StampTickScale * fx.Pop);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }

    private static bool Hovering(Vector2 min, Vector2 max) => UiInteract.Hover(min, max);

    private readonly struct BubbleStamp
    {
        public readonly string Time;
        public readonly string? TickGlyph;
        public readonly bool Seen;
        public readonly float Width;
        public readonly float Height;
        public readonly float TickWidth;

        public BubbleStamp(string time, string? tickGlyph, bool seen, float width, float height, float tickWidth)
        {
            Time = time;
            TickGlyph = tickGlyph;
            Seen = seen;
            Width = width;
            Height = height;
            TickWidth = tickWidth;
        }
    }

}
