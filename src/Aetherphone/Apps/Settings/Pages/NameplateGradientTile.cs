using Aetherphone.Core;
using Aetherphone.Core.Honorific;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Settings.Pages;

internal readonly ref struct NameplateGradientTile
{
    private const int Strips = 24;
    private const float StrokeAlpha = 0.12f;
    private const float HoverStrokeAlpha = 0.35f;
    private const float RingGap = 1.5f;
    private const float LabelHaloRadius = 1.2f;
    private static readonly Vector4 LabelInk = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 LabelHalo = new(0f, 0f, 0f, 0.85f);

    private readonly ImDrawListPtr drawList;
    private readonly PhoneTheme theme;
    private readonly Vector3 first;
    private readonly Vector3 second;
    private readonly NameplateGradientMotion motion;
    private readonly long tick;
    private readonly float scale;

    public NameplateGradientTile(NameplateTitleSettings settings, long tick, PhoneTheme theme, float scale)
    {
        drawList = ImGui.GetWindowDrawList();
        this.theme = theme;
        first = NameplatePalette.Unpack(settings.GradientFirst);
        second = NameplatePalette.Unpack(settings.GradientSecond);
        motion = settings.Motion;
        this.tick = tick;
        this.scale = scale;
    }

    public void Draw(Vector2 min, Vector2 max, int gradient, bool selected, bool hovered)
    {
        var count = NameplateGradients.StepCount(gradient);
        var phase = tick % count;
        var width = max.X - min.X;
        var leftColor = ColorAt(gradient, count, phase, 0f);
        for (var strip = 0; strip < Strips; strip++)
        {
            var across = (strip + 1) / (float)Strips;
            var rightColor = ColorAt(gradient, count, phase, across);
            drawList.AddRectFilledMultiColor(new Vector2(min.X + width * strip / Strips, min.Y),
                new Vector2(min.X + width * across, max.Y), leftColor, rightColor, rightColor, leftColor);
            leftColor = rightColor;
        }

        var radius = Metrics.Radius.Md * scale;
        Squircle.FillOutsideCorners(drawList, min, max, radius, ImGui.GetColorU32(theme.GroupedCard), 0f);
        Squircle.Stroke(drawList, min, max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, hovered ? HoverStrokeAlpha : StrokeAlpha)),
            Metrics.Stroke.Hairline * scale);
        Typography.DrawCenteredHalo((min + max) * 0.5f, Loc.T(NameplateGradients.Label(gradient)), LabelInk,
            LabelHalo, LabelHaloRadius * scale, width - Metrics.Space.Md * scale * 2f, TextStyles.FootnoteEmphasized);
        if (!selected)
        {
            return;
        }

        var ring = Metrics.Stroke.Ring * scale * RingGap;
        Squircle.Stroke(drawList, min - new Vector2(ring), max + new Vector2(ring), radius + ring,
            ImGui.GetColorU32(theme.Accent), Metrics.Stroke.Ring * scale);
    }

    private uint ColorAt(int gradient, int count, long phase, float across)
    {
        var position = motion switch
        {
            NameplateGradientMotion.Pulse => phase,
            NameplateGradientMotion.Wave => phase + across * count,
            _ => across * (count - 1),
        };
        return ImGui.GetColorU32(new Vector4(NameplateGradients.Blend(gradient, first, second, position), 1f));
    }
}
