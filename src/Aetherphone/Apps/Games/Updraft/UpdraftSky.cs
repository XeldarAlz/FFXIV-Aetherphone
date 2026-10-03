using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Updraft;

internal readonly struct UpdraftSkyState
{
    public readonly Vector4 Top;
    public readonly Vector4 Middle;
    public readonly Vector4 Bottom;
    public readonly Vector4 Light;
    public readonly Vector4 Sun;
    public readonly float SunHeight;
    public readonly float Moon;
    public readonly float Stars;
    public readonly float Aurora;

    public UpdraftSkyState(Vector4 top, Vector4 middle, Vector4 bottom, Vector4 light, Vector4 sun, float sunHeight,
        float moon, float stars, float aurora)
    {
        Top = top;
        Middle = middle;
        Bottom = bottom;
        Light = light;
        Sun = sun;
        SunHeight = sunHeight;
        Moon = moon;
        Stars = stars;
        Aurora = aurora;
    }
}

internal static class UpdraftSky
{
    private const float HoldFraction = 0.55f;
    private static readonly float[] BandStartMetres = { 0f, 250f, 600f, 1000f, 1500f };
    private static readonly Vector4[] Tops =
    {
        new(0.33f, 0.42f, 0.74f, 1f), new(0.24f, 0.53f, 0.92f, 1f), new(0.30f, 0.22f, 0.52f, 1f),
        new(0.03f, 0.04f, 0.13f, 1f), new(0.02f, 0.04f, 0.11f, 1f),
    };

    private static readonly Vector4[] Middles =
    {
        new(0.78f, 0.60f, 0.74f, 1f), new(0.47f, 0.74f, 0.97f, 1f), new(0.86f, 0.42f, 0.42f, 1f),
        new(0.08f, 0.10f, 0.25f, 1f), new(0.04f, 0.13f, 0.21f, 1f),
    };

    private static readonly Vector4[] Bottoms =
    {
        new(1.00f, 0.78f, 0.60f, 1f), new(0.78f, 0.92f, 1.00f, 1f), new(1.00f, 0.66f, 0.38f, 1f),
        new(0.16f, 0.18f, 0.36f, 1f), new(0.07f, 0.22f, 0.29f, 1f),
    };

    private static readonly Vector4[] Lights =
    {
        new(1f, 0.93f, 0.90f, 1f), new(1f, 1f, 1f, 1f), new(1f, 0.84f, 0.74f, 1f), new(0.60f, 0.66f, 0.88f, 1f),
        new(0.60f, 0.78f, 0.86f, 1f),
    };

    private static readonly Vector4[] Suns =
    {
        new(1f, 0.86f, 0.62f, 1f), new(1f, 0.97f, 0.84f, 1f), new(1f, 0.56f, 0.30f, 1f), new(0.92f, 0.94f, 1f, 1f),
        new(0.86f, 0.96f, 1f, 1f),
    };

    private static readonly float[] SunHeights = { 0.74f, 0.16f, 0.66f, 0.18f, 0.14f };
    private static readonly float[] Moons = { 0f, 0f, 0f, 1f, 1f };
    private static readonly float[] StarAmounts = { 0f, 0f, 0.12f, 0.85f, 1f };
    private static readonly float[] AuroraAmounts = { 0f, 0f, 0f, 0.12f, 1f };

    public static UpdraftSkyState At(float metres)
    {
        var band = 0;
        while (band < BandStartMetres.Length - 1 && metres >= BandStartMetres[band + 1])
        {
            band++;
        }

        if (band == BandStartMetres.Length - 1)
        {
            return Compose(band, band, 0f);
        }

        var span = BandStartMetres[band + 1] - BandStartMetres[band];
        var progress = (metres - BandStartMetres[band]) / span;
        var blend = progress <= HoldFraction ? 0f : Easing.SmoothStep((progress - HoldFraction) / (1f - HoldFraction));
        return Compose(band, band + 1, blend);
    }

    private static UpdraftSkyState Compose(int from, int to, float blend) =>
        new(Vector4.Lerp(Tops[from], Tops[to], blend), Vector4.Lerp(Middles[from], Middles[to], blend),
            Vector4.Lerp(Bottoms[from], Bottoms[to], blend), Vector4.Lerp(Lights[from], Lights[to], blend),
            Vector4.Lerp(Suns[from], Suns[to], blend), Easing.Lerp(SunHeights[from], SunHeights[to], blend),
            Easing.Lerp(Moons[from], Moons[to], blend), Easing.Lerp(StarAmounts[from], StarAmounts[to], blend),
            Easing.Lerp(AuroraAmounts[from], AuroraAmounts[to], blend));
}
