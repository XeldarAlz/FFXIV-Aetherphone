using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
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

internal static class ReactionArt
{
    public static readonly string[] Tokens = { "+1", "heart", "laugh", "wow", "sad", "pray" };

    public static string Glyph(string token)
    {
        return token switch
        {
            "heart" => FontAwesomeIcon.Heart.ToIconString(),
            "laugh" => FontAwesomeIcon.Laugh.ToIconString(),
            "wow" => FontAwesomeIcon.Surprise.ToIconString(),
            "sad" => FontAwesomeIcon.SadTear.ToIconString(),
            "pray" => FontAwesomeIcon.PrayingHands.ToIconString(),
            _ => FontAwesomeIcon.ThumbsUp.ToIconString(),
        };
    }

    public static Vector4 Color(string token)
    {
        return token switch
        {
            "heart" => new Vector4(0.94f, 0.35f, 0.44f, 1f),
            "laugh" => new Vector4(0.97f, 0.79f, 0.26f, 1f),
            "wow" => new Vector4(0.97f, 0.72f, 0.32f, 1f),
            "sad" => new Vector4(0.48f, 0.71f, 0.98f, 1f),
            "pray" => new Vector4(0.88f, 0.76f, 0.48f, 1f),
            _ => new Vector4(0.42f, 0.66f, 0.98f, 1f),
        };
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

    public TranscriptMessage(string id, string senderId, string body, int kind, long createdAtUnix, int mediaWidth,
        int mediaHeight, long? readAtUnix, string senderName, Vector4 senderTint, byte flags = 0,
        string? replyToId = null, string replySenderName = "", string replyBody = "", int replyKind = 0,
        int durationSecs = 0, TranscriptReaction[]? reactions = null)
    {
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
    bool Available);

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
    public IChatTranscriptMedia? Media { get; init; }
    public IChatTranscriptInteractions? Interactions { get; init; }
    public IChatTranscriptVoice? Voice { get; init; }
    public IChatTranscriptPaging? Paging { get; init; }
    public IChatTranscriptPostCards? PostCards { get; init; }
    public IChatTranscriptStoryReplies? StoryReplies { get; init; }
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
        var scale = ImGuiHelpers.GlobalScale;
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

        using (AppSurface.Begin(listRect))
        {
            if (model.Messages.Length == 0 && typingReveal < 0.01f)
            {
                Typography.DrawCentered(new Vector2(listRect.Center.X, listRect.Min.Y + 60f * scale),
                    model.Loading ? model.LoadingText : model.EmptyText, model.MutedInk);
                return;
            }

            SyncFollow(model.ThreadId);
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

                if (model.IsGroup && !grouped && message.SenderId != model.MyUserId)
                {
                    DrawSenderLabel(message);
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

        var scale = ImGuiHelpers.GlobalScale;
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
        var scale = ImGuiHelpers.GlobalScale;
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

    private void SyncFollow(string threadId)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

        if (snapToBottom)
        {
            followBottom = true;
            snapToBottom = false;
        }
    }

    private static void DrawSenderLabel(TranscriptMessage message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var textLeft = origin.X + 4f * scale;
        var maxWidth = ScrollLayout.StableContentWidth() - 4f * scale;
        var rect = new Vector2(textLeft, origin.Y);
        var hovering = ImGui.IsMouseHoveringRect(rect, new Vector2(rect.X + maxWidth, rect.Y + 16f * scale));
        var name = FirstName(message.SenderName);
        Marquee.DrawLeft("chattranscript.sender." + message.SenderId, name, textLeft, origin.Y, maxWidth,
            new TextStyle(0.78f, FontWeight.SemiBold), message.SenderTint, hovering);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + 16f * scale));
    }

    private void DrawSystemMessage(TranscriptMessage message, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
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

        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var textSize = Typography.Measure(label, 0.72f, FontWeight.Medium);
        var chipWidth = textSize.X + 20f * scale;
        var chipHeight = textSize.Y + 8f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var chipMin = new Vector2(origin.X + (available - chipWidth) * 0.5f, origin.Y + 4f * scale);
        var chipMax = chipMin + new Vector2(chipWidth, chipHeight);
        Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
        Typography.DrawCentered(drawList, (chipMin + chipMax) * 0.5f, label, model.MutedInk, 0.72f, FontWeight.Medium);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, chipMax.Y + 10f * scale));
    }

    private void DrawTextBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var mine = message.SenderId == model.MyUserId;
        var deleted = (message.Flags & TranscriptFlags.Deleted) != 0;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 11f * scale;
        var paddingY = 7f * scale;
        var wrap = available * 0.74f - paddingX * 2f;
        var linkLayout = deleted ? null : LinkText.LayoutFor(message.Body, wrap);
        var textSize = linkLayout is null ? ImGui.CalcTextSize(message.Body, false, wrap) : linkLayout.Size;
        var deletedIconWidth = deleted ? 17f * scale : 0f;
        var stamp = MeasureStamp(message, mine, scale);
        var stampGap = 7f * scale;
        var inline = textSize.Y <= ImGui.GetTextLineHeight() * 1.5f &&
                     deletedIconWidth + textSize.X + stampGap + stamp.Width <= wrap;
        var contentWidth = inline
            ? deletedIconWidth + textSize.X + stampGap + stamp.Width
            : MathF.Max(deletedIconWidth + textSize.X, stamp.Width);
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
        var contentHeight = (inline ? textSize.Y : textSize.Y + stamp.Height + 2f * scale) + quoteBlock + forwardBlock;
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
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
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
            AppSkin.Icon(drawList, fx.Apply(iconCenter), FontAwesomeIcon.Ban.ToIconString(),
                Palette.WithAlpha(ink, ink.W * fx.Alpha * 0.9f), 0.68f * fx.Pop);
        }

        var textPos = fx.Apply(new Vector2(bubbleMin.X + paddingX + deletedIconWidth, contentTop));
        if (linkLayout is null)
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * fx.Pop, textPos,
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * fx.Alpha)), message.Body, wrap * fx.Pop);
        }
        else
        {
            var linkInk = mine || placeholder ? ink : model.Accent;
            LinkText.Draw(drawList, linkLayout, textPos, fx.Pop, ink, linkInk, fx.Alpha, entrance >= 1f);
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
        var scale = ImGuiHelpers.GlobalScale;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ScrollLayout.StableContentWidth();
        var paddingX = 7f * scale;
        var paddingY = 7f * scale;
        var innerWidth = MathF.Min(available * 0.62f, 210f * scale);
        var stamp = MeasureStamp(message, mine, scale);
        var snippet = card.Available ? card.Snippet : string.Empty;
        var snippetHeight = 0f;
        if (snippet.Length > 0)
        {
            var lineHeight = Typography.Measure("Ag", TextStyles.Footnote).Y;
            snippetHeight = MathF.Min(Typography.MeasureWrappedBlock(snippet, TextStyles.Footnote, innerWidth).Y,
                lineHeight * 2f);
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
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
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
            var texture = card.ThumbnailUrl is null ? null : cards.Thumbnail(card.ThumbnailUrl);
            if (texture is null)
            {
                Squircle.Fill(drawList, thumbMin, thumbMax, rounding,
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * fx.Alpha)));
                AppSkin.Icon((thumbMin + thumbMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(),
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
                Typography.DrawWrappedLeft(fx.Apply(snippetMin), snippet,
                    Palette.WithAlpha(mutedInk, mutedInk.W * fx.Alpha), TextStyles.Footnote, innerWidth);
                drawList.PopClipRect();
                contentTop += snippetHeight;
            }
        }
        else
        {
            var iconCenter = new Vector2(bubbleMin.X + paddingX + 6f * scale,
                contentTop + unavailableSize.Y * 0.5f);
            AppSkin.Icon(drawList, fx.Apply(iconCenter), FontAwesomeIcon.EyeSlash.ToIconString(),
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
        var scale = ImGuiHelpers.GlobalScale;
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

        var textHeight = eyebrowSize.Y + 3f * scale + zoneSize.Y
                         + (worldSize.Y > 0f ? 2f * scale + worldSize.Y : 0f)
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
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
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
        AppSkin.Icon(drawList, fx.Apply(badgeCenter), FontAwesomeIcon.MapMarkerAlt.ToIconString(),
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

        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), fx, timeColor);
        if (entrance >= 1f && Hovering(bubbleMin, bubbleMax))
        {
            if (location.MapId != 0)
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
        var scale = ImGuiHelpers.GlobalScale;
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
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f)));
            AppSkin.Icon(drawList, new Vector2(chipMin.X + 12f * scale + 5f * scale, chipMin.Y + chipHeight * 0.5f),
                FontAwesomeIcon.EyeSlash.ToIconString(), Palette.WithAlpha(model.MutedInk, 0.9f), 0.62f);
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
                    ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)));
                AppSkin.Icon((thumbMin + thumbMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(), model.MutedInk,
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
        AppSkin.Icon(drawList, fx.Apply(iconCenter), FontAwesomeIcon.Share.ToIconString(),
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

        var chipHeight = 20f * scale;
        var chipGap = 4f * scale;
        var top = bubbleMax.Y + 3f * scale;
        var totalWidth = 0f;
        Span<float> widths = stackalloc float[reactions.Length];
        for (var index = 0; index < reactions.Length; index++)
        {
            var width = 26f * scale;
            if (reactions[index].Count > 1)
            {
                width += Typography.Measure(reactions[index].Count.ToString(Loc.Culture), 0.68f).X + 3f * scale;
            }

            widths[index] = width;
            totalWidth += width + (index > 0 ? chipGap : 0f);
        }

        var cursor = mine ? bubbleMax.X - totalWidth : bubbleMin.X;
        for (var index = 0; index < reactions.Length; index++)
        {
            var reaction = reactions[index];
            var chipMin = new Vector2(cursor, top);
            var chipMax = new Vector2(cursor + widths[index], top + chipHeight);
            var fill = new Vector4(0.13f, 0.13f, 0.16f, 0.92f);
            Squircle.Fill(drawList, chipMin, chipMax, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
            if (reaction.Mine)
            {
                Squircle.Stroke(drawList, chipMin, chipMax, chipHeight * 0.5f,
                    ImGui.GetColorU32(Palette.WithAlpha(model.Accent, 0.9f * alpha)), 1.2f * scale);
            }

            var tokenColor = ReactionArt.Color(reaction.Token);
            AppSkin.Icon(drawList, new Vector2(chipMin.X + 13f * scale, top + chipHeight * 0.5f),
                ReactionArt.Glyph(reaction.Token), Palette.WithAlpha(tokenColor, tokenColor.W * alpha), 0.62f);
            if (reaction.Count > 1)
            {
                Typography.Draw(drawList, new Vector2(chipMin.X + 23f * scale,
                    top + chipHeight * 0.5f - Typography.Measure("0", 0.68f).Y * 0.5f),
                    reaction.Count.ToString(Loc.Culture), new Vector4(0.94f, 0.94f, 0.97f, alpha), 0.68f);
            }

            if (model.Interactions is { } interactions && Hovering(chipMin, chipMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    interactions.OnReactionClick(message.Id, reaction.Mine ? string.Empty : reaction.Token);
                }
            }

            cursor = chipMax.X + chipGap;
        }

        return chipHeight + 4f * scale;
    }

    private void DrawVoiceBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
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
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
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
        AppSkin.Icon(drawList, playCenter, (state.Playing ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play)
            .ToIconString(), new Vector4(1f, 1f, 1f, fx.Alpha), 0.7f);
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
        var scale = ImGuiHelpers.GlobalScale;
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
        var captionHeight = caption.Length > 0 ? Typography.Measure(caption, 0.9f).Y + 6f * scale : 0f;
        var stampRowHeight = caption.Length > 0 ? stamp.Height + 3f * scale : 0f;
        var forwardLabel = MeasureForwardLabel(message, scale);
        var forwardBlock = forwardLabel.Y > 0f ? forwardLabel.Y + 4f * scale : 0f;
        var bubbleWidth = imageWidth + padding * 2f;
        var bubbleHeight = imageHeight + padding * 2f + captionHeight + stampRowHeight + forwardBlock;
        var start = ImGui.GetCursorScreenPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
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
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * fx.Alpha)));
            AppSkin.Icon((imageMin + imageMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(),
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
            Marquee.DrawLeftAuto("chattranscript.caption." + message.Id, caption, imageMin.X, captionTop,
                captionMaxWidth, new TextStyle(0.9f * fx.Pop, FontWeight.Regular),
                Palette.WithAlpha(ink, fx.Alpha));
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
        var scale = ImGuiHelpers.GlobalScale;
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
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f * eased)));
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
        var scale = ImGuiHelpers.GlobalScale;
        var quoteMin = origin;
        var quoteMax = origin + new Vector2(width, quote.Height);
        var scaledMin = fx.Apply(quoteMin);
        var scaledMax = fx.Apply(quoteMax);
        var fill = mine ? new Vector4(0f, 0f, 0f, 0.20f) : new Vector4(1f, 1f, 1f, 0.07f);
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
                KindVoice => FontAwesomeIcon.Microphone.ToIconString(),
                KindLocation => FontAwesomeIcon.MapMarkerAlt.ToIconString(),
                _ => FontAwesomeIcon.Camera.ToIconString(),
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
        var glyph = (seen ? FontAwesomeIcon.CheckDouble : FontAwesomeIcon.Check).ToIconString();
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

    private static bool Hovering(Vector2 min, Vector2 max) =>
        !UiInteract.InputBlocked && ImGui.IsMouseHoveringRect(min, max);

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
