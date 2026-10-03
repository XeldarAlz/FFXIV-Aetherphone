using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float GameRowHeight = 96f;
    private const float GameRowGap = 10f;
    private const float InfoButtonSize = 30f;

    private void DrawGamesTab(Rect body)
    {
        var scale = UiScale.Current;
        using var surface = AppSurface.Begin(body);

        DrawStakeNotice(scale);
        ui.SectionHeading(Loc.T(L.Casino.GamesHeading), 4f);

        for (var index = 0; index < FloorTiles.Length; index++)
        {
            DrawGameRow(FloorTiles[index], index == 0, scale);
            if (index + 1 < FloorTiles.Length)
            {
                ImGui.Dummy(new Vector2(0f, GameRowGap * scale));
            }
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
        if (DrawNavRow(FontAwesomeIcon.ThList, L.Casino.TablesRow, L.Casino.TablesRowHint, scale))
        {
            OpenTables();
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
    }

    private void DrawGameRow(in FloorTileDefinition definition, bool reportsRules, float scale)
    {
        var width = ScrollLayout.StableContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var height = GameRowHeight * scale;
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var rounding = Metrics.Radius.Card * scale;

        var infoMax = new Vector2(row.Max.X - 12f * scale, row.Min.Y + 12f * scale + InfoButtonSize * scale);
        var infoMin = new Vector2(infoMax.X - InfoButtonSize * scale, row.Min.Y + 12f * scale);
        var infoRect = new Rect(infoMin, infoMax);
        if (reportsRules)
        {
            UiAnchors.Report("casino.rules", infoRect);
        }

        var infoHovered = UiInteract.Hover(infoRect.Min, infoRect.Max);
        var hovered = definition.Playable && !infoHovered && UiInteract.Hover(row.Min, row.Max);

        ui.Card(drawList, row.Min, row.Max, rounding);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var glyphCenter = new Vector2(row.Min.X + 42f * scale, row.Min.Y + 40f * scale);
        var glyphRadius = 22f * scale;
        drawList.AddCircleFilled(glyphCenter, glyphRadius, ImGui.GetColorU32(ui.Palette.FieldSurface), 40);
        drawList.AddCircle(glyphCenter, glyphRadius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.35f)), 40, Metrics.Stroke.Thin * scale);
        CasinoGlyphs.Draw(drawList, definition.GameId, glyphCenter, 12f * scale,
            ImGui.GetColorU32(ui.TitleInk), ImGui.GetColorU32(ui.Palette.FieldSurface));

        var textLeft = row.Min.X + 76f * scale;
        var textRight = infoMin.X - 10f * scale;
        var textWidth = textRight - textLeft;

        var name = Typography.FitText(Loc.T(definition.Name), textWidth, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 14f * scale), name, ui.TitleInk,
            TextStyles.Headline);

        var pitch = Typography.FitText(Loc.T(CasinoRules.PitchOf(definition.GameId)), textWidth,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(textLeft, row.Min.Y + 38f * scale), pitch, ui.MutedInk,
            TextStyles.Footnote);

        var stakeLine = MinimumStakeLine(definition.GameId);
        if (stakeLine.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Max.Y - 26f * scale), stakeLine, ui.BodyInk,
                TextStyles.Caption1);
        }

        var crowd = CrowdAt(definition);
        if (crowd > 0)
        {
            var crowdText = Loc.T(L.Casino.LivePlayers, Apps.Games.Framework.GameNumber.Label(crowd));
            var crowdSize = Typography.Measure(crowdText, TextStyles.Caption1);
            var dotCenter = new Vector2(row.Max.X - 14f * scale - crowdSize.X - 10f * scale, row.Max.Y - 20f * scale);
            drawList.AddCircleFilled(dotCenter, 3.5f * scale, ImGui.GetColorU32(ui.Accent), 12);
            Typography.Draw(drawList, new Vector2(dotCenter.X + 8f * scale, row.Max.Y - 26f * scale), crowdText,
                ui.Accent, TextStyles.Caption1);
        }

        DrawInfoButton(drawList, infoRect, infoHovered, scale);

        if (UiInteract.Click(infoRect.Min, infoRect.Max, infoHovered))
        {
            rulesSheet.Open(definition.GameId);
            return;
        }

        var clicked = UiInteract.Click(row.Min, row.Max, hovered);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
        if (clicked)
        {
            OpenGame(definition.GameId);
        }
    }

    private void DrawInfoButton(ImDrawListPtr drawList, Rect rect, bool hovered, float scale)
    {
        var rounding = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.FieldSurface));
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        AppSkin.Icon(drawList, rect.Center, IconGlyph.Of(FontAwesomeIcon.Question),
            hovered ? ui.Accent : ui.MutedInk, 0.72f);
    }
}
