using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class CasinoInfoSheet
{
    private const float PanelHeightShare = 0.62f;
    private const float RowHeight = 52f;
    private const float RowGap = 10f;
    private const float ButtonGap = 8f;

    private readonly SheetSurface sheet = new("casino.info");
    private readonly Action<Rect> drawSheetBody;

    private AppSkin skin = null!;
    private CasinoStageSpec spec;
    private CasinoInfoRequest request;
    private string returnText = string.Empty;
    private string explainText = string.Empty;
    private int returnTenths = -1;
    private LanguageInfo? returnLanguage;
    private string reasonText = string.Empty;
    private CasinoCeiling reasonCeiling;
    private LanguageInfo? reasonLanguage;

    public CasinoInfoSheet()
    {
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public bool Instant { get; set; }

    public CasinoCeiling Ceiling { get; set; }

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

    public CasinoInfoRequest TakeRequest()
    {
        var taken = request;
        request = CasinoInfoRequest.None;
        return taken;
    }

    public void Draw(Rect screen, AppSkin ui, in CasinoStageSpec current)
    {
        skin = ui;
        spec = current;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(spec.Title), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##casinoInfoBody", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(UiScale.Current);
        }
    }

    private void DrawBody(float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var y = origin.Y;
        if (spec.InstantAvailable)
        {
            y = DrawInstantRow(drawList, origin.X, y, width, scale);
            y += RowGap * scale;
        }

        if (spec.Deck && Ceiling.MaxBet > 0)
        {
            y = DrawCeiling(drawList, origin.X, y, width, scale);
            y += RowGap * scale;
        }

        if (spec.ReturnTenths > 0)
        {
            y = DrawReturnRow(drawList, origin.X, y, width, scale);
            y += RowGap * scale;
        }

        y = DrawButtons(origin.X, y, width, scale);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Lg * scale));
    }

    private float DrawInstantRow(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var textWidth = width - toggleWidth - Metrics.Space.Md * scale;
        var title = Typography.FitText(Loc.T(L.Strip.Instant), textWidth, TextStyles.SubheadlineEmphasized);
        var hint = Loc.T(L.Strip.InstantHint);
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var hintBlock = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth);
        var height = MathF.Max(RowHeight * scale, titleHeight + hintBlock.Y + Metrics.Space.Xxs * scale);
        Typography.Draw(drawList, new Vector2(left, y), title, skin.TitleInk, TextStyles.SubheadlineEmphasized);
        Typography.DrawWrappedLeft(new Vector2(left, y + titleHeight + Metrics.Space.Xxs * scale), hint, skin.MutedInk,
            TextStyles.Footnote, textWidth);
        var toggleTop = y + (height - toggleHeight) * 0.5f;
        var toggleRect = new Rect(new Vector2(left + width - toggleWidth, toggleTop),
            new Vector2(left + width, toggleTop + toggleHeight));
        var rowMin = new Vector2(left, y);
        var rowMax = new Vector2(left + width, y + height);
        var hovered = sheet.IsOpen && UiInteract.HoverWindowOnly(rowMin, rowMax);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(rowMin, rowMax, hovered, false))
        {
            Instant = !Instant;
            UiFeedback.Play(Instant ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        Toggle.Draw("casino.info.instant", toggleRect, Instant, skin.Theme, 1f, false);
        return y + height;
    }

    private float DrawCeiling(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var value = NumberText.Compact(Ceiling.MaxBet);
        var valueSize = CurrencyGlyph.MeasureAmount(value, TextStyles.SubheadlineEmphasized);
        var label = Typography.FitText(Loc.T(L.Strip.MaxBet), width - valueSize.X - Metrics.Space.Md * scale,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y), label, skin.TitleInk, TextStyles.SubheadlineEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(left + width - valueSize.X, y), value, CurrencyKind.Chips,
            CasinoColors.Money, TextStyles.SubheadlineEmphasized);
        var reasonTop = y + valueSize.Y + Metrics.Space.Xxs * scale;
        var reason = CeilingReasonText();
        var height = Typography.DrawWrappedLeft(new Vector2(left, reasonTop), reason, skin.MutedInk,
            TextStyles.Footnote, width);
        return reasonTop + height;
    }

    private string CeilingReasonText()
    {
        if (ReferenceEquals(reasonLanguage, Loc.Current) && reasonCeiling == Ceiling)
        {
            return reasonText;
        }

        reasonLanguage = Loc.Current;
        reasonCeiling = Ceiling;
        var level = Games.Framework.GameNumber.Label(Ceiling.Level);
        reasonText = Ceiling.Reason == CeilingReason.Balance
            ? Loc.T(L.Strip.CeilingBalance, level, NumberText.Compact(Ceiling.LevelCap))
            : Loc.T(L.Strip.CeilingLevel, level, NumberText.Compact(Ceiling.LevelCap));
        return reasonText;
    }

    private float DrawReturnRow(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var value = ReturnText(spec.ReturnTenths);
        var valueSize = Typography.Measure(value, TextStyles.SubheadlineEmphasized);
        var label = Typography.FitText(Loc.T(L.Strip.PaysBack), width - valueSize.X - Metrics.Space.Md * scale,
            TextStyles.Footnote);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = Metrics.Size.Row * scale;
        var center = y + rowHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(left, center - labelHeight * 0.5f), label, skin.MutedInk,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(left + width - valueSize.X, center - valueSize.Y * 0.5f), value,
            CasinoColors.Money, TextStyles.SubheadlineEmphasized);
        var explainTop = y + rowHeight;
        var explainHeight = Typography.DrawWrappedLeft(new Vector2(left, explainTop), explainText, skin.MutedInk,
            TextStyles.Footnote, width);
        var bottom = explainTop + explainHeight + Metrics.Space.Xs * scale;
        drawList.AddLine(new Vector2(left, bottom), new Vector2(left + width, bottom),
            ImGui.GetColorU32(Palette.WithAlpha(skin.TitleInk, 0.06f)), 1f);
        return bottom;
    }

    private float DrawButtons(float left, float y, float width, float scale)
    {
        var height = Button.RegularHeight * scale;
        var gap = ButtonGap * scale;
        var hasExtra = spec.Extra.Key is not null;
        var columns = hasExtra ? 2 : 1;
        var columnWidth = (width - gap * (columns - 1)) / columns;
        var rules = new Rect(new Vector2(left, y), new Vector2(left + columnWidth, y + height));
        if (Button.Draw(rules, Loc.T(L.Strip.HowToPlay), skin.Ink, ButtonStyle.Gray, enabled: sheet.IsOpen,
                overlay: true))
        {
            Choose(CasinoInfoRequest.Rules);
        }

        if (hasExtra)
        {
            var extra = new Rect(new Vector2(left + columnWidth + gap, y), new Vector2(left + width, y + height));
            if (Button.Draw(extra, Loc.T(spec.Extra), skin.Ink, ButtonStyle.Gray, enabled: sheet.IsOpen,
                    overlay: true))
            {
                Choose(CasinoInfoRequest.Extra);
            }
        }

        y += height + gap;
        var fairness = new Rect(new Vector2(left, y), new Vector2(left + width, y + height));
        if (Button.Draw(fairness, Loc.T(L.Casino.FairnessRow), skin.Ink, ButtonStyle.Plain, enabled: sheet.IsOpen,
                overlay: true))
        {
            Choose(CasinoInfoRequest.Fairness);
        }

        return y + height;
    }

    private void Choose(CasinoInfoRequest chosen)
    {
        request = chosen;
        Close();
    }

    private string ReturnText(int tenths)
    {
        if (tenths == returnTenths && ReferenceEquals(returnLanguage, Loc.Current))
        {
            return returnText;
        }

        returnTenths = tenths;
        returnLanguage = Loc.Current;
        returnText = Loc.T(L.Strip.ReturnValue, (tenths / 10m).ToString("0.0", Loc.Culture));
        explainText = Loc.T(L.Strip.PaysBackExplain, (tenths / 10m).ToString("0.#", Loc.Culture));
        return returnText;
    }
}
