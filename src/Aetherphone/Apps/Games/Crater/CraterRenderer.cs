using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Crater;

internal static class CraterRenderer
{
    public const float HintShare = 0.25f;
    private const float WaveAmplitude = 0.08f;
    private const float BackWaveLift = 0.12f;
    private const float StripPixels = 3f;
    private const float ShakeMargin = 14f;
    private const int MaxFoam = 28;
    private const float LabelLift = 2f;
    private const float BarWidth = 44f;
    private const float BarHeight = 5f;
    private const float NameGap = 9f;
    private const float MarkerSize = 7f;
    private const float ReticleDistance = 2.2f;
    private const int PowerDots = 9;
    private static readonly Vector4 WaterTop = new(0.22f, 0.55f, 0.80f, 0.80f);
    private static readonly Vector4 WaterDeep = new(0.05f, 0.17f, 0.36f, 0.95f);
    private static readonly Vector4 WaterBack = new(0.45f, 0.74f, 0.94f, 0.32f);
    private static readonly Vector4 Crest = new(0.86f, 0.96f, 1f, 0.9f);
    private static readonly Vector4 Foam = new(1f, 1f, 1f, 0.75f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 BarBack = new(0.05f, 0.05f, 0.08f, 0.62f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ember = new(1f, 0.55f, 0.18f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 PowerLow = new(1f, 0.86f, 0.32f, 1f);
    private static readonly Vector4 PowerHigh = new(1f, 0.32f, 0.22f, 1f);

    public static float Wave(float x, float time) =>
        MathF.Sin(x * 2.3f + time * 1.7f) * 0.05f + MathF.Sin(x * 5.1f - time * 2.6f) * 0.022f;

    public static void Water(ImDrawListPtr drawList, in Camera2D camera, TerrainMask terrain, float level, float time,
        float scale)
    {
        var view = camera.View;
        var surfaceTop = camera.ToScreen(new Vector2(0f, level - BackWaveLift - WaveAmplitude)).Y;
        if (surfaceTop > view.Max.Y + ShakeMargin * scale)
        {
            return;
        }

        var floor = camera.ToScreen(new Vector2(0f, level + WaveAmplitude)).Y;
        var left = view.Min.X - ShakeMargin * scale;
        var right = view.Max.X + ShakeMargin * scale;
        var bottom = view.Max.Y + ShakeMargin * scale;
        Band(drawList, camera, left, right, level - BackWaveLift, time * 0.8f + 1.3f, floor, WaterBack, scale);
        Band(drawList, camera, left, right, level, time, floor, WaterTop, scale);
        if (bottom > floor)
        {
            drawList.AddRectFilledMultiColor(new Vector2(left, floor), new Vector2(right, bottom),
                CraterArt.Color(WaterTop), CraterArt.Color(WaterTop), CraterArt.Color(WaterDeep),
                CraterArt.Color(WaterDeep));
        }

        Crests(drawList, camera, left, right, level, time, scale);
        Shore(drawList, camera, terrain, level, time);
    }

    public static void Embers(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, float radius, float fade)
    {
        if (fade <= 0f)
        {
            return;
        }

        var screen = camera.ToScreen(center);
        var pixels = camera.Px(radius);
        drawList.AddCircleFilled(screen, pixels * 1.1f, CraterArt.Color(Ember with { W = 0.22f * fade }),
            CraterArt.Segments(pixels));
        drawList.AddCircleFilled(screen, pixels * 0.55f, CraterArt.Color(CraterArt.Fire with { W = 0.3f * fade }),
            CraterArt.Segments(pixels));
    }

    public static void Bodies(ImDrawListPtr drawList, in Camera2D camera, CraterBoard board, ReadOnlySpan<float> hurt,
        bool showLauncher, float time) =>
        Bodies(drawList, in camera, board.Moogles, board.ActiveMoogle, board.ActiveAim, hurt, showLauncher, time);

    public static void Bodies(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<CraterMoogle> moogles,
        int activeMoogle, float activeAim, ReadOnlySpan<float> hurt, bool showLauncher, float time)
    {
        var radius = camera.Px(CraterRules.MoogleRadius);
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (moogle.Sunk)
            {
                continue;
            }

            var center = camera.ToScreen(moogle.Position);
            var team = GameSeats.Color(moogle.Team);
            if (!moogle.Alive)
            {
                CraterArt.Tombstone(drawList, center, radius, team);
                continue;
            }

            var flap = moogle.Grounded ? 0.15f * MathF.Sin(time * 2f + index) : MathF.Sin(time * 18f + index);
            var sway = MathF.Sin(time * 2.4f + index * 1.3f);
            CraterArt.Moogle(drawList, center, radius, moogle.Facing, team, flap, index < hurt.Length ? hurt[index] : 0f,
                sway);
            if (showLauncher && index == activeMoogle)
            {
                var direction = CraterRules.AimDirection(activeAim, moogle.Facing);
                CraterArt.Launcher(drawList, center, radius, direction, team);
            }

            if (moogle.Shielded)
            {
                CraterArt.Bubble(drawList, center, radius, MathF.Sin(time * 4f + index));
            }
        }
    }

    public static void Projectiles(ImDrawListPtr drawList, in Camera2D camera, CraterBoard board, float time,
        float scale) =>
        Projectiles(drawList, in camera, board.Projectiles, time, scale);

    public static void Projectiles(ImDrawListPtr drawList, in Camera2D camera,
        ReadOnlySpan<CraterProjectile> projectiles, float time, float scale)
    {
        for (var index = 0; index < projectiles.Length; index++)
        {
            ref readonly var projectile = ref projectiles[index];
            if (!projectile.Alive)
            {
                continue;
            }

            var center = camera.ToScreen(projectile.Position);
            var radius = MathF.Max(3f * scale, camera.Px(CraterRules.ProjectileRadius(projectile.Kind)) * 1.6f);
            var heading = projectile.Velocity.LengthSquared() > 0.0001f
                ? Vector2.Normalize(projectile.Velocity)
                : new Vector2(1f, 0f);
            CraterArt.Projectile(drawList, center, radius, projectile.Kind, heading, projectile.Fuse, time);
            if (projectile.Kind != ProjectileKind.Grenade)
            {
                continue;
            }

            var seconds = GameNumber.Label(Math.Max(1, (int)MathF.Ceiling(projectile.Fuse)));
            var labelCenter = center - new Vector2(0f, radius * 2.6f);
            Typography.DrawCentered(drawList, labelCenter + new Vector2(1f, 1f) * scale, seconds, Shadow,
                TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, labelCenter, seconds, White, TextStyles.FootnoteEmphasized);
        }
    }

    public static void Labels(ImDrawListPtr drawList, in Camera2D camera, CraterBoard board, CraterLabels labels,
        bool showMarker, float time, float scale) =>
        Labels(drawList, in camera, board.Moogles, board.ActiveMoogle, labels.TeamNames, showMarker, time, scale);

    public static void Labels(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<CraterMoogle> moogles,
        int activeMoogle, ReadOnlySpan<string> teamNames, bool showMarker, float time, float scale)
    {
        for (var index = 0; index < moogles.Length; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (!moogle.Alive)
            {
                continue;
            }

            var head = camera.ToScreen(moogle.Position - new Vector2(0f, CraterRules.MoogleRadius * LabelLift));
            var team = GameSeats.Color(moogle.Team);
            var barWidth = BarWidth * scale;
            var barHeight = BarHeight * scale;
            var barMin = new Vector2(head.X - barWidth * 0.5f, head.Y - barHeight);
            var barMax = new Vector2(head.X + barWidth * 0.5f, head.Y);
            drawList.AddRectFilled(barMin - Vector2.One * scale, barMax + Vector2.One * scale,
                CraterArt.Color(BarBack), barHeight);
            var fraction = Math.Clamp(moogle.Health / (float)CraterRules.MaxHealth, 0f, 1f);
            if (fraction > 0f)
            {
                var fill = moogle.Health <= 25 ? Vector4.Lerp(team, Danger, 0.5f + 0.5f * MathF.Sin(time * 8f)) : team;
                drawList.AddRectFilled(barMin, new Vector2(barMin.X + barWidth * fraction, barMax.Y),
                    CraterArt.Color(fill), barHeight);
            }

            var health = GameNumber.Label(moogle.Health);
            var healthCenter = new Vector2(barMax.X + 4f * scale + Typography.Measure(health, TextStyles.Caption2).X * 0.5f,
                head.Y - barHeight * 0.5f);
            Shadowed(drawList, healthCenter, health, White, TextStyles.Caption2, scale);
            var nameCenter = new Vector2(head.X, head.Y - barHeight - NameGap * scale);
            var name = moogle.Team >= 0 && moogle.Team < teamNames.Length ? teamNames[moogle.Team] : string.Empty;
            Shadowed(drawList, nameCenter, name, GamePalette.Lighten(team, 0.35f), TextStyles.Caption1, scale);
            if (!showMarker || index != activeMoogle)
            {
                continue;
            }

            var bob = MathF.Abs(MathF.Sin(time * 4f)) * 5f * scale;
            var tip = new Vector2(head.X, nameCenter.Y - 9f * scale - bob);
            var size = MarkerSize * scale;
            drawList.AddTriangleFilled(tip - new Vector2(size, size * 1.3f), tip + new Vector2(size, -size * 1.3f), tip,
                CraterArt.Color(team));
            drawList.AddTriangle(tip - new Vector2(size, size * 1.3f), tip + new Vector2(size, -size * 1.3f), tip,
                CraterArt.Color(White with { W = 0.8f }), MathF.Max(1f, scale));
        }
    }

    public static void Hint(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> path, Vector4 team,
        float scale)
    {
        var count = path.Length;
        for (var index = 1; index < count; index++)
        {
            var fade = 1f - index / (float)count;
            var point = camera.ToScreen(path[index]);
            var radius = (1.6f + 1.4f * fade) * scale;
            drawList.AddCircleFilled(point, radius * 2f, CraterArt.Color(team with { W = 0.25f * fade }), 10);
            drawList.AddCircleFilled(point, radius, CraterArt.Color(White with { W = 0.35f + 0.6f * fade }), 10);
        }
    }

    public static void Reticle(ImDrawListPtr drawList, in Camera2D camera, Vector2 origin, Vector2 direction,
        Vector4 team, float time, float scale)
    {
        var center = camera.ToScreen(origin + direction * ReticleDistance);
        var radius = (7f + MathF.Sin(time * 5f)) * scale;
        var thickness = MathF.Max(1f, 1.6f * scale);
        drawList.AddCircle(center, radius, CraterArt.Color(team with { W = 0.9f }), 20, thickness);
        drawList.AddCircleFilled(center, 1.8f * scale, CraterArt.Color(White), 8);
        var arm = radius * 0.55f;
        drawList.AddLine(center - new Vector2(radius + arm, 0f), center - new Vector2(radius - 2f * scale, 0f),
            CraterArt.Color(White with { W = 0.8f }), thickness);
        drawList.AddLine(center + new Vector2(radius - 2f * scale, 0f), center + new Vector2(radius + arm, 0f),
            CraterArt.Color(White with { W = 0.8f }), thickness);
    }

    public static void Power(ImDrawListPtr drawList, in Camera2D camera, Vector2 origin, Vector2 direction,
        float charge, float scale)
    {
        var start = origin + direction * CraterRules.MuzzleDistance * 1.3f;
        for (var dot = 0; dot < PowerDots; dot++)
        {
            var share = (dot + 1) / (float)PowerDots;
            var point = camera.ToScreen(start + direction * (share * 1.6f));
            var lit = charge >= share - 0.5f / PowerDots;
            var radius = (2f + share * 4.5f) * scale;
            var color = Vector4.Lerp(PowerLow, PowerHigh, share);
            if (!lit)
            {
                drawList.AddCircle(point, radius, CraterArt.Color(White with { W = 0.35f }), 12, MathF.Max(1f, scale));
                continue;
            }

            drawList.AddCircleFilled(point, radius * 1.8f, CraterArt.Color(color with { W = 0.25f }), 12);
            drawList.AddCircleFilled(point, radius, CraterArt.Color(color), 12);
        }
    }

    public static void Ghost(ImDrawListPtr drawList, in Camera2D camera, Vector2 spot, bool valid, Vector4 team,
        float time, float scale)
    {
        var center = camera.ToScreen(spot);
        var radius = camera.Px(CraterRules.MoogleRadius);
        var tint = valid ? team : Danger;
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 6f);
        drawList.AddCircleFilled(center, radius, CraterArt.Color(tint with { W = 0.18f + 0.12f * pulse }),
            CraterArt.Segments(radius));
        drawList.AddCircle(center, radius * (1f + 0.15f * pulse), CraterArt.Color(tint with { W = 0.85f }),
            CraterArt.Segments(radius), MathF.Max(1f, 2f * scale));
        if (valid)
        {
            return;
        }

        var arm = radius * 0.45f;
        drawList.AddLine(center - new Vector2(arm, arm), center + new Vector2(arm, arm), CraterArt.Color(tint),
            MathF.Max(1f, 2f * scale));
        drawList.AddLine(center + new Vector2(-arm, arm), center + new Vector2(arm, -arm), CraterArt.Color(tint),
            MathF.Max(1f, 2f * scale));
    }

    private static void Band(ImDrawListPtr drawList, in Camera2D camera, float left, float right, float level,
        float phase, float floor, Vector4 color, float scale)
    {
        var packed = CraterArt.Color(color);
        var stride = StripPixels * scale;
        for (var x = left; x < right; x += stride)
        {
            var world = camera.ToWorld(new Vector2(x + stride * 0.5f, floor)).X;
            var top = camera.ToScreen(new Vector2(world, level + Wave(world, phase))).Y;
            if (top >= floor)
            {
                continue;
            }

            drawList.AddRectFilled(new Vector2(x, top), new Vector2(MathF.Min(right, x + stride), floor), packed);
        }
    }

    private static void Crests(ImDrawListPtr drawList, in Camera2D camera, float left, float right, float level,
        float time, float scale)
    {
        var packed = CraterArt.Color(Crest);
        var stride = StripPixels * 4f * scale;
        var thickness = MathF.Max(1f, 2f * scale);
        var previousWorld = camera.ToWorld(new Vector2(left, 0f)).X;
        var previous = camera.ToScreen(new Vector2(previousWorld, level + Wave(previousWorld, time)));
        for (var x = left + stride; x <= right + stride; x += stride)
        {
            var world = camera.ToWorld(new Vector2(x, 0f)).X;
            var current = camera.ToScreen(new Vector2(world, level + Wave(world, time)));
            drawList.AddLine(previous, current, packed, thickness);
            previous = current;
        }
    }

    private static void Shore(ImDrawListPtr drawList, in Camera2D camera, TerrainMask terrain, float level, float time)
    {
        var row = terrain.RowOf(level - 0.01f);
        if (row < 0 || row >= terrain.Height)
        {
            return;
        }

        var visible = camera.VisibleWorld;
        var first = Math.Max(0, terrain.ColumnOf(visible.Min.X));
        var last = Math.Min(terrain.Width - 1, terrain.ColumnOf(visible.Max.X));
        if (first >= last)
        {
            return;
        }

        var wasSolid = terrain.IsSolid(first, row);
        var drawn = 0;
        for (var column = first + 1; column <= last && drawn < MaxFoam; column++)
        {
            var solid = terrain.IsSolid(column, row);
            if (solid == wasSolid)
            {
                continue;
            }

            var x = column * terrain.MetresPerCell;
            var outward = solid ? -1f : 1f;
            wasSolid = solid;
            drawn++;
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 3f + x * 7f);
            var center = camera.ToScreen(new Vector2(x + outward * 0.06f, level + Wave(x, time)));
            drawList.AddCircleFilled(center, camera.Px(0.11f + 0.05f * pulse),
                CraterArt.Color(Foam with { W = 0.45f + 0.3f * pulse }), 12);
            var spray = camera.ToScreen(new Vector2(x + outward * (0.26f + 0.08f * pulse), level + Wave(x, time) + 0.02f));
            drawList.AddCircleFilled(spray, camera.Px(0.06f), CraterArt.Color(Foam with { W = 0.5f * pulse }), 8);
        }
    }

    private static void Shadowed(ImDrawListPtr drawList, Vector2 center, string text, Vector4 color, in TextStyle style,
        float scale)
    {
        Typography.DrawCentered(drawList, center + new Vector2(1f, 1f) * scale, text, Shadow, style);
        Typography.DrawCentered(drawList, center, text, color, style);
    }
}
