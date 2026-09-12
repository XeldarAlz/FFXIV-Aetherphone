using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Linkpearl;

internal enum InboxRowAction : byte
{
    None,
    Open,
    Menu,
    TogglePin,
    ToggleMute,
}

internal static class InboxRowView
{
    public const float Height = 72f;

    private const float AvatarRadius = 25f;
    private const float TitleTop = 13f;
    private const float QuickRadius = 17f;
    private const float QuickPitch = 38f;
    private const float QuickGlyph = 18f;
    private const float RevealSmoothTime = 0.10f;
    private const float MaxFrameSeconds = 0.1f;
    private const float StatusGlyph = 16f;
    private const float StatusPitch = 20f;
    private const float TimeGap = 8f;
    private const float TagPadX = 6f;
    private const float TagHeight = 16f;
    private const float TagGap = 6f;
    private const float TagMaxFraction = 0.42f;
    private const float TagFillAlpha = 0.18f;
    private const float BadgeHeight = 20f;
    private const float BadgePadX = 6f;
    private const float MutedTitleAlpha = 0.72f;
    private const float DotRadius = 4f;

    private static readonly TextStyle UnreadCountStyle = new(0.68f, FontWeight.SemiBold);
    private static readonly TextStyle TagStyle = TextStyles.Caption2;
    private static readonly Dictionary<string, Spring> Reveals = new(StringComparer.Ordinal);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static InboxRowAction Draw(ChatListChrome chrome, in ChatTheme theme, InboxRow row,
        LodestoneService lodestone, bool quickActions)
    {
        var ink = chrome.Ink;
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var person = chrome.BeginPersonRow(drawList, Height, AvatarRadius, 0f, true, out var avatarCenter);
        var bounds = person.Bounds;
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        GameChatTiles.DrawAvatar(drawList, avatarCenter, AvatarRadius * scale, row, lodestone, ink.Accent);
        var reveal = StepReveal(row.Key, hovered && quickActions);
        var restAlpha = 1f - reveal;
        var titleTop = bounds.Min.Y + TitleTop * scale;
        var titleHeight = Typography.LineHeight(ChatListChrome.RowTitleStyle);
        var right = person.TextRight;
        var titleRight = right;
        var action = InboxRowAction.None;
        var overActions = false;
        if (reveal > 0.01f)
        {
            action = DrawQuickActions(drawList, row, ink, right, bounds.Center.Y, reveal, scale, out overActions);
        }

        if (restAlpha > 0.01f)
        {
            titleRight = DrawTime(drawList, row, ink, right, titleTop, restAlpha, scale);
        }
        else
        {
            titleRight = right - (QuickPitch * 2f + QuickRadius * 2f + TimeGap) * scale;
        }

        var titleWidth = MathF.Max(1f, titleRight - person.TextLeft);
        var titleHovering = UiInteract.Hover(new Vector2(person.TextLeft, titleTop),
            new Vector2(person.TextLeft + titleWidth, titleTop + titleHeight));
        var titleInk = row.Muted ? Palette.WithAlpha(ink.TitleInk, MutedTitleAlpha) : ink.TitleInk;
        Marquee.DrawLeft(drawList, new MarqueeId("linkpearl.row.", row.Key),
            row.IsTell ? NameMask.Display(row.Title) : row.Title, person.TextLeft, titleTop, titleWidth,
            ChatListChrome.RowTitleStyle, titleInk, titleHovering);

        var lineTop = titleTop + titleHeight + ChatListChrome.RowLineGap * scale;
        var lineCenter = lineTop + Typography.LineHeight(ChatListChrome.RowSubStyle) * 0.5f;
        var previewRight = right;
        if (restAlpha > 0.01f)
        {
            previewRight = DrawTrailing(drawList, row, theme, ink, right, lineCenter, restAlpha, scale);
        }
        else
        {
            previewRight = titleRight;
        }

        DrawPreview(drawList, row, ink, person.TextLeft, lineTop, previewRight - TimeGap * scale - person.TextLeft,
            scale);
        chrome.EndPersonRow(drawList, person);
        if (action != InboxRowAction.None)
        {
            return action;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            return InboxRowAction.Menu;
        }

        return person.Tapped && !overActions ? InboxRowAction.Open : InboxRowAction.None;
    }

    private static float StepReveal(string key, bool target)
    {
        if (!Reveals.TryGetValue(key, out var spring))
        {
            spring = default;
        }

        spring.Step(target ? 1f : 0f, RevealSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds));
        Reveals[key] = spring;
        return Math.Clamp(spring.Value, 0f, 1f);
    }

    private static float DrawTime(ImDrawListPtr drawList, InboxRow row, SocialInk ink, float right, float titleTop,
        float alpha, float scale)
    {
        var time = row.LastActivity == default ? string.Empty : TimeText.Short(row.LastActivity);
        if (time.Length == 0)
        {
            return right;
        }

        var timeSize = Typography.Measure(time, ChatListChrome.RowMetaStyle);
        var timeInk = row.HasBadge ? ink.AccentLink : ink.MutedInk;
        var titleHeight = Typography.LineHeight(ChatListChrome.RowTitleStyle);
        Typography.Draw(drawList, new Vector2(right - timeSize.X, titleTop + (titleHeight - timeSize.Y) * 0.5f), time,
            Palette.WithAlpha(timeInk, timeInk.W * alpha), ChatListChrome.RowMetaStyle);
        return right - timeSize.X - TimeGap * scale;
    }

    private static InboxRowAction DrawQuickActions(ImDrawListPtr drawList, InboxRow row, SocialInk ink, float right,
        float centerY, float reveal, float scale, out bool overActions)
    {
        var interactive = reveal > 0.5f;
        var radius = QuickRadius * scale;
        var moreCenter = new Vector2(right - radius, centerY);
        var bellCenter = new Vector2(moreCenter.X - QuickPitch * scale, centerY);
        var pinCenter = new Vector2(moreCenter.X - QuickPitch * 2f * scale, centerY);
        var bandMin = new Vector2(pinCenter.X - radius, centerY - radius);
        var bandMax = new Vector2(moreCenter.X + radius, centerY + radius);
        overActions = interactive && UiInteract.Hover(bandMin, bandMax);
        var action = InboxRowAction.None;
        if (DrawQuickButton(drawList, pinCenter, radius, row.Pinned ? PhoneIcons.PinFilled : PhoneIcons.Pin,
                row.Pinned ? ink.AccentLink : ink.MutedInk, ink, reveal, interactive,
                Loc.T(row.Pinned ? L.Common.Unpin : L.Common.Pin), scale))
        {
            action = InboxRowAction.TogglePin;
        }

        if (DrawQuickButton(drawList, bellCenter, radius, row.Muted ? PhoneIcons.BellOff : PhoneIcons.Bell,
                row.Muted ? ink.AccentLink : ink.MutedInk, ink, reveal, interactive,
                Loc.T(row.Muted ? L.Linkpearl.Unmute : L.Linkpearl.Mute), scale))
        {
            action = InboxRowAction.ToggleMute;
        }

        if (DrawQuickButton(drawList, moreCenter, radius, PhoneIcons.Dots, ink.MutedInk, ink, reveal, interactive,
                Loc.T(L.Linkpearl.More), scale))
        {
            action = InboxRowAction.Menu;
        }

        return action;
    }

    private static bool DrawQuickButton(ImDrawListPtr drawList, Vector2 center, float radius, string glyph,
        Vector4 glyphInk, SocialInk ink, float alpha, bool interactive, string tooltip, float scale)
    {
        var half = new Vector2(radius, radius);
        var hovered = interactive && UiInteract.Hover(center - half, center + half);
        if (hovered)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ink.FieldFill, ink.FieldFill.W * alpha)), 32);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        PhoneIcon.Draw(drawList, center, glyph, Palette.WithAlpha(glyphInk, glyphInk.W * alpha), QuickGlyph * scale);
        if (!interactive)
        {
            return false;
        }

        HoverTooltip.Show(new Rect(center - half, center + half), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - half, center + half, hovered);
    }

    private static float DrawTrailing(ImDrawListPtr drawList, InboxRow row, in ChatTheme theme, SocialInk ink,
        float right, float centerY, float alpha, float scale)
    {
        var cursor = right;
        if (row.HasBadge)
        {
            cursor = DrawBadge(drawList, row.Unread, theme, cursor, centerY, alpha, scale);
        }
        else if (row.Muted && row.Unread > 0)
        {
            cursor = DrawDot(drawList, ink, cursor, centerY, alpha, scale);
        }

        if (row.Muted)
        {
            PhoneIcon.Draw(drawList, new Vector2(cursor - StatusGlyph * 0.5f * scale, centerY), PhoneIcons.BellOff,
                Palette.WithAlpha(ink.MutedInk, ink.MutedInk.W * alpha), StatusGlyph * scale);
            cursor -= StatusPitch * scale;
        }

        if (row.Pinned)
        {
            PhoneIcon.Draw(drawList, new Vector2(cursor - StatusGlyph * 0.5f * scale, centerY), PhoneIcons.PinFilled,
                Palette.WithAlpha(ink.MutedInk, ink.MutedInk.W * alpha), StatusGlyph * scale);
            cursor -= StatusPitch * scale;
        }

        return cursor;
    }

    private static float DrawBadge(ImDrawListPtr drawList, int unread, in ChatTheme theme, float right, float centerY,
        float alpha, float scale)
    {
        var label = unread > 99 ? "99+" : unread.ToString(Loc.Culture);
        var labelSize = Typography.Measure(label, UnreadCountStyle);
        var height = BadgeHeight * scale;
        var width = MathF.Max(labelSize.X + BadgePadX * 2f * scale, height);
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(Palette.WithAlpha(theme.Badge, alpha)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, label, Palette.WithAlpha(White, alpha), UnreadCountStyle);
        return min.X - TimeGap * scale;
    }

    private static float DrawDot(ImDrawListPtr drawList, SocialInk ink, float right, float centerY, float alpha,
        float scale)
    {
        var radius = DotRadius * scale;
        var center = new Vector2(right - radius, centerY);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(ink.MutedInk, 0.7f * alpha)), 12);
        return center.X - radius - TimeGap * scale;
    }

    private static void DrawPreview(ImDrawListPtr drawList, InboxRow row, SocialInk ink, float left, float top,
        float width, float scale)
    {
        if (width <= 0f)
        {
            return;
        }

        var cursor = left;
        var lineHeight = Typography.LineHeight(ChatListChrome.RowSubStyle);
        if (row.PreviewChannel.Length > 0 && GameChannels.TryByKey(row.PreviewChannel, out var channel))
        {
            var tag = LinkshellNames.Label(channel);
            var tagLabel = Typography.FitText(tag, width * TagMaxFraction, TagStyle);
            var tagSize = Typography.Measure(tagLabel, TagStyle);
            var tagHeight = TagHeight * scale;
            var tagMin = new Vector2(cursor, top + (lineHeight - tagHeight) * 0.5f);
            var tagMax = new Vector2(tagMin.X + tagSize.X + TagPadX * 2f * scale, tagMin.Y + tagHeight);
            Squircle.Fill(drawList, tagMin, tagMax, tagHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(channel.Tint, TagFillAlpha)));
            Typography.DrawCentered(drawList, (tagMin + tagMax) * 0.5f, tagLabel, channel.Tint, TagStyle);
            cursor = tagMax.X + TagGap * scale;
        }

        var remaining = left + width - cursor;
        if (remaining <= 0f)
        {
            return;
        }

        if (row.PreviewSender.Length > 0)
        {
            var senderName = NameMask.Enabled ? NameMask.Of(row.PreviewSender) : FirstName(row.PreviewSender);
            var sender = Typography.FitText(string.Concat(senderName, ": "), remaining * 0.5f,
                ChatListChrome.RowSubStyle);
            var senderSize = Typography.Measure(sender, ChatListChrome.RowSubStyle);
            Typography.Draw(drawList, new Vector2(cursor, top), sender, ink.BodyInk, ChatListChrome.RowSubStyle);
            cursor += senderSize.X;
            remaining = left + width - cursor;
        }

        if (remaining <= 0f)
        {
            return;
        }

        var preview = row.PreviewText.Length > 0 ? row.PreviewText : Loc.T(L.Linkpearl.NoMessagesYetPreview);
        Typography.Draw(drawList, new Vector2(cursor, top),
            Typography.FitText(preview, remaining, ChatListChrome.RowSubStyle), ink.MutedInk,
            ChatListChrome.RowSubStyle);
    }

    private static string FirstName(string name)
    {
        var space = name.IndexOf(' ');
        return space > 0 ? name[..space] : name;
    }
}
