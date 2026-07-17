using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Widgets;

internal sealed class ActivityRingsWidget : IHomeWidget
{
    private const float ThicknessFactor = 0.24f;
    private const float GapFactor = 0.07f;

    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;

    public string Id => "character.rings";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string AppId => "character";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public ActivityRingsWidget(ActivityTracker tracker, Configuration configuration)
    {
        this.tracker = tracker;
        this.configuration = configuration;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        var day = tracker.IsTracking ? tracker.Today : null;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, day);
            return;
        }

        DrawMedium(context, day);
    }

    private void DrawSmall(in WidgetContext context, ActivityDay? day)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var pad = 13f * scale;
        WidgetChrome.Eyebrow(context.DrawList, new Vector2(bounds.Min.X + pad, bounds.Min.Y + pad), DisplayName,
            context.Theme.TextMuted, scale, context.Opacity);
        var center = new Vector2(bounds.Center.X, bounds.Center.Y + 7f * scale);
        var radius = MathF.Min(bounds.Width, bounds.Height) * 0.5f - 24f * scale;
        DrawRings(context, center, radius, day);
    }

    private void DrawMedium(in WidgetContext context, ActivityDay? day)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        var radius = bounds.Height * 0.5f - 22f * scale;
        var ringsCenter = new Vector2(bounds.Min.X + 22f * scale + radius, bounds.Center.Y);
        DrawRings(context, ringsCenter, radius, day);
        var textLeft = ringsCenter.X + radius + 24f * scale;
        var rowStep = bounds.Height * 0.30f;
        DrawLegendRow(context, new Vector2(textLeft, bounds.Center.Y - rowStep), ActivityRings.RingOneTint,
            Loc.T(L.Character.RingProgress), ProgressValue(day));
        DrawLegendRow(context, new Vector2(textLeft, bounds.Center.Y), ActivityRings.RingTwoTint,
            Loc.T(L.Character.RingAdventure), AdventureValue(day));
        DrawLegendRow(context, new Vector2(textLeft, bounds.Center.Y + rowStep), ActivityRings.RingThreeTint,
            Loc.T(L.Character.RingFortune), FortuneValue(day));
    }

    private void DrawRings(in WidgetContext context, Vector2 center, float radius, ActivityDay? day)
    {
        var opacity = context.Opacity;
        var track = Palette.WithAlpha(context.Theme.TextStrong, 0.12f * opacity);
        var thickness = radius * ThicknessFactor;
        var gap = radius * GapFactor;
        var middle = radius - thickness - gap;
        var inner = middle - thickness - gap;
        DrawRing(center, radius, thickness,
            day is null ? 0f : ActivityGoals.ProgressFraction(configuration, day), ActivityRings.RingOneTint, track,
            opacity);
        DrawRing(center, middle, thickness,
            day is null ? 0f : ActivityGoals.AdventureFraction(configuration, day), ActivityRings.RingTwoTint, track,
            opacity);
        DrawRing(center, inner, thickness,
            day is null ? 0f : ActivityGoals.FortuneFraction(configuration, day), ActivityRings.RingThreeTint, track,
            opacity);
    }

    private static void DrawRing(Vector2 center, float radius, float thickness, float fraction, Vector4 tint,
        Vector4 track, float opacity)
    {
        ProgressRing.Track(center, radius, thickness, track);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            ProgressRing.Fill(center, radius, thickness, clamped, Palette.WithAlpha(tint, opacity));
        }
    }

    private void DrawLegendRow(in WidgetContext context, Vector2 position, Vector4 tint, string label, string value)
    {
        var scale = context.Scale;
        var opacity = context.Opacity;
        var drawList = context.DrawList;
        var dot = 3.4f * scale;
        drawList.AddCircleFilled(new Vector2(position.X + dot, position.Y), dot,
            ImGui.GetColorU32(Palette.WithAlpha(tint, opacity)));
        var textLeft = position.X + dot * 2f + 7f * scale;
        WidgetChrome.Eyebrow(drawList, new Vector2(textLeft, position.Y - 15f * scale), label,
            context.Theme.TextMuted, scale, opacity);
        var valueSize = Typography.Measure(value, 0.86f, FontWeight.SemiBold);
        Typography.Draw(new Vector2(textLeft, position.Y - valueSize.Y * 0.5f + 7f * scale), value,
            Palette.WithAlpha(context.Theme.TextStrong, opacity), 0.86f, FontWeight.SemiBold);
    }

    private string ProgressValue(ActivityDay? day)
    {
        var fraction = day is null ? 0f : ActivityGoals.ProgressFraction(configuration, day);
        return $"{(int)MathF.Round(Math.Clamp(fraction, 0f, 9.99f) * 100f)}%";
    }

    private string AdventureValue(ActivityDay? day) =>
        $"{day?.DutiesCompleted ?? 0} / {configuration.ActivityGoalDuties}";

    private string FortuneValue(ActivityDay? day) =>
        $"{Compact(day?.GilEarned ?? 0)} / {Compact(configuration.ActivityGoalGil)}";

    private static string Compact(long value)
    {
        if (value >= 1_000_000)
        {
            var millions = value / 1_000_000f;
            return millions.ToString(millions >= 10f ? "0" : "0.#", Loc.Culture) + "M";
        }

        if (value >= 1_000)
        {
            var thousands = value / 1_000f;
            return thousands.ToString(thousands >= 10f ? "0" : "0.#", Loc.Culture) + "K";
        }

        return value.ToString(Loc.Culture);
    }

    public void Dispose()
    {
    }
}
