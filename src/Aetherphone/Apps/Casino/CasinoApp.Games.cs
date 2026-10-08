using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const float GameRowPad = 16f;
    private const float GameRowTile = 52f;
    private const float GameRowLineGap = 4f;
    private const int GamePitchLines = 2;
    private const float InfoButtonSize = 32f;
    private const float InfoGlyphScale = 0.72f;
    private const float InfoFillAlpha = 0.10f;
    private const float InfoHoverAlpha = 0.18f;

    private static readonly LocString[] OriginalsGameNames =
    {
        L.Originals.GameMines,
        L.Originals.GameDice,
        L.Originals.GameLimbo,
        L.Originals.GameKeno,
        L.Originals.GameHiLo,
    };

    private void DrawGamesTab(Rect body)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId("casino.games"))
        using (AppSurface.Begin(body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawStakeNotice(origin, width, scale);
            for (var index = 0; index < FloorGameIds.Length; index++)
            {
                if (index > 0)
                {
                    cursorY += CardGap * scale;
                }

                using (ImRaii.PushId(index))
                {
                    cursorY = DrawGameRow(drawList, new Vector2(origin.X, cursorY), width, FloorGameIds[index],
                        FloorGameNames[index], index == 0, scale);
                }
            }

            for (var index = 0; index < OriginalsGameNames.Length; index++)
            {
                cursorY += CardGap * scale;
                using (ImRaii.PushId(FloorGameIds.Length + index))
                {
                    cursorY = DrawGameRow(drawList, new Vector2(origin.X, cursorY), width,
                        Originals.OriginalsCabinet.GameIds[index], OriginalsGameNames[index], false, scale);
                }
            }

            using (ImRaii.PushId(FloorGameIds.Length + OriginalsGameNames.Length))
            {
                cursorY = DrawGameRow(drawList, new Vector2(origin.X, cursorY + CardGap * scale), width,
                    CasinoGames.Race, L.Race.Title, false, scale);
            }

            cursorY += CardGap * scale;
            using (ImRaii.PushId(CasinoGames.Plinko))
            {
                cursorY = DrawGameRow(drawList, new Vector2(origin.X, cursorY), width, CasinoGames.Plinko,
                    L.Plinko.Game, false, scale);
            }

            cursorY = DrawTablesLink(drawList, new Vector2(origin.X, cursorY + CoinArt.SectionGap * scale), width,
                scale);
            CoinArt.Reserve(origin, width, cursorY + CoinArt.BottomPad * scale);
        }
    }

    private float DrawGameRow(ImDrawListPtr drawList, Vector2 origin, float width, string gameId, LocString name,
        bool first, float scale)
    {
        var pad = GameRowPad * scale;
        var tile = GameRowTile * scale;
        var info = InfoButtonSize * scale;
        var textLeft = origin.X + pad + tile + CoinArt.TextGap * scale;
        var textRight = origin.X + width - pad - info - CoinArt.ValueGap * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var pitch = WidgetText.Clamp(Loc.T(CasinoRules.PitchOf(gameId)), TextStyles.Footnote, textWidth,
            GamePitchLines);
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var textHeight = headline + GameRowLineGap * scale + pitch.Length * footnote + GameRowLineGap * scale +
                         footnote;
        var height = MathF.Max(tile, textHeight) + pad * 2f;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);

        var infoMin = new Vector2(max.X - pad - info, min.Y + pad);
        var infoMax = infoMin + new Vector2(info, info);
        if (first)
        {
            UiAnchors.Report("casino.rules", new Rect(infoMin, infoMax));
        }

        var overInfo = UiInteract.Hover(infoMin, infoMax);
        var hovered = !overInfo && UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(ImGui.GetID("card"), pressed, Core.Animation.Motion.PressScaleCard);
        var center = (min + max) * 0.5f;
        var half = (max - min) * 0.5f * factor;
        ui.Card(drawList, center - half, center + half, Metrics.Radius.Grouped * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        CasinoArt.GameTile(drawList, gameId, new Vector2(min.X + pad + tile * 0.5f, min.Y + pad + tile * 0.5f),
            tile);
        var top = min.Y + pad;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.T(name), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        top += headline + GameRowLineGap * scale;
        top = WidgetText.Lines(drawList, pitch, new Vector2(textLeft, top), ui.MutedInk, TextStyles.Footnote,
            footnote);
        top += GameRowLineGap * scale;
        DrawGameRowFooter(drawList, gameId, textLeft, max.X - pad, top, scale);

        DrawInfoButton(drawList, new Rect(infoMin, infoMax), overInfo);
        if (UiInteract.Click(infoMin, infoMax, overInfo))
        {
            rulesSheet.Open(gameId);
        }
        else if (UiInteract.Click(min, max, hovered))
        {
            OpenGame(gameId);
        }

        return max.Y;
    }

    private void DrawGameRowFooter(ImDrawListPtr drawList, string gameId, float left, float right, float top,
        float scale)
    {
        var racing = string.Equals(gameId, CasinoGames.Race, StringComparison.Ordinal);
        var crowd = racing ? casinoRooms.OccupancyOf(Core.Casino.CasinoRoomIds.RaceTrack) : CrowdAt(gameId);
        var crowdWidth = 0f;
        if (crowd > 0)
        {
            var crowdText = texts.Count(L.Casino.LivePlayers, crowd);
            var crowdSize = Typography.Measure(crowdText, TextStyles.FootnoteEmphasized);
            var crowdX = right - crowdSize.X;
            Typography.Draw(drawList, new Vector2(crowdX, top), crowdText, ui.Accent, TextStyles.FootnoteEmphasized);
            CasinoArt.LiveDot(drawList, new Vector2(crowdX - (CasinoArt.LiveDotRadius + 4f) * scale,
                top + crowdSize.Y * 0.5f), scale, ui.Accent, true);
            crowdWidth = crowdSize.X + (CasinoArt.LiveDotRadius * 2f + 4f + CoinArt.ValueGap) * scale;
        }

        var stake = racing
            ? texts.Number(L.Casino.MinimumStake, Core.Casino.RaceRules.MinBet)
            : Originals.OriginalsCabinet.Owns(gameId)
                ? texts.Number(L.Casino.MinimumStake, Core.Casino.OriginalsRules.MinBet)
                : string.Equals(gameId, CasinoGames.Plinko, StringComparison.Ordinal)
                    ? texts.Number(L.Casino.MinimumStake, Core.Casino.PlinkoRules.MinBet)
                    : MinimumStakeLine(gameId);
        if (stake.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(stake, MathF.Max(1f, right - crowdWidth - left), TextStyles.Footnote), ui.BodyInk,
            TextStyles.Footnote);
    }

    private void DrawInfoButton(ImDrawListPtr drawList, Rect rect, bool hovered)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var factor = PressFx.Scale(ImGui.GetID("info"), pressed, Core.Animation.Motion.PressScaleControl);
        var radius = rect.Height * 0.5f * factor;
        drawList.AddCircleFilled(rect.Center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, hovered ? InfoHoverAlpha : InfoFillAlpha)), 32);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        AppSkin.Icon(drawList, rect.Center, IconGlyph.Of(FontAwesomeIcon.Question),
            hovered ? ui.Accent : ui.BodyInk, InfoGlyphScale);
    }

    private float DrawTablesLink(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = RecordRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        if (DrawRecordRow(drawList, new Rect(origin, max), FontAwesomeIcon.ThList, CasinoArt.TintOf(CasinoGames.Blackjack),
                L.Casino.TablesRow, L.Casino.TablesRowHint, false, scale))
        {
            OpenTables();
        }

        return max.Y;
    }
}
