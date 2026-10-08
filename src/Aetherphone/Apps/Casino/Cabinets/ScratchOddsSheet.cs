using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Cabinets;

internal sealed class ScratchOddsSheet
{
    private const float RowHeight = 40f;
    private const float PanelHeightShare = 0.72f;

    private readonly SheetSurface sheet = new("casino.scratchOdds");
    private readonly Action<Rect> drawSheetBody;

    private readonly string[] priceLines = new string[ScratchRules.TierCount];
    private readonly string[] chances = new string[ScratchRules.PrizeSymbolCount];

    private AppSkin skin = null!;
    private int tier;
    private string returnLine = string.Empty;
    private LanguageInfo? labelsLanguage;

    public ScratchOddsSheet()
    {
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, int tier)
    {
        skin = ui;
        this.tier = tier;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Casino.ScratchOdds), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##scratchOddsRows", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawRows(skin, UiScale.Current, tier);
        }
    }

    private void DrawRows(AppSkin ui, float scale, int tier)
    {
        RefreshLabels();
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var intro = Loc.T(L.Casino.ScratchOddsIntro);
        var introOrigin = ImGui.GetCursorScreenPos();
        var introBlock = Typography.MeasureWrappedBlock(intro, TextStyles.Footnote, width);
        Typography.DrawWrappedLeft(introOrigin, intro, ui.MutedInk, TextStyles.Footnote, width);
        ImGui.Dummy(new Vector2(width, introBlock.Y + 10f * scale));

        var priceOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, priceOrigin, Typography.FitText(priceLines[tier], width, TextStyles.SubheadlineEmphasized),
            ui.TitleInk, TextStyles.SubheadlineEmphasized);
        ImGui.Dummy(new Vector2(width, 26f * scale));

        var headerOrigin = ImGui.GetCursorScreenPos();
        var chanceHeader = Loc.T(L.Casino.ScratchOddsChance);
        var chanceHeaderSize = Typography.Measure(chanceHeader, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(headerOrigin.X + 44f * scale, headerOrigin.Y + 6f * scale),
            Typography.FitText(Loc.T(L.Casino.ScratchOddsPrize), width - chanceHeaderSize.X - 52f * scale,
                TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(headerOrigin.X + width - chanceHeaderSize.X, headerOrigin.Y + 6f * scale),
            chanceHeader, ui.MutedInk, TextStyles.FootnoteEmphasized);
        ImGui.Dummy(new Vector2(width, 24f * scale));

        var table = ScratchRules.PrizeTables[tier];
        for (var prizeIndex = 0; prizeIndex < table.Length; prizeIndex++)
        {
            var rowOrigin = ImGui.GetCursorScreenPos();
            var rowCenterY = rowOrigin.Y + RowHeight * scale * 0.5f;
            ScratchSymbolArt.Draw(drawList, prizeIndex, new Vector2(rowOrigin.X + 18f * scale, rowCenterY),
                13f * scale);
            var multiple = CasinoMultiples.Label(ScratchRules.PrizeMultiples[prizeIndex] * 100);
            var multipleSize = Typography.Measure(multiple, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + 44f * scale, rowCenterY - multipleSize.Y * 0.5f),
                multiple, CasinoColors.Money, TextStyles.FootnoteEmphasized);
            var amount = NumberText.Group(table[prizeIndex].Chips);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + 44f * scale + multipleSize.X + 10f * scale,
                rowCenterY - 9f * scale), amount, ui.TitleInk, TextStyles.SubheadlineEmphasized);
            var chance = chances[prizeIndex];
            var chanceSize = Typography.Measure(chance, TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(rowOrigin.X + width - chanceSize.X, rowCenterY - 9f * scale),
                chance, ui.BodyInk, TextStyles.Subheadline);
            ImGui.Dummy(new Vector2(width, RowHeight * scale));
        }

        var returnOrigin = ImGui.GetCursorScreenPos();
        Typography.Draw(drawList, returnOrigin, Typography.FitText(returnLine, width, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        ImGui.Dummy(new Vector2(width, Typography.LineHeight(TextStyles.Footnote) + Metrics.Space.Lg * scale));
    }

    private void RefreshLabels()
    {
        if (ReferenceEquals(labelsLanguage, Loc.Current))
        {
            return;
        }

        labelsLanguage = Loc.Current;
        for (var tierIndex = 0; tierIndex < ScratchRules.TierCount; tierIndex++)
        {
            priceLines[tierIndex] = Loc.T(L.Casino.ScratchPrice) + ": " + NumberText.Group(ScratchRules.Prices[tierIndex]);
        }

        for (var prizeIndex = 0; prizeIndex < chances.Length; prizeIndex++)
        {
            chances[prizeIndex] = Loc.T(L.Casino.ScratchOddsChanceValue,
                (ScratchRules.PrizeCountsPerMillion[prizeIndex] / 10_000.0).ToString("0.#", Loc.Culture));
        }

        returnLine = Loc.T(L.Strip.Return) + ": " + Loc.T(L.Strip.ReturnValue,
            (ScratchRules.ReturnBasisPoints / 100m).ToString("0.#", Loc.Culture));
    }
}
