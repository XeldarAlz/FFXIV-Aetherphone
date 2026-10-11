using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Machines;

internal readonly struct MachineStageLayout
{
    public const float ChassisInset = 6f;
    public const float ChassisPad = 12f;
    public const float GlassShare = 0.24f;
    public const float GlassMin = 104f;
    public const float GlassMax = 140f;
    public const float GlassMaxOfRoom = 0.3f;
    public const float StripHeight = 72f;
    public const float StripMaxOfRoom = 0.25f;

    public readonly Rect Chassis;
    public readonly Rect Glass;
    public readonly Rect Window;
    public readonly Rect Strip;

    private MachineStageLayout(Rect chassis, Rect glass, Rect window, Rect strip)
    {
        Chassis = chassis;
        Glass = glass;
        Window = window;
        Strip = strip;
    }

    public static MachineStageLayout Compute(Rect full, Rect safe, float scale)
    {
        var pad = ChassisPad * scale;
        var left = full.Min.X + (ChassisInset + ChassisPad) * scale;
        var right = MathF.Max(left, full.Max.X - (ChassisInset + ChassisPad) * scale);
        var top = safe.Min.Y + pad;
        var bottom = MathF.Max(top, safe.Max.Y - pad * 0.5f);
        var gapAbove = pad * 0.75f;
        var gapBelow = pad * 0.5f;
        var room = MathF.Max(0f, bottom - top - gapAbove - gapBelow);
        var stripHeight = MathF.Min(StripHeight * scale, room * StripMaxOfRoom);
        var glassHeight = Math.Clamp(safe.Height * GlassShare, GlassMin * scale, GlassMax * scale);
        glassHeight = MathF.Min(glassHeight, room * GlassMaxOfRoom);
        var glass = new Rect(new Vector2(left, top), new Vector2(right, top + glassHeight));
        var strip = new Rect(new Vector2(left, bottom - stripHeight), new Vector2(right, bottom));
        var window = new Rect(new Vector2(left, glass.Max.Y + gapAbove),
            new Vector2(right, MathF.Max(glass.Max.Y + gapAbove, strip.Min.Y - gapBelow)));
        var chassis = new Rect(new Vector2(full.Min.X + ChassisInset * scale, glass.Min.Y - pad),
            new Vector2(MathF.Max(full.Min.X, full.Max.X - ChassisInset * scale), strip.Max.Y + pad * 0.5f));
        return new MachineStageLayout(chassis, glass, window, strip);
    }
}
