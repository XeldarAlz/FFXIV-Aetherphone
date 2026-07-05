using System.Numerics;

namespace Aetherphone.Core.Theme;

internal static class Palette
{
    public static Vector4 WithAlpha(Vector4 color, float alpha) => color with { W = alpha };
    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);
}
