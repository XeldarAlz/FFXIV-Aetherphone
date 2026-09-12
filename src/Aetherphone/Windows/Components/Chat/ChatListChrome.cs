using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly struct PersonRowResult
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

internal sealed class ChatListChrome
{
    public const float CellPadX = SocialChrome.CellPadX;
    public const float HeaderIconSize = 24f;
    public const float ActionRowHeight = 56f;
    public const float ActionTileRadius = 20f;
    public const float ActionTileGlyph = 22f;
    public const float SettingRowHeight = 52f;
    public const float SettingTileSize = 30f;
    public const float SettingTileGlyph = 18f;
    public const float SettingTileRounding = 8f;
    public const float RowTextGap = 12f;
    public const float RowLineGap = 3f;
    public const float ChevronSize = 18f;
    public const float RowTrailingGap = 8f;
    public const float RowAvatarGap = 14f;
    public const float RoleTagPadX = 7f;
    public const float RoleTagHeight = 20f;
    public const float HeroActionGlyphTop = 20f;
    public const float HeroActionLabelBottom = 14f;

    private const float PresenceDotFactor = 0.22f;
    private const float PresenceDotMinRadius = 3.5f;
    private const float PresenceDotRing = 2f;
    private const float PresenceDotOffset = 0.72f;
    private const int PresenceDotSegments = 20;

    public static readonly TextStyle ScreenTitleStyle = new(1.05f, FontWeight.SemiBold);
    public static readonly TextStyle TabTitleStyle = new(1.3f, FontWeight.Bold);
    public static readonly TextStyle RowTitleStyle = TextStyles.Headline;
    public static readonly TextStyle RowSubStyle = TextStyles.Subheadline;
    public static readonly TextStyle RowMetaStyle = TextStyles.Footnote;
    public static readonly TextStyle SectionStyle = TextStyles.FootnoteEmphasized;
    public static readonly TextStyle RoleTagStyle = TextStyles.Caption1;
    public static readonly Vector4 OnlineDot = new(0.204f, 0.816f, 0.478f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly AppSkin ui;

    public ChatListChrome(AppSkin ui, SocialInk ink)
    {
        this.ui = ui;
        Ink = ink;
    }

    public AppSkin Ui => ui;

    public SocialInk Ink { get; set; }

    public PhoneTheme Theme { get; set; } = PhoneTheme.Default;

    public Rect ScreenRect { get; set; }

    public Rect PaintHeaderBand(Rect area)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var band = new Rect(area.Min, new Vector2(area.Max.X, area.Min.Y + AppHeader.Height * scale));
        ui.PaintGradient(drawList, band, ScreenRect, 0f);
        drawList.AddLine(new Vector2(band.Min.X, band.Max.Y), band.Max, ImGui.GetColorU32(ui.Hairline), 1f);
        return band;
    }

    public float DrawScreenHeader(Rect area, string title, Action back, int trailingSlots = 0,
        bool showBack = true, bool centered = false, string subtitle = "")
    {
        PaintHeaderBand(area);
        return SocialChrome.DrawScreenHeader(area, title, Ink, back, ScreenTitleStyle,
            SocialChrome.HeaderReserve(trailingSlots), subtitle, showBack, centered);
    }

    public void DrawTabHeader(Rect area, string title, Action back, int trailingSlots)
    {
        PaintHeaderBand(area);
        SocialChrome.DrawScreenHeader(area, title, Ink, back, TabTitleStyle, SocialChrome.HeaderReserve(trailingSlots),
            string.Empty, false, false);
    }

    public bool DrawHeaderIcon(ImDrawListPtr drawList, Vector2 center, string glyph, string tooltip,
        bool highlighted = false, int badge = 0) =>
        SocialChrome.DrawHeaderIcon(drawList, center, SocialChrome.HeaderIconRadius * UiScale.Current, glyph,
            HeaderIconSize, tooltip, Ink, Ink.TitleInk, highlighted, badge);

    public void DrawSectionLabel(string label) => SocialChrome.DrawSectionLabel(label, Ink, SectionStyle);

    public void DrawRowHairline(ImDrawListPtr drawList, in FeedCellScope cell, float textLeft)
    {
        FeedCell.End(drawList, cell, ui.Hairline, false);
        FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
    }

    public void DrawPresenceDot(ImDrawListPtr drawList, Vector2 avatarCenter, float avatarRadius)
    {
        var scale = UiScale.Current;
        var dotRadius = MathF.Max(PresenceDotMinRadius * scale, avatarRadius * PresenceDotFactor);
        var offset = avatarRadius * PresenceDotOffset;
        var center = new Vector2(avatarCenter.X + offset, avatarCenter.Y + offset);
        drawList.AddCircleFilled(center, dotRadius + PresenceDotRing * scale,
            ImGui.GetColorU32(ui.Palette.BackdropTop), PresenceDotSegments);
        drawList.AddCircleFilled(center, dotRadius, ImGui.GetColorU32(OnlineDot), PresenceDotSegments);
    }

    public PersonRowResult BeginPersonRow(ImDrawListPtr drawList, float height, float avatarRadius,
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

    public void EndPersonRow(ImDrawListPtr drawList, in PersonRowResult row, bool separator = true)
    {
        var cell = new FeedCellScope(row.Bounds, false, false);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, row.TextLeft, row.Bounds.Max.X, row.Bounds.Max.Y, ui.Hairline);
        }
    }

    public void DrawRowTitleAndSub(ImDrawListPtr drawList, MarqueeId id, string title, string subtitle,
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

    public bool DrawActionRow(ImDrawListPtr drawList, string marqueePrefix, string glyph, string label,
        string subtitle = "", bool chevron = false, bool separator = true)
    {
        var scale = UiScale.Current;
        var row = BeginPersonRow(drawList, ActionRowHeight, ActionTileRadius, chevron ? ChevronSize * scale : 0f,
            true, out var tileCenter);
        var hovered = UiInteract.Hover(row.Bounds.Min, row.Bounds.Max);
        var fill = hovered ? Palette.Lighten(ui.Accent, 0.08f) : ui.Accent;
        drawList.AddCircleFilled(tileCenter, ActionTileRadius * scale, ImGui.GetColorU32(fill), 32);
        PhoneIcon.Draw(drawList, tileCenter, glyph, White, ActionTileGlyph * scale);
        DrawRowTitleAndSub(drawList, new MarqueeId(marqueePrefix, label), label, subtitle, row.TextLeft,
            row.TextRight, row.Bounds.Center.Y, Ink.TitleInk, Ink.MutedInk);
        if (chevron)
        {
            PhoneIcon.Draw(drawList, new Vector2(row.Bounds.Max.X - CellPadX * scale - ChevronSize * 0.5f * scale,
                row.Bounds.Center.Y), PhoneIcons.ChevronRight, Ink.FaintInk, ChevronSize * scale);
        }

        EndPersonRow(drawList, row, separator);
        return row.Tapped;
    }

    public bool DrawSettingRow(ImDrawListPtr drawList, string glyph, Vector4 tint, string label, string value = "",
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
                PhoneIcons.ChevronRight, Ink.FaintInk, ChevronSize * scale);
            right -= ChevronSize * scale + RowTrailingGap * scale;
        }

        if (badge > 0)
        {
            var badgeCenter = new Vector2(right - 10f * scale, cell.Bounds.Center.Y);
            SocialChrome.DrawCountBadge(drawList, badgeCenter, badge, Ink);
            right = badgeCenter.X - 14f * scale - RowTrailingGap * scale;
        }

        if (value.Length > 0)
        {
            var fittedValue = Typography.FitText(value, MathF.Max(1f, (right - textLeft) * 0.5f), RowSubStyle);
            var valueSize = Typography.Measure(fittedValue, RowSubStyle);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, cell.Bounds.Center.Y - valueSize.Y * 0.5f),
                fittedValue, Ink.MutedInk, RowSubStyle);
            right -= valueSize.X + RowTrailingGap * scale;
        }

        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - textLeft), RowTitleStyle), labelInk ?? Ink.TitleInk,
            RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return cell.Tapped;
    }

    public bool DrawSwitchRow(ImDrawListPtr drawList, string glyph, Vector4 tint, string label, bool value,
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
        var next = Toggle.Draw(id, toggleRect, value, PhoneTheme.WithAccent(Theme, ui.Accent));
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, toggleMin.X - RowTrailingGap * scale - textLeft), RowTitleStyle),
            Ink.TitleInk, RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return next;
    }

    public bool DrawDangerRow(ImDrawListPtr drawList, string glyph, string label, bool separator = true)
    {
        var scale = UiScale.Current;
        var cell = FeedCell.Begin(drawList, SettingRowHeight * scale, ui.HoverWash);
        var pad = CellPadX * scale;
        var tileHalf = SettingTileSize * 0.5f * scale;
        var tileCenter = new Vector2(cell.Bounds.Min.X + pad + tileHalf, cell.Bounds.Center.Y);
        PhoneIcon.Draw(drawList, tileCenter, glyph, Ink.Danger, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, cell.Bounds.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, cell.Bounds.Max.X - pad - textLeft), RowTitleStyle), Ink.Danger,
            RowTitleStyle);
        FeedCell.End(drawList, cell, ui.Hairline, false);
        if (separator)
        {
            FeedCell.Hairline(drawList, textLeft, cell.Bounds.Max.X, cell.Bounds.Max.Y, ui.Hairline);
        }

        return cell.Tapped;
    }

    public void DrawRoleTag(ImDrawListPtr drawList, float right, float centerY, string label, out float left)
    {
        var scale = UiScale.Current;
        var size = Typography.Measure(label, RoleTagStyle);
        var height = RoleTagHeight * scale;
        var padX = RoleTagPadX * scale;
        var min = new Vector2(right - size.X - padX * 2f, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, 6f * scale, ImGui.GetColorU32(Ink.ChipFill));
        Squircle.Stroke(drawList, min, max, 6f * scale, ImGui.GetColorU32(Ink.ChipStroke), 1f);
        Typography.Draw(drawList, new Vector2(min.X + padX, centerY - size.Y * 0.5f), label, Ink.MutedInk,
            RoleTagStyle);
        left = min.X;
    }

    public static string TrimmedLetter(string label)
    {
        if (label.Length == 0)
        {
            return "#";
        }

        var first = char.ToUpperInvariant(label[0]);
        return char.IsLetter(first) ? first.ToString() : "#";
    }

    public void DrawLetterHeader(ImDrawListPtr drawList, string letter)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var height = Typography.LineHeight(SectionStyle) + 10f * scale;
        Typography.Draw(drawList, new Vector2(origin.X + CellPadX * scale, origin.Y + 6f * scale), letter,
            Ink.AccentLink, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static string ChatTime(long unix)
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

    public static Rect RowBand(Rect row, float scale) =>
        new(new Vector2(row.Min.X - Metrics.Space.Lg * scale, row.Min.Y),
            new Vector2(row.Max.X + Metrics.Space.Lg * scale, row.Max.Y));

    public void DrawInsetSectionLabel(string label)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var height = Typography.LineHeight(SectionStyle) + 12f * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X + 4f * scale, origin.Y + 6f * scale),
            Loc.Upper(label), Ink.FaintInk, SectionStyle);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, height));
    }

    public static void DrawCardGap()
    {
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * UiScale.Current));
    }

    public bool DrawCardRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
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
                PhoneIcons.ChevronRight, Ink.FaintInk, ChevronSize * scale);
            right -= ChevronSize * scale + RowTrailingGap * scale;
        }

        if (badge > 0)
        {
            var badgeCenter = new Vector2(right - 10f * scale, row.Center.Y);
            SocialChrome.DrawCountBadge(drawList, badgeCenter, badge, Ink);
            right = badgeCenter.X - 14f * scale - RowTrailingGap * scale;
        }

        if (value.Length > 0)
        {
            var fittedValue = Typography.FitText(value, MathF.Max(1f, (right - textLeft) * 0.5f), RowSubStyle);
            var valueSize = Typography.Measure(fittedValue, RowSubStyle);
            Typography.Draw(drawList, new Vector2(right - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fittedValue,
                Ink.MutedInk, RowSubStyle);
            right -= valueSize.X + RowTrailingGap * scale;
        }

        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - textLeft), RowTitleStyle), labelInk ?? Ink.TitleInk,
            RowTitleStyle);
        return UiInteract.Click(band.Min, band.Max, hovered);
    }

    public bool DrawCardSwitchRow(ImDrawListPtr drawList, Rect row, string glyph, Vector4 tint, string label,
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
            PhoneTheme.WithAccent(Theme, ui.Accent));
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, toggleMin.X - RowTrailingGap * scale - textLeft), RowTitleStyle),
            Ink.TitleInk, RowTitleStyle);
        return next;
    }

    public bool DrawCardDangerRow(ImDrawListPtr drawList, Rect row, string glyph, string label)
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
        PhoneIcon.Draw(drawList, tileCenter, glyph, Ink.Danger, SettingTileGlyph * scale);
        var textLeft = tileCenter.X + tileHalf + RowTextGap * scale;
        var labelHeight = Typography.LineHeight(RowTitleStyle);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, row.Max.X - textLeft), RowTitleStyle), Ink.Danger, RowTitleStyle);
        return UiInteract.Click(band.Min, band.Max, hovered);
    }

    public void DrawInfoRow(ImDrawListPtr drawList, Rect row, string label, string value)
    {
        var labelHeight = Typography.LineHeight(RowSubStyle);
        var labelSize = Typography.Measure(label, RowSubStyle);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelHeight * 0.5f), label, Ink.MutedInk,
            RowSubStyle);
        var valueMaxWidth = MathF.Max(1f, row.Width - labelSize.X - RowTextGap * UiScale.Current);
        var valueHovering = UiInteract.Hover(new Vector2(row.Max.X - valueMaxWidth, row.Min.Y),
            new Vector2(row.Max.X, row.Max.Y));
        Marquee.DrawRight(drawList, new MarqueeId(label, ":value"), value, row.Max.X,
            row.Center.Y - labelHeight * 0.5f, valueMaxWidth, RowSubStyle, Ink.TitleInk, valueHovering);
    }

    public bool DrawHeroActionButton(ImDrawListPtr drawList, Rect rect, string glyph, string label, bool enabled)
    {
        var scale = UiScale.Current;
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var fill = hovered ? Palette.Lighten(ui.Palette.CardFill, 0.06f) : ui.Palette.CardFill;
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(fill));
        var glyphInk = enabled ? Ink.AccentLink : Ink.FaintInk;
        PhoneIcon.Draw(drawList, new Vector2(rect.Center.X, rect.Min.Y + HeroActionGlyphTop * scale), glyph, glyphInk,
            ActionTileGlyph * scale);
        Typography.DrawCentered(drawList, new Vector2(rect.Center.X, rect.Max.Y - HeroActionLabelBottom * scale),
            Typography.FitText(label, rect.Width - 8f * scale, RowMetaStyle), glyphInk, RowMetaStyle);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }
}
