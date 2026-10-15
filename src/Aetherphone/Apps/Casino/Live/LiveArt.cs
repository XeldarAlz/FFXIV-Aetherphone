using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Live;

internal readonly record struct LiveRoomCard(
    string GameId,
    string Name,
    string Phase,
    string Crowd,
    float Ring,
    bool Open,
    bool Clocked);

internal readonly record struct LiveRowAction(string Label, bool Primary, bool Chevron, string Id);

internal static class LiveArt
{
    public const float SectionGap = 18f;
    public const float HeaderGap = 2f;
    public const float RowGap = 8f;
    public const float RoomGap = 8f;
    public const int MinimumPhaseLines = 2;

    private const float CardPad = 12f;
    private const float RowPadY = 11f;
    private const float RowTile = 46f;
    private const float OwnTile = 48f;
    private const float TextGap = 12f;
    private const float LineGap = 3f;
    private const float BadgePadX = 7f;
    private const float BadgePadY = 2f;
    private const float BadgeGap = 6f;
    private const float TagPadX = 5f;
    private const float TagPadY = 1.5f;
    private const float TagGlow = 4f;
    private const float TagRadius = 6f;
    private const float LockSize = 11f;
    private const float LockGap = 3f;
    private const float ChevronSlot = 16f;
    private const float RoomPad = 10f;
    private const float RingBox = 54f;
    private const float RingInset = 2f;
    private const float RingStroke = 3f;
    private const float RingTile = 36f;
    private const float RingTextGap = 6f;
    private const float NamePhaseGap = 2f;
    private const float PhaseCrowdGap = 4f;
    private const float RoomMinHeight = 132f;
    private const float DotGap = 5f;
    private const float CtaPad = 14f;
    private const float CtaCircle = 44f;
    private const float CtaGlyph = 20f;
    private const float EmptyPad = 20f;
    private const float SublineGap = 8f;
    private const float HairlineInset = 70f;
    private const float PickChevron = 12f;
    private const float PickChevronGap = 4f;
    private const float TintWashAlpha = 0.30f;
    private const float TintRimAlpha = 0.45f;
    private const float RingTrackAlpha = 0.10f;
    private const float RingLift = 0.45f;
    private const float HereWashAlpha = 0.10f;
    private const float HereRimAlpha = 0.35f;
    private const float HereFillLift = 0.35f;
    private const float HereGlowAlpha = 0.25f;
    private const float RoseWashAlpha = 0.20f;
    private const float RoseRimAlpha = 0.60f;
    private const float BadgeFillAlpha = 0.20f;
    private const float BadgeInkLift = 0.45f;
    private const float CtaTopLift = 0.18f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly string LockGlyph = PhoneIcons.Lock;
    private static readonly string PlusGlyph = PhoneIcons.Plus;
    private static readonly string DownGlyph = PhoneIcons.ChevronDown;

    public static float Header(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string subline, float scale)
    {
        var titleStyle = TextStyles.Title3;
        var sublineStyle = TextStyles.Footnote;
        var band = CardSectionHeader.HeightUnits * scale;
        var gap = SublineGap * scale;
        var sublineWidth = subline.Length > 0 ? Typography.Measure(subline, sublineStyle).X : 0f;
        var inline = subline.Length > 0
                     && Typography.Measure(title, titleStyle).X + gap + sublineWidth <= width;
        CardSectionHeader.Draw(drawList, origin, width, title, ui.TitleInk, inline ? sublineWidth + gap : 0f);
        if (subline.Length == 0)
        {
            return origin.Y + band;
        }

        var titleLine = Typography.LineHeight(titleStyle);
        var titleTop = origin.Y + (band - titleLine) * 0.5f;
        if (inline)
        {
            Typography.Draw(drawList,
                new Vector2(origin.X + width - sublineWidth,
                    titleTop + titleLine - Typography.LineHeight(sublineStyle)), subline, ui.MutedInk, sublineStyle);
            return origin.Y + band;
        }

        var bottom = PitText.Draw(drawList, subline, sublineStyle, ui.MutedInk, origin.X, titleTop + titleLine, width,
            false);
        return MathF.Max(origin.Y + band, bottom);
    }

    public static float RoomCardHeight(float nameHeight, int phaseLines, float scale) =>
        MathF.Max(RoomMinHeight * scale,
            (RoomPad * 2f + RingBox + RingTextGap + NamePhaseGap + PhaseCrowdGap) * scale + nameHeight
            + phaseLines * Typography.LineHeight(TextStyles.FootnoteEmphasized)
            + Typography.LineHeight(TextStyles.Footnote));

    public static float RoomTextWidth(float cardWidth, float scale) => MathF.Max(1f, cardWidth - RoomPad * 2f * scale);

    public static float RoomNameHeight(string name, float cardWidth, float scale) =>
        PitText.Height(name, TextStyles.SubheadlineEmphasized, RoomTextWidth(cardWidth, scale));

    public static int PhaseLinesOf(string phase, float cardWidth, float scale) =>
        Typography.WrapText(phase, TextStyles.FootnoteEmphasized, RoomTextWidth(cardWidth, scale)).Length;

    public static bool RoomCard(ImDrawListPtr drawList, AppSkin ui, in LiveRoomCard card, Rect rect, float nameHeight,
        int phaseLines, bool interactive, float scale, out Rect pressed)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID(card.GameId), rect.Min, rect.Max, out var min, out var max,
            interactive);
        pressed = new Rect(min, max);
        var radius = Metrics.Radius.Lg * scale;
        var tint = CasinoArt.TintOf(card.GameId);
        ui.Card(drawList, min, max, radius);
        Squircle.FillVerticalGradient(drawList, min, max, radius, Color(tint, TintWashAlpha), Color(tint, 0f));
        Squircle.Stroke(drawList, min, max, radius, Color(tint, TintRimAlpha), MathF.Max(1f, scale));
        var pad = RoomPad * scale;
        var ringCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + pad + RingBox * 0.5f * scale);
        var ringRadius = (RingBox * 0.5f - RingInset) * scale;
        var stroke = RingStroke * scale;
        var lifted = Palette.Lighten(tint, RingLift);
        ProgressRing.Track(drawList, ringCenter, ringRadius, stroke, White with { W = RingTrackAlpha });
        ProgressRing.Fill(drawList, ringCenter, ringRadius, stroke, card.Ring, card.Open ? lifted : ui.MutedInk);
        CasinoArt.GameTile(drawList, card.GameId, ringCenter, RingTile * scale);
        var left = min.X + pad;
        var textWidth = RoomTextWidth(rect.Width, scale);
        var top = ringCenter.Y + (RingBox * 0.5f + RingTextGap) * scale;
        PitText.Draw(drawList, card.Name, TextStyles.SubheadlineEmphasized, ui.TitleInk, left, top, textWidth, true);
        top += nameHeight + NamePhaseGap * scale;
        var phaseInk = card.Open ? lifted : card.Clocked ? ui.BodyInk : ui.MutedInk;
        DrawLines(drawList, card.Phase, TextStyles.FootnoteEmphasized, phaseInk, left, top, textWidth, phaseLines);
        top += phaseLines * Typography.LineHeight(TextStyles.FootnoteEmphasized) + PhaseCrowdGap * scale;
        DrawCrowd(drawList, card.Crowd, (min.X + max.X) * 0.5f, top, textWidth, card.Clocked, ui, scale);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float OwnCardHeight(float scale) =>
        CardPad * 2f * scale + MathF.Max(OwnTile * scale,
            Typography.LineHeight(TextStyles.Headline) + LineGap * scale + Typography.LineHeight(TextStyles.Footnote));

    public static bool OwnCard(ImDrawListPtr drawList, AppSkin ui, Rect rect, string gameId, string name, string line,
        in LiveRowAction action, bool interactive, float scale)
    {
        var pad = CardPad * scale;
        var restingPill = PillRect(action, rect.Max.X - pad, rect.Center.Y, scale);
        var overPill = interactive && UiInteract.Hover(restingPill.Min, restingPill.Max);
        var hovered = CasinoArt.PressCard(ImGui.GetID("own"), rect.Min, rect.Max, out var min, out var max,
            interactive && !overPill);
        var radius = Metrics.Radius.Grouped * scale;
        var rose = CasinoColors.LightA;
        ui.Card(drawList, min, max, radius);
        Squircle.FillVerticalGradient(drawList, min, max, radius, Color(rose, RoseWashAlpha), Color(rose, 0f));
        Squircle.Stroke(drawList, min, max, radius, Color(rose, RoseRimAlpha), MathF.Max(1.5f * scale, 1f));
        var tile = OwnTile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, (min.Y + max.Y) * 0.5f);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);
        var pill = PillRect(action, max.X - pad, tileCenter.Y, scale);
        var left = tileCenter.X + tile * 0.5f + TextGap * scale;
        var textWidth = MathF.Max(1f, pill.Min.X - TextGap * scale - left);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = tileCenter.Y - (nameHeight + LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(name, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        var lineTop = top + nameHeight + LineGap * scale;
        var dotCenter = new Vector2(left + CasinoArt.LiveDotRadius * scale, lineTop + lineHeight * 0.5f);
        CasinoArt.LiveDot(drawList, dotCenter, scale, rose, true);
        var lineLeft = dotCenter.X + (CasinoArt.LiveDotRadius + DotGap) * scale;
        Typography.Draw(drawList, new Vector2(lineLeft, lineTop),
            Typography.FitText(line, MathF.Max(1f, left + textWidth - lineLeft), TextStyles.Footnote), ui.BodyInk,
            TextStyles.Footnote);
        var pillTapped = DrawPill(drawList, ui, pill, action, interactive, scale);
        return pillTapped || (interactive && !overPill && UiInteract.Click(rect.Min, rect.Max, hovered));
    }

    public static float HouseRowHeight(string line, float rowWidth, in LiveRowAction action, float scale) =>
        CardPad * 2f * scale + MathF.Max(RowTile * scale,
            Typography.LineHeight(TextStyles.Headline) + LineGap * scale
            + PitText.Height(line, TextStyles.Footnote, HouseTextWidth(rowWidth, action, scale)) + LineGap * scale
            + Typography.LineHeight(TextStyles.FootnoteEmphasized));

    public static bool HouseRow(ImDrawListPtr drawList, AppSkin ui, Rect row, string gameId, string name, string line,
        string crowd, in LiveRowAction action, bool hairline, bool interactive, float scale)
    {
        var pad = CardPad * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + HairlineInset * scale, row.Max.X, row.Min.Y);
        }

        var hovered = interactive && CoinArt.RowInteraction(drawList, ui, row, scale);
        var tile = RowTile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);
        var pill = PillRect(action, row.Max.X - pad, row.Center.Y, scale);
        var left = tileCenter.X + tile * 0.5f + TextGap * scale;
        var textWidth = HouseTextWidth(row.Width, action, scale);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(line, TextStyles.Footnote, textWidth);
        var crowdHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var top = row.Center.Y - (nameHeight + lineHeight + crowdHeight + LineGap * 2f * scale) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(name, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        top += nameHeight + LineGap * scale;
        top = PitText.Draw(drawList, line, TextStyles.Footnote, ui.BodyInk, left, top, textWidth, false) +
              LineGap * scale;
        var dotCenter = new Vector2(left + CasinoArt.LiveDotRadius * scale, top + crowdHeight * 0.5f);
        CasinoArt.LiveDot(drawList, dotCenter, scale, CasinoColors.LightA, true);
        var crowdLeft = dotCenter.X + (CasinoArt.LiveDotRadius + DotGap) * scale;
        Typography.Draw(drawList, new Vector2(crowdLeft, top),
            Typography.FitText(crowd, MathF.Max(1f, left + textWidth - crowdLeft), TextStyles.FootnoteEmphasized),
            StageText.Body, TextStyles.FootnoteEmphasized);
        DrawPill(drawList, ui, pill, action, false, scale);
        return interactive && UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static float TableRowHeight(float scale) =>
        RowPadY * 2f * scale + MathF.Max(RowTile * scale,
            Typography.LineHeight(TextStyles.Headline) + Typography.LineHeight(TextStyles.Footnote)
            + BadgeHeight(scale) + LineGap * 2f * scale);

    public static bool TableRow(ImDrawListPtr drawList, AppSkin ui, Rect rect, string gameId, in LiveTableView view,
        bool here, bool interactive, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID("row"), rect.Min, rect.Max, out var min, out var max,
            interactive);
        var radius = Metrics.Radius.Lg * scale;
        ui.Card(drawList, min, max, radius);
        if (here)
        {
            var cyan = CasinoColors.LightB;
            Squircle.FillHorizontalGradient(drawList, min, max, radius, Color(cyan, HereWashAlpha), Color(cyan, 0f));
            Squircle.Stroke(drawList, min, max, radius, Color(cyan, HereRimAlpha), MathF.Max(1f, scale));
        }

        var pad = CardPad * scale;
        var tile = RowTile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, (min.Y + max.Y) * 0.5f);
        CasinoArt.GameTile(drawList, gameId, tileCenter, tile);
        var chevronX = max.X - pad - ChevronSlot * 0.5f * scale;
        CasinoArt.Chevron(drawList, new Vector2(chevronX, tileCenter.Y), ui.MutedInk);
        var left = tileCenter.X + tile * 0.5f + TextGap * scale;
        var right = chevronX - (ChevronSlot * 0.5f + TextGap * 0.5f) * scale;
        var textWidth = MathF.Max(1f, right - left);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var badge = BadgeHeight(scale);
        var top = tileCenter.Y - (nameHeight + lineHeight + badge + LineGap * 2f * scale) * 0.5f;
        DrawName(drawList, ui, view.Name, here, left, top, textWidth, scale);
        top += nameHeight + LineGap * scale;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(view.Line, textWidth, TextStyles.Footnote),
            ui.BodyInk, TextStyles.Footnote);
        top += lineHeight + LineGap * scale;
        DrawBadges(drawList, ui, view, left, top, right, scale);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float EmptyHeight(string text, float width, float scale) =>
        EmptyPad * 2f * scale + PitText.Height(text, TextStyles.Subheadline, width - EmptyPad * 2f * scale);

    public static void Empty(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string text, float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + EmptyHeight(text, width, scale));
        ui.Card(drawList, origin, max, Metrics.Radius.Lg * scale);
        var pad = EmptyPad * scale;
        PitText.Draw(drawList, text, TextStyles.Subheadline, ui.BodyInk, origin.X + pad, origin.Y + pad,
            width - pad * 2f, true);
    }

    public static float CtaHeight(string line, float width, float scale) =>
        CtaPad * 2f * scale + MathF.Max(CtaCircle * scale,
            Typography.LineHeight(TextStyles.Headline) + LineGap * scale
            + PitText.Height(line, TextStyles.Footnote, CtaTextWidth(width, scale)));

    public static bool HostCta(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, string title,
        string line, bool interactive, float scale)
    {
        var height = CtaHeight(line, width, scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var hovered = CasinoArt.PressCard(ImGui.GetID("host"), rect.Min, rect.Max, out var min, out var max,
            interactive);
        var radius = Metrics.Radius.Lg * scale;
        ui.Card(drawList, min, max, radius);
        var pad = CtaPad * scale;
        var circle = CtaCircle * scale;
        var center = new Vector2(min.X + pad + circle * 0.5f, (min.Y + max.Y) * 0.5f);
        Squircle.FillCircleVerticalGradient(drawList, center, circle * 0.5f,
            ImGui.GetColorU32(Palette.Lighten(ui.Accent, CtaTopLift)), ImGui.GetColorU32(ui.Accent));
        PhoneIcon.Draw(drawList, center, PlusGlyph, White, CtaGlyph * scale);
        var chevronX = max.X - pad - ChevronSlot * 0.5f * scale;
        CasinoArt.Chevron(drawList, new Vector2(chevronX, center.Y), ui.MutedInk);
        var left = center.X + circle * 0.5f + TextGap * scale;
        var textWidth = CtaTextWidth(width, scale);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(line, TextStyles.Footnote, textWidth);
        var top = center.Y - (titleHeight + LineGap * scale + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(title, textWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        PitText.Draw(drawList, line, TextStyles.Footnote, ui.BodyInk, left, top + titleHeight + LineGap * scale,
            textWidth, false);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static Vector4 CurrencyTint(int currency) => currency switch
    {
        CasinoCurrencies.Gil => CasinoColors.Money,
        CasinoCurrencies.Practice => CasinoColors.Practice,
        _ => CasinoColors.LightA,
    };

    public static LocString CurrencyName(int currency) => currency switch
    {
        CasinoCurrencies.Gil => L.LivePage.FilterGil,
        CasinoCurrencies.Practice => L.LivePage.FilterFreePlay,
        _ => L.LivePage.FilterChips,
    };

    private static float BadgeHeight(float scale) =>
        Typography.LineHeight(TextStyles.FootnoteEmphasized) + BadgePadY * 2f * scale;

    private static float HouseTextWidth(float rowWidth, in LiveRowAction action, float scale) =>
        MathF.Max(1f, rowWidth - (CardPad * 2f + RowTile + TextGap * 2f) * scale - PillWidth(action, scale));

    private static float CtaTextWidth(float width, float scale) =>
        MathF.Max(1f, width - (CtaPad * 2f + CtaCircle + TextGap * 1.5f + ChevronSlot) * scale);

    private static float PillWidth(in LiveRowAction action, float scale) =>
        Button.WidthFor(action.Label, ButtonSize.Regular) + (action.Chevron ? (PickChevron + PickChevronGap) * scale : 0f);

    private static Rect PillRect(in LiveRowAction action, float right, float centerY, float scale)
    {
        var height = Button.RegularHeight * scale;
        var width = PillWidth(action, scale);
        return new Rect(new Vector2(right - width, centerY - height * 0.5f), new Vector2(right, centerY + height * 0.5f));
    }

    private static bool DrawPill(ImDrawListPtr drawList, AppSkin ui, Rect pill, in LiveRowAction action,
        bool interactive, float scale)
    {
        var hovered = interactive && UiInteract.Hover(pill.Min, pill.Max);
        var face = Button.Surface(drawList, pill, ui.Ink, action.Primary ? ButtonStyle.Prominent : ButtonStyle.Gray,
            ButtonRole.Normal, true, hovered, ImGui.GetID(action.Id));
        if (!action.Chevron)
        {
            Button.DrawLabel(drawList, face, action.Label);
            return interactive && UiInteract.Click(pill.Min, pill.Max, hovered);
        }

        var style = Button.LabelStyle(face.Face.Height);
        var labelWidth = Typography.Measure(action.Label, style).X;
        var glyph = PickChevron * scale;
        var gap = PickChevronGap * scale;
        var left = face.Face.Center.X - (labelWidth + gap + glyph) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, face.Face.Center.Y - Typography.LineHeight(style) * 0.5f),
            action.Label, face.LabelInk, style);
        PhoneIcon.Draw(drawList, new Vector2(left + labelWidth + gap + glyph * 0.5f, face.Face.Center.Y), DownGlyph,
            face.LabelInk, glyph);
        return interactive && UiInteract.Click(pill.Min, pill.Max, hovered);
    }

    private static void DrawName(ImDrawListPtr drawList, AppSkin ui, string name, bool here, float left, float top,
        float width, float scale)
    {
        var nameStyle = TextStyles.Headline;
        if (!here)
        {
            Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(name, width, nameStyle), ui.TitleInk,
                nameStyle);
            return;
        }

        var tagStyle = TextStyles.FootnoteEmphasized;
        var tag = Loc.T(L.LivePage.Here);
        var tagWidth = Typography.Measure(tag, tagStyle).X + TagPadX * 2f * scale;
        var gap = BadgeGap * scale;
        var fitted = Typography.FitText(name, MathF.Max(1f, width - tagWidth - gap), nameStyle);
        Typography.Draw(drawList, new Vector2(left, top), fitted, ui.TitleInk, nameStyle);
        var nameLine = Typography.LineHeight(nameStyle);
        var tagHeight = Typography.LineHeight(tagStyle) + TagPadY * 2f * scale;
        var tagLeft = left + Typography.Measure(fitted, nameStyle).X + gap;
        var tagMin = new Vector2(tagLeft, top + (nameLine - tagHeight) * 0.5f);
        var tagMax = new Vector2(tagLeft + tagWidth, tagMin.Y + tagHeight);
        var cyan = CasinoColors.LightB;
        var glow = TagGlow * scale;
        Squircle.Fill(drawList, tagMin - new Vector2(glow, glow), tagMax + new Vector2(glow, glow),
            Metrics.Radius.Sm * scale, Color(cyan, HereGlowAlpha * 0.5f));
        Squircle.Fill(drawList, tagMin, tagMax, TagRadius * scale,
            ImGui.GetColorU32(Palette.Lighten(cyan, HereFillLift)));
        Typography.Draw(drawList, new Vector2(tagMin.X + TagPadX * scale, tagMin.Y + TagPadY * scale), tag,
            CasinoColors.InkOnLight, tagStyle);
    }

    private static void DrawBadges(ImDrawListPtr drawList, AppSkin ui, in LiveTableView view, float left, float top,
        float right, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var height = BadgeHeight(scale);
        var tint = CurrencyTint(view.Currency);
        var label = Loc.T(CurrencyName(view.Currency));
        var badgeWidth = MathF.Min(Typography.Measure(label, style).X + BadgePadX * 2f * scale, right - left);
        var badgeMax = new Vector2(left + badgeWidth, top + height);
        Squircle.Fill(drawList, new Vector2(left, top), badgeMax, height * 0.4f, Color(tint, BadgeFillAlpha));
        Typography.Draw(drawList, new Vector2(left + BadgePadX * scale, top + BadgePadY * scale),
            Typography.FitText(label, MathF.Max(1f, badgeWidth - BadgePadX * 2f * scale), style),
            Palette.Lighten(tint, BadgeInkLift), style);
        var cursor = badgeMax.X + BadgeGap * scale;
        var noteStyle = TextStyles.Footnote;
        var noteTop = top + (height - Typography.LineHeight(noteStyle)) * 0.5f;
        if (view.Knock)
        {
            var lockSize = LockSize * scale;
            var textLeft = cursor + lockSize + LockGap * scale;
            if (textLeft + lockSize * 2f <= right)
            {
                var knock = Typography.FitText(Loc.T(L.LivePage.KnockToJoin), right - textLeft, noteStyle);
                PhoneIcon.Draw(drawList, new Vector2(cursor + lockSize * 0.5f, top + height * 0.5f), LockGlyph,
                    ui.BodyInk, lockSize);
                Typography.Draw(drawList, new Vector2(textLeft, noteTop), knock, ui.BodyInk, noteStyle);
                cursor = textLeft + Typography.Measure(knock, noteStyle).X + BadgeGap * scale;
            }
        }

        var status = view.Paused ? Loc.T(L.Tables.PausedBadge) : view.Full ? Loc.T(L.Casino.TableFullBadge) : string.Empty;
        if (status.Length == 0 || cursor + LockSize * 2f * scale > right)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(cursor, noteTop),
            Typography.FitText(status, MathF.Max(1f, right - cursor), noteStyle), ui.MutedInk, noteStyle);
    }

    private static void DrawCrowd(ImDrawListPtr drawList, string crowd, float centerX, float top, float width,
        bool live, AppSkin ui, float scale)
    {
        var style = TextStyles.Footnote;
        var dot = (CasinoArt.LiveDotRadius * 2f + DotGap) * scale;
        var fitted = Typography.FitText(crowd, MathF.Max(1f, width - dot), style);
        var textWidth = Typography.Measure(fitted, style).X;
        var left = centerX - (dot + textWidth) * 0.5f;
        var lineHeight = Typography.LineHeight(style);
        CasinoArt.LiveDot(drawList, new Vector2(left + CasinoArt.LiveDotRadius * scale, top + lineHeight * 0.5f), scale,
            live ? CasinoColors.LightA : ui.MutedInk, live);
        Typography.Draw(drawList, new Vector2(left + dot, top), fitted, StageText.Body, style);
    }

    private static void DrawLines(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 ink, float left,
        float top, float width, int maxLines)
    {
        if (text.Length == 0)
        {
            return;
        }

        var lines = Typography.WrapText(text, style, width);
        var lineHeight = Typography.LineHeight(style);
        var shown = Math.Min(lines.Length, maxLines);
        for (var index = 0; index < shown; index++)
        {
            var line = lines[index];
            var x = left + (width - Typography.Measure(line, style).X) * 0.5f;
            Typography.Draw(drawList, new Vector2(x, top + index * lineHeight), line, ink, style);
        }
    }

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = alpha });
}
