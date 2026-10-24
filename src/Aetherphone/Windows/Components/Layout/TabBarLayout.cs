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

    public static float ContentInset(float scale, float height = Height) => (height + BottomInset + ContentGap) * scale;

    public static Rect ContentArea(Rect area, float scale, float height = Height) =>
        new(area.Min, new Vector2(area.Max.X, MathF.Max(area.Min.Y, area.Max.Y - ContentInset(scale, height))));

    public static Rect Zone(Rect area, float scale, float height = Height) =>
        new(new Vector2(area.Min.X, MathF.Max(area.Min.Y, area.Max.Y - (height + BottomInset) * scale)), area.Max);

    public static Rect FullCapsule(Rect area, float scale, bool hasAction, float height = Height,
        float side = SideInset)
    {
        var bottom = area.Max.Y - BottomInset * scale;
        var left = area.Min.X + side * scale;
        var right = area.Max.X - side * scale;
        if (hasAction)
        {
            right -= (ActionDiameter + ActionGap) * scale;
        }

        return new Rect(new Vector2(left, bottom - height * scale), new Vector2(MathF.Max(left, right), bottom));
    }

    public static Rect ActionCircle(Rect area, float scale, float height = Height, float side = SideInset)
    {
        var diameter = ActionDiameter * scale;
        var right = area.Max.X - side * scale;
        var centerY = area.Max.Y - BottomInset * scale - height * scale * 0.5f;
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

    public static Rect Highlight(Rect capsule, int count, int index, float scale)
    {
        var cell = Cell(capsule, count, index, scale);
        var inset = HighlightInset * scale;
        var left = index == 0 ? capsule.Min.X + inset : cell.Min.X + inset;
        var right = index == count - 1 ? capsule.Max.X - inset : cell.Max.X - inset;
        return new Rect(new Vector2(left, cell.Min.Y + inset), new Vector2(MathF.Max(left, right), cell.Max.Y - inset));
    }

    public static Rect Highlight(Rect capsule, int count, float slot, float scale)
    {
        var last = Math.Max(0, count - 1);
        var clamped = Math.Clamp(slot, 0f, last);
        var lower = Math.Min((int)clamped, last);
        var from = Highlight(capsule, count, lower, scale);
        var to = Highlight(capsule, count, Math.Min(lower + 1, last), scale);
        var amount = clamped - lower;
        return new Rect(Vector2.Lerp(from.Min, to.Min, amount), Vector2.Lerp(from.Max, to.Max, amount));
    }
}
