using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Broadside;

internal static class BroadsideArt
{
    public static readonly Vector4 SkyTop = new(0.22f, 0.46f, 0.76f, 1f);
    public static readonly Vector4 SkyBottom = new(0.10f, 0.22f, 0.46f, 1f);
    public static readonly Vector4 Hull = new(0.74f, 0.78f, 0.86f, 1f);
    public static readonly Vector4 EnemyHull = new(0.70f, 0.40f, 0.34f, 1f);
    public static readonly Vector4 Wreck = new(0.20f, 0.16f, 0.16f, 1f);
    public static readonly Vector4 Ember = new(1f, 0.55f, 0.20f, 1f);
    public static readonly Vector4 Flame = new(1f, 0.86f, 0.40f, 1f);
    public static readonly Vector4 Smoke = new(0.30f, 0.30f, 0.34f, 1f);
    public static readonly Vector4 Cloud = new(0.96f, 0.98f, 1f, 1f);
    public static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    public static readonly Vector4 Brass = new(0.92f, 0.74f, 0.36f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.28f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private const float CellInset = 0.06f;
    private const float CellRadius = 0.16f;
    private const int CloudCount = 3;
    private const float CloudSpeed = 0.018f;
    private const float SinkTilt = 0.3f;
    private const float SinkShrink = 0.14f;
    private const float SinkDrop = 0.16f;
    private const float PuffPop = 0.35f;
    private const float FirePop = 0.25f;

    public static void DrawSky(ImDrawListPtr drawList, Rect grid, Vector4 accent, StageInk ink, float scale,
        float clock, bool compact, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var pitch = LayoutPitch(grid);
        var plate = BoardPlate.Around(grid, compact ? scale * 0.5f : scale);
        BoardPlate.Draw(drawList, plate, (compact ? BoardPlate.Radius * 0.5f : BoardPlate.Radius) * scale, scale,
            accent, ink);
        var inset = MathF.Max(1f, pitch * CellInset);
        var radius = pitch * CellRadius;
        for (var row = 0; row < BroadsideFleet.Size; row++)
        {
            var tone = Vector4.Lerp(SkyTop, SkyBottom, row / (BroadsideFleet.Size - 1f));
            for (var column = 0; column < BroadsideFleet.Size; column++)
            {
                var fill = (column + row) % 2 == 0 ? GamePalette.Lighten(tone, 0.04f) : tone;
                var cell = BroadsideLayout.CellRect(grid, column, row).Inset(inset);
                StageCell.Draw(drawList, cell, fill with { W = alpha }, CellDepth.Sunken, radius, scale);
            }
        }

        drawList.PushClipRect(grid.Min, grid.Max, true);
        for (var cloud = 0; cloud < CloudCount; cloud++)
        {
            var width = grid.Width * (0.32f + cloud * 0.08f);
            var phase = Fraction(clock * CloudSpeed * (1f + cloud * 0.4f) + cloud * 0.37f);
            var x = grid.Min.X - width + phase * (grid.Width + width * 2f);
            var y = grid.Min.Y + grid.Height * (0.18f + cloud * 0.3f);
            var color = ImGui.GetColorU32(White with { W = 0.06f * alpha });
            drawList.AddCircleFilled(new Vector2(x, y), width * 0.22f, color, 20);
            drawList.AddCircleFilled(new Vector2(x + width * 0.22f, y - width * 0.06f), width * 0.26f, color, 20);
            drawList.AddCircleFilled(new Vector2(x + width * 0.46f, y), width * 0.2f, color, 20);
        }

        drawList.PopClipRect();
    }

    public static void DrawAirship(ImDrawListPtr drawList, Rect ship, bool across, Vector4 hull, float scale,
        float clock, float sink, float alpha, float warn = 0f, float lift = 0f)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var along = across ? Vector2.UnitX : Vector2.UnitY;
        var side = across ? Vector2.UnitY : Vector2.UnitX;
        var pitch = across ? ship.Height : ship.Width;
        var length = across ? ship.Width : ship.Height;
        var center = ship.Center - new Vector2(0f, lift);
        var halfLength = length * 0.5f - pitch * 0.1f;
        var halfWidth = pitch * 0.31f;
        var half = along * halfLength + side * halfWidth;
        var bodyMin = center - half;
        var bodyMax = center + half;
        var wrecked = sink >= 0f;
        var body = wrecked ? Vector4.Lerp(hull, Wreck, Easing.Clamp01(sink * 1.2f)) : hull;
        var shade = GamePalette.Darken(body, 0.3f);
        var light = GamePalette.Lighten(body, 0.2f);
        var shadowOffset = new Vector2(2f, 3f) * scale + new Vector2(0f, lift * 0.6f);
        Squircle.Fill(drawList, bodyMin + shadowOffset, bodyMax + shadowOffset, halfWidth,
            ImGui.GetColorU32(Shadow with { W = Shadow.W * alpha }));
        var tail = center + along * halfLength;
        var finRoot = tail - along * pitch * 0.42f;
        var finColor = ImGui.GetColorU32(shade with { W = alpha });
        for (var fin = -1; fin <= 1; fin += 2)
        {
            drawList.AddTriangleFilled(finRoot + side * (halfWidth * 0.5f * fin), tail - along * pitch * 0.05f,
                tail + along * pitch * 0.04f + side * (pitch * 0.47f * fin), finColor);
        }

        for (var pod = 0; pod < 2; pod++)
        {
            var station = center + along * (halfLength * (pod == 0 ? -0.38f : 0.3f));
            for (var flank = -1; flank <= 1; flank += 2)
            {
                var podCenter = station + side * ((halfWidth + pitch * 0.08f) * flank);
                drawList.AddCircleFilled(podCenter, pitch * 0.085f, finColor, 12);
                if (wrecked)
                {
                    continue;
                }

                var spin = clock * 26f + pod * 1.3f + flank;
                var blade = (along * MathF.Cos(spin) + side * MathF.Sin(spin) * 0.25f) * pitch * 0.13f;
                drawList.AddLine(podCenter - blade, podCenter + blade,
                    ImGui.GetColorU32(White with { W = 0.45f * alpha }), MathF.Max(1f, pitch * 0.03f));
            }
        }

        var top = ImGui.GetColorU32(light with { W = alpha });
        var bottom = ImGui.GetColorU32(shade with { W = alpha });
        if (across)
        {
            Squircle.FillVerticalGradient(drawList, bodyMin, bodyMax, halfWidth, top, bottom);
        }
        else
        {
            Squircle.FillHorizontalGradient(drawList, bodyMin, bodyMax, halfWidth, top, bottom);
        }

        var cells = (int)MathF.Round(length / MathF.Max(1f, pitch));
        var ribColor = ImGui.GetColorU32(shade with { W = 0.5f * alpha });
        for (var rib = 1; rib < cells; rib++)
        {
            var station = center - along * (length * 0.5f) + along * (rib * pitch);
            drawList.AddLine(station - side * halfWidth * 0.86f, station + side * halfWidth * 0.86f, ribColor,
                MathF.Max(1f, pitch * 0.035f));
        }

        var spine = -side * halfWidth * 0.42f;
        drawList.AddLine(center - along * halfLength * 0.8f + spine, center + along * halfLength * 0.7f + spine,
            ImGui.GetColorU32(White with { W = 0.32f * alpha }), MathF.Max(1f, pitch * 0.06f));
        var gondolaHalf = along * length * 0.16f + side * pitch * 0.07f;
        var gondolaMin = Vector2.Min(center - gondolaHalf, center + gondolaHalf);
        var gondolaMax = Vector2.Max(center - gondolaHalf, center + gondolaHalf);
        Squircle.Fill(drawList, gondolaMin, gondolaMax, pitch * 0.05f,
            ImGui.GetColorU32((wrecked ? shade : Brass) with { W = 0.85f * alpha }));
        var nose = center - along * (halfLength - pitch * 0.18f);
        drawList.AddCircleFilled(nose, halfWidth * 0.5f, ImGui.GetColorU32(White with { W = 0.22f * alpha }), 14);
        Squircle.Stroke(drawList, bodyMin, bodyMax, halfWidth,
            ImGui.GetColorU32(GamePalette.Darken(body, 0.55f) with { W = 0.7f * alpha }), MathF.Max(1f, 1f * scale));
        if (warn > 0f)
        {
            Squircle.Fill(drawList, bodyMin, bodyMax, halfWidth, ImGui.GetColorU32(Danger with { W = 0.55f * warn * alpha }));
        }

        if (wrecked)
        {
            var progress = Easing.EaseOutCubic(Easing.Clamp01(sink));
            Transform(drawList, firstVertex, center, SinkTilt * progress * (across ? 1f : -1f),
                1f - SinkShrink * progress, new Vector2(0f, pitch * SinkDrop * progress));
        }
    }

    public static void DrawFire(ImDrawListPtr drawList, Vector2 center, float pitch, float clock, int seed, float age)
    {
        var pop = age >= 0f && age < FirePop ? GameJuice.PopIn(age / FirePop) : 1f;
        var flicker = 0.5f + 0.5f * MathF.Sin(clock * 14f + seed * 1.7f);
        drawList.AddCircleFilled(center + new Vector2(0f, pitch * 0.08f), pitch * 0.34f,
            ImGui.GetColorU32(new Vector4(0.05f, 0.03f, 0.03f, 0.45f)), 18);
        ProgressRing.Glow(center, pitch * 0.72f, Ember, 0.22f + 0.2f * flicker);
        var baseY = center.Y + pitch * 0.16f;
        for (var flame = 0; flame < 3; flame++)
        {
            var offset = (flame - 1) * pitch * 0.15f;
            var height = pitch * (0.3f + 0.12f * MathF.Sin(clock * 11f + seed + flame * 2.1f)) * pop *
                         (flame == 1 ? 1.25f : 1f);
            var sway = MathF.Sin(clock * 7f + flame + seed) * pitch * 0.04f;
            var width = pitch * 0.11f * pop;
            var tip = new Vector2(center.X + offset + sway, baseY - height);
            drawList.AddTriangleFilled(new Vector2(center.X + offset - width, baseY),
                new Vector2(center.X + offset + width, baseY), tip, ImGui.GetColorU32(Ember));
            drawList.AddTriangleFilled(new Vector2(center.X + offset - width * 0.5f, baseY),
                new Vector2(center.X + offset + width * 0.5f, baseY),
                new Vector2(tip.X, baseY - height * 0.55f), ImGui.GetColorU32(Flame));
        }

        drawList.AddCircleFilled(new Vector2(center.X, baseY), pitch * 0.09f * pop, ImGui.GetColorU32(Flame), 12);
        for (var puff = 0; puff < 3; puff++)
        {
            var phase = Fraction(clock * 0.55f + puff / 3f + seed * 0.137f);
            var position = center + new Vector2(MathF.Sin(phase * 6f + seed) * pitch * 0.12f, -pitch * (0.22f + phase * 0.9f));
            var radius = pitch * (0.1f + 0.16f * phase);
            var alpha = 0.34f * (1f - phase) * Easing.Clamp01(phase * 5f);
            drawList.AddCircleFilled(position, radius, ImGui.GetColorU32(Smoke with { W = alpha }), 14);
        }
    }

    public static void DrawPuff(ImDrawListPtr drawList, Vector2 center, float pitch, float clock, int seed, float age)
    {
        var pop = age >= 0f && age < PuffPop ? GameJuice.PopIn(age / PuffPop) : 1f;
        var drift = new Vector2(MathF.Sin(clock * 1.3f + seed) * pitch * 0.03f, 0f);
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.14f));
        var fill = ImGui.GetColorU32(Cloud with { W = 0.86f });
        var size = pitch * pop;
        drawList.AddCircleFilled(center + drift + new Vector2(0f, pitch * 0.06f), size * 0.24f, shadow, 16);
        drawList.AddCircleFilled(center + drift + new Vector2(-size * 0.13f, size * 0.04f), size * 0.15f, fill, 16);
        drawList.AddCircleFilled(center + drift + new Vector2(size * 0.12f, size * 0.05f), size * 0.13f, fill, 16);
        drawList.AddCircleFilled(center + drift + new Vector2(0f, -size * 0.05f), size * 0.18f, fill, 16);
        drawList.AddCircle(center, pitch * 0.38f, ImGui.GetColorU32(Cloud with { W = 0.16f }), 20,
            MathF.Max(1f, pitch * 0.03f));
    }

    public static void DrawReticle(ImDrawListPtr drawList, Vector2 center, float pitch, Vector4 color, float clock,
        float scale)
    {
        var radius = pitch * 0.42f;
        var thickness = MathF.Max(1f, 2f * scale);
        var ink = ImGui.GetColorU32(color);
        ProgressRing.Glow(center, radius * 1.6f, color, 0.25f);
        drawList.AddCircle(center, radius, ink, 24, thickness);
        var spin = clock * 1.5f;
        for (var tick = 0; tick < 4; tick++)
        {
            var angle = spin + tick * MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * radius * 0.55f, center + direction * radius * 1.3f, ink, thickness);
        }

        drawList.AddCircleFilled(center, thickness, ink, 8);
    }

    public static void DrawShipIcon(ImDrawListPtr drawList, Vector2 left, float unit, int length, bool sunk,
        Vector4 hull, float scale)
    {
        var ship = new Rect(left - new Vector2(0f, unit * 0.5f), left + new Vector2(unit * length, unit * 0.5f));
        DrawAirship(drawList, ship, true, sunk ? Wreck : hull, scale, 0f, -1f, sunk ? 0.55f : 1f);
        if (!sunk)
        {
            return;
        }

        var thickness = MathF.Max(1f, 2f * scale);
        var color = ImGui.GetColorU32(Danger);
        drawList.AddLine(ship.Min + new Vector2(unit * 0.2f, unit * 0.1f), ship.Max - new Vector2(unit * 0.2f, unit * 0.1f),
            color, thickness);
    }

    public static void DrawProjectile(ImDrawListPtr drawList, Vector2 position, float radius, Vector4 color)
    {
        ProgressRing.Glow(position, radius * 3f, color, 0.6f);
        drawList.AddCircleFilled(position, radius, ImGui.GetColorU32(color), 16);
        drawList.AddCircleFilled(position, radius * 0.45f, ImGui.GetColorU32(White with { W = 0.9f }), 10);
    }

    private static float LayoutPitch(Rect grid) => grid.Width / BroadsideFleet.Size;

    private static float Fraction(float value) => value - MathF.Floor(value);

    private static void Transform(ImDrawListPtr drawList, int firstVertex, Vector2 pivot, float angle, float shrink,
        Vector2 offset)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = Math.Max(0, firstVertex); vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var localX = (vertex.Pos.X - pivot.X) * shrink;
            var localY = (vertex.Pos.Y - pivot.Y) * shrink;
            vertex.Pos = new Vector2(pivot.X + localX * cosine - localY * sine + offset.X,
                pivot.Y + localX * sine + localY * cosine + offset.Y);
        }
    }
}
