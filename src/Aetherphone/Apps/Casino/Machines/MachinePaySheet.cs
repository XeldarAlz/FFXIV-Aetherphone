using Aetherphone.Apps.Casino.Originals;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Machines;

internal sealed class MachinePaySheet
{
    private const float PanelHeightShare = 0.82f;
    private const float RowHeight = 44f;
    private const float StatRowHeight = 34f;
    private const float IconColumn = 48f;
    private const int MaxSymbols = 9;
    private const int Columns = 3;
    private const float MapCell = 7f;
    private const float MapGap = 10f;

    private static readonly string[] LineHeaders = { "3", "4", "5" };
    private static readonly string[] ClusterHeaders = { "8-9", "10-11", "12+" };

    private readonly SheetSurface sheet = new("casino.machines.pays");
    private readonly Action<Rect> drawSheetBody;
    private readonly string[] pays = new string[MaxSymbols * Columns];
    private readonly string[] scatters = new string[7];
    private readonly string[] coins = new string[MoogleMoneyRules.CoinValues.Length];

    private AppSkin skin = null!;
    private string machineId = SlotsRules.BirdId;
    private long bet;
    private string builtMachine = string.Empty;
    private long builtBet = -1;
    private LanguageInfo? builtLanguage;
    private string returnBase = string.Empty;
    private string returnAnte = string.Empty;
    private string returnBuy = string.Empty;
    private string hit = string.Empty;
    private string bonus = string.Empty;
    private string bonusTwo = string.Empty;
    private string maxWinMultiple = string.Empty;
    private string maxWin = string.Empty;
    private string mini = string.Empty;
    private string minor = string.Empty;
    private string major = string.Empty;
    private string grand = string.Empty;

    public MachinePaySheet()
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

    public void Draw(Rect screen, AppSkin ui, string machine, long currentBet, string maxWinLine)
    {
        maxWin = maxWinLine;
        skin = ui;
        machineId = machine;
        bet = Math.Max(SlotsRules.MinStake, currentBet);
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Machines.PaysTitle), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        Rebuild();
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##machinePays", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(UiScale.Current);
        }
    }

    private void DrawBody(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var left = origin.X;
        var y = origin.Y;
        var info = SlotsMachines.For(machineId);
        y = Stat(drawList, left, y, width, Loc.T(L.Strip.PaysBack), returnBase, scale);
        if (info.OffersAnte)
        {
            y = Stat(drawList, left, y, width, Loc.T(L.Machines.ReturnAnte), returnAnte, scale);
        }

        if (info.OffersBuy)
        {
            y = Stat(drawList, left, y, width, Loc.T(L.Machines.ReturnBuy), returnBuy, scale);
        }

        y = Stat(drawList, left, y, width, Loc.T(L.Machines.HitFrequency), hit, scale);
        y = Stat(drawList, left, y, width, Loc.T(info.Layout == SlotsLayout.Hold ? L.Machines.HoldFrequency
            : L.Machines.BonusFrequency), bonus, scale);
        if (bonusTwo.Length > 0)
        {
            y = Stat(drawList, left, y, width, Loc.T(L.Machines.FreeGamesFrequency), bonusTwo, scale);
        }

        y = Stat(drawList, left, y, width, Loc.T(L.Machines.MaxWin), maxWinMultiple, scale);
        y = Paragraph(drawList, left, y, width, maxWin, scale);
        y = VolatilityRow(drawList, left, y, width, info.VolatilityBars, scale);
        y += Metrics.Space.Lg * scale;
        y = Heading(drawList, left, y, width, Loc.T(L.Machines.PaysAtBet), scale);
        y = PayTable(drawList, left, y, width, info, scale);
        y += Metrics.Space.Md * scale;
        if (info.Layout == SlotsLayout.Hold)
        {
            y = Heading(drawList, left, y, width, Loc.T(L.Machines.CoinsHeading), scale);
            y = CoinsBlock(drawList, left, y, width, scale);
            y += Metrics.Space.Md * scale;
        }

        y = Heading(drawList, left, y, width, Loc.T(L.Machines.FeaturesHeading), scale);
        y = Paragraph(drawList, left, y, width, Loc.T(FeatureOne()), scale);
        y = Paragraph(drawList, left, y, width, Loc.T(FeatureTwo()), scale);
        y = Paragraph(drawList, left, y, width, Loc.T(L.Machines.JackpotRule), scale);
        if (info.Layout != SlotsLayout.Cluster)
        {
            y += Metrics.Space.Md * scale;
            y = Heading(drawList, left, y, width, Loc.T(L.Machines.PaylinesHeading), scale);
            y = Paylines(drawList, left, y, width, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Lg * scale));
    }

    private LocString FeatureOne() => machineId switch
    {
        SlotsRules.CascadeId => L.Machines.CascadeRuleTumble,
        SlotsRules.MoogleId => L.Machines.MoogleRuleHold,
        _ => L.Machines.BirdRuleFree,
    };

    private LocString FeatureTwo() => machineId switch
    {
        SlotsRules.CascadeId => L.Machines.CascadeRuleFree,
        SlotsRules.MoogleId => L.Machines.MoogleRuleGames,
        _ => L.Machines.BirdRuleGamble,
    };

    private float Stat(ImDrawListPtr drawList, float left, float y, float width, string label, string value,
        float scale)
    {
        var height = StatRowHeight * scale;
        var center = y + height * 0.5f;
        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        var fitted = Typography.FitText(label, width - valueSize.X - Metrics.Space.Md * scale, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, center - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
            fitted, skin.BodyInk, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left + width - valueSize.X, center - valueSize.Y * 0.5f), value,
            CasinoColors.Money, TextStyles.SubheadlineEmphasized);
        drawList.AddLine(new Vector2(left, y + height), new Vector2(left + width, y + height),
            ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, 0.06f)), 1f);
        return y + height;
    }

    private float VolatilityRow(ImDrawListPtr drawList, float left, float y, float width, int bars, float scale)
    {
        var height = StatRowHeight * scale;
        var center = y + height * 0.5f;
        Typography.Draw(drawList, new Vector2(left, center - Typography.LineHeight(TextStyles.Subheadline) * 0.5f),
            Typography.FitText(Loc.T(L.Machines.Volatility), width * 0.6f, TextStyles.Subheadline), skin.BodyInk,
            TextStyles.Subheadline);
        var barsHeight = 16f * scale;
        MachineArt.VolatilityBars(drawList, new Vector2(left + width - 42f * scale, center - barsHeight * 0.5f),
            barsHeight, bars, CasinoColors.Money, scale);
        return y + height;
    }

    private float Heading(ImDrawListPtr drawList, float left, float y, float width, string text, float scale)
    {
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(text, width, TextStyles.Headline),
            skin.TitleInk, TextStyles.Headline);
        return y + Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale;
    }

    private float Paragraph(ImDrawListPtr drawList, float left, float y, float width, string text, float scale)
    {
        var height = Typography.DrawWrappedLeft(new Vector2(left, y), text, skin.BodyInk, TextStyles.Subheadline, width);
        return y + height + Metrics.Space.Sm * scale;
    }

    private float PayTable(ImDrawListPtr drawList, float left, float y, float width, SlotsMachineInfo info,
        float scale)
    {
        var headers = info.Layout == SlotsLayout.Cluster ? ClusterHeaders : LineHeaders;
        var columnWidth = (width - IconColumn * scale) / Columns;
        for (var column = 0; column < Columns; column++)
        {
            var x = left + IconColumn * scale + column * columnWidth;
            var size = Typography.Measure(headers[column], TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(x + columnWidth - size.X, y), headers[column], skin.MutedInk,
                TextStyles.FootnoteEmphasized);
        }

        y += Typography.LineHeight(TextStyles.FootnoteEmphasized) + Metrics.Space.Xs * scale;
        var symbols = MachineSymbols.SymbolCount(machineId);
        var height = RowHeight * scale;
        for (var symbol = 0; symbol < symbols; symbol++)
        {
            var center = y + height * 0.5f;
            MachineSymbols.Draw(drawList, machineId, symbol, new Vector2(left + IconColumn * 0.5f * scale, center),
                height * 0.34f, 1f, false);
            for (var column = 0; column < Columns; column++)
            {
                var text = pays[symbol * Columns + column];
                var x = left + IconColumn * scale + column * columnWidth;
                var fitted = Typography.FitText(text, columnWidth - Metrics.Space.Xs * scale, TextStyles.Subheadline);
                var size = Typography.Measure(fitted, TextStyles.Subheadline);
                Typography.Draw(drawList, new Vector2(x + columnWidth - size.X, center - size.Y * 0.5f), fitted,
                    CasinoColors.Money, TextStyles.Subheadline);
            }

            y += height;
        }

        var scatter = MachineSymbols.Scatter(machineId);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        for (var count = 0; count < scatters.Length; count++)
        {
            if (scatters[count].Length == 0)
            {
                continue;
            }

            var center = y + height * 0.5f;
            MachineSymbols.Draw(drawList, machineId, scatter, new Vector2(left + IconColumn * 0.5f * scale, center),
                height * 0.34f, 1f, false);
            Typography.Draw(drawList, new Vector2(left + IconColumn * scale, center - lineHeight * 0.5f),
                Typography.FitText(scatters[count], width - IconColumn * scale, TextStyles.Subheadline),
                CasinoColors.Money, TextStyles.Subheadline);
            y += height;
        }

        return y;
    }

    private float CoinsBlock(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline) + Metrics.Space.Xs * scale;
        var columnWidth = width / Columns;
        var shown = 0;
        for (var index = 0; index < coins.Length; index++)
        {
            if (coins[index].Length == 0)
            {
                continue;
            }

            var column = shown % Columns;
            var row = shown / Columns;
            Typography.Draw(drawList, new Vector2(left + column * columnWidth, y + row * lineHeight),
                Typography.FitText(coins[index], columnWidth - Metrics.Space.Xs * scale, TextStyles.Subheadline),
                CasinoColors.Money, TextStyles.Subheadline);
            shown++;
        }

        y += (shown + Columns - 1) / Columns * lineHeight + Metrics.Space.Sm * scale;
        y = Stat(drawList, left, y, width, Loc.T(L.Machines.Mini), mini, scale);
        y = Stat(drawList, left, y, width, Loc.T(L.Machines.Minor), minor, scale);
        y = Stat(drawList, left, y, width, Loc.T(L.Machines.Major), major, scale);
        return Stat(drawList, left, y, width, Loc.T(L.Machines.Grand), grand, scale);
    }

    private float Paylines(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var cell = MapCell * scale;
        var gap = MapGap * scale;
        var tileWidth = cell * SlotsRules.ReelCount;
        var tileHeight = cell * SlotsRules.RowCount;
        var perRow = Math.Max(1, (int)((width + gap) / (tileWidth + gap)));
        var dim = ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, 0.10f));
        var lit = ImGui.GetColorU32(CasinoColors.Money);
        for (var line = 0; line < SlotsRules.PaylineCount; line++)
        {
            var column = line % perRow;
            var row = line / perRow;
            var origin = new Vector2(left + column * (tileWidth + gap), y + row * (tileHeight + gap));
            var rows = SlotsRules.Paylines[line];
            for (var reel = 0; reel < SlotsRules.ReelCount; reel++)
            {
                for (var slot = 0; slot < SlotsRules.RowCount; slot++)
                {
                    var min = origin + new Vector2(reel * cell + 1f, slot * cell + 1f);
                    drawList.AddRectFilled(min, min + new Vector2(cell - 2f, cell - 2f),
                        rows[reel] == slot ? lit : dim, 2f * scale);
                }
            }
        }

        var lines = (SlotsRules.PaylineCount + perRow - 1) / perRow;
        return y + lines * (tileHeight + gap);
    }

    private void Rebuild()
    {
        if (string.Equals(builtMachine, machineId, StringComparison.Ordinal) && builtBet == bet
            && ReferenceEquals(builtLanguage, Loc.Current))
        {
            return;
        }

        builtMachine = machineId;
        builtBet = bet;
        builtLanguage = Loc.Current;
        var info = SlotsMachines.For(machineId);
        returnBase = OriginalsText.Percent(info.ReturnBasisPoints);
        returnAnte = info.OffersAnte ? OriginalsText.Percent(info.AnteReturnBasisPoints) : string.Empty;
        returnBuy = info.OffersBuy ? OriginalsText.Percent(info.BuyReturnBasisPoints) : string.Empty;
        hit = OriginalsText.Percent(info.HitBasisPoints);
        bonus = Loc.T(L.Machines.OneIn, NumberText.Group(info.BonusOneIn));
        if (info.OffersAnte)
        {
            bonus = Loc.T(L.Machines.OneInAnte, NumberText.Group(info.BonusOneIn), NumberText.Group(info.AnteBonusOneIn));
        }

        bonusTwo = info.SecondBonusOneIn > 0 ? Loc.T(L.Machines.OneIn, NumberText.Group(info.SecondBonusOneIn))
            : string.Empty;
        maxWinMultiple = Loc.T(L.Machines.TimesBet, NumberText.Group(info.MaxWinMultiple));
        Array.Fill(pays, string.Empty);
        Array.Fill(scatters, string.Empty);
        Array.Fill(coins, string.Empty);
        var table = machineId switch
        {
            SlotsRules.CascadeId => CrystalCascadeRules.ClusterPays,
            SlotsRules.MoogleId => MoogleMoneyRules.LinePays,
            _ => GoldenBirdRules.LinePays,
        };
        for (var symbol = 0; symbol < table.Length && symbol < MaxSymbols; symbol++)
        {
            for (var column = 0; column < Columns; column++)
            {
                pays[symbol * Columns + column] = NumberText.Compact(SlotsRules.ChipsFor(bet, table[symbol][column]));
            }
        }

        var scatterPays = machineId switch
        {
            SlotsRules.CascadeId => CrystalCascadeRules.ScatterPays,
            SlotsRules.MoogleId => Array.Empty<long>(),
            _ => GoldenBirdRules.ScatterPays,
        };
        for (var count = 0; count < scatterPays.Length && count < scatters.Length; count++)
        {
            if (scatterPays[count] > 0)
            {
                scatters[count] = Loc.T(L.Machines.ScatterPay, GameNumber.Label(count),
                    NumberText.Compact(SlotsRules.ChipsFor(bet, scatterPays[count])));
            }
        }

        if (info.Layout != SlotsLayout.Hold)
        {
            return;
        }

        for (var index = 0; index < coins.Length; index++)
        {
            if (MoogleMoneyRules.CoinKinds[index] == SlotsRules.CoinCash)
            {
                coins[index] = NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.CoinValues[index]));
            }
        }

        mini = Loc.T(L.Machines.MeterRange, NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.MiniResetUnits)),
            NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.MiniCeilingUnits)));
        minor = Loc.T(L.Machines.MeterRange,
            NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.MinorResetUnits)),
            NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.MinorCeilingUnits)));
        major = NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.MajorUnits));
        grand = NumberText.Compact(SlotsRules.ChipsFor(bet, MoogleMoneyRules.GrandUnits));
    }
}
