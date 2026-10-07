using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Games.Framework;

internal static class StageInks
{
    public const float DarkMutedAlpha = 0.62f;
    public static readonly Vector4 Strong = GamePalette.InkLight;
    public static readonly Vector4 Muted = new(0.56f, 0.56f, 0.60f, 1f);
    public static readonly Vector4 Surface = new(0.22f, 0.22f, 0.28f, 0.65f);
    public static readonly Vector4 Track = Surfaces.Fill(Strong, FillLevel.Tertiary);

    public static Vector4 StrongOn(StageInk tone) => tone == StageInk.Dark ? GamePalette.InkDark : Strong;

    public static Vector4 MutedOn(StageInk tone) =>
        tone == StageInk.Dark ? GamePalette.InkDark with { W = DarkMutedAlpha } : Muted;
}
