using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Message;
using Aetherphone.Core.Social;
using Aetherphone.Core.Telephony;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessageApp
{
    private const float CellPadX = SocialChrome.CellPadX;
    private const float PresenceDotFactor = 0.22f;
    private const float PresenceDotMinRadius = 3.5f;
    private const float PresenceDotRing = 2f;
    private const float PresenceDotOffset = 0.72f;
    private const int PresenceDotSegments = 20;
    private const float HeaderIconSize = 24f;
    private const float ActionRowHeight = 56f;
    private const float ActionTileRadius = 20f;
    private const float ActionTileGlyph = 22f;
    private const float SettingRowHeight = 52f;
    private const float SettingTileSize = 30f;
    private const float SettingTileGlyph = 18f;
    private const float SettingTileRounding = 8f;
    private const float RowTextGap = 12f;
    private const float RowLineGap = 3f;
    private const float ChevronSize = 18f;
    private const float RowTrailingGap = 8f;
    private const float RowAvatarGap = 14f;
    private const float RoleTagPadX = 7f;
    private const float RoleTagHeight = 20f;

    private static readonly TextStyle ScreenTitleStyle = new(1.05f, FontWeight.SemiBold);
    private static readonly TextStyle TabTitleStyle = new(1.3f, FontWeight.Bold);
    private static readonly TextStyle RowTitleStyle = TextStyles.Headline;
    private static readonly TextStyle RowSubStyle = TextStyles.Subheadline;
    private static readonly TextStyle RowMetaStyle = TextStyles.Footnote;
    private static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle RoleTagStyle = TextStyles.Caption1;
    private static readonly Vector4 OnlineDot = new(0.204f, 0.816f, 0.478f, 1f);

    private SocialInk ink = MessageThemes.InkFor(string.Empty);
    private MessageTheme activeTheme = MessageThemes.All[0];
    private Rect screenRect;

    private void ResolveTheme()
    {
        var id = configuration.MessageChatTheme;
        activeTheme = MessageThemes.Resolve(id);
        ink = MessageThemes.InkFor(id);
        ui.Palette = MessageThemes.PaletteFor(id);
    }

    private float DrawScreenHeader(Rect area, string title, int trailingSlots = 0, bool showBack = true,
        bool centered = false, string subtitle = "")
    {
        PaintHeaderBand(area);
        return SocialChrome.DrawScreenHeader(area, title, ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), subtitle, showBack, centered);
    }

    private void DrawTabHeader(Rect area, string title, int trailingSlots)
    {
        PaintHeaderBand(area);
        SocialChrome.DrawScreenHeader(area, title, ink, back, TabTitleStyle, SocialChrome.HeaderReserve(trailingSlots),
            string.Empty, false, false);
    }

    private bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            HeaderIconSize, tooltip, ink, ink.TitleInk, highlighted, badge);

    private void DrawSectionLabel(string label) => SocialChrome.DrawSectionLabel(label, ink, SectionStyle);

    private void DrawRowHairline(ImDrawListPtr drawList, in FeedCellScope cell, float textLeft)
    {
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
    }

    private void DrawConversationAvatar(ImDrawListPtr drawList, ConversationDto item, Vector2 center, float radius)
    {
        ConversationAvatar.Draw(drawList, item, center, radius, theme, ui, images, lodestone);
        if (!item.IsGroup && ChatPresence.IsOnline(item.Presence))
        {
            DrawPresenceDot(drawList, center, radius);
        }
    }

    private void DrawGroupAvatar(ImDrawListPtr drawList, Vector2 center, float radius, string title,
        string? avatarUrl) =>
        ConversationAvatar.DrawGroup(drawList, center, radius, title, avatarUrl, theme, ui, images, lodestone);

    private void DrawContactAvatar(ImDrawListPtr drawList, ContactDto contact, Vector2 center, float radius)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, ContactBook.DisplayLabel(contact), string.Empty,
            contact.AvatarUrl, images, lodestone, 0.95f, 32, 1f, Frames.Of(contact.FrameId));
        if (ChatPresence.IsOnline(contact.Presence))
        {
            DrawPresenceDot(drawList, center, radius);
        }
    }

    private void DrawPresenceDot(ImDrawListPtr drawList, Vector2 avatarCenter, float avatarRadius)
    {
        var scale = UiScale.Current;
        var dotRadius = MathF.Max(PresenceDotMinRadius * scale, avatarRadius * PresenceDotFactor);
        var offset = avatarRadius * PresenceDotOffset;
        var center = new Vector2(avatarCenter.X + offset, avatarCenter.Y + offset);
        drawList.AddCircleFilled(center, dotRadius + PresenceDotRing * scale,
            ImGui.GetColorU32(ui.Palette.BackdropTop), PresenceDotSegments);
        drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(OnlineDot), PresenceDotSegments);
    }

    private void DrawMemberAvatar(ImDrawListPtr drawList, ConversationMemberDto member, Vector2 center, float radius)
    {
        AvatarView.DrawRemote(drawList, center, radius, theme, DirectMessagesStore.MemberLabel(member), string.Empty, member.AvatarUrl,
            images, lodestone, 0.9f, 32, 1f, Frames.Of(member.FrameId));
    }

    private readonly struct PersonRowResult
    {
        public readonly bool Tapped;
        public readonly Rect Bounds;
        public readonly float TextLeft;
        public readonly float TextRight;

        public PersonRowResult(bool tapped, Rect bounds, float textLeft, float textRight)
        {
            Tapped = tapped;
            Bounds = bounds;
            TextLeft = textLeft;
            TextRight = textRight;
        }
    }

    private PersonRowResult BeginPersonRow(ImDrawListPtr drawList, float height, float avatarRadius,
        float trailingReserve, bool interactive, out Vector2 avatarCenter)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, height * scale, ui.HoverWash, interactive);
        var pad = CellPadX * scale;
        var radius = avatarRadius * scale;
        avatarCenter = new Vector2(cell.Bounds.Min.X + pad + radius, cell.Bounds.Center.Y);
        var textLeft = avatarCenter.X + radius + RowAvatarGap * scale;
        var textRight = cell.Bounds.Max.X - pad - trailingReserve;
        return new PersonRowResult(cell.Tapped, cell.Bounds, textLeft, textRight);
    }

    private void EndPersonRow(ImDrawListPtr drawList, in PersonRowResult row, bool separator = true)
    {
        var cell = new FeedCellScope(row.Bounds, false, false);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, row.TextLeft, row.Bounds.Max.X, row.Bounds.Max.Y, ui.Hairline);
        }
    }

    private void DrawRowTitleAndSub(ImDrawListPtr drawList, MarqueeId id, string title, string subtitle,
        float left, float right, float centerY, Vector4 titleInk, Vector4 subInk)
    {
        var scale = UiScale.Current;
        var width = MathF.Max(1f, right - left);
        var titleHeight = Typography.LineHeight(RowTitleStyle);
        if (subtitle.Length == 0)
        {
            var soloTop = centerY - titleHeight * 0.5f;
            var soloHovering = UiInteract.Hover(new Vector2(left, soloTop), new Vector2(right, soloTop + titleHeight));
            Marquee.DrawLeft(drawList, id, title, left, soloTop, width, RowTitleStyle, titleInk, soloHovering);
            return;
        }

        var subHeight = Typography.LineHeight(RowSubStyle);
        var top = centerY - (titleHeight + RowLineGap * scale + subHeight) * 0.5f;
        var hovering = UiInteract.Hover(new Vector2(left, top), new Vector2(right, top + titleHeight));
        Marquee.DrawLeft(drawList, id, title, left, top, width, RowTitleStyle, titleInk, hovering);
        Typography.Draw(drawList, new Vector2(left, top + titleHeight + RowLineGap * scale),
            Typography.FitText(subtitle, width, RowSubStyle), subInk, RowSubStyle);
    }

    private bool DrawActionRow(ImDrawListPtr drawList, string glyph, string label, string subtitle = "",
        bool chevron = false, bool separator = true)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, ActionRowHeight, ActionTileRadius, chevron ? ChevronSize * scale : 0f,
            true, out var tileCenter);
        var hovered = UiInteract.Hover(row.Bounds.Min, row.Bounds.Max);
        var fill = hovered ? Palette.Lighten(ui.Accent, 0.08f) : ui.Accent;
        drawList.AddCircleFilled(tileCenter, ActionTileRadius * scale, ImGui.GetColorU32(fill), 32);
        PhoneIcon.Draw(drawList, tileCenter, glyph, White, ActionTileGlyph * scale);
        DrawRowTitleAndSub(drawList, new MarqueeId("message.action.", label), label, subtitle, row.TextLeft,
            row.TextRight, row.Bounds.Center.Y, ink.TitleInk, ink.MutedInk);
        if (chevron)
        {
            PhoneIcon.Draw(drawList, new Vector2(row.Bounds.Max.X - CellPadX * scale - ChevronSize * 0.5f * scale,
                row.Bounds.Center.Y), PhoneIcons.ChevronRight, ink.FaintInk, ChevronSize * scale);
        }

        EndPersonRow(drawList, row, separator);
        return row.Tapped;
    }

    private bool DrawSettingRow(ImDrawListPtr drawList, string glyph, Vector4 tint, string label, string value = "",
        bool chevron = true, bool separator = true, int badge = 0, Vector4? labelInk = null)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, SettingRowHeight * scale, ui.HoverWash);
        var pad = CellPadX * scale;
        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(cell.Bounds.Min.X + pad + tileHalf, cell.Bounds.Center.Y);
        var tileMin = tileCenter - new Vector2(tileHalf, tileHalf);
        var tileMax = tileCenter + new Vector2(tileHalf, tileHalf);
        Squircle.Fill(drawList, tileMin, tileMax, SettingTileRounding * scale, ImGui.GetColorU32(tint));
        PhoneIcon.Draw(drawList, tileCenter, glyph, White, SettingTileGlyph * scale);
        var textLeft = tileMax.X + RowTextGap * scale;
        var right = cell.Bounds.Max.X - pad;
        if (chevron)
        {
            PhoneIcon.Draw(drawList, new Vector2(right - ChevronSize * 0.5f * scale, cell.Bounds.Center.Y),
                PhoneIcons.ChevronRight, ink.FaintInk, ChevronSize * scale);
            right -= ChevronSize * scale + RowTrailingGap * scale;
        }

        if (badge > 0)
        {
            var badgeCenter = new Vector2(right - 10f * scale, cell.Bounds.Center.Y);
            SocialChrome.DrawCountBadge(drawList, badgeCenter, badge, ink);
            right = badgeCenter.X - 14f * scale - RowTrailingGap * scale;
        }

        if (value.Length > 0)
        {
            var fittedValue = Typography.FitText(value, MathF.Max(1f, (right - textLeft) * 0.5f), RowSubStyle);
            var valueSize = Typography.Measure(fittedValue, RowSubStyle);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, cell.Bounds.Center.Y - valueSize.Y * 0.5f),
                fittedValue, ink.MutedInk, RowSubStyle);
            right -= valueSize.X + RowTrailingGap * scale;
        }

        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - textLeft), RowTitleStyle), labelInk ?? ink.TitleInk,
            RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return cell.Tapped;
    }

    private bool DrawSwitchRow(ImDrawListPtr drawList, string glyph, Vector4 tint, string label, bool value,
        string id, bool separator = true)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, SettingRowHeight * scale, ui.HoverWash, false);
        var pad = CellPadX * scale;
        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(cell.Bounds.Min.X + pad + tileHalf, cell.Bounds.Center.Y);
        Squircle.Fill(drawList, tileCenter - new Vector2(tileHalf, tileHalf), tileCenter + new Vector2(tileHalf, tileHalf),
            SettingTileRounding * scale, ImGui.GetColorU32(tint));
        PhoneIcon.Draw(drawList, tileCenter, glyph, White, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(cell.Bounds.Max.X - pad - toggleWidth, cell.Bounds.Center.Y - toggleHeight * 0.5f);
        var toggleRect = new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight));
        var next = Toggle.Draw(id, toggleRect, value, PhoneTheme.WithAccent(theme, ui.Accent));
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, toggleMin.X - RowTrailingGap * scale - textLeft), RowTitleStyle),
            ink.TitleInk, RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return next;
    }

    private bool DrawDangerRow(ImDrawListPtr drawList, string glyph, string label, bool separator = true)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, SettingRowHeight * scale, ui.HoverWash);
        var pad = CellPadX * scale;
        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(cell.Bounds.Min.X + pad + tileHalf, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, tileCenter, glyph, ink.Danger, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, cell.Bounds.Max.X - pad - textLeft), RowTitleStyle), ink.Danger,
            RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return cell.Tapped;
    }

    private void DrawRoleTag(ImDrawListPtr drawList, float right, float centerY, string label, out float left)
    {
        var scale = UiScale.Current;
        var size = Typography.Measure(label, RoleTagStyle);
        var height = RoleTagHeight * scale;
        var padX = RoleTagPadX * scale;
        var min = new Vector2(right - size.X - padX * 2f, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, 6f * scale, ImGui.GetColorU32(ink.ChipFill));
        Squircle.Stroke(drawList, min, max, 6f * scale, ImGui.GetColorU32(ink.ChipStroke), 1f);
        Typography.Draw(drawList, new Vector2(min.X + padX, centerY - size.Y * 0.5f), label, ink.MutedInk,
            RoleTagStyle);
        left = min.X;
    }

    private static string TrimmedLetter(string label)
    {
        if (label.Length == 0)
        {
            return "#";
        }

        var first = char.ToUpperInvariant(label[0]);
        return char.IsLetter(first) ? first.ToString() : "#";
    }

    private void DrawLetterHeader(ImDrawListPtr drawList, string letter)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.LineHeight(SectionStyle) + 10f * scale;
        Typography.Draw(drawList, new Vector2(origin.X + CellPadX * scale, origin.Y + 6f * scale), letter,
            ink.AccentLink, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static string ChatTime(long unix)
    {
        if (unix <= 0)
        {
            return string.Empty;
        }

        var local = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        if (local.Date == today)
        {
            return TimeText.Clock(local);
        }

        return (today - local.Date).TotalDays < 7d
            ? local.ToString("ddd", Loc.Culture)
            : local.ToString("d", Loc.Culture);
    }

    private static Rect RowBand(Rect row, float scale) =>
        new(new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y),
            new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y));

    private void DrawInsetSectionLabel(string label)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.LineHeight(SectionStyle) + 12f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 4f * scale, origin.Y + 6f * scale),
            Loc.Upper(label), ink.FaintInk, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, height));
    }

    private void DrawCardGap()
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
    }

    private bool DrawCardRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
        string value = "", bool chevron = true, int badge = 0, Vector4? labelInk = null)
    {
        var scale = UiScale.Current;
        var band = RowBand(row, scale);
        var hovered = UiInteract.Hover(band.Min, band.Max);
        if (hovered)
        {
            drawList.AddRectFilled(band.Min, band.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var textLeft = row.Min.X;
        if (glyph.Length > 0)
        {
            var tileHalf = SettingTileSize * 0.5f * scale;
            var tileCenter = new Vector2(row.Min.X + tileHalf, row.Center.Y);
            Squircle.Fill(drawList, tileCenter - new Vector2(tileHalf, tileHalf),
                tileCenter + new Vector2(tileHalf, tileHalf), SettingTileRounding * scale, ImGui.GetColorU32(tint));
            PhoneIcon.Draw(drawList, tileCenter, glyph, White, SettingTileGlyph * scale);
            textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        }

        var right = row.Max.X;
        if (chevron)
        {
            PhoneIcon.Draw(drawList, new Vector2(right - ChevronSize * 0.5f * scale, row.Center.Y),
                PhoneIcons.ChevronRight, ink.FaintInk, ChevronSize * scale);
            right -= ChevronSize * scale + RowTrailingGap * scale;
        }

        if (badge > 0)
        {
            var badgeCenter = new Vector2(right - 10f * scale, row.Center.Y);
            SocialChrome.DrawCountBadge(drawList, badgeCenter, badge, ink);
            right = badgeCenter.X - 14f * scale - RowTrailingGap * scale;
        }

        if (value.Length > 0)
        {
            var fittedValue = Typography.FitText(value, MathF.Max(1f, (right - textLeft) * 0.5f), RowSubStyle);
            var valueSize = Typography.Measure(fittedValue, RowSubStyle);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fittedValue,
                ink.MutedInk, RowSubStyle);
            right -= valueSize.X + RowTrailingGap * scale;
        }

        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - textLeft), RowTitleStyle), labelInk ?? ink.TitleInk,
            RowTitleStyle);
        return UiInteract.Click(band.Min, band.Max, hovered);
    }

    private bool DrawCardSwitchRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
        bool value, string id)
    {
        var scale = UiScale.Current;
        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(row.Min.X + tileHalf, row.Center.Y);
        Squircle.Fill(drawList, tileCenter - new Vector2(tileHalf, tileHalf), tileCenter + new Vector2(tileHalf, tileHalf),
            SettingTileRounding * scale, ImGui.GetColorU32(tint));
        PhoneIcon.Draw(drawList, tileCenter, glyph, White, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - toggleWidth, row.Center.Y - toggleHeight * 0.5f);
        var next = Toggle.Draw(id, new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight)), value,
            PhoneTheme.WithAccent(theme, ui.Accent));
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, toggleMin.X - RowTrailingGap * scale - textLeft), RowTitleStyle),
            ink.TitleInk, RowTitleStyle);
        return next;
    }

    private bool DrawCardDangerRow(ImDrawListPtr drawList, Rect row, string glyph, string label)
    {
        var scale = UiScale.Current;
        var band = RowBand(row, scale);
        var hovered = UiInteract.Hover(band.Min, band.Max);
        if (hovered)
        {
            drawList.AddRectFilled(band.Min, band.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(row.Min.X + tileHalf, row.Center.Y);
        PhoneIcon.Draw(drawList, tileCenter, glyph, ink.Danger, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, row.Max.X - textLeft), RowTitleStyle), ink.Danger, RowTitleStyle);
        return UiInteract.Click(band.Min, band.Max, hovered);
    }

    private void DrawInfoRow(ImDrawListPtr drawList, Rect row, string label, string value)
    {
        var labelHeight = Typography.LineHeight(RowSubStyle);
        var labelSize = Typography.Measure(label, RowSubStyle);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f), label, ink.MutedInk,
            RowSubStyle);
        var valueMaxWidth = MathF.Max(1f, row.Width - labelSize.X - RowTextGap * UiScale.Current);
        var valueHovering = UiInteract.Hover(new Vector2(row.Max.X - valueMaxWidth, row.Min.Y),
            new Vector2(row.Max.X, row.Max.Y));
        Marquee.DrawRight(drawList, new MarqueeId(label, ":value"), value, row.Max.X,
            row.Center.Y - labelHeight * 0.5f, valueMaxWidth, RowSubStyle, ink.TitleInk, valueHovering);
    }

    private bool DrawHeroActionButton(ImDrawListPtr drawList, Rect rect, string glyph, string label, bool enabled)
    {
        var scale = UiScale.Current;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var fill = hovered ? Palette.Lighten(ui.Palette.CardFill, 0.06f) : ui.Palette.CardFill;
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(fill));
        var glyphInk = enabled ? ink.AccentLink : ink.FaintInk;
        PhoneIcon.Draw(drawList, new Vector2(rect.Center.X, rect.Min.Y + 20f * scale), glyph, glyphInk,
            ActionTileGlyph * scale);
        Typography.DrawCentered(drawList, new Vector2(rect.Center.X, rect.Max.Y - 14f * scale),
            Typography.FitText(label, rect.Width - 8f * scale, RowMetaStyle), glyphInk, RowMetaStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
