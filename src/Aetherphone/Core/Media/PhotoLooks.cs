namespace Aetherphone.Core.Media;

internal readonly struct LookParameters
{
    public readonly float Brightness;
    public readonly float Contrast;
    public readonly float Saturation;
    public readonly float Warmth;
    public readonly float Vignette;
    public readonly float Lift;

    public LookParameters(float brightness, float contrast, float saturation, float warmth, float vignette, float lift)
    {
        Brightness = brightness;
        Contrast = contrast;
        Saturation = saturation;
        Warmth = warmth;
        Vignette = vignette;
        Lift = lift;
    }

    public static LookParameters Neutral => new(0f, 0f, 0f, 0f, 0f, 0f);

    public LookParameters Scaled(float strength)
    {
        return new LookParameters(Brightness * strength, Contrast * strength, Saturation * strength,
            Warmth * strength, Vignette * strength, Lift * strength);
    }
}

internal static class PhotoLooks
{
    public static readonly PhotoLook[] All =
    {
        PhotoLook.None,
        PhotoLook.Warm,
        PhotoLook.Cool,
        PhotoLook.Vivid,
        PhotoLook.Film,
        PhotoLook.Fade,
        PhotoLook.Mono,
        PhotoLook.Noir,
    };

    public static LookParameters Of(PhotoLook look)
    {
        switch (look)
        {
            case PhotoLook.Warm:
                return new LookParameters(0.03f, 0f, 0.08f, 0.5f, 0f, 0f);
            case PhotoLook.Cool:
                return new LookParameters(0f, 0.05f, 0f, -0.5f, 0f, 0f);
            case PhotoLook.Vivid:
                return new LookParameters(0.02f, 0.18f, 0.45f, 0f, 0f, 0f);
            case PhotoLook.Film:
                return new LookParameters(0f, -0.12f, -0.12f, 0.18f, 0.25f, 0.10f);
            case PhotoLook.Fade:
                return new LookParameters(0f, -0.2f, -0.2f, 0f, 0f, 0.18f);
            case PhotoLook.Mono:
                return new LookParameters(0f, 0.08f, -1f, 0f, 0f, 0f);
            case PhotoLook.Noir:
                return new LookParameters(-0.05f, 0.35f, -1f, 0f, 0.55f, 0f);
            default:
                return LookParameters.Neutral;
        }
    }
}
