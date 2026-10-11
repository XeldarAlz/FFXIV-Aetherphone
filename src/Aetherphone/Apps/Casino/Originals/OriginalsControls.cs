using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Originals;

internal static class OriginalsControls
{
    public const float StatHeight = 28f;

    private const float StatPad = 10f;

    public static LocString ReasonNotice(string reason) =>
        CasinoReasons.MessageFor(reason.Length > 0 ? reason : CasinoReasons.Unreachable);

    public static float StatPill(ImDrawListPtr drawList, Vector2 origin, string caption, string value, Vector4 valueInk,
        AppSkin ui, float scale)
    {
        var height = StatHeight * scale;
        var pad = StatPad * scale;
        var gap = Metrics.Space.Xs * scale;
        var captionSize = Typography.Measure(caption, TextStyles.Footnote);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        var width = pad * 2f + captionSize.X + gap + valueSize.X;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, origin, max, height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var center = origin.Y + height * 0.5f;
        Typography.Draw(drawList, new Vector2(origin.X + pad, center - captionSize.Y * 0.5f), caption, ui.MutedInk,
            TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(origin.X + pad + captionSize.X + gap, center - valueSize.Y * 0.5f), value,
            valueInk, TextStyles.FootnoteEmphasized);
        return width;
    }

    public static float StatWidth(string caption, string value, float scale) =>
        StatPad * 2f * scale + Typography.Measure(caption, TextStyles.Footnote).X + Metrics.Space.Xs * scale
        + Typography.Measure(value, TextStyles.FootnoteEmphasized).X;

    public static void StatRow(ImDrawListPtr drawList, Rect row, string firstCaption, string firstValue,
        Vector4 firstInk, string secondCaption, string secondValue, Vector4 secondInk, AppSkin ui, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var total = StatWidth(firstCaption, firstValue, scale) + gap + StatWidth(secondCaption, secondValue, scale);
        if (total > row.Width)
        {
            var room = MathF.Max(1f, (row.Width - gap) * 0.5f - StatWidth(string.Empty, string.Empty, scale));
            firstCaption = string.Empty;
            secondCaption = string.Empty;
            firstValue = Typography.FitText(firstValue, room, TextStyles.FootnoteEmphasized);
            secondValue = Typography.FitText(secondValue, room, TextStyles.FootnoteEmphasized);
            total = StatWidth(firstCaption, firstValue, scale) + gap + StatWidth(secondCaption, secondValue, scale);
        }

        var left = row.Center.X - total * 0.5f;
        var top = row.Center.Y - StatHeight * scale * 0.5f;
        var width = StatPill(drawList, new Vector2(left, top), firstCaption, firstValue, firstInk, ui, scale);
        StatPill(drawList, new Vector2(left + width + gap, top), secondCaption, secondValue, secondInk, ui, scale);
    }
}

internal sealed class OriginalsStepper
{
    private const float ButtonRadius = 15f;
    private const float TrackHeight = 8f;

    private readonly string minusId;
    private readonly string plusId;
    private readonly string trackId;

    private bool dragging;

    public OriginalsStepper(string id)
    {
        minusId = id + ".minus";
        plusId = id + ".plus";
        trackId = id + ".track";
    }

    public int Draw(ImDrawListPtr drawList, Rect rect, string caption, int value, int minimum, int maximum,
        AppSkin ui, bool enabled, float scale)
    {
        var radius = ButtonRadius * scale;
        var center = rect.Center.Y;
        var minus = new Vector2(rect.Min.X + radius, center);
        var plus = new Vector2(rect.Max.X - radius, center);
        var next = value;
        if (StepButton(drawList, minusId, minus, radius, FontAwesomeIcon.Minus, ui, enabled && value > minimum,
                scale))
        {
            next = Math.Max(minimum, value - 1);
        }

        if (StepButton(drawList, plusId, plus, radius, FontAwesomeIcon.Plus, ui, enabled && value < maximum, scale))
        {
            next = Math.Min(maximum, value + 1);
        }

        var trackLeft = minus.X + radius + Metrics.Space.Md * scale;
        var trackRight = plus.X - radius - Metrics.Space.Md * scale;
        var bar = TrackHeight * scale;
        var barTop = rect.Max.Y - bar - Metrics.Space.Xs * scale;
        var fraction = maximum > minimum ? (value - minimum) / (float)(maximum - minimum) : 0f;
        var barMin = new Vector2(trackLeft, barTop);
        var barMax = new Vector2(trackRight, barTop + bar);
        Squircle.Fill(drawList, barMin, barMax, bar * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var knobX = trackLeft + (trackRight - trackLeft) * fraction;
        if (knobX > barMin.X + bar)
        {
            Squircle.Fill(drawList, barMin, new Vector2(knobX, barMax.Y), bar * 0.5f,
                ImGui.GetColorU32(enabled ? CasinoColors.LightA : ui.MutedInk));
        }

        drawList.AddCircleFilled(new Vector2(knobX, barTop + bar * 0.5f), bar,
            ImGui.GetColorU32(enabled ? CasinoColors.InkTitle : ui.MutedInk), 16);
        var labelTop = rect.Min.Y;
        var number = GameNumber.Label(value);
        var numberSize = Typography.Measure(number, TextStyles.FootnoteEmphasized);
        var title = Typography.FitText(caption, MathF.Max(1f, trackRight - trackLeft - numberSize.X - 4f * scale),
            TextStyles.Footnote);
        var titleSize = Typography.Measure(title, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(trackLeft, labelTop + (numberSize.Y - titleSize.Y) * 0.5f), title,
            CasinoColors.InkBody, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(trackRight - numberSize.X, labelTop), number,
            enabled ? CasinoColors.InkTitle : ui.MutedInk, TextStyles.FootnoteEmphasized);
        if (!enabled)
        {
            dragging = false;
            return next;
        }

        var track = new Rect(new Vector2(trackLeft, rect.Min.Y), new Vector2(trackRight, rect.Max.Y));
        var hovered = PressSurface.Claim(trackId, track, out var activated);
        if (activated)
        {
            dragging = true;
        }

        if (dragging && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            dragging = false;
        }

        if (hovered || dragging)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        }

        if (!dragging)
        {
            return next;
        }

        var at = Math.Clamp((ImGui.GetMousePos().X - trackLeft) / MathF.Max(1f, trackRight - trackLeft), 0f, 1f);
        return minimum + (int)MathF.Round(at * (maximum - minimum));
    }

    private static bool StepButton(ImDrawListPtr drawList, string id, Vector2 center, float radius,
        FontAwesomeIcon icon, AppSkin ui, bool enabled, float scale)
    {
        var face = new Vector2(radius, radius);
        var reach = MathF.Max(radius, CasinoStageLayout.TouchTarget * 0.5f * scale);
        var hitMin = center - new Vector2(reach, reach);
        var hitMax = center + new Vector2(reach, reach);
        var hovered = enabled && UiInteract.Hover(hitMin, hitMax);
        var drawn = RoundButton.Surface(drawList, new Rect(center - face, center + face), ui.Ink, ButtonStyle.Gray,
            enabled, hovered, ImGui.GetID(id));
        ProgressRing.CenterIcon(drawList, center, icon, drawn.LabelInk,
            radius * 0.8f * (drawn.Face.Width / MathF.Max(radius * 2f, 0.0001f)));
        return enabled && UiInteract.Click(hitMin, hitMax, hovered);
    }
}
