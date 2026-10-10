using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal static class TreatBand
{
    private const float Left = 0.3f;
    private const float Right = 0.7f;

    public static Rect Header(Rect screen, float top, float height) =>
        new(new Vector2(screen.Min.X + screen.Width * Left, top),
            new Vector2(screen.Min.X + screen.Width * Right, top + height));
}
