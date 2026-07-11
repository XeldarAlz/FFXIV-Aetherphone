using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

internal readonly ref struct ChatTranscriptModel
{
    public readonly string ThreadId;
    public readonly ReadOnlySpan<TranscriptMessage> Messages;
    public readonly string MyUserId;
    public readonly Vector4 Accent;
    public readonly PhoneTheme Theme;
    public readonly Vector4 MutedInk;
    public readonly Vector4 BodyInk;
    public readonly bool OtherTyping;
    public readonly bool Loading;
    public readonly bool IsGroup;
    public readonly RemoteImageCache Images;
    public readonly Func<string, string?> MediaUrl;
    public readonly Action<string> OnImageClick;
    public readonly string EmptyText;
    public readonly string LoadingText;
    public readonly Action<string>? OnMessageContext;
    public readonly Action<string>? OnQuoteClick;
    public readonly Action<string, string>? OnReactionClick;
    public readonly Func<string, VoiceNoteState>? VoiceState;
    public readonly Action<string>? OnVoiceToggle;

    public ChatTranscriptModel(string threadId, ReadOnlySpan<TranscriptMessage> messages, string myUserId,
        Vector4 accent, PhoneTheme theme, Vector4 mutedInk, Vector4 bodyInk, bool otherTyping, bool loading,
        bool isGroup, RemoteImageCache images, Func<string, string?> mediaUrl, Action<string> onImageClick,
        string emptyText, string loadingText, Action<string>? onMessageContext = null,
        Action<string>? onQuoteClick = null, Action<string, string>? onReactionClick = null,
        Func<string, VoiceNoteState>? voiceState = null, Action<string>? onVoiceToggle = null)
    {
        ThreadId = threadId;
        Messages = messages;
        MyUserId = myUserId;
        Accent = accent;
        Theme = theme;
        MutedInk = mutedInk;
        BodyInk = bodyInk;
        OtherTyping = otherTyping;
        Loading = loading;
        IsGroup = isGroup;
        Images = images;
        MediaUrl = mediaUrl;
        OnImageClick = onImageClick;
        EmptyText = emptyText;
        LoadingText = loadingText;
        OnMessageContext = onMessageContext;
        OnQuoteClick = onQuoteClick;
        OnReactionClick = onReactionClick;
        VoiceState = voiceState;
        OnVoiceToggle = onVoiceToggle;
    }
}

internal sealed class ChatTranscript
{
    private const long GroupWindowSeconds = 240;
    private const int KindText = 0;
    private const int KindImage = 1;
    private const int KindSystem = 2;
    private const int KindVoice = 3;
    private const float StampTextScale = 0.70f;
    private const float StampTickScale = 0.58f;
    private const float BubbleGap = 3f;
    private const float QuoteSenderScale = 0.75f;
    private const float QuotePreviewScale = 0.80f;
    private static readonly Vector4 SeenTickColor = new(0.45f, 0.83f, 1f, 1f);

    private const float FlashSeconds = 1.6f;

    private readonly List<BubbleEntrance> entrances = new();
    private string? entranceThreadId;
    private int entranceSettled;
    private bool entrancePrimed;
    private string? followThreadId;
    private bool followBottom;
    private bool snapToBottom;
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
        SyncEntrances(model.ThreadId, model.Messages.Length, delta, model.Loading);
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
        }
    }

    private void SyncEntrances(string threadId, int count, float delta, bool loading)
    {
        if (entranceThreadId != threadId)
        {
            entranceThreadId = threadId;
            entranceSettled = count;
            entrancePrimed = count > 0 || !loading;
            entrances.Clear();
            return;
        }

        if (!entrancePrimed)
        {
            entranceSettled = count;
            entrancePrimed = count > 0 || !loading;
            return;
        }

        if (count < entranceSettled)
        {
            entranceSettled = count;
        }

        while (entranceSettled < count)
        {
            entrances.Add(new BubbleEntrance { Line = entranceSettled, Elapsed = 0f });
            entranceSettled++;
        }

        for (var index = entrances.Count - 1; index >= 0; index--)
        {
            var entrance = entrances[index];
            entrance.Elapsed += delta;
            if (entrance.Elapsed >= TransitionTiming.BubbleSeconds || entrance.Line >= count)
            {
                entrances.RemoveAt(index);
            }
            else
            {
                entrances[index] = entrance;
            }
        }
    }

    private float EntranceProgress(int line)
    {
        for (var index = 0; index < entrances.Count; index++)
        {
            if (entrances[index].Line == line)
            {
                return entrances[index].Elapsed / TransitionTiming.BubbleSeconds;
            }
        }

        return 1f;
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
        Typography.Draw(new Vector2(origin.X + 4f * scale, origin.Y), FirstName(message.SenderName),
            message.SenderTint, 0.78f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + 16f * scale));
    }

    private void DrawSystemMessage(TranscriptMessage message, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail().X;
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
        var available = ImGui.GetContentRegionAvail().X;
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
        var available = ImGui.GetContentRegionAvail().X;
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
        var entrance = EntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutQuint(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        var placeholder = (message.Flags & TranscriptFlags.Placeholder) != 0;
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        if (placeholder || deleted)
        {
            fill = Palette.WithAlpha(fill, fill.W * 0.55f);
            ink = model.MutedInk;
        }

        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), anchor, pop, rise, alpha,
                mine, model, scale);
            contentTop += forwardBlock;
        }

        if (quote.Height > 0f)
        {
            DrawQuote(drawList, message, quote, new Vector2(bubbleMin.X + paddingX, contentTop),
                contentWidth, anchor, pop, rise, alpha, mine, model);
            contentTop += quoteBlock;
        }

        if (deleted)
        {
            var iconCenter = new Vector2(bubbleMin.X + paddingX + 6f * scale, contentTop + textSize.Y * 0.5f);
            AppSkin.Icon(drawList, anchor + (iconCenter - anchor) * pop + rise, FontAwesomeIcon.Ban.ToIconString(),
                Palette.WithAlpha(ink, ink.W * alpha * 0.9f), 0.68f * pop);
        }

        var textPos = anchor + (new Vector2(bubbleMin.X + paddingX + deletedIconWidth, contentTop) - anchor)
            * pop + rise;
        if (linkLayout is null)
        {
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * pop, textPos,
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha)), message.Body, wrap * pop);
        }
        else
        {
            var linkInk = mine || placeholder ? ink : model.Accent;
            LinkText.Draw(drawList, linkLayout, textPos, pop, ink, linkInk, alpha, entrance >= 1f);
        }
        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), anchor, pop, rise,
            alpha, timeColor);
        if (entrance >= 1f && !deleted && model.OnMessageContext is not null && message.Kind != KindSystem
            && ImGui.IsMouseHoveringRect(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            model.OnMessageContext(message.Id);
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
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

    private void DrawForwardLabel(ImDrawListPtr drawList, Vector2 origin, Vector2 anchor, float pop, Vector2 rise,
        float alpha, bool mine, in ChatTranscriptModel model, float scale)
    {
        var ink = mine ? new Vector4(1f, 1f, 1f, 0.70f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var label = Loc.T(L.Message.ForwardedLabel);
        var size = Typography.Measure(label, 0.72f);
        var iconCenter = new Vector2(origin.X + 5f * scale, origin.Y + size.Y * 0.5f);
        AppSkin.Icon(drawList, anchor + (iconCenter - anchor) * pop + rise, FontAwesomeIcon.Share.ToIconString(),
            Palette.WithAlpha(ink, ink.W * alpha), 0.58f * pop);
        var textPos = anchor + (new Vector2(origin.X + 15f * scale, origin.Y) - anchor) * pop + rise;
        Typography.Draw(drawList, textPos, label, Palette.WithAlpha(ink, ink.W * alpha), 0.72f * pop);
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

            if (model.OnReactionClick is not null && ImGui.IsMouseHoveringRect(chipMin, chipMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnReactionClick(message.Id, reaction.Mine ? string.Empty : reaction.Token);
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
        var available = ImGui.GetContentRegionAvail().X;
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
        var entrance = EntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutQuint(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * pop, mine, model);
        var contentTop = bubbleMin.Y + paddingY;
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + paddingX, contentTop), anchor, pop, rise, alpha,
                mine, model, scale);
            contentTop += forwardBlock;
        }

        var state = model.VoiceState?.Invoke(message.Id) ?? default;
        var playCenter = new Vector2(bubbleMin.X + paddingX + playRadius, contentTop + playRadius);
        var playFill = mine ? new Vector4(1f, 1f, 1f, 0.22f) : Palette.WithAlpha(model.Accent, 0.9f);
        drawList.AddCircleFilled(playCenter, playRadius, ImGui.GetColorU32(Palette.WithAlpha(playFill,
            playFill.W * alpha)), 28);
        AppSkin.Icon(drawList, playCenter, (state.Playing ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play)
            .ToIconString(), new Vector4(1f, 1f, 1f, alpha), 0.7f);
        var trackLeft = playCenter.X + playRadius + 9f * scale;
        var trackRight = bubbleMax.X - paddingX;
        var trackY = playCenter.Y;
        var trackColor = mine ? new Vector4(1f, 1f, 1f, 0.35f) : Palette.WithAlpha(model.MutedInk, 0.55f);
        drawList.AddRectFilled(new Vector2(trackLeft, trackY - 2f * scale), new Vector2(trackRight, trackY + 2f * scale),
            ImGui.GetColorU32(Palette.WithAlpha(trackColor, trackColor.W * alpha)), 2f * scale);
        var progress = state.Current ? state.Progress : 0f;
        if (progress > 0f)
        {
            var fillRight = trackLeft + (trackRight - trackLeft) * progress;
            drawList.AddRectFilled(new Vector2(trackLeft, trackY - 2f * scale), new Vector2(fillRight, trackY + 2f * scale),
                ImGui.GetColorU32(Palette.WithAlpha(ink, 0.95f * alpha)), 2f * scale);
            drawList.AddCircleFilled(new Vector2(fillRight, trackY), 4.5f * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 16);
        }

        var duration = state.Current && state.Playing
            ? (int)MathF.Round(progress * message.DurationSecs)
            : message.DurationSecs;
        var durationText = TimeText.MinutesSeconds(duration);
        Typography.Draw(drawList, new Vector2(trackLeft, bubbleMax.Y - paddingY - stamp.Height),
            durationText, Palette.WithAlpha(mine ? new Vector4(1f, 1f, 1f, 0.72f) : model.MutedInk, alpha),
            StampTextScale);
        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), anchor, pop, rise,
            alpha, timeColor);
        var playHitMin = playCenter - new Vector2(playRadius, playRadius);
        var playHitMax = playCenter + new Vector2(playRadius, playRadius);
        if (entrance >= 1f && model.OnVoiceToggle is not null && ImGui.IsMouseHoveringRect(playHitMin, playHitMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                model.OnVoiceToggle(message.Id);
            }
        }

        if (entrance >= 1f && model.OnMessageContext is not null
            && ImGui.IsMouseHoveringRect(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            model.OnMessageContext(message.Id);
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, alpha, model, scale);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + chipRow + BubbleGap * scale));
    }

    private void DrawImageBubble(TranscriptMessage message, int index, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var mine = message.SenderId == model.MyUserId;
        var drawList = ImGui.GetWindowDrawList();
        var available = ImGui.GetContentRegionAvail().X;
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
        var entrance = EntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutQuint(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        var bubbleMin = start + new Vector2(offsetX, 0f);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        ConsumeScrollTarget(message.Id, bubbleMin.Y);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        if (forwardBlock > 0f)
        {
            DrawForwardLabel(drawList, new Vector2(bubbleMin.X + padding, bubbleMin.Y + padding), anchor, pop, rise,
                alpha, mine, model, scale);
        }

        var imageMin = scaledMin + new Vector2(padding * pop, (padding + forwardBlock) * pop);
        var imageMax = imageMin + new Vector2(imageWidth * pop, imageHeight * pop);
        var rounding = 10f * scale * pop;
        var texture = model.Images.Get(model.MediaUrl(message.Id));
        if (texture is null)
        {
            Squircle.Fill(drawList, imageMin, imageMax, rounding,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f * alpha)));
            AppSkin.Icon((imageMin + imageMax) * 0.5f, FontAwesomeIcon.Image.ToIconString(),
                Palette.WithAlpha(model.MutedInk, alpha), 1.2f);
        }
        else
        {
            drawList.AddImageRounded(texture.Handle, imageMin, imageMax, Vector2.Zero, Vector2.One,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
            if (entrance >= 1f && ImGui.IsMouseHoveringRect(imageMin, imageMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    model.OnImageClick(message.Id);
                }
            }
        }

        DrawFlash(drawList, message.Id, scaledMin, scaledMax, 14f * scale * pop, mine, model);
        if (entrance >= 1f && model.OnMessageContext is not null
            && ImGui.IsMouseHoveringRect(bubbleMin, bubbleMax)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            model.OnMessageContext(message.Id);
        }

        if (caption.Length > 0)
        {
            var ink = mine ? new Vector4(1f, 1f, 1f, 1f) : model.Theme.TextStrong;
            Typography.Draw(drawList, new Vector2(imageMin.X, imageMax.Y + 4f * scale * pop),
                UiText.Truncate(caption, 60), Palette.WithAlpha(ink, alpha), 0.9f * pop);
            var timeColor = mine
                ? new Vector4(1f, 1f, 1f, 0.72f)
                : Palette.WithAlpha(model.MutedInk, 0.95f);
            DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - padding - 4f * scale, bubbleMax.Y - padding),
                anchor, pop, rise, alpha, timeColor);
        }
        else
        {
            var stampPad = new Vector2(7f * scale, 3f * scale);
            var pillMax = bubbleMin + new Vector2(padding + imageWidth, padding + forwardBlock + imageHeight) -
                          new Vector2(6f * scale, 6f * scale);
            var pillMin = pillMax - new Vector2(stamp.Width + stampPad.X * 2f, stamp.Height + stampPad.Y * 2f);
            Squircle.Fill(drawList, anchor + (pillMin - anchor) * pop + rise, anchor + (pillMax - anchor) * pop + rise,
                (pillMax.Y - pillMin.Y) * 0.5f * pop, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f * alpha)));
            DrawStamp(drawList, stamp, pillMax - stampPad, anchor, pop, rise, alpha, new Vector4(1f, 1f, 1f, 0.92f));
        }

        var chipRow = DrawReactionChips(drawList, message, mine, bubbleMin, bubbleMax, alpha, model, scale);
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
        var iconWidth = message.ReplyKind is KindImage or KindVoice ? 15f * scale : 0f;
        var innerWidth = MathF.Max(senderSize.X, iconWidth + previewSize.X);
        var desired = 3f * scale + 7f * scale + innerWidth + 8f * scale;
        var height = 5f * scale * 2f + senderSize.Y + 1f * scale + previewSize.Y;
        return new QuoteMeasure(height, MathF.Min(desired, wrap), senderSize.Y);
    }

    private void DrawQuote(ImDrawListPtr drawList, in TranscriptMessage message, in QuoteMeasure quote,
        Vector2 origin, float width, Vector2 anchor, float pop, Vector2 rise, float alpha, bool mine,
        in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var quoteMin = origin;
        var quoteMax = origin + new Vector2(width, quote.Height);
        var scaledMin = anchor + (quoteMin - anchor) * pop + rise;
        var scaledMax = anchor + (quoteMax - anchor) * pop + rise;
        var fill = mine ? new Vector4(0f, 0f, 0f, 0.20f) : new Vector4(1f, 1f, 1f, 0.07f);
        Squircle.Fill(drawList, scaledMin, scaledMax, 8f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        var barColor = mine ? new Vector4(1f, 1f, 1f, 0.92f) : model.Accent;
        var barMax = new Vector2(quoteMin.X + 3f * scale, quoteMax.Y);
        Squircle.Fill(drawList, anchor + (quoteMin - anchor) * pop + rise, anchor + (barMax - anchor) * pop + rise,
            1.5f * scale * pop, ImGui.GetColorU32(Palette.WithAlpha(barColor, barColor.W * alpha)));
        var textLeft = quoteMin.X + 3f * scale + 7f * scale;
        var textWidth = quoteMax.X - 8f * scale - textLeft;
        var senderInk = mine ? new Vector4(1f, 1f, 1f, 0.95f) : model.Accent;
        var senderPos = anchor + (new Vector2(textLeft, quoteMin.Y + 5f * scale) - anchor) * pop + rise;
        Typography.Draw(drawList, senderPos, Typography.FitText(message.ReplySenderName, textWidth,
            QuoteSenderScale, FontWeight.SemiBold), Palette.WithAlpha(senderInk, senderInk.W * alpha),
            QuoteSenderScale * pop, FontWeight.SemiBold);
        var previewInk = mine ? new Vector4(1f, 1f, 1f, 0.78f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        var previewTop = quoteMin.Y + 5f * scale + quote.SenderHeight + 1f * scale;
        var previewLeft = textLeft;
        if (message.ReplyKind is KindImage or KindVoice)
        {
            var iconCenter = new Vector2(textLeft + 5f * scale, previewTop + 7f * scale);
            var glyph = message.ReplyKind == KindVoice
                ? FontAwesomeIcon.Microphone.ToIconString()
                : FontAwesomeIcon.Camera.ToIconString();
            AppSkin.Icon(drawList, anchor + (iconCenter - anchor) * pop + rise, glyph,
                Palette.WithAlpha(previewInk, previewInk.W * alpha), 0.62f * pop);
            previewLeft += 15f * scale;
        }

        var previewPos = anchor + (new Vector2(previewLeft, previewTop) - anchor) * pop + rise;
        Typography.Draw(drawList, previewPos, Typography.FitText(message.ReplyBody,
            textWidth - (previewLeft - textLeft), QuotePreviewScale, FontWeight.Regular),
            Palette.WithAlpha(previewInk, previewInk.W * alpha), QuotePreviewScale * pop);
        if (model.OnQuoteClick is not null && ImGui.IsMouseHoveringRect(quoteMin, quoteMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                model.OnQuoteClick(message.ReplyToId!);
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
        var timeSize = Typography.Measure(time, StampTextScale);
        if ((message.Flags & TranscriptFlags.Deleted) != 0)
        {
            return new BubbleStamp(time, null, false, timeSize.X, timeSize.Y, 0f);
        }

        var encrypted = (message.Flags & TranscriptFlags.Encrypted) != 0;
        var lockWidth = 0f;
        if (encrypted)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                lockWidth = ImGui.CalcTextSize(FontAwesomeIcon.Lock.ToIconString()).X * StampTickScale + 4f * scale;
            }
        }

        if (!mine)
        {
            return new BubbleStamp(time, null, false, lockWidth + timeSize.X, timeSize.Y, 0f, lockWidth);
        }

        var seen = message.ReadAtUnix is not null;
        var glyph = (seen ? FontAwesomeIcon.CheckDouble : FontAwesomeIcon.Check).ToIconString();
        float tickWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            tickWidth = ImGui.CalcTextSize(glyph).X * StampTickScale;
        }

        return new BubbleStamp(time, glyph, seen, lockWidth + timeSize.X + 4f * scale + tickWidth, timeSize.Y,
            tickWidth, lockWidth);
    }

    private static void DrawStamp(ImDrawListPtr drawList, in BubbleStamp stamp, Vector2 bottomRight, Vector2 anchor,
        float pop, Vector2 rise, float alpha, Vector4 timeColor)
    {
        var topLeft = new Vector2(bottomRight.X - stamp.Width, bottomRight.Y - stamp.Height);
        if (stamp.LockWidth > 0f)
        {
            var lockCenter = new Vector2(topLeft.X + stamp.LockWidth * 0.5f, bottomRight.Y - stamp.Height * 0.45f);
            AppSkin.Icon(drawList, anchor + (lockCenter - anchor) * pop + rise,
                FontAwesomeIcon.Lock.ToIconString(), Palette.WithAlpha(timeColor, timeColor.W * alpha * 0.9f),
                StampTickScale * pop);
            topLeft = new Vector2(topLeft.X + stamp.LockWidth, topLeft.Y);
        }

        var timePos = anchor + (topLeft - anchor) * pop + rise;
        Typography.Draw(drawList, timePos, stamp.Time, Palette.WithAlpha(timeColor, timeColor.W * alpha),
            StampTextScale * pop);
        if (stamp.TickGlyph is null)
        {
            return;
        }

        var tickCenter = new Vector2(bottomRight.X - stamp.TickWidth * 0.5f, bottomRight.Y - stamp.Height * 0.45f);
        var tickColor = stamp.Seen ? SeenTickColor : timeColor;
        AppSkin.Icon(drawList, anchor + (tickCenter - anchor) * pop + rise, stamp.TickGlyph,
            Palette.WithAlpha(tickColor, tickColor.W * alpha), StampTickScale * pop);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name.Substring(0, space) : name;
    }

    private readonly struct BubbleStamp
    {
        public readonly string Time;
        public readonly string? TickGlyph;
        public readonly bool Seen;
        public readonly float Width;
        public readonly float Height;
        public readonly float TickWidth;
        public readonly float LockWidth;

        public BubbleStamp(string time, string? tickGlyph, bool seen, float width, float height, float tickWidth,
            float lockWidth = 0f)
        {
            Time = time;
            TickGlyph = tickGlyph;
            Seen = seen;
            Width = width;
            Height = height;
            TickWidth = tickWidth;
            LockWidth = lockWidth;
        }
    }

    private struct BubbleEntrance
    {
        public int Line;
        public float Elapsed;
    }
}
