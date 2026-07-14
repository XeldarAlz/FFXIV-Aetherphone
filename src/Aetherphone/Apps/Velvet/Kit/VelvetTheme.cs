using System.Numerics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VelvetTheme
{
    public static readonly Vector4 GroundTop = new(0.094f, 0.039f, 0.110f, 1f);
    public static readonly Vector4 GroundBottom = new(0.024f, 0.012f, 0.031f, 1f);
    public static readonly Vector4 PlumWell = new(0.071f, 0.027f, 0.078f, 1f);
    public static readonly Vector4 Sunken = new(0.043f, 0.020f, 0.063f, 1f);

    public static readonly Vector4 Card = new(0.141f, 0.067f, 0.161f, 1f);
    public static readonly Vector4 CardHi = new(0.184f, 0.094f, 0.212f, 1f);
    public static readonly Vector4 CardStroke = new(0.235f, 0.133f, 0.259f, 0.60f);
    public static readonly Vector4 Sheen = new(1f, 0.851f, 0.902f, 0.10f);
    public static readonly Vector4 Divider = new(1f, 0.878f, 0.925f, 0.07f);
    public static readonly Vector4 Hairline = new(0.984f, 0.953f, 0.969f, 0.10f);

    public static readonly Vector4 RoseGlow = new(1f, 0.361f, 0.541f, 1f);
    public static readonly Vector4 RoseBright = new(0.961f, 0.243f, 0.447f, 1f);
    public static readonly Vector4 Rose = new(0.898f, 0.102f, 0.357f, 1f);
    public static readonly Vector4 RoseDeep = new(0.722f, 0.078f, 0.282f, 1f);
    public static readonly Vector4 RoseShadow = new(0.486f, 0.055f, 0.200f, 1f);
    public static readonly Vector4 RoseInk = new(1f, 0.612f, 0.733f, 1f);

    public static readonly Vector4 BloomTop = new(0.870f, 0.140f, 0.380f, 0.22f);
    public static readonly Vector4 BloomBottom = new(0.360f, 0.090f, 0.440f, 0f);

    public static readonly Vector4 Moonlight = new(0.769f, 0.706f, 0.871f, 1f);
    public static readonly Vector4 MoonGlow = new(0.906f, 0.871f, 0.961f, 0.14f);

    public static readonly Vector4 TitleInk = new(0.984f, 0.953f, 0.969f, 1f);
    public static readonly Vector4 BodyInk = new(0.925f, 0.871f, 0.906f, 0.96f);
    public static readonly Vector4 MutedInk = new(0.706f, 0.604f, 0.675f, 0.90f);
    public static readonly Vector4 HeaderInk = new(0.961f, 0.663f, 0.761f, 0.95f);
    public static readonly Vector4 Faint = new(0.541f, 0.463f, 0.525f, 0.80f);

    public static readonly Vector4 Gold = new(0.910f, 0.784f, 0.475f, 1f);
    public static readonly Vector4 GoldInk = new(0.953f, 0.851f, 0.561f, 1f);

    public static readonly Vector4 Danger = new(0.784f, 0.196f, 0.290f, 1f);
    public static readonly Vector4 OnAccent = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Scrim = new(0f, 0f, 0f, 0.55f);

    public static readonly Vector4 Online = new(0.204f, 0.816f, 0.478f, 1f);
    public static readonly Vector4 Away = new(0.941f, 0.706f, 0.161f, 1f);
    public static readonly Vector4 Dnd = new(0.898f, 0.282f, 0.302f, 1f);
    public static readonly Vector4 OfflineDot = new(0.431f, 0.384f, 0.439f, 1f);

    public static readonly AppPalette Palette = new()
    {
        Accent = Rose,
        TitleInk = TitleInk,
        BodyInk = BodyInk,
        MutedInk = MutedInk,
        HeaderInk = HeaderInk,
        HeadingInk = TitleInk,
        BackdropTop = GroundTop,
        BackdropBottom = GroundBottom,
        BloomTop = BloomTop,
        BloomBottom = BloomBottom,
        CardFill = Card,
        CardStroke = CardStroke,
        FieldSurface = PlumWell,
        HoverTint = new(1f, 0.878f, 0.925f, 0.06f),
    };

    public static Vector4 Alpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    public static Vector4 Lerp(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);

    public static Vector4 PresenceColor(int presence) =>
        presence switch
        {
            VelvetPresence.Online => Online,
            VelvetPresence.Away => Away,
            VelvetPresence.Dnd => Dnd,
            _ => OfflineDot,
        };

    public static bool PresenceActive(int presence) => presence != VelvetPresence.Offline;

    public static uint Packed(this Vector4 color) => ImGui.GetColorU32(color);
}
