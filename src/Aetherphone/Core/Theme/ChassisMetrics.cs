namespace Aetherphone.Core.Theme;

internal readonly struct ChassisMetrics
{
    private const float RailFraction = 0.0195f;
    private const float ScreenRoundingFraction = 0.1028f;
    private const float ColorMetalFraction = 0.0115f;
    private const float ColorGlassFraction = 0.0135f;
    private const float ArtMetalFraction = 0.0365f;
    private const float ArtGlassFraction = 0.0155f;

    public readonly float RailWidth;
    public readonly float MetalWidth;
    public readonly float GlassWidth;
    public readonly float DeviceRounding;

    private ChassisMetrics(float railWidth, float metalWidth, float glassWidth, float deviceRounding)
    {
        RailWidth = railWidth;
        MetalWidth = metalWidth;
        GlassWidth = glassWidth;
        DeviceRounding = deviceRounding;
    }

    public static ChassisMetrics For(PhoneCaseKind kind, float deviceWidth)
    {
        var art = kind == PhoneCaseKind.Art;
        var metal = (art ? ArtMetalFraction : ColorMetalFraction) * deviceWidth;
        var glass = (art ? ArtGlassFraction : ColorGlassFraction) * deviceWidth;
        return new ChassisMetrics(RailFraction * deviceWidth, metal, glass,
            ScreenRoundingFraction * deviceWidth + metal + glass);
    }

    public static ChassisMetrics ForBody(PhoneCaseKind kind, float bodyWidth) =>
        For(kind, bodyWidth / (1f - 2f * RailFraction));

    public static ChassisMetrics Default => For(PhoneCaseKind.Color, PhoneSizeCatalog.DesignWidth);
}
