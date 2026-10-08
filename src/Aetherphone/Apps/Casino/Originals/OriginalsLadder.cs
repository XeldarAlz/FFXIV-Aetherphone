using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal enum LadderState : byte
{
    Upcoming,
    Past,
    Current,
    Hit,
    Lost,
}

internal struct LadderStep
{
    public string Caption;
    public string Value;
    public LadderState State;

    public LadderStep(string caption, string value, LadderState state)
    {
        Caption = caption;
        Value = value;
        State = state;
    }
}

internal static class OriginalsLadder
{
    public const float Height = 46f;
    public const int Capacity = 25;

    private const float MinChipWidth = 62f;
    private const float MaxChipWidth = 110f;
    private const float Gap = 6f;
    private const float Pad = 6f;
    private const float FillAlpha = 0.10f;
    private const float CurrentFillAlpha = 0.22f;
    private const float StrokeAlpha = 0.85f;

    public static void Draw(ImDrawListPtr drawList, Rect row, ReadOnlySpan<LadderStep> steps, int focus, AppSkin ui,
        float scale)
    {
        if (steps.Length == 0 || row.Width <= 0f)
        {
            return;
        }

        var gap = Gap * scale;
        var fit = Math.Max(1, (int)((row.Width + gap) / (MinChipWidth * scale + gap)));
        var visible = Math.Min(steps.Length, fit);
        var chipWidth = MathF.Min(MaxChipWidth * scale, (row.Width - gap * (visible - 1)) / visible);
        var first = Math.Clamp(focus - visible / 2, 0, steps.Length - visible);
        var total = chipWidth * visible + gap * (visible - 1);
        var left = row.Center.X - total * 0.5f;
        var rounding = Metrics.Radius.Md * scale;
        for (var slot = 0; slot < visible; slot++)
        {
            var step = steps[first + slot];
            var min = new Vector2(left + slot * (chipWidth + gap), row.Min.Y);
            var max = new Vector2(min.X + chipWidth, row.Max.Y);
            DrawChip(drawList, new Rect(min, max), step, rounding, ui, scale);
        }
    }

    private static void DrawChip(ImDrawListPtr drawList, Rect rect, in LadderStep step, float rounding, AppSkin ui,
        float scale)
    {
        var tint = Tint(step.State, ui);
        var current = step.State is LadderState.Current or LadderState.Hit;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(tint, current ? CurrentFillAlpha : FillAlpha)));
        if (current)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(tint, StrokeAlpha)), MathF.Max(1f, 1.4f * scale));
        }

        var pad = Pad * scale;
        var inner = rect.Width - pad * 2f;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var top = rect.Center.Y - (captionHeight + valueHeight) * 0.5f;
        var caption = Typography.FitText(step.Caption, inner, TextStyles.Footnote);
        var captionSize = Typography.Measure(caption, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(rect.Center.X - captionSize.X * 0.5f, top), caption,
            step.State == LadderState.Upcoming ? ui.BodyInk : tint, TextStyles.Footnote);
        var value = Typography.FitText(step.Value, inner, TextStyles.FootnoteEmphasized);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Center.X - valueSize.X * 0.5f, top + captionHeight), value,
            step.State == LadderState.Upcoming ? ui.BodyInk : tint, TextStyles.FootnoteEmphasized);
    }

    private static Vector4 Tint(LadderState state, AppSkin ui) => state switch
    {
        LadderState.Current => CasinoColors.LightB,
        LadderState.Hit => CasinoColors.Money,
        LadderState.Past => CasinoColors.MoneyHighlight,
        LadderState.Lost => CasinoColors.InkMuted,
        _ => ui.TitleInk,
    };
}
