using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Race;

internal readonly struct RaceStatusLayout
{
    public const float MaxHeight = 40f;
    public const float CapsuleHeight = 32f;
    public const float PadX = 14f;
    public const float DotRadius = 4f;
    public const float Gap = 8f;

    public readonly Rect Area;
    public readonly Rect Capsule;
    public readonly Vector2 Dot;
    public readonly Rect Label;
    public readonly Rect Clock;

    private RaceStatusLayout(Rect area, Rect capsule, Vector2 dot, Rect label, Rect clock)
    {
        Area = area;
        Capsule = capsule;
        Dot = dot;
        Label = label;
        Clock = clock;
    }

    public bool Visible => Capsule.Width > 0f && Capsule.Height > 0f;

    public static RaceStatusLayout Compute(in CasinoStageLayout stage, float clockWidth, float scale)
    {
        var top = stage.Ribbon.Min.Y;
        var height = MathF.Min(stage.Ribbon.Height, MaxHeight * scale);
        var area = new Rect(new Vector2(stage.Safe.Min.X, top), new Vector2(stage.Safe.Max.X, top + height));
        var capsuleHeight = MathF.Min(CapsuleHeight * scale, height);
        var capsuleTop = area.Center.Y - capsuleHeight * 0.5f;
        var capsule = new Rect(new Vector2(area.Min.X, capsuleTop),
            new Vector2(area.Max.X, capsuleTop + capsuleHeight));
        var pad = PadX * scale;
        var gap = Gap * scale;
        var dotRadius = DotRadius * scale;
        var dot = new Vector2(capsule.Min.X + pad + dotRadius, capsule.Center.Y);
        var labelLeft = MathF.Min(capsule.Max.X, dot.X + dotRadius + gap);
        var clockRight = MathF.Max(labelLeft, capsule.Max.X - pad);
        var clockLeft = MathF.Max(labelLeft, clockRight - MathF.Max(0f, clockWidth));
        var clock = new Rect(new Vector2(clockLeft, capsule.Min.Y), new Vector2(clockRight, capsule.Max.Y));
        var labelRight = MathF.Max(labelLeft, clockWidth > 0f ? clockLeft - gap : clockRight);
        var label = new Rect(new Vector2(labelLeft, capsule.Min.Y), new Vector2(labelRight, capsule.Max.Y));
        return new RaceStatusLayout(area, capsule, dot, label, clock);
    }
}
