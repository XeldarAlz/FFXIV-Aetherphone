using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal sealed class DealerHoldemPaySheet
{
    private const float PanelHeightShare = 0.78f;
    private const float RowHeight = 30f;
    private const float SectionGap = 14f;

    private readonly SheetSurface sheet = new("casino.dealerholdem.pays");
    private readonly Action<Rect> drawSheetBody;
    private readonly string[] blindOdds = new string[DealerHoldemTexts.BlindRows];
    private readonly string[] tripsOdds = new string[DealerHoldemTexts.TableRows];

    private AppSkin skin = null!;
    private DealerHoldemTexts texts = null!;
    private string mainReturn = string.Empty;
    private string tripsReturn = string.Empty;
    private string mainExplain = string.Empty;
    private LanguageInfo? language;

    public DealerHoldemPaySheet()
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

    public void Draw(Rect screen, AppSkin ui, DealerHoldemTexts labels)
    {
        skin = ui;
        texts = labels;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.DealerHoldem.PayTables), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##dealerHoldemPays", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawRows(UiScale.Current);
        }
    }

    private void DrawRows(float scale)
    {
        Refresh();
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var y = origin.Y;
        y = Paragraph(drawList, origin.X, y, width, Loc.T(L.DealerHoldem.PaysIntro), scale);
        y = Table(drawList, origin.X, y + SectionGap * scale, width, Loc.T(L.DealerHoldem.BlindPays),
            texts.BlindNames, blindOdds, scale);
        y = Paragraph(drawList, origin.X, y, width, Loc.T(L.DealerHoldem.BlindPushNote), scale);
        y = Table(drawList, origin.X, y + SectionGap * scale, width, Loc.T(L.DealerHoldem.TripsPays),
            texts.TripsNames, tripsOdds, scale);
        y = Paragraph(drawList, origin.X, y, width, Loc.T(L.DealerHoldem.TripsNote), scale);
        y = ReturnRow(drawList, origin.X, y + SectionGap * scale, width, Loc.T(L.DealerHoldem.MainGame), mainReturn);
        y = ReturnRow(drawList, origin.X, y, width, Loc.T(L.DealerHoldem.SpotTrips), tripsReturn);
        y = Paragraph(drawList, origin.X, y + Metrics.Space.Xs * scale, width, mainExplain, scale);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Lg * scale));
    }

    private float Paragraph(ImDrawListPtr drawList, float left, float y, float width, string text, float scale)
    {
        var height = Typography.DrawWrappedLeft(new Vector2(left, y + Metrics.Space.Xs * scale), text, skin.BodyInk,
            TextStyles.Footnote, width);
        return y + Metrics.Space.Xs * scale + height;
    }

    private float Table(ImDrawListPtr drawList, float left, float y, float width, string title,
        ReadOnlySpan<string> names, ReadOnlySpan<string> odds, float scale)
    {
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(title, width, TextStyles.SubheadlineEmphasized),
            skin.TitleInk, TextStyles.SubheadlineEmphasized);
        y += titleHeight + Metrics.Space.Xs * scale;
        var rowHeight = RowHeight * scale;
        for (var index = 0; index < names.Length; index++)
        {
            var center = y + rowHeight * 0.5f;
            var oddsSize = Typography.Measure(odds[index], TextStyles.SubheadlineEmphasized);
            var name = Typography.FitText(names[index], width - oddsSize.X - Metrics.Space.Md * scale,
                TextStyles.Subheadline);
            var nameHeight = Typography.LineHeight(TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(left, center - nameHeight * 0.5f), name, skin.TitleInk,
                TextStyles.Subheadline);
            Typography.Draw(drawList, new Vector2(left + width - oddsSize.X, center - oddsSize.Y * 0.5f), odds[index],
                CasinoColors.Money, TextStyles.SubheadlineEmphasized);
            drawList.AddLine(new Vector2(left, y + rowHeight), new Vector2(left + width, y + rowHeight),
                ImGui.GetColorU32(Core.Theme.Palette.WithAlpha(skin.TitleInk, 0.06f)), 1f);
            y += rowHeight;
        }

        return y;
    }

    private float ReturnRow(ImDrawListPtr drawList, float left, float y, float width, string label, string value)
    {
        var rowHeight = Metrics.Size.Row * UiScale.Current;
        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        var shown = Typography.FitText(label, width - valueSize.X - Metrics.Space.Md * UiScale.Current,
            TextStyles.Subheadline);
        var center = y + rowHeight * 0.5f;
        var labelHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left, center - labelHeight * 0.5f), shown, skin.TitleInk,
            TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(left + width - valueSize.X, center - valueSize.Y * 0.5f), value,
            CasinoColors.Money, TextStyles.SubheadlineEmphasized);
        return y + rowHeight;
    }

    private void Refresh()
    {
        if (ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        language = Loc.Current;
        for (var index = 0; index < DealerHoldemTexts.TableRows; index++)
        {
            var category = DealerHoldemRules.PayCategories[index];
            tripsOdds[index] = Loc.T(L.DealerHoldem.Odds, GameNumber.Label(DealerHoldemRules.TripsPays[category]),
                GameNumber.Label(1));
            if (index < DealerHoldemTexts.BlindRows)
            {
                blindOdds[index] = Loc.T(L.DealerHoldem.Odds,
                    GameNumber.Label(DealerHoldemRules.BlindNumerators[category]),
                    GameNumber.Label(DealerHoldemRules.BlindDenominators[category]));
            }
        }

        mainReturn = PaysBack(DealerHoldemRules.ReturnTenths);
        tripsReturn = PaysBack(DealerHoldemRules.TripsReturnTenths);
        mainExplain = Loc.T(L.DealerHoldem.PaysBackExplain, Tenths(DealerHoldemRules.ReturnTenths));
    }

    private static string PaysBack(int tenths) => Loc.T(L.DealerHoldem.PaysBackValue, Tenths(tenths));

    private static string Tenths(int tenths) => (tenths / 10m).ToString("0.#", Loc.Culture);
}
