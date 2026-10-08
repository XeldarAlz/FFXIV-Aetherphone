using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class FamePodium
{
    public const float Pad = 16f;
    public const float StepUnit = 22f;
    public const float Medal = 30f;
    public const float ColumnGap = 8f;

    private static readonly int[] ColumnRanks = { 1, 0, 2 };
    private static readonly float[] StepHeights = { 3f, 2f, 1.4f };

    private static readonly Vector4[] MedalTints =
    {
        CasinoColors.Money,
        new(0.78f, 0.80f, 0.86f, 1f),
        new(0.80f, 0.52f, 0.30f, 1f),
    };

    private readonly CasinoTextCache texts = new();
    private readonly string[] rankLabels = { "1", "2", "3" };

    public static float Height(float scale, float headline, float footnote, float championLine) =>
        Pad * scale * 2f + headline + Metrics.Space.Md * scale + Medal * scale + footnote * 2f
        + StepUnit * StepHeights[0] * scale + championLine;

    public bool Draw(ImDrawListPtr drawList, AppSkin ui, CasinoFameBoardDto board, Vector2 origin, float width,
        float scale, out float bottom)
    {
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var champion = board.Champion;
        var championLine = champion is null ? 0f : Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Sm * scale;
        var height = Height(scale, headline, footnote, championLine);
        var max = new Vector2(origin.X + width, origin.Y + height);
        bottom = max.Y;
        var hovered = CasinoArt.PressCard(ImGui.GetID("casino.fame.podium"), origin, max, out var min, out var shown);
        ui.Card(drawList, min, shown, Metrics.Radius.Grouped * scale);
        var pad = Pad * scale;
        var seeAll = Loc.T(L.Club.FameSeeAll);
        var seeAllWidth = Typography.Measure(seeAll, TextStyles.SubheadlineEmphasized).X;
        Typography.Draw(drawList, new Vector2(min.X + pad, min.Y + pad),
            Typography.FitText(Loc.T(L.Club.FamePodiumTitle), width - pad * 3f - seeAllWidth, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(shown.X - pad - seeAllWidth, min.Y + pad), seeAll, ui.Accent,
            TextStyles.SubheadlineEmphasized);
        var floor = shown.Y - pad - championLine;
        var columnWidth = (width - pad * 2f - ColumnGap * scale * 2f) / 3f;
        var entries = board.Entries ?? Array.Empty<CasinoFameEntryDto>();
        for (var column = 0; column < ColumnRanks.Length; column++)
        {
            var rank = ColumnRanks[column];
            var left = min.X + pad + column * (columnWidth + ColumnGap * scale);
            var stepTop = floor - StepUnit * StepHeights[rank] * scale;
            Squircle.Fill(drawList, new Vector2(left, stepTop), new Vector2(left + columnWidth, floor),
                Metrics.Radius.Sm * scale, ImGui.GetColorU32(MedalTints[rank] with { W = 0.18f }));
            Typography.DrawCentered(drawList, new Vector2(left + columnWidth * 0.5f, (stepTop + floor) * 0.5f),
                rankLabels[rank], MedalTints[rank], TextStyles.Title3);
            if (rank >= entries.Length)
            {
                continue;
            }

            var entry = entries[rank];
            var valueTop = stepTop - footnote - Metrics.Space.Xs * scale;
            var nameTop = valueTop - footnote;
            var medalCenter = new Vector2(left + columnWidth * 0.5f, nameTop - Metrics.Space.Xs * scale - Medal * scale * 0.5f);
            drawList.AddCircleFilled(medalCenter, Medal * scale * 0.5f, ImGui.GetColorU32(MedalTints[rank]), 28);
            AppSkin.Icon(drawList, medalCenter, IconGlyph.Of(rank == 0 ? FontAwesomeIcon.Crown : FontAwesomeIcon.Trophy),
                CasinoColors.FeltBottom, 0.9f);
            Typography.DrawCentered(drawList, new Vector2(medalCenter.X, nameTop + footnote * 0.5f),
                Typography.FitText(FameText.Name(entry), columnWidth, TextStyles.FootnoteEmphasized), ui.TitleInk,
                TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, new Vector2(medalCenter.X, valueTop + footnote * 0.5f),
                Typography.FitText(FameText.Value(board.Board, entry.Value), columnWidth, TextStyles.FootnoteEmphasized),
                CasinoColors.Money, TextStyles.FootnoteEmphasized);
        }

        if (champion is not null)
        {
            var line = texts.Named(L.Club.FameChampionLine, FameText.Name(champion));
            Typography.Draw(drawList, new Vector2(min.X + pad, floor + Metrics.Space.Sm * scale),
                Typography.FitText(line, width - pad * 2f, TextStyles.Subheadline), ui.BodyInk, TextStyles.Subheadline);
        }

        return UiInteract.Click(origin, max, hovered);
    }
}
