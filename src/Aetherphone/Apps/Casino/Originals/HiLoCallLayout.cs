using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Originals;

internal static class HiLoCallLayout
{
    public const float CallHeight = 56f;
    public const float MinimumWidth = 44f;
    public const float Gap = 10f;
    public const int MaxCalls = 3;

    public static float GroupHeight(float cardHeight, float callsHeight, float scale) =>
        cardHeight + Gap * scale + callsHeight;

    public static float GroupTop(Rect stage, float groupHeight) =>
        stage.Min.Y + MathF.Max(0f, (stage.Height - groupHeight) * 0.5f);

    public static bool Distribute(ReadOnlySpan<float> naturals, float rowWidth, float scale, Span<float> widths)
    {
        var count = naturals.Length;
        if (count == 0)
        {
            return true;
        }

        var minimum = MinimumWidth * scale;
        var room = MathF.Max(0f, rowWidth - Gap * scale * (count - 1));
        var total = 0f;
        for (var index = 0; index < count; index++)
        {
            total += MathF.Max(minimum, naturals[index]);
        }

        var fits = total <= room + 0.5f;
        var slack = fits ? (room - total) / count : 0f;
        var shrink = fits || total <= 0f ? 1f : room / total;
        for (var index = 0; index < count; index++)
        {
            widths[index] = MathF.Max(minimum, naturals[index]) * shrink + slack;
        }

        return fits;
    }

    public static Rect Call(Rect row, ReadOnlySpan<float> widths, int index, float scale)
    {
        var left = row.Min.X;
        for (var earlier = 0; earlier < index; earlier++)
        {
            left += widths[earlier] + Gap * scale;
        }

        return new Rect(new Vector2(left, row.Min.Y), new Vector2(left + widths[index], row.Min.Y + CallHeight * scale));
    }
}
