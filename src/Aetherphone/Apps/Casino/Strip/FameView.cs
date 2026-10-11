using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Coin;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class FameView
{
    private const float SegmentHeight = 36f;
    private const float RowHeight = 56f;
    private const float RankWidth = 36f;
    private const float ChampionHeight = 64f;

    private readonly CasinoFloorStore floor;
    private readonly CasinoTextCache texts = new();
    private readonly string[] boardLabels = new string[CasinoFameBoards.All.Length];
    private readonly string[] spanLabels = new string[2];
    private int board;
    private int span;

    public FameView(CasinoFloorStore floor)
    {
        this.floor = floor;
    }

    public void Enter()
    {
        board = 0;
        span = 0;
    }

    public void Draw(Rect body, AppSkin ui)
    {
        var scale = UiScale.Current;
        var boardKey = CasinoFameBoards.All[board];
        var spanKey = span == 0 ? CasinoFameSpans.Week : CasinoFameSpans.Last;
        floor.EnsureFame(boardKey, spanKey);
        using var surface = AppSurface.Begin(body);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        for (var index = 0; index < boardLabels.Length; index++)
        {
            boardLabels[index] = Loc.T(FameText.BoardName(CasinoFameBoards.All[index]));
        }

        spanLabels[0] = Loc.T(L.Club.FameThisWeek);
        spanLabels[1] = Loc.T(L.Club.FameLastWeek);
        var segment = SegmentHeight * scale;
        var boardRow = new Rect(origin, new Vector2(origin.X + width, origin.Y + segment));
        board = SegmentStrip.Draw("casino.fame.board", boardRow, boardLabels, board, ui.Palette, SegmentHeight);
        var spanRow = new Rect(new Vector2(origin.X, boardRow.Max.Y + Metrics.Space.Sm * scale),
            new Vector2(origin.X + width, boardRow.Max.Y + Metrics.Space.Sm * scale + segment));
        span = SegmentStrip.Draw("casino.fame.span", spanRow, spanLabels, span, ui.Palette, SegmentHeight);
        var top = spanRow.Max.Y + Metrics.Space.Md * scale;
        var data = floor.Fame(boardKey, spanKey);
        if (data is null)
        {
            top = CoinArt.DrawPanel(ui, new Vector2(origin.X, top), width, FontAwesomeIcon.Trophy, CasinoColors.Money,
                Loc.T(L.Club.FameTitle), Loc.T(L.Casino.TablesLoading), scale);
            CoinArt.Reserve(origin, width, top + CoinArt.BottomPad * scale);
            return;
        }

        if (data.Champion is { } champion)
        {
            top = DrawChampion(drawList, ui, data.Board, champion, new Vector2(origin.X, top), width, scale)
                  + Metrics.Space.Md * scale;
        }

        top = DrawMe(drawList, ui, data, new Vector2(origin.X, top), width, scale) + Metrics.Space.Md * scale;
        var entries = data.Entries ?? Array.Empty<CasinoFameEntryDto>();
        if (entries.Length == 0)
        {
            top = CoinArt.DrawPanel(ui, new Vector2(origin.X, top), width, FontAwesomeIcon.Trophy, CasinoColors.Money,
                Loc.T(L.Club.FameTitle), Loc.T(L.Club.FameEmpty), scale);
        }
        else
        {
            var rowHeight = RowHeight * scale;
            var min = new Vector2(origin.X, top);
            var max = new Vector2(origin.X + width, top + rowHeight * entries.Length);
            ui.Card(drawList, min, max, Metrics.Radius.Grouped * scale);
            for (var index = 0; index < entries.Length; index++)
            {
                var row = new Rect(new Vector2(min.X, min.Y + index * rowHeight),
                    new Vector2(max.X, min.Y + (index + 1) * rowHeight));
                DrawEntry(drawList, ui, data.Board, entries[index], row, index > 0, scale);
            }

            top = max.Y;
        }

        top += Metrics.Space.Md * scale;
        top += Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(L.Club.FameOptIn), ui.BodyInk,
            TextStyles.Footnote, width);
        CoinArt.Reserve(origin, width, top + CoinArt.BottomPad * scale);
    }

    private float DrawChampion(ImDrawListPtr drawList, AppSkin ui, string boardKey, CasinoFameEntryDto champion,
        Vector2 origin, float width, float scale)
    {
        var height = ChampionHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.FillVerticalGradient(drawList, origin, max, radius,
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.28f }), ImGui.GetColorU32(CasinoColors.Money with { W = 0.08f }));
        Squircle.Stroke(drawList, origin, max, radius, ImGui.GetColorU32(CasinoColors.Money with { W = 0.6f }),
            MathF.Max(1f, scale));
        var pad = Metrics.Space.Lg * scale;
        var tile = 36f * scale;
        var tileCenter = new Vector2(origin.X + pad + tile * 0.5f, origin.Y + height * 0.5f);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, CasinoColors.Money, FontAwesomeIcon.Crown);
        var textLeft = tileCenter.X + tile * 0.5f + CoinArt.TextGap * scale;
        CoinArt.Labels(drawList, textLeft, max.X - pad, tileCenter.Y, FameText.Name(champion),
            texts.Named(L.Club.FameChampionValue, FameText.Value(CasinoFameBoards.Profit, champion.Value)), ui.TitleInk,
            CasinoColors.Money, scale);
        return max.Y;
    }

    private float DrawMe(ImDrawListPtr drawList, AppSkin ui, CasinoFameBoardDto data, Vector2 origin, float width,
        float scale)
    {
        var me = data.Me;
        if (me is null)
        {
            return origin.Y;
        }

        var line = me.Rank > 0
            ? texts.Number(L.Club.FameYourRank, me.Rank)
            : Loc.T(L.Club.FameNotRanked);
        var value = me.Value != 0 ? FameText.Value(data.Board, me.Value) : string.Empty;
        var height = RowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var valueWidth = value.Length > 0 ? Typography.Measure(value, TextStyles.Headline).X : 0f;
        Typography.Draw(drawList, new Vector2(origin.X + pad, origin.Y + (height - Typography.LineHeight(TextStyles.Headline)) * 0.5f),
            Typography.FitText(line, width - pad * 3f - valueWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        if (valueWidth > 0f)
        {
            Typography.Draw(drawList,
                new Vector2(max.X - pad - valueWidth, origin.Y + (height - Typography.LineHeight(TextStyles.Headline)) * 0.5f),
                value, CasinoColors.Money, TextStyles.Headline);
        }

        return max.Y;
    }

    private void DrawEntry(ImDrawListPtr drawList, AppSkin ui, string boardKey, CasinoFameEntryDto entry, Rect row,
        bool hairline, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        if (hairline)
        {
            CoinArt.Hairline(drawList, ui, row.Min.X + pad, row.Max.X, row.Min.Y);
        }

        var rankWidth = RankWidth * scale;
        var rankCenter = new Vector2(row.Min.X + pad + rankWidth * 0.5f, row.Center.Y);
        var podium = entry.Rank is >= 1 and <= CasinoFloorRules.PodiumPlaces;
        if (podium)
        {
            drawList.AddCircleFilled(rankCenter, rankWidth * 0.42f,
                ImGui.GetColorU32(CasinoColors.Money with { W = entry.Rank == 1 ? 0.9f : 0.35f }), 24);
        }

        Typography.DrawCentered(drawList, rankCenter, Games.Framework.GameNumber.Label(entry.Rank),
            podium && entry.Rank == 1 ? CasinoColors.FeltBottom : ui.TitleInk, TextStyles.Headline);
        var value = FameText.Value(boardKey, entry.Value);
        var valueWidth = Typography.Measure(value, TextStyles.Headline).X;
        var textLeft = rankCenter.X + rankWidth * 0.5f + CoinArt.TextGap * scale;
        var textRight = row.Max.X - pad - valueWidth - Metrics.Space.Sm * scale;
        var detail = entry.GameKind.Length > 0
            ? Loc.T(CasinoGameNames.Of(CasinoRecentGames.ClientGameId(entry.GameKind)))
            : string.Empty;
        if (detail.Length > 0)
        {
            CoinArt.Labels(drawList, textLeft, textRight, row.Center.Y, FameText.Name(entry), detail,
                entry.Player is null ? ui.BodyInk : ui.TitleInk, ui.BodyInk, scale);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f),
                Typography.FitText(FameText.Name(entry), MathF.Max(1f, textRight - textLeft), TextStyles.Headline),
                entry.Player is null ? ui.BodyInk : ui.TitleInk, TextStyles.Headline);
        }

        Typography.Draw(drawList, new Vector2(row.Max.X - pad - valueWidth, row.Center.Y - Typography.LineHeight(TextStyles.Headline) * 0.5f),
            value, CasinoColors.Money, TextStyles.Headline);
    }
}
