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

    public TranscriptMessage(string id, string senderId, string body, int kind, long createdAtUnix, int mediaWidth,
        int mediaHeight, long? readAtUnix, string senderName, Vector4 senderTint)
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

    public ChatTranscriptModel(string threadId, ReadOnlySpan<TranscriptMessage> messages, string myUserId,
        Vector4 accent, PhoneTheme theme, Vector4 mutedInk, Vector4 bodyInk, bool otherTyping, bool loading,
        bool isGroup, RemoteImageCache images, Func<string, string?> mediaUrl, Action<string> onImageClick,
        string emptyText, string loadingText)
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
    }
}

internal sealed class ChatTranscript
{
    private const long GroupWindowSeconds = 240;
    private const int KindText = 0;
    private const int KindImage = 1;
    private const int KindSystem = 2;
    private const float StampTextScale = 0.70f;
    private const float StampTickScale = 0.58f;
    private const float BubbleGap = 3f;
    private static readonly Vector4 SeenTickColor = new(0.45f, 0.83f, 1f, 1f);

    private readonly List<BubbleEntrance> entrances = new();
    private string? entranceThreadId;
    private int entranceSettled;
    private bool entrancePrimed;
    private string? followThreadId;
    private bool followBottom;
    private bool snapToBottom;
    private float typingReveal;
    private float typingPhase;

    public void RequestSnapToBottom() => snapToBottom = true;

    public void Draw(Rect listRect, in ChatTranscriptModel model)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var delta = ImGui.GetIO().DeltaTime;
        SyncEntrances(model.ThreadId, model.Messages.Length, delta, model.Loading);
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
                else
                {
                    DrawTextBubble(message, index, model);
                }
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
        var drawList = ImGui.GetWindowDrawList();
        var available = ImGui.GetContentRegionAvail().X;
        var paddingX = 11f * scale;
        var paddingY = 7f * scale;
        var wrap = available * 0.74f - paddingX * 2f;
        var textSize = ImGui.CalcTextSize(message.Body, false, wrap);
        var stamp = MeasureStamp(message, mine, scale);
        var stampGap = 7f * scale;
        var inline = textSize.Y <= ImGui.GetTextLineHeight() * 1.5f &&
                     textSize.X + stampGap + stamp.Width <= wrap;
        var contentWidth = inline ? textSize.X + stampGap + stamp.Width : MathF.Max(textSize.X, stamp.Width);
        var contentHeight = inline ? textSize.Y : textSize.Y + stamp.Height + 2f * scale;
        var bubbleWidth = contentWidth + paddingX * 2f;
        var bubbleHeight = contentHeight + paddingY * 2f;
        var start = ImGui.GetCursorScreenPos();
        var bubbleMin = new Vector2(mine ? start.X + available - bubbleWidth : start.X, start.Y);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
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
        var textPos = anchor + (new Vector2(bubbleMin.X + paddingX, bubbleMin.Y + paddingY) - anchor) * pop + rise;
        drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize() * pop, textPos,
            ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * alpha)), message.Body, wrap * pop);
        var timeColor = mine ? new Vector4(1f, 1f, 1f, 0.72f) : Palette.WithAlpha(model.MutedInk, 0.95f);
        DrawStamp(drawList, stamp, new Vector2(bubbleMax.X - paddingX, bubbleMax.Y - paddingY), anchor, pop, rise,
            alpha, timeColor);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + BubbleGap * scale));
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
        var bubbleWidth = imageWidth + padding * 2f;
        var bubbleHeight = imageHeight + padding * 2f + captionHeight + stampRowHeight;
        var start = ImGui.GetCursorScreenPos();
        var offsetX = mine ? available - bubbleWidth : 0f;
        var fill = mine ? model.Accent : new Vector4(1f, 1f, 1f, 0.10f);
        var entrance = EntranceProgress(index);
        var pop = entrance < 1f ? 0.80f + 0.20f * Easing.EaseOutQuint(entrance) : 1f;
        var alpha = entrance < 1f ? MathF.Min(entrance * 1.8f, 1f) : 1f;
        var rise = new Vector2(0f, entrance < 1f ? (1f - Easing.EaseOutCubic(entrance)) * 10f * scale : 0f);
        var bubbleMin = start + new Vector2(offsetX, 0f);
        var bubbleMax = bubbleMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(mine ? bubbleMax.X : bubbleMin.X, bubbleMax.Y);
        var scaledMin = anchor + (bubbleMin - anchor) * pop + rise;
        var scaledMax = anchor + (bubbleMax - anchor) * pop + rise;
        Squircle.Fill(drawList, scaledMin, scaledMax, 14f * scale * pop,
            ImGui.GetColorU32(Palette.WithAlpha(fill, fill.W * alpha)));
        var imageMin = scaledMin + new Vector2(padding * pop, padding * pop);
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
            var pillMax = bubbleMin + new Vector2(padding + imageWidth, padding + imageHeight) -
                          new Vector2(6f * scale, 6f * scale);
            var pillMin = pillMax - new Vector2(stamp.Width + stampPad.X * 2f, stamp.Height + stampPad.Y * 2f);
            Squircle.Fill(drawList, anchor + (pillMin - anchor) * pop + rise, anchor + (pillMax - anchor) * pop + rise,
                (pillMax.Y - pillMin.Y) * 0.5f * pop, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.38f * alpha)));
            DrawStamp(drawList, stamp, pillMax - stampPad, anchor, pop, rise, alpha, new Vector4(1f, 1f, 1f, 0.92f));
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + bubbleHeight + BubbleGap * scale));
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

    private static BubbleStamp MeasureStamp(TranscriptMessage message, bool mine, float scale)
    {
        var time = TimeText.Clock(message.CreatedAtUnix);
        var timeSize = Typography.Measure(time, StampTextScale);
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

    private static void DrawStamp(ImDrawListPtr drawList, in BubbleStamp stamp, Vector2 bottomRight, Vector2 anchor,
        float pop, Vector2 rise, float alpha, Vector4 timeColor)
    {
        var topLeft = new Vector2(bottomRight.X - stamp.Width, bottomRight.Y - stamp.Height);
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

    private struct BubbleEntrance
    {
        public int Line;
        public float Elapsed;
    }
}
