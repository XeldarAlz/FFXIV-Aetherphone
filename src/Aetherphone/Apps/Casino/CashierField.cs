using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed class CashierField
{
    public static readonly TextStyle NumberStyle = TextStyles.Title1;

    private const string Placeholder = "0";
    private const float GlyphSize = 30f;
    private const float InsetLeft = 12f;
    private const float InsetRight = 18f;
    private const float Gap = 10f;
    private const float UnitShare = 0.4f;
    private const float IdleRimAlpha = 0.08f;
    private const float StateRimWidth = 2f;
    private const float ReadyGlow = 0.25f;

    private const ImGuiInputTextFlags Flags = ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll;

    private static readonly Vector4 InsetFill = new(0f, 0f, 0f, 0.35f);

    private readonly string id;
    private string buffer = string.Empty;
    private bool editing;

    public CashierField(string id)
    {
        this.id = id;
    }

    public long Coins => CashierAmounts.Parse(buffer);

    public void Set(long coins)
    {
        var shown = Coins;
        if (shown == coins && (coins > 0 || buffer.Length == 0))
        {
            return;
        }

        buffer = CashierAmounts.Format(coins);
    }

    public void Clear()
    {
        buffer = string.Empty;
    }

    public void Draw(ImDrawListPtr drawList, AppSkin ui, Rect field, CashierAmountState state, bool editable,
        bool interactive)
    {
        var scale = UiScale.Current;
        var radius = field.Height * 0.5f;
        if (state == CashierAmountState.Ready)
        {
            MotionButton.Halo(drawList, field, radius, ui.Accent, ReadyGlow);
        }

        Squircle.Fill(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(InsetFill));
        DrawRim(drawList, ui, field, radius, state, scale);

        var glyph = GlyphSize * scale;
        var glyphCenter = new Vector2(field.Min.X + InsetLeft * scale + glyph * 0.5f, field.Center.Y);
        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, glyphCenter, glyph);

        var textLeft = glyphCenter.X + glyph * 0.5f + Gap * scale;
        var unitRight = field.Max.X - InsetRight * scale;
        var unitStyle = TextStyles.SubheadlineEmphasized;
        var unit = Typography.FitText(Loc.T(L.CashierDesk.CoinsUnit), MathF.Max(1f, (unitRight - textLeft) * UnitShare),
            unitStyle);
        var unitSize = Typography.Measure(unit, unitStyle);
        Typography.Draw(drawList, new Vector2(unitRight - unitSize.X, field.Center.Y - unitSize.Y * 0.5f), unit,
            ui.MutedInk, unitStyle);

        var textRight = MathF.Max(textLeft + 1f, unitRight - unitSize.X - Gap * scale);
        var area = new Rect(new Vector2(textLeft, field.Min.Y), new Vector2(textRight, field.Max.Y));
        if (!editable)
        {
            editing = false;
            DrawValue(drawList, ui, area);
            return;
        }

        var showInput = editing;
        DrawInput(ui, area, showInput, interactive);
        if (!showInput)
        {
            DrawValue(drawList, ui, area);
        }
    }

    private void DrawInput(AppSkin ui, Rect area, bool showInput, bool interactive)
    {
        var cursor = ImGui.GetCursorScreenPos();
        using (Plugin.Fonts.Push(NumberStyle.Scale, NumberStyle.Weight))
        {
            var padding = ImGui.GetStyle().FramePadding.X;
            ImGui.SetCursorScreenPos(new Vector2(area.Min.X - padding, area.Center.Y - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(area.Width + padding * 2f);
            using (UiScope.Disabled(!interactive))
            using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (UiScope.PushColor(ImGuiCol.Text, showInput ? ui.TitleInk : AppSkin.Transparent))
            using (UiScope.PushColor(ImGuiCol.TextDisabled, showInput ? ui.MutedInk : AppSkin.Transparent))
            {
                ImGui.InputTextWithHint(id, Placeholder, ref buffer, CashierAmounts.FieldLength, Flags);
                editing = interactive && ImGui.IsItemActive();
            }
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    private void DrawValue(ImDrawListPtr drawList, AppSkin ui, Rect area)
    {
        var coins = Coins;
        var empty = buffer.Length == 0;
        var text = empty ? Placeholder : coins > 0 ? NumberText.Group(coins) : buffer;
        var fit = Typography.FitScale(text, area.Width, NumberStyle.Scale, TextStyles.Headline.Scale,
            NumberStyle.Weight);
        var size = Typography.Measure(text, fit, NumberStyle.Weight);
        Typography.Draw(drawList, new Vector2(area.Min.X, area.Center.Y - size.Y * 0.5f), text,
            empty ? ui.MutedInk : ui.TitleInk, fit, NumberStyle.Weight);
    }

    private static void DrawRim(ImDrawListPtr drawList, AppSkin ui, Rect field, float radius, CashierAmountState state,
        float scale)
    {
        switch (state)
        {
            case CashierAmountState.Ready:
                Squircle.Stroke(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(ui.Accent),
                    StateRimWidth * scale);
                break;
            case CashierAmountState.Over:
                Squircle.Stroke(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(ui.Theme.Danger),
                    StateRimWidth * scale);
                break;
            default:
                Squircle.Stroke(drawList, field.Min, field.Max, radius,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, IdleRimAlpha)), Metrics.Stroke.Hairline);
                break;
        }
    }
}
