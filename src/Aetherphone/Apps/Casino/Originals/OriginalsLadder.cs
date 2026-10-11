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

internal readonly struct LadderLayout
{
    public const float MinChipWidth = 62f;
    public const float MaxChipWidth = 110f;
    public const float Gap = 6f;
    public const float CaptionGap = 10f;
    public const float CaptionShare = 0.3f;

    public readonly float ChipsLeft;
    public readonly float ChipWidth;
    public readonly int First;
    public readonly int Visible;

    private readonly float gap;

    private LadderLayout(float chipsLeft, float chipWidth, int first, int visible, float gap)
    {
        ChipsLeft = chipsLeft;
        ChipWidth = chipWidth;
        First = first;
        Visible = visible;
        this.gap = gap;
    }

    public float ChipLeft(int slot) => ChipsLeft + slot * (ChipWidth + gap);

    public static float CaptionRoom(float rowWidth) => rowWidth * CaptionShare;

    public static LadderLayout Compute(float rowLeft, float rowRight, float captionWidth, int count, int focus,
        float scale)
    {
        var gap = Gap * scale;
        var start = captionWidth > 0f ? rowLeft + captionWidth + CaptionGap * scale : rowLeft;
        var width = MathF.Max(0f, rowRight - start);
        if (count <= 0 || width <= 0f)
        {
            return new LadderLayout(start, 0f, 0, 0, gap);
        }

        var fit = Math.Max(1, (int)((width + gap) / (MinChipWidth * scale + gap)));
        var visible = Math.Min(count, fit);
        var chipWidth = MathF.Min(MaxChipWidth * scale, (width - gap * (visible - 1)) / visible);
        var first = Math.Clamp(focus - visible / 2, 0, count - visible);
        var total = chipWidth * visible + gap * (visible - 1);
        var left = start + (width - total) * 0.5f;
        return new LadderLayout(left, chipWidth, first, visible, gap);
    }
}

internal static class OriginalsLadder
{
    public const float Height = 46f;
    public const int Capacity = 25;

    private const float Pad = 6f;
    private const float FillAlpha = 0.10f;
    private const float CurrentFillAlpha = 0.22f;
    private const float StrokeAlpha = 0.85f;

    public static void Draw(ImDrawListPtr drawList, Rect row, string caption, ReadOnlySpan<LadderStep> steps, int focus,
        AppSkin ui, float scale)
    {
        if (steps.Length == 0 || row.Width <= 0f)
        {
            return;
        }

        var captionStyle = TextStyles.FootnoteEmphasized;
        var shownCaption = caption.Length > 0
            ? Typography.FitText(caption, LadderLayout.CaptionRoom(row.Width), captionStyle)
            : string.Empty;
        var captionSize = shownCaption.Length > 0 ? Typography.Measure(shownCaption, captionStyle) : Vector2.Zero;
        if (shownCaption.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - captionSize.Y * 0.5f), shownCaption,
                CasinoColors.InkBody, captionStyle);
        }

        var layout = LadderLayout.Compute(row.Min.X, row.Max.X, captionSize.X, steps.Length, focus, scale);
        var rounding = Metrics.Radius.Md * scale;
        for (var slot = 0; slot < layout.Visible; slot++)
        {
            var step = steps[layout.First + slot];
            var min = new Vector2(layout.ChipLeft(slot), row.Min.Y);
            var max = new Vector2(min.X + layout.ChipWidth, row.Max.Y);
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
        var ink = step.State == LadderState.Upcoming ? ui.BodyInk : tint;
        var caption = Typography.FitText(step.Caption, inner, TextStyles.Footnote);
        var captionSize = Typography.Measure(caption, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(rect.Center.X - captionSize.X * 0.5f, top), caption, ink,
            TextStyles.Footnote);
        var value = Typography.FitText(step.Value, inner, TextStyles.FootnoteEmphasized);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(rect.Center.X - valueSize.X * 0.5f, top + captionHeight), value, ink,
            TextStyles.FootnoteEmphasized);
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
