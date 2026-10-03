using Aetherphone.Apps.Music.Components;
using Aetherphone.Apps.Music.Jam;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float JamHomeCompactHeight = 68f;
    private const float JamHomeTile = 40f;
    private const float JamHomeTileGlyphScale = 1.05f;
    private const float JamHomeStackRadius = 13f;
    private const float JamHomeOpenHeight = 30f;
    private const float JamHomeOpenPadX = 16f;
    private const float JamHomeChevronScale = 0.7f;
    private const float JamHomeLiveDot = 3.5f;
    private const float JamHomeHoverAlpha = 0.06f;
    private const float JamHomeMusicGlyphScale = 0.62f;
    private const float JamBadgePillHeight = 34f;
    private const float JamBadgeStackRadius = 10f;
    private const float JamBadgeGlyphScale = 0.78f;
    private const float JamBadgeHoverAlpha = 0.10f;
    private const float JamBadgeCodeAlpha = 0.62f;
    private const int JamBadgeStackMax = 3;
    private const int JamHomeSegments = 16;

    private static readonly Vector4 JamBadgeInk = new(1f, 1f, 1f, 1f);

    private JamTextCache jamHomeEyebrowText;
    private JamTextCache jamHomeListeningText;
    private JamTextCache jamBadgeText;

    private void DrawJamHomeCard(float scale)
    {
        EnsureJamMembers();
        jam.WantNearby();
        JamGap(MusicUi.SectionGap);
        if (jam.InJam)
        {
            DrawJamHomeLive(scale);
            return;
        }

        DrawJamHomeCompact(scale);
    }

    private void DrawJamHomeCompact(float scale)
    {
        var card = BeginJamBlock(JamHomeCompactHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        Material.ThemedGlass(drawList, card.Min, card.Max, rounding, scale, theme);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, JamHomeHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = Metrics.Space.Md * scale;
        var tile = JamHomeTile * scale;
        var tileMin = new Vector2(card.Min.X + pad, card.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            IconTile.Surface(ui.Accent));
        DrawJamGlyph(drawList, tileMin + new Vector2(tile * 0.5f, tile * 0.5f), AccentRing.Ink, JamHomeTileGlyphScale);
        var chevron = new Vector2(card.Max.X - pad - Metrics.Space.Xs * scale, card.Center.Y);
        AppSkin.Icon(drawList, chevron, IconGlyph.Of(FontAwesomeIcon.ChevronRight), ui.MutedInk, JamHomeChevronScale);
        var textLeft = tileMin.X + tile + pad;
        var textWidth = MathF.Max(1f, chevron.X - Metrics.Space.Lg * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var bodyHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = card.Center.Y - (titleHeight + bodyHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(Loc.T(L.Music.Jam.Start), textWidth,
            TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + titleHeight),
            Typography.FitText(JamHomeIdleBody(), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        EndJamBlock();
        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenJamLobby();
        }
    }

    private void DrawJamHomeLive(float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var stackRow = MathF.Max(JamHomeStackRadius * 2f, JamHomeOpenHeight) * scale;
        var card = BeginJamBlock(pad + eyebrowHeight + Metrics.Space.Xs * scale + titleHeight + lineHeight
            + Metrics.Space.Md * scale + stackRow + pad);
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Card * scale;
        var hovered = UiInteract.Hover(card.Min, card.Max);
        Material.ThemedGlass(drawList, card.Min, card.Max, rounding, scale, theme);
        Material.TopGlow(drawList, card.Min, card.Max, rounding, ui.Accent, JamGlowCoverage, JamGlowStrength);
        if (hovered)
        {
            Squircle.Fill(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, JamHomeHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var width = right - left;
        var top = card.Min.Y + pad;
        var dotCenter = new Vector2(left + JamHomeLiveDot * scale, top + eyebrowHeight * 0.5f);
        drawList.AddCircleFilled(dotCenter, JamHomeLiveDot * scale, ImGui.GetColorU32(ui.Accent), JamHomeSegments);
        var eyebrowLeft = dotCenter.X + JamHomeLiveDot * scale + Metrics.Space.Sm * scale;
        var listening = jamHomeListeningText.Format(Loc.T(L.Music.ListeningCount), Math.Max(1, jamMembers.Length));
        var listeningWidth = Typography.Measure(listening, TextStyles.Footnote).X;
        Typography.Draw(drawList, new Vector2(right - listeningWidth, top), listening, ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(eyebrowLeft, top),
            Typography.FitText(jamHomeEyebrowText.Upper(Loc.T(L.Music.Jam.Title)),
                right - listeningWidth - Metrics.Space.Md * scale - eyebrowLeft, TextStyles.FootnoteEmphasized),
            ui.Accent, TextStyles.FootnoteEmphasized);
        top += eyebrowHeight + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(JamTitle, width, TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        top += titleHeight;
        DrawJamHomeNowPlaying(drawList, left, top, width, lineHeight, scale);
        top += lineHeight + Metrics.Space.Md * scale;
        var rowCenter = top + stackRow * 0.5f;
        var stackRadius = JamHomeStackRadius * scale;
        JamAvatarStack.Draw(drawList, new Vector2(left, rowCenter), stackRadius, jamMembers, jamMemberNames,
            JamStackMax, jamOverflowLabel, ui.Palette.BackdropBottom, theme, images, lodestone, scale);
        var openLabel = Loc.T(L.Music.Jam.Open);
        var openWidth = Typography.Measure(openLabel, TextStyles.SubheadlineEmphasized).X + JamHomeOpenPadX * 2f * scale;
        var openHeight = JamHomeOpenHeight * scale;
        var open = new Rect(new Vector2(right - openWidth, rowCenter - openHeight * 0.5f),
            new Vector2(right, rowCenter + openHeight * 0.5f));
        Squircle.Fill(drawList, open.Min, open.Max, openHeight * 0.5f, ImGui.GetColorU32(ui.Accent));
        Typography.DrawCentered(drawList, open.Center, openLabel, AccentRing.Ink, TextStyles.SubheadlineEmphasized);
        EndJamBlock();
        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenJamLobby();
        }
    }

    private void DrawJamHomeNowPlaying(ImDrawListPtr drawList, float left, float top, float width, float lineHeight,
        float scale)
    {
        var song = JamNowPlaying;
        if (song.IsEmpty)
        {
            Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(Loc.T(jam.IsHost
                    ? L.Music.Jam.NothingPlayingHost
                    : L.Music.Jam.NothingPlayingGuest), width, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
            return;
        }

        var glyphBox = lineHeight;
        AppSkin.Icon(drawList, new Vector2(left + glyphBox * 0.5f, top + lineHeight * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.Music), ui.Accent, JamHomeMusicGlyphScale);
        var textLeft = left + glyphBox + Metrics.Space.Xxs * scale;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(song.Title,
            MathF.Max(1f, left + width - textLeft), TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
    }

    private float DrawJamNowPlayingBadge(ImDrawListPtr drawList, Vector2 min, float width, float scale)
    {
        if (!jam.InJam)
        {
            return 0f;
        }

        EnsureJamMembers();
        var pillHeight = JamBadgePillHeight * scale;
        var others = JamOthersCount;
        var label = others > 0 ? jamBadgeText.Format(Loc.T(L.Music.Jam.JamWith), others) : Loc.T(L.Music.Jam.Title);
        var stackRadius = JamBadgeStackRadius * scale;
        var stackWidth = JamAvatarStack.Width(jamMembers.Length, JamBadgeStackMax, stackRadius);
        var gap = Metrics.Space.Sm * scale;
        var glyphBox = pillHeight * 0.5f;
        var code = jam.DisplayCode;
        var labelWidth = Typography.Measure(label, TextStyles.SubheadlineEmphasized).X;
        var codeWidth = code.Length > 0 ? Typography.Measure(code, TextStyles.FootnoteEmphasized).X + gap : 0f;
        var content = glyphBox + gap + labelWidth + gap + stackWidth + codeWidth;
        var pillWidth = MathF.Min(width, content + pillHeight);
        var pillMin = new Vector2(min.X + (width - pillWidth) * 0.5f, min.Y);
        var pillMax = pillMin + new Vector2(pillWidth, pillHeight);
        var radius = pillHeight * 0.5f;
        var hovered = UiInteract.Hover(pillMin, pillMax);
        Material.LiquidGlass(drawList, pillMin, pillMax, radius, scale, GlassTone.Dark, 0f);
        if (hovered)
        {
            Squircle.Fill(drawList, pillMin, pillMax, radius,
                ImGui.GetColorU32(Palette.WithAlpha(JamBadgeInk, JamBadgeHoverAlpha)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var cursor = pillMin.X + radius;
        var right = pillMax.X - radius;
        DrawJamGlyph(drawList, new Vector2(cursor + glyphBox * 0.5f, pillMin.Y + radius), ui.Accent, JamBadgeGlyphScale);
        cursor += glyphBox + gap;
        if (codeWidth > 0f)
        {
            var codeText = Typography.FitText(code, codeWidth - gap, TextStyles.FootnoteEmphasized);
            var codeHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(right - codeWidth + gap, pillMin.Y + radius - codeHeight * 0.5f),
                codeText, Palette.WithAlpha(JamBadgeInk, JamBadgeCodeAlpha), TextStyles.FootnoteEmphasized);
            right -= codeWidth;
        }

        if (stackWidth > 0f && right - stackWidth - gap > cursor)
        {
            JamAvatarStack.Draw(drawList, new Vector2(right - stackWidth, pillMin.Y + radius), stackRadius, jamMembers,
                jamMemberNames, JamBadgeStackMax, jamBadgeOverflowLabel, ui.Palette.BackdropBottom, theme, images,
                lodestone, scale);
            right -= stackWidth + gap;
        }

        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(cursor, pillMin.Y + radius - labelHeight * 0.5f),
            Typography.FitText(label, MathF.Max(1f, right - cursor), TextStyles.SubheadlineEmphasized), JamBadgeInk,
            TextStyles.SubheadlineEmphasized);
        if (UiInteract.Click(pillMin, pillMax, hovered))
        {
            OpenJamLobby();
        }

        var barTop = pillMax.Y + gap;
        var bar = new Rect(new Vector2(min.X, barTop), new Vector2(min.X + width, barTop + JamReactionBar.Height * scale));
        var kind = JamReactionBar.Draw(drawList, bar, GlassTone.Dark, JamBadgeInk, scale);
        if (kind >= 0)
        {
            jam.React(kind);
        }

        JamReactionBar.DrawRising(drawList, bar, jam.Reactions, scale);
        return bar.Max.Y - min.Y;
    }
}
