using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineGambleLayout
{
    public const float RowGap = 8f;

    public readonly Rect Red;
    public readonly Rect Black;
    public readonly Rect Collect;

    private MachineGambleLayout(Rect red, Rect black, Rect collect)
    {
        Red = red;
        Black = black;
        Collect = collect;
    }

    public static MachineGambleLayout Compute(Rect inner, float buttonHeight, float scale)
    {
        var gap = RowGap * scale;
        var collectTop = MathF.Max(inner.Min.Y, inner.Max.Y - buttonHeight);
        var choiceTop = MathF.Max(inner.Min.Y, collectTop - gap - buttonHeight);
        var choiceBottom = MathF.Max(choiceTop, collectTop - gap);
        var half = MathF.Max(0f, (inner.Width - gap) * 0.5f);
        var red = new Rect(new Vector2(inner.Min.X, choiceTop), new Vector2(inner.Min.X + half, choiceBottom));
        var black = new Rect(new Vector2(MathF.Min(inner.Max.X, red.Max.X + gap), choiceTop),
            new Vector2(inner.Max.X, choiceBottom));
        var collect = new Rect(new Vector2(inner.Min.X, collectTop), inner.Max);
        return new MachineGambleLayout(red, black, collect);
    }
}
