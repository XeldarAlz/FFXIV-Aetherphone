namespace Aetherphone.Core.Theme;

internal static class ChatInk
{
    private const float IncomingBubbleAlpha = 0.10f;
    private const float LightWashScale = 0.85f;
    private static readonly Vector4 LightWash = new(0.10f, 0.10f, 0.14f, 1f);
    private static readonly Vector4 DarkWash = new(1f, 1f, 1f, 1f);

    public static readonly Vector4 SeenTick = new(0.45f, 0.83f, 1f, 1f);

    public static Vector4 Wash(PhoneTheme theme, float alpha) =>
        RoleInk.IsLight(theme)
            ? LightWash with { W = alpha * LightWashScale }
            : DarkWash with { W = alpha };

    public static Vector4 IncomingBubble(PhoneTheme theme) => Wash(theme, IncomingBubbleAlpha);
}
