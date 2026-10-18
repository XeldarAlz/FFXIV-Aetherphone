using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal static class TapArea
{
    public static Rect Around(Vector2 center, float side, Rect container)
    {
        var half = new Vector2(side * 0.5f);
        return new Rect(center - half, center + half).Intersect(container);
    }
}
