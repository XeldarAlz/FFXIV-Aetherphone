using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Stage;

internal sealed class AutoBetSheet
{
    private const float PanelHeightShare = 0.74f;
    private const float RowHeight = 44f;
    private const float FieldWidth = 104f;
    private const float AdjustWidth = 168f;
    private const int RoundsDigits = 4;
    private const int PercentDigits = 4;
    private const int ChipDigits = 14;
    private const int RoundsField = 0;
    private const int WinField = 1;
    private const int LossField = 2;
    private const int ProfitField = 3;
    private const int StopLossField = 4;
    private const int FieldCount = 5;

    private readonly SheetSurface sheet;
    private readonly AutoBetPlan plan;
    private readonly string id;
    private readonly string childId;
    private readonly string winId;
    private readonly string lossId;
    private readonly string bonusId;
    private readonly Action<Rect> drawSheetBody;
    private readonly string[] buffers = new string[FieldCount];
    private readonly string[] adjustLabels = new string[2];

    private AppSkin skin = null!;
    private bool bonusAvailable;
    private string suffix = string.Empty;
    private LanguageInfo? suffixLanguage;

    public AutoBetSheet(string id, AutoBetPlan plan)
    {
        this.id = id;
        this.plan = plan;
        childId = "##" + id;
        winId = id + ".win";
        lossId = id + ".loss";
        bonusId = id + ".bonus";
        sheet = new SheetSurface(id);
        drawSheetBody = DrawSheetBody;
        for (var index = 0; index < FieldCount; index++)
        {
            buffers[index] = string.Empty;
        }
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open()
    {
        buffers[RoundsField] = Digits(plan.Rounds);
        buffers[WinField] = Digits(plan.OnWinPercent);
        buffers[LossField] = Digits(plan.OnLossPercent);
        buffers[ProfitField] = Digits(plan.StopOnProfit);
        buffers[StopLossField] = Digits(plan.StopOnLoss);
        sheet.Open();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui, bool bonus)
    {
        skin = ui;
        bonusAvailable = bonus;
        sheet.Draw(screen, CasinoArt.Sheet(ui), Loc.T(L.Strip.AutoTitle), PanelHeightShare, drawSheetBody);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child(childId, content.Size, false, ImGuiWindowFlags.NoBackground))
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
        y = DrawNumberRow(drawList, origin.X, y, width, Loc.T(L.Strip.AutoRounds), Loc.T(L.Strip.AutoRoundsHint),
            RoundsField, RoundsDigits, scale);
        plan.Rounds = (int)Math.Min(AutoBetPlan.MaxRounds, Parse(RoundsField));
        y = DrawAdjustRow(drawList, origin.X, y, width, Loc.T(L.Strip.OnWin), WinField, true, scale);
        y = DrawAdjustRow(drawList, origin.X, y, width, Loc.T(L.Strip.OnLoss), LossField, false, scale);
        y = DrawNumberRow(drawList, origin.X, y, width, Loc.T(L.Strip.StopOnProfit), Loc.T(L.Strip.StopOffHint),
            ProfitField, ChipDigits, scale);
        plan.StopOnProfit = Parse(ProfitField);
        y = DrawNumberRow(drawList, origin.X, y, width, Loc.T(L.Strip.StopOnLoss), Loc.T(L.Strip.StopOffHint),
            StopLossField, ChipDigits, scale);
        plan.StopOnLoss = Parse(StopLossField);
        if (bonusAvailable)
        {
            y = DrawBonusRow(drawList, origin.X, y, width, scale);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, y - origin.Y + Metrics.Space.Lg * scale));
    }

    private float DrawNumberRow(ImDrawListPtr drawList, float left, float y, float width, string label, string hint,
        int field, int digits, float scale)
    {
        var fieldWidth = FieldWidth * scale;
        var textWidth = width - fieldWidth - Metrics.Space.Md * scale;
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(label, textWidth,
            TextStyles.SubheadlineEmphasized), skin.TitleInk, TextStyles.SubheadlineEmphasized);
        var hintHeight = Typography.DrawWrappedLeft(new Vector2(left, y + titleHeight), hint, skin.MutedInk,
            TextStyles.Footnote, textWidth);
        var height = MathF.Max(RowHeight * scale, titleHeight + hintHeight + Metrics.Space.Sm * scale);
        var fieldRect = new Rect(new Vector2(left + width - fieldWidth, y + (height - BetComposer.AmountHeight * scale) * 0.5f),
            new Vector2(left + width, y + (height + BetComposer.AmountHeight * scale) * 0.5f));
        DrawField(drawList, fieldRect, field, digits);
        return y + height + Metrics.Space.Sm * scale;
    }

    private float DrawAdjustRow(ImDrawListPtr drawList, float left, float y, float width, string label, int field,
        bool win, float scale)
    {
        var titleHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y), Typography.FitText(label, width,
            TextStyles.SubheadlineEmphasized), skin.TitleInk, TextStyles.SubheadlineEmphasized);
        var rowTop = y + titleHeight + Metrics.Space.Xs * scale;
        var rowHeight = BetComposer.AmountHeight * scale;
        adjustLabels[0] = Loc.T(L.Strip.AdjustReset);
        adjustLabels[1] = Loc.T(L.Strip.AdjustIncrease);
        var current = win ? plan.OnWin : plan.OnLoss;
        var segment = new Rect(new Vector2(left, rowTop),
            new Vector2(left + MathF.Min(AdjustWidth * scale, width * 0.55f), rowTop + rowHeight));
        var picked = SegmentStrip.Draw(win ? winId : lossId, segment, adjustLabels, (int)current,
            Surfaces.Fill(skin.TitleInk, FillLevel.Tertiary), skin.Accent, skin.MutedInk, CasinoColors.InkTitle,
            overlay: true);
        var adjust = (AutoAdjust)picked;
        var percent = (int)Math.Min(AutoBetPlan.MaxPercent, Parse(field));
        if (adjust == AutoAdjust.Increase)
        {
            var fieldRect = new Rect(new Vector2(segment.Max.X + Metrics.Space.Sm * scale, rowTop),
                new Vector2(left + width, rowTop + rowHeight));
            DrawField(drawList, fieldRect, field, PercentDigits);
            var suffix = PercentSuffix();
            var suffixSize = Typography.Measure(suffix, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(fieldRect.Max.X - suffixSize.X - Metrics.Space.Md * scale,
                fieldRect.Center.Y - suffixSize.Y * 0.5f), suffix, skin.MutedInk, TextStyles.Footnote);
        }

        if (win)
        {
            plan.OnWin = adjust;
            plan.OnWinPercent = percent;
        }
        else
        {
            plan.OnLoss = adjust;
            plan.OnLossPercent = percent;
        }

        return rowTop + rowHeight + Metrics.Space.Md * scale;
    }

    private float DrawBonusRow(ImDrawListPtr drawList, float left, float y, float width, float scale)
    {
        var height = RowHeight * scale;
        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var labelHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, y + (height - labelHeight) * 0.5f),
            Typography.FitText(Loc.T(L.Strip.StopOnBonus), width - toggleWidth - Metrics.Space.Md * scale,
                TextStyles.SubheadlineEmphasized), skin.TitleInk, TextStyles.SubheadlineEmphasized);
        var rowMin = new Vector2(left, y);
        var rowMax = new Vector2(left + width, y + height);
        var hovered = sheet.IsOpen && UiInteract.HoverWindowOnly(rowMin, rowMax);
        if (UiInteract.Click(rowMin, rowMax, hovered, false))
        {
            plan.StopOnBonus = !plan.StopOnBonus;
            UiFeedback.Play(plan.StopOnBonus ? UiSound.ToggleOn : UiSound.ToggleOff);
        }

        var toggleTop = y + (height - toggleHeight) * 0.5f;
        Toggle.Draw(bonusId, new Rect(new Vector2(left + width - toggleWidth, toggleTop),
            new Vector2(left + width, toggleTop + toggleHeight)), plan.StopOnBonus, skin.Theme, 1f, false);
        return y + height;
    }

    private string PercentSuffix()
    {
        if (ReferenceEquals(suffixLanguage, Loc.Current))
        {
            return suffix;
        }

        suffixLanguage = Loc.Current;
        suffix = Loc.T(L.Strip.ReturnValue, string.Empty).Trim();
        return suffix;
    }

    private void DrawField(ImDrawListPtr drawList, Rect field, int index, int digits)
    {
        SearchBar.Surface(drawList, field, skin.Ink);
        var capsule = SearchBar.Capsule(field);
        var inset = capsule.Height * 0.4f;
        ImGui.SetCursorScreenPos(new Vector2(capsule.Min.X + inset, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(capsule.Width - inset * 2f);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, CasinoColors.InkTitle))
        {
            ImGui.InputText($"##{id}.field{index}", ref buffers[index], digits + 1,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }
    }

    private long Parse(int index) =>
        long.TryParse(buffers[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 0;

    private static string Digits(long value) =>
        value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "0";
}
