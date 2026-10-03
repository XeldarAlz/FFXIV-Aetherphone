using Aetherphone.Core;

namespace Aetherphone.Windows.Components;

internal static class TabBarLayout
{
    public const float Height = 50f;
    public const float SideInset = 12f;
    public const float BottomInset = 10f;
    public const float ContentGap = 10f;
    public const float IconSize = 24f;
    public const float AvatarRadius = 12f;
    public const float AvatarRingGap = 2f;
    public const float ActionDiameter = 44f;
    public const float ActionGap = 8f;
    public const float CapsulePadding = 4f;
    public const float HighlightInset = 4f;

    public static float ContentInset(float scale) => (Height + BottomInset + ContentGap) * scale;

    public static Rect ContentArea(Rect area, float scale) =>
        new(area.Min, new Vector2(area.Max.X, MathF.Max(area.Min.Y, area.Max.Y - ContentInset(scale))));

    public static Rect Zone(Rect area, float scale) =>
        new(new Vector2(area.Min.X, MathF.Max(area.Min.Y, area.Max.Y - (Height + BottomInset) * scale)), area.Max);

    public static Rect FullCapsule(Rect area, float scale, bool hasAction)
    {
        var bottom = area.Max.Y - BottomInset * scale;
        var left = area.Min.X + SideInset * scale;
        var right = area.Max.X - SideInset * scale;
        if (hasAction)
        {
            right -= (ActionDiameter + ActionGap) * scale;
        }

        return new Rect(new Vector2(left, bottom - Height * scale), new Vector2(MathF.Max(left, right), bottom));
    }

    public static Rect ActionCircle(Rect area, float scale)
    {
        var diameter = ActionDiameter * scale;
        var right = area.Max.X - SideInset * scale;
        var centerY = area.Max.Y - BottomInset * scale - Height * scale * 0.5f;
        return new Rect(new Vector2(right - diameter, centerY - diameter * 0.5f),
            new Vector2(right, centerY + diameter * 0.5f));
    }

    public static Rect Cell(Rect capsule, int count, int index, float scale)
    {
        var padding = CapsulePadding * scale;
        var inner = MathF.Max(0f, capsule.Width - padding * 2f);
        var width = inner / Math.Max(1, count);
        var left = capsule.Min.X + padding + width * index;
        return new Rect(new Vector2(left, capsule.Min.Y), new Vector2(left + width, capsule.Max.Y));
    }

    public static Vector2 IconCenter(Rect cell) => cell.Center;

    public static Rect Highlight(Rect cell, float scale)
    {
        var inset = HighlightInset * scale;
        return new Rect(new Vector2(cell.Min.X + inset, cell.Min.Y + inset),
            new Vector2(MathF.Max(cell.Min.X + inset, cell.Max.X - inset), cell.Max.Y - inset));
    }
}
