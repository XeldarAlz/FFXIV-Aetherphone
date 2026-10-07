using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterArt
{
    public static readonly Vector4 Fur = new(0.98f, 0.97f, 0.95f, 1f);
    public static readonly Vector4 Pom = new(0.96f, 0.30f, 0.38f, 1f);
    public static readonly Vector4 ShieldTint = new(0.45f, 0.85f, 1f, 1f);
    public static readonly Vector4 Fire = new(1f, 0.62f, 0.22f, 1f);
    private static readonly Vector4 FurShade = new(0.80f, 0.78f, 0.86f, 1f);
    private static readonly Vector4 Outline = new(0.34f, 0.30f, 0.40f, 0.75f);
    private static readonly Vector4 Wing = new(0.56f, 0.38f, 0.74f, 1f);
    private static readonly Vector4 WingInner = new(0.74f, 0.58f, 0.88f, 1f);
    private static readonly Vector4 Stalk = new(0.30f, 0.26f, 0.34f, 1f);
    private static readonly Vector4 Nose = new(0.90f, 0.36f, 0.44f, 1f);
    private static readonly Vector4 Eye = new(0.18f, 0.13f, 0.20f, 1f);
    private static readonly Vector4 Blush = new(1f, 0.55f, 0.62f, 0.45f);
    private static readonly Vector4 Hurt = new(1f, 0.25f, 0.2f, 1f);
    private static readonly Vector4 Barrel = new(0.27f, 0.28f, 0.33f, 1f);
    private static readonly Vector4 BarrelLight = new(0.50f, 0.52f, 0.60f, 1f);
    private static readonly Vector4 Muzzle = new(0.12f, 0.12f, 0.15f, 1f);
    private static readonly Vector4 Stone = new(0.66f, 0.66f, 0.71f, 1f);
    private static readonly Vector4 StoneDark = new(0.40f, 0.40f, 0.46f, 1f);
    private static readonly Vector4 ShellBody = new(0.25f, 0.26f, 0.31f, 1f);
    private static readonly Vector4 GrenadeBody = new(0.33f, 0.47f, 0.27f, 1f);
    private static readonly Vector4 ClusterBody = new(0.62f, 0.34f, 0.22f, 1f);
    private static readonly Vector4 DrillBody = new(0.74f, 0.75f, 0.82f, 1f);
    private static readonly Vector4 Shine = new(1f, 1f, 1f, 0.55f);
    private static readonly Vector4 Spark = new(1f, 0.86f, 0.40f, 1f);

    public static uint Color(Vector4 color) => ImGui.GetColorU32(color);

    public static int Segments(float radius) => Math.Clamp((int)(radius * 0.8f), 12, 40);

    public static void Moogle(ImDrawListPtr drawList, Vector2 center, float radius, int facing, Vector4 team,
        float flap, float hurt, float sway)
    {
        var side = facing < 0 ? -1f : 1f;
        var segments = Segments(radius);
        DrawWing(drawList, center, radius, side, flap);
        DrawAntenna(drawList, center, radius, side, sway);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.05f), radius, Color(FurShade), segments);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.04f * side, radius * 0.05f), radius * 0.9f,
            Color(Fur), segments);
        drawList.AddCircle(center + new Vector2(0f, radius * 0.05f), radius, Color(Outline), segments,
            MathF.Max(1f, radius * 0.07f));
        DrawScarf(drawList, center, radius, side, team);
        DrawFace(drawList, center, radius, side);
        if (hurt > 0f)
        {
            drawList.AddCircleFilled(center, radius * 1.02f, Color(Hurt with { W = 0.55f * hurt }), segments);
        }
    }

    public static void Bubble(ImDrawListPtr drawList, Vector2 center, float radius, float pulse)
    {
        var bubble = radius * (1.42f + pulse * 0.05f);
        drawList.AddCircleFilled(center, bubble, Color(ShieldTint with { W = 0.13f }), Segments(bubble));
        drawList.AddCircle(center, bubble, Color(ShieldTint with { W = 0.75f }), Segments(bubble),
            MathF.Max(1f, radius * 0.08f));
        drawList.AddCircleFilled(center + new Vector2(-bubble * 0.4f, -bubble * 0.45f), bubble * 0.16f,
            Color(Shine), 12);
    }

    public static void Launcher(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 direction, Vector4 team)
    {
        var side = new Vector2(-direction.Y, direction.X) * radius * 0.2f;
        var start = center + direction * radius * 0.1f;
        var end = center + direction * radius * 1.35f;
        drawList.AddQuadFilled(start + side, end + side, end - side, start - side, Color(Barrel));
        drawList.AddLine(start + side * 0.45f, end + side * 0.45f, Color(BarrelLight), MathF.Max(1f, radius * 0.07f));
        var bandStart = center + direction * radius * 0.62f;
        var bandEnd = center + direction * radius * 0.8f;
        drawList.AddQuadFilled(bandStart + side * 1.1f, bandEnd + side * 1.1f, bandEnd - side * 1.1f,
            bandStart - side * 1.1f, Color(team));
        drawList.AddCircleFilled(end, radius * 0.25f, Color(Barrel), 16);
        drawList.AddCircleFilled(end, radius * 0.15f, Color(Muzzle), 12);
    }

    public static void Tombstone(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 team)
    {
        var halfWidth = radius * 0.55f;
        var shoulder = center.Y - radius * 0.35f;
        var foot = center.Y + radius;
        var segments = Segments(radius);
        drawList.AddCircleFilled(new Vector2(center.X, shoulder), halfWidth, Color(Stone), segments);
        drawList.AddRectFilled(new Vector2(center.X - halfWidth, shoulder), new Vector2(center.X + halfWidth, foot),
            Color(Stone));
        drawList.AddRectFilled(new Vector2(center.X - halfWidth * 1.25f, foot - radius * 0.16f),
            new Vector2(center.X + halfWidth * 1.25f, foot), Color(StoneDark), radius * 0.05f);
        var thickness = MathF.Max(1f, radius * 0.1f);
        drawList.AddLine(new Vector2(center.X, shoulder - radius * 0.25f), new Vector2(center.X, center.Y + radius * 0.4f),
            Color(StoneDark), thickness);
        drawList.AddLine(new Vector2(center.X - radius * 0.25f, shoulder), new Vector2(center.X + radius * 0.25f, shoulder),
            Color(StoneDark), thickness);
        drawList.AddCircleFilled(new Vector2(center.X, shoulder - halfWidth - radius * 0.1f), radius * 0.17f,
            Color(team), 12);
    }

    public static void Projectile(ImDrawListPtr drawList, Vector2 center, float radius, ProjectileKind kind,
        Vector2 heading, float fuse, float time)
    {
        switch (kind)
        {
            case ProjectileKind.Grenade:
                DrawGrenade(drawList, center, radius, fuse, time);
                return;
            case ProjectileKind.Cluster:
                DrawCluster(drawList, center, radius);
                return;
            case ProjectileKind.Bomblet:
                drawList.AddCircleFilled(center, radius * 1.8f, Color(Fire with { W = 0.25f }), 12);
                drawList.AddCircleFilled(center, radius, Color(ShellBody), 12);
                return;
            case ProjectileKind.Drill:
                DrawDrill(drawList, center, radius, heading, time);
                return;
            default:
                DrawShell(drawList, center, radius, heading);
                return;
        }
    }

    public static void WeaponIcon(ImDrawListPtr drawList, Vector2 center, float size, CraterWeapon weapon, Vector4 ink)
    {
        var color = Color(ink);
        switch (weapon)
        {
            case CraterWeapon.Grenade:
                drawList.AddCircleFilled(center + new Vector2(0f, size * 0.12f), size * 0.38f, color, 20);
                drawList.AddRectFilled(center + new Vector2(-size * 0.12f, -size * 0.38f),
                    center + new Vector2(size * 0.12f, -size * 0.2f), color, size * 0.03f);
                drawList.AddCircle(center + new Vector2(size * 0.24f, -size * 0.36f), size * 0.12f, color, 12,
                    MathF.Max(1f, size * 0.06f));
                return;
            case CraterWeapon.Cluster:
                drawList.AddCircleFilled(center + new Vector2(0f, -size * 0.2f), size * 0.2f, color, 14);
                drawList.AddCircleFilled(center + new Vector2(-size * 0.22f, size * 0.18f), size * 0.2f, color, 14);
                drawList.AddCircleFilled(center + new Vector2(size * 0.22f, size * 0.18f), size * 0.2f, color, 14);
                return;
            case CraterWeapon.Drill:
                drawList.AddRectFilled(center + new Vector2(-size * 0.3f, -size * 0.5f),
                    center + new Vector2(size * 0.3f, -size * 0.3f), color, size * 0.04f);
                drawList.AddTriangleFilled(center + new Vector2(-size * 0.3f, -size * 0.24f),
                    center + new Vector2(size * 0.3f, -size * 0.24f), center + new Vector2(0f, size * 0.55f), color);
                return;
            case CraterWeapon.Shield:
                drawList.PathClear();
                drawList.PathLineTo(center + new Vector2(-size * 0.4f, -size * 0.45f));
                drawList.PathLineTo(center + new Vector2(size * 0.4f, -size * 0.45f));
                drawList.PathLineTo(center + new Vector2(size * 0.4f, size * 0.05f));
                drawList.PathLineTo(center + new Vector2(0f, size * 0.55f));
                drawList.PathLineTo(center + new Vector2(-size * 0.4f, size * 0.05f));
                drawList.PathFillConvex(color);
                return;
            case CraterWeapon.Teleport:
                drawList.AddCircle(center, size * 0.36f, color, 20, MathF.Max(1f, size * 0.09f));
                drawList.AddCircleFilled(center, size * 0.1f, color, 10);
                for (var spark = 0; spark < 4; spark++)
                {
                    var angle = spark * MathF.PI * 0.5f + MathF.PI * 0.25f;
                    var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size * 0.55f;
                    drawList.AddCircleFilled(point, size * 0.07f, color, 8);
                }

                return;
            default:
                DrawShellIcon(drawList, center, size, color);
                return;
        }
    }

    private static void DrawShellIcon(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        var axis = Vector2.Normalize(new Vector2(1f, -1f));
        var across = new Vector2(-axis.Y, axis.X) * size * 0.2f;
        var tail = center - axis * size * 0.5f;
        var shoulder = center + axis * size * 0.15f;
        drawList.AddQuadFilled(tail + across, shoulder + across, shoulder - across, tail - across, color);
        drawList.AddTriangleFilled(shoulder + across, center + axis * size * 0.55f, shoulder - across, color);
        drawList.AddTriangleFilled(tail + across * 2f, tail + axis * size * 0.2f, tail + across * 0.6f, color);
        drawList.AddTriangleFilled(tail - across * 2f, tail - across * 0.6f, tail + axis * size * 0.2f, color);
    }

    private static void DrawWing(ImDrawListPtr drawList, Vector2 center, float radius, float side, float flap)
    {
        var root = center + new Vector2(-side * radius * 0.55f, -radius * 0.2f);
        var tip = root + new Vector2(-side * radius * 0.8f, -radius * (0.55f + flap * 0.35f));
        var edge = root + new Vector2(-side * radius * 1.0f, -radius * 0.05f);
        var low = root + new Vector2(-side * radius * 0.55f, radius * 0.3f);
        drawList.AddTriangleFilled(root, tip, edge, Color(Wing));
        drawList.AddTriangleFilled(root, edge, low, Color(Wing));
        var inner = root + (edge - root) * 0.55f;
        drawList.AddTriangleFilled(root, root + (tip - root) * 0.7f, inner, Color(WingInner));
    }

    private static void DrawAntenna(ImDrawListPtr drawList, Vector2 center, float radius, float side, float sway)
    {
        var root = center + new Vector2(-side * radius * 0.1f, -radius * 0.82f);
        var pom = center + new Vector2(-side * radius * (0.32f - sway * 0.25f), -radius * 1.5f);
        var bend = new Vector2(root.X - side * radius * 0.02f, (root.Y + pom.Y) * 0.5f);
        drawList.AddBezierQuadratic(root, bend, pom, Color(Stalk), MathF.Max(1f, radius * 0.08f), 8);
        drawList.AddCircleFilled(pom, radius * 0.25f, Color(Pom), 16);
        drawList.AddCircleFilled(pom - new Vector2(radius * 0.08f, radius * 0.08f), radius * 0.08f, Color(Shine), 8);
    }

    private static void DrawScarf(ImDrawListPtr drawList, Vector2 center, float radius, float side, Vector4 team)
    {
        var thickness = MathF.Max(1.5f, radius * 0.2f);
        drawList.PathClear();
        drawList.PathArcTo(center, radius * 0.8f, MathF.PI * 0.18f, MathF.PI * 0.82f, 14);
        drawList.PathStroke(Color(team), ImDrawFlags.None, thickness);
        var knot = center + new Vector2(-side * radius * 0.55f, radius * 0.55f);
        drawList.AddTriangleFilled(knot, knot + new Vector2(-side * radius * 0.35f, radius * 0.38f),
            knot + new Vector2(-side * radius * 0.05f, radius * 0.45f), Color(GamePalette.Darken(team, 0.18f)));
    }

    private static void DrawFace(ImDrawListPtr drawList, Vector2 center, float radius, float side)
    {
        var eyeY = center.Y - radius * 0.14f;
        var thickness = MathF.Max(1f, radius * 0.08f);
        for (var eye = 0; eye < 2; eye++)
        {
            var eyeX = center.X + side * radius * (eye == 0 ? 0.14f : 0.5f);
            drawList.PathClear();
            drawList.PathArcTo(new Vector2(eyeX, eyeY + radius * 0.06f), radius * 0.1f, MathF.PI * 1.15f,
                MathF.PI * 1.85f, 8);
            drawList.PathStroke(Color(Eye), ImDrawFlags.None, thickness);
        }

        drawList.AddCircleFilled(new Vector2(center.X + side * radius * 0.34f, center.Y + radius * 0.04f),
            radius * 0.12f, Color(Nose), 12);
        drawList.AddCircleFilled(new Vector2(center.X + side * radius * 0.62f, center.Y + radius * 0.1f),
            radius * 0.09f, Color(Blush), 10);
    }

    private static void DrawShell(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 heading)
    {
        var across = new Vector2(-heading.Y, heading.X) * radius * 0.75f;
        var tail = center - heading * radius * 1.4f;
        var shoulder = center + heading * radius * 0.5f;
        drawList.AddQuadFilled(tail + across, shoulder + across, shoulder - across, tail - across, Color(ShellBody));
        drawList.AddTriangleFilled(shoulder + across, center + heading * radius * 1.5f, shoulder - across,
            Color(ShellBody));
        drawList.AddTriangleFilled(tail + across * 1.8f, tail + heading * radius * 0.6f, tail, Color(Pom));
        drawList.AddTriangleFilled(tail - across * 1.8f, tail, tail + heading * radius * 0.6f, Color(Pom));
        drawList.AddLine(tail + across * 0.4f, shoulder + across * 0.4f, Color(Shine), MathF.Max(1f, radius * 0.25f));
    }

    private static void DrawGrenade(ImDrawListPtr drawList, Vector2 center, float radius, float fuse, float time)
    {
        drawList.AddCircleFilled(center, radius, Color(GrenadeBody), Segments(radius));
        drawList.AddCircle(center, radius, Color(GamePalette.Darken(GrenadeBody, 0.2f)), Segments(radius),
            MathF.Max(1f, radius * 0.12f));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.35f, radius * 0.35f), radius * 0.25f, Color(Shine), 8);
        var cap = center - new Vector2(0f, radius * 1.05f);
        drawList.AddRectFilled(cap - new Vector2(radius * 0.3f, radius * 0.2f), cap + new Vector2(radius * 0.3f, radius * 0.2f),
            Color(ShellBody));
        var urgency = fuse < 1f ? 22f : 10f;
        var flicker = 0.6f + 0.4f * MathF.Sin(time * urgency);
        var spark = cap - new Vector2(0f, radius * 0.35f);
        drawList.AddCircleFilled(spark, radius * 0.7f * flicker, Color(Spark with { W = 0.3f }), 10);
        drawList.AddCircleFilled(spark, radius * 0.28f, Color(Spark), 8);
    }

    private static void DrawCluster(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        drawList.AddCircleFilled(center, radius * 1.15f, Color(ClusterBody), Segments(radius));
        var dot = radius * 0.3f;
        drawList.AddCircleFilled(center + new Vector2(0f, -radius * 0.4f), dot, Color(ShellBody), 8);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.4f, radius * 0.3f), dot, Color(ShellBody), 8);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.4f, radius * 0.3f), dot, Color(ShellBody), 8);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.45f, radius * 0.6f), radius * 0.2f, Color(Shine), 8);
    }

    private static void DrawDrill(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 heading, float time)
    {
        var across = new Vector2(-heading.Y, heading.X) * radius;
        var tip = center + heading * radius * 2f;
        var back = center - heading * radius;
        drawList.AddTriangleFilled(back + across, tip, back - across, Color(DrillBody));
        var turn = time * 18f % 1f;
        for (var thread = 0; thread < 3; thread++)
        {
            var along = (thread + turn) / 3f;
            var point = back + (tip - back) * along;
            var width = 1f - along;
            drawList.AddLine(point + across * width, point - across * width * 0.4f + heading * radius * 0.3f,
                Color(ShellBody), MathF.Max(1f, radius * 0.15f));
        }

        drawList.AddRectFilled(back - across * 0.8f - heading * radius * 0.5f, back + across * 0.8f,
            Color(ShellBody), radius * 0.1f);
    }
}
