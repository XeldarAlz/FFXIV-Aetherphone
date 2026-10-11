using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class ChipStack
{
    public const int DenominationCount = 10;

    public const int MaxColumns = 3;

    public const int MaxDiscsPerColumn = 5;

    public static readonly long[] Denominations =
    {
        100_000_000, 10_000_000, 1_000_000, 500_000, 100_000, 25_000, 5_000, 1_000, 500, 100,
    };

    private const float DiscRadius = 9f;
    private const float DiscStep = 3.6f;
    private const float ColumnGap = 3f;
    private const float LabelGap = 5f;
    private const int NotchCount = 6;
    private const int DotCount = 12;
    private const int Segments = 20;

    private static readonly Vector4[] DiscColors =
    {
        new(0.93f, 0.93f, 0.97f, 1f),
        new(0.07f, 0.06f, 0.10f, 1f),
        new(0.84f, 0.86f, 0.90f, 1f),
        new(0.10f, 0.08f, 0.06f, 1f),
        new(1.00f, 0.79f, 0.29f, 1f),
        new(0.55f, 0.36f, 0.90f, 1f),
        new(0.26f, 0.55f, 0.95f, 1f),
        new(0.15f, 0.68f, 0.50f, 1f),
        new(0.93f, 0.30f, 0.52f, 1f),
        new(0.45f, 0.50f, 0.58f, 1f),
    };

    private static readonly Vector4[] NotchColors =
    {
        new(1.00f, 0.24f, 0.60f, 1f),
        new(0.62f, 0.45f, 1.00f, 1f),
        new(0.36f, 0.62f, 0.95f, 1f),
        new(1.00f, 0.79f, 0.29f, 1f),
        new(1.00f, 0.97f, 0.88f, 1f),
        new(0.97f, 0.95f, 0.98f, 1f),
        new(0.97f, 0.97f, 1.00f, 1f),
        new(0.95f, 1.00f, 0.97f, 1f),
        new(1.00f, 0.95f, 0.97f, 1f),
        new(0.92f, 0.94f, 0.97f, 1f),
    };

    private static readonly Vector4[] PrismNotches =
    {
        new(1.00f, 0.24f, 0.60f, 1f),
        new(1.00f, 0.79f, 0.29f, 1f),
        new(0.18f, 0.90f, 1.00f, 1f),
        new(0.55f, 0.36f, 0.90f, 1f),
        new(0.15f, 0.80f, 0.50f, 1f),
        new(1.00f, 0.55f, 0.25f, 1f),
    };

    private static readonly Vector4 PracticeBody = new(0.604f, 0.627f, 0.651f, 1f);
    private static readonly Vector4 PracticeDots = new(0.92f, 0.93f, 0.95f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.30f);
    private static readonly Vector4 Rim = new(0f, 0f, 0f, 0.30f);

    public static Vector4 ColorFor(long denomination) => DiscColors[IndexOf(denomination)];

    public static Vector4 NotchColorFor(long denomination) => NotchColors[IndexOf(denomination)];

    public static int Breakdown(long amount, Span<int> counts)
    {
        counts.Clear();
        if (amount <= 0)
        {
            return 0;
        }

        var used = 0;
        var remaining = amount;
        for (var index = 0; index < Denominations.Length && used < MaxColumns; index++)
        {
            var denomination = Denominations[index];
            if (remaining < denomination)
            {
                continue;
            }

            var count = remaining / denomination;
            remaining -= count * denomination;
            counts[index] = count > MaxDiscsPerColumn ? MaxDiscsPerColumn : (int)count;
            used++;
        }

        if (used == 0)
        {
            counts[Denominations.Length - 1] = 1;
            used = 1;
        }

        return used;
    }

    public static float HeightFor(float scale)
    {
        return (DiscRadius * 2f + DiscStep * (MaxDiscsPerColumn - 1) + LabelGap + 14f) * scale;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 baseCenter, long amount, float scale, Vector4 ink) =>
        Draw(drawList, baseCenter, amount, scale, ink, false);

    public static void Draw(ImDrawListPtr drawList, Vector2 baseCenter, long amount, float scale, Vector4 ink,
        bool practice)
    {
        if (amount <= 0)
        {
            return;
        }

        Span<int> counts = stackalloc int[DenominationCount];
        var columns = Breakdown(amount, counts);
        var radius = DiscRadius * scale;
        var step = DiscStep * scale;
        var columnWidth = radius * 2f + ColumnGap * scale;
        var left = baseCenter.X - (columns - 1) * columnWidth * 0.5f;
        var column = 0;
        for (var index = 0; index < DenominationCount; index++)
        {
            if (counts[index] == 0)
            {
                continue;
            }

            var centerX = left + column * columnWidth;
            for (var disc = 0; disc < counts[index]; disc++)
            {
                var center = new Vector2(centerX, baseCenter.Y - disc * step);
                if (practice)
                {
                    DrawPracticeDisc(drawList, center, radius);
                }
                else
                {
                    DrawDisc(drawList, center, radius, index, disc);
                }
            }

            column++;
        }

        var label = NumberText.Compact(amount);
        Typography.DrawCentered(drawList,
            new Vector2(baseCenter.X, baseCenter.Y + radius + LabelGap * scale + 6f * scale), label, ink,
            TextStyles.Caption2);
    }

    public static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, long denomination,
        float alpha = 1f)
    {
        DrawDisc(drawList, center, radius, IndexOf(denomination), 0, alpha);
    }

    private static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, int index, int layer,
        float alpha = 1f)
    {
        var body = DiscColors[index];
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius,
            ImGui.GetColorU32(Palette.WithAlpha(Shadow, alpha)), Segments);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(body, alpha)), Segments);
        var prism = index == 0;
        for (var notch = 0; notch < NotchCount; notch++)
        {
            var angle = MathF.PI * 2f * notch / NotchCount + layer * 0.35f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var tint = prism ? PrismNotches[(notch + layer) % PrismNotches.Length] : NotchColors[index];
            drawList.AddLine(center + direction * radius * 0.70f, center + direction * radius * 0.96f,
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.95f * alpha)), radius * 0.28f);
        }

        drawList.AddCircle(center, radius * 0.56f,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(body, 0.35f), 0.9f * alpha)), Segments,
            radius * 0.14f);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(Rim, alpha)), Segments,
            radius * 0.10f);
    }

    private static void DrawPracticeDisc(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius, ImGui.GetColorU32(Shadow),
            Segments);
        DrawPractice(drawList, center, radius);
    }

    public static void DrawPractice(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(PracticeBody), Segments);
        var dot = ImGui.GetColorU32(PracticeDots);
        for (var dotIndex = 0; dotIndex < DotCount; dotIndex++)
        {
            var angle = MathF.PI * 2f * dotIndex / DotCount;
            drawList.AddCircleFilled(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.84f,
                radius * 0.08f, dot, 6);
        }

        drawList.AddCircle(center, radius * 0.56f, dot, Segments, radius * 0.10f);
    }

    private static int IndexOf(long denomination)
    {
        for (var index = 0; index < Denominations.Length; index++)
        {
            if (Denominations[index] == denomination)
            {
                return index;
            }
        }

        return DenominationCount - 1;
    }
}
