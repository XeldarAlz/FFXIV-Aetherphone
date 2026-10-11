namespace Aetherphone.Apps.Casino.Stage;

internal static class CasinoColors
{
    public static readonly Vector4 Money = new(1f, 0.788f, 0.290f, 1f);
    public static readonly Vector4 MoneyHighlight = new(1f, 0.878f, 0.541f, 1f);
    public static readonly Vector4 LightA = new(1f, 0.239f, 0.604f, 1f);
    public static readonly Vector4 LightB = new(0.180f, 0.902f, 1f, 1f);
    public static readonly Vector4 FeltTop = new(0.055f, 0.231f, 0.180f, 1f);
    public static readonly Vector4 FeltBottom = new(0.031f, 0.141f, 0.098f, 1f);
    public static readonly Vector4 InkTitle = new(0.957f, 0.937f, 0.894f, 1f);
    public static readonly Vector4 InkBody = new(0.851f, 0.839f, 0.800f, 1f);
    public static readonly Vector4 InkMuted = new(0.557f, 0.541f, 0.612f, 1f);
    public static readonly Vector4 Practice = new(0.604f, 0.627f, 0.651f, 1f);
    public static readonly Vector4 Loss = InkMuted;

    public static readonly Vector4[] Confetti =
    {
        Money,
        MoneyHighlight,
        LightA,
        LightB,
    };
}
