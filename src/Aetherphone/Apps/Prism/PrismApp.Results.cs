using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int PodiumSize = 3;
    private const float RankBadgeRadius = 12f;

    private static readonly string[] RankDigits = { "1", "2", "3" };

    private PrismResultsDto? linesFor;
    private string linesLang = string.Empty;
    private string placementLine = string.Empty;
    private string finalLine = string.Empty;
    private string percentLine = string.Empty;
    private string coinsLine = string.Empty;

    private void DrawResults(Rect body)
    {
        using var surface = AppSurface.Begin(body);
        if (store.Results is not { } results)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Common.Loading), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        SyncResultLines(results);
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Lg * scale;
        var top = DrawPlacementCard(drawList, origin, width, scale) + gap;
        top = DrawPodium(drawList, new Vector2(origin.X, top), width, Loc.T(L.Prism.YourHeat), results.HeatPodium,
            scale);
        top = DrawPodium(drawList, new Vector2(origin.X, top), width, Loc.T(L.Prism.FinalTitle), results.FinalPodium,
            scale);
        ImGui.Dummy(new Vector2(width, top - origin.Y + BottomPad * scale));
    }

    private void SyncResultLines(PrismResultsDto results)
    {
        var lang = Loc.Current.Code;
        if (ReferenceEquals(linesFor, results) && linesLang == lang)
        {
            return;
        }

        linesFor = results;
        linesLang = lang;
        placementLine = results.MyHeatRank > 0
            ? Loc.T(L.Prism.HeatRank, results.MyHeatRank, results.HeatSize)
            : Loc.T(L.Prism.NotEntered);
        finalLine = results.MyFinalRank > 0 ? Loc.T(L.Prism.FinalRank, results.MyFinalRank) : string.Empty;
        percentLine = results.MyHeatRank > 0 && results.Percentile > 0
            ? Loc.T(L.Prism.TopPercent, results.Percentile)
            : string.Empty;
        coinsLine = results.CoinsAwarded > 0 ? Loc.T(L.Prism.CoinsWon, results.CoinsAwarded) : string.Empty;
    }

    private float DrawPlacementCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var inner = width - pad * 2f;
        var lineGap = Metrics.Space.Xxs * scale;
        var height = pad * 2f + Typography.MeasureWrappedBlock(placementLine, TextStyles.Title3, inner).Y;
        height += LineBlock(finalLine, TextStyles.Body, inner, lineGap);
        height += LineBlock(percentLine, TextStyles.Body, inner, lineGap);
        height += LineBlock(coinsLine, TextStyles.Headline, inner, lineGap);
        var bottom = origin.Y + height;
        ui.Card(drawList, origin, new Vector2(origin.X + width, bottom), Metrics.Radius.Card * scale);

        var left = origin.X + pad;
        var top = origin.Y + pad;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), placementLine, ui.TitleInk, TextStyles.Title3, inner);
        top = DrawLine(left, top, finalLine, ui.BodyInk, TextStyles.Body, inner, lineGap);
        top = DrawLine(left, top, percentLine, ui.BodyInk, TextStyles.Body, inner, lineGap);
        DrawLine(left, top, coinsLine, ui.Accent, TextStyles.Headline, inner, lineGap);
        return bottom;
    }

    private static float LineBlock(string text, in TextStyle style, float width, float gap) =>
        text.Length > 0 ? Typography.MeasureWrappedBlock(text, style, width).Y + gap : 0f;

    private static float DrawLine(float left, float top, string text, Vector4 color, in TextStyle style, float width,
        float gap)
    {
        if (text.Length == 0)
        {
            return top;
        }

        return top + gap + Typography.DrawWrappedLeft(new Vector2(left, top + gap), text, color, style, width);
    }

    private float DrawPodium(ImDrawListPtr drawList, Vector2 origin, float width, string title,
        PrismPlacingDto[]? podium, float scale)
    {
        if (podium is null || podium.Length == 0)
        {
            return origin.Y;
        }

        var top = origin.Y;
        Typography.Draw(drawList, new Vector2(origin.X, top), title, ui.HeaderInk, TextStyles.FootnoteEmphasized);
        top += Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale;
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (width - gap * (PodiumSize - 1)) / PodiumSize;
        var imageHeight = tileWidth / PrismStore.EntryRatio;
        var nameHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var worldHeight = Typography.LineHeight(TextStyles.Caption1);
        var count = Math.Min(podium.Length, PodiumSize);
        for (var index = 0; index < count; index++)
        {
            var placing = podium[index];
            var left = origin.X + index * (tileWidth + gap);
            var image = new Rect(new Vector2(left, top), new Vector2(left + tileWidth, top + imageHeight));
            DrawLook(drawList, image, placing.ImageUrl, Metrics.Radius.Md * scale, false);
            DrawRankBadge(drawList, image, placing.Rank, scale);
            var textTop = image.Max.Y + Metrics.Space.Xs * scale;
            Typography.Draw(drawList, new Vector2(left, textTop),
                Typography.FitText(placing.DisplayName, tileWidth, TextStyles.FootnoteEmphasized), ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(left, textTop + nameHeight),
                Typography.FitText(placing.World, tileWidth, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
        }

        return top + imageHeight + Metrics.Space.Xs * scale + nameHeight + worldHeight + Metrics.Space.Xl * scale;
    }

    private void DrawRankBadge(ImDrawListPtr drawList, Rect image, int rank, float scale)
    {
        if (rank < 1 || rank > RankDigits.Length)
        {
            return;
        }

        var radius = RankBadgeRadius * scale;
        var center = new Vector2(image.Min.X + radius + Metrics.Space.Xs * scale,
            image.Min.Y + radius + Metrics.Space.Xs * scale);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.Accent));
        Typography.DrawCentered(drawList, center, RankDigits[rank - 1], AccentRing.Ink, TextStyles.FootnoteEmphasized);
    }
}
