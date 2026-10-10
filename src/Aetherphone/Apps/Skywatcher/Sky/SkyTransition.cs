using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal sealed class SkyTransition
{
    private const float SmoothSeconds = 0.55f;
    private const float SettledThreshold = 0.995f;
    private const float TreatTop = 0.08f;
    private const float TreatBottom = 0.16f;
    private WeatherKind fromKind;
    private WeatherKind toKind;
    private Spring progress = new(1f);
    private SkyPalette fromPalette;
    private float fromDensity;
    private bool started;

    public SkyPalette Palette { get; private set; }

    public float Density { get; private set; }

    public void Step(WeatherKind kind, float daylight, float deltaSeconds)
    {
        if (!started)
        {
            fromKind = kind;
            toKind = kind;
            progress = new Spring(1f);
            started = true;
        }
        else if (kind != toKind)
        {
            fromPalette = Palette;
            fromDensity = Density;
            fromKind = progress.Value >= 0.5f ? toKind : fromKind;
            toKind = kind;
            progress = new Spring(0f);
        }

        var amount = progress.Step(1f, SmoothSeconds, deltaSeconds);
        var target = WeatherSky.Blend(toKind, daylight);
        var targetDensity = WeatherCard.DensityFor(toKind);
        if (amount >= SettledThreshold)
        {
            Palette = target;
            Density = targetDensity;
            return;
        }

        Palette = WeatherSky.Mix(fromPalette, target, amount);
        Density = fromDensity + (targetDensity - fromDensity) * amount;
    }

    public void Snap() => started = false;

    public void Draw(ImDrawListPtr drawList, in Rect screen, float rounding, float daylight, float scale)
    {
        var amount = progress.Value;
        if (amount < SettledThreshold)
        {
            WeatherAmbience.Draw(drawList, screen, rounding, fromKind, daylight, Palette, scale, 1f - amount);
        }

        WeatherAmbience.Draw(drawList, screen, rounding, toKind, daylight, Palette, scale, amount);
        Treats.Offer(drawList, TreatSpot.Skywatcher,
            new Rect(new Vector2(screen.Min.X, screen.Min.Y + screen.Height * TreatTop),
                new Vector2(screen.Max.X, screen.Min.Y + screen.Height * TreatBottom)));
    }
}
