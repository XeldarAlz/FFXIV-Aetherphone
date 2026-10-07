using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Pegfall;

internal static class PegfallRenderer
{
    public const float TrailWidth = 0.24f;
    private const float RailWidth = 0.14f;
    private const float LauncherRadius = 0.42f;
    private const float BarrelHalfWidth = 0.15f;
    private const float KickTravel = 0.16f;
    private const float GuideDotRadius = 0.05f;
    private const float PopSeconds = 0.22f;
    private const float PopGrowth = 0.4f;
    private const float BucketDepth = 0.7f;
    private const float BucketTaper = 0.25f;
    private const float SlotDepth = 1.1f;
    private const int DangerOranges = 3;
    public static readonly Vector4 Blue = new(0.30f, 0.58f, 1f, 1f);
    public static readonly Vector4 Orange = new(1f, 0.55f, 0.18f, 1f);
    public static readonly Vector4 Green = new(0.32f, 0.86f, 0.45f, 1f);
    public static readonly Vector4 BlueLit = new(0.66f, 0.88f, 1f, 1f);
    public static readonly Vector4 OrangeLit = new(1f, 0.84f, 0.46f, 1f);
    public static readonly Vector4 GreenLit = new(0.72f, 1f, 0.72f, 1f);
    public static readonly Vector4 Steel = new(0.90f, 0.93f, 0.98f, 1f);
    public static readonly Vector4 Gold = new(1f, 0.84f, 0.36f, 1f);
    public static readonly Vector4 Magnet = new(0.45f, 0.85f, 1f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.35f);
    private static readonly Vector4 Glass = new(0.80f, 0.90f, 1f, 0.16f);
    private static readonly Vector4 GlassEdge = new(0.85f, 0.95f, 1f, 0.55f);
    private static readonly Vector4 SlotLow = new(0.25f, 0.80f, 0.85f, 1f);
    private static readonly Vector4 SlotMiddle = new(0.70f, 0.50f, 1f, 1f);
    private static readonly Vector4 SlotHigh = new(1f, 0.80f, 0.30f, 1f);
    private static readonly Vector4 Ink = new(0.08f, 0.07f, 0.10f, 1f);

    public static Vector4 BaseColor(PegKind kind) => kind switch
    {
        PegKind.Orange => Orange,
        PegKind.Green => Green,
        _ => Blue,
    };

    public static Vector4 LitColor(PegKind kind) => kind switch
    {
        PegKind.Orange => OrangeLit,
        PegKind.Green => GreenLit,
        _ => BlueLit,
    };

    public static Vector4 Rainbow(float phase) => ArtGradient.FromHsv(phase - MathF.Floor(phase), 0.7f, 1f);

    public static void DrawField(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent, float scale)
    {
        var topLeft = camera.ToScreen(Vector2.Zero);
        var bottomLeft = camera.ToScreen(new Vector2(0f, PegfallBoard.Height));
        var topRight = camera.ToScreen(new Vector2(PegfallBoard.Width, 0f));
        var bottomRight = camera.ToScreen(new Vector2(PegfallBoard.Width, PegfallBoard.Height));
        var fieldTop = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f));
        var fieldBottom = ImGui.GetColorU32(accent with { W = 0.08f });
        drawList.AddRectFilledMultiColor(topLeft, bottomRight, fieldTop, fieldTop, fieldBottom, fieldBottom);
        var rail = camera.Px(RailWidth);
        var railColor = GamePalette.Lighten(accent, 0.25f);
        var glow = ImGui.GetColorU32(railColor with { W = 0.16f });
        var line = ImGui.GetColorU32(railColor with { W = 0.85f });
        drawList.AddLine(topLeft, bottomLeft, glow, rail * 3f);
        drawList.AddLine(topRight, bottomRight, glow, rail * 3f);
        drawList.AddLine(topLeft, topRight, glow, rail * 3f);
        drawList.AddLine(topLeft, bottomLeft, line, MathF.Max(1.5f * scale, rail * 0.6f));
        drawList.AddLine(topRight, bottomRight, line, MathF.Max(1.5f * scale, rail * 0.6f));
        drawList.AddLine(topLeft, topRight, line, MathF.Max(1.5f * scale, rail * 0.6f));
    }

    public static void DrawRingGuides(ImDrawListPtr drawList, in Camera2D camera, PegfallBoard board, Vector4 accent,
        float scale)
    {
        var color = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = 0.14f });
        for (var ring = 0; ring < board.RingCount; ring++)
        {
            var spec = board.Ring(ring);
            drawList.AddCircle(camera.ToScreen(spec.Center), camera.Px(spec.Radius), color, 48, 1.5f * scale);
        }
    }

    public static void DrawPegs(ImDrawListPtr drawList, in Camera2D camera, PegfallBoard board)
    {
        var radius = camera.Px(PegfallBoard.PegRadius);
        var danger = board.OrangeLeft > 0 && board.OrangeLeft <= DangerOranges;
        var wave = Pulse.Wave(Pulse.Fast);
        for (var peg = 0; peg < board.PegCount; peg++)
        {
            var state = board.StateOf(peg);
            if (state == PegState.Cleared)
            {
                continue;
            }

            var kind = board.KindOf(peg);
            var center = camera.ToScreen(board.RenderPegPosition(peg));
            if (state == PegState.Lit)
            {
                DrawLitPeg(drawList, center, radius, kind, board.LitAge(peg));
            }
            else
            {
                var pulse = danger && kind == PegKind.Orange ? wave : 0f;
                DrawIdlePeg(drawList, center, radius, kind, pulse);
            }

            if (kind == PegKind.Green)
            {
                DrawPowerGlyph(drawList, center, radius, board.PowerOf(peg));
            }
        }
    }

    private static void DrawIdlePeg(ImDrawListPtr drawList, Vector2 center, float radius, PegKind kind, float pulse)
    {
        var color = BaseColor(kind);
        if (pulse > 0f)
        {
            ProgressRing.Glow(center, radius * (1.6f + 0.5f * pulse), color, 0.6f + 0.6f * pulse);
        }

        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.18f), radius * 1.02f,
            ImGui.GetColorU32(Shadow), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(color, 0.35f)), 20);
        drawList.AddCircleFilled(center, radius * 0.8f, ImGui.GetColorU32(color), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.28f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(White with { W = 0.45f }), 12);
    }

    private static void DrawLitPeg(ImDrawListPtr drawList, Vector2 center, float radius, PegKind kind, float age)
    {
        var color = LitColor(kind);
        var pop = age < PopSeconds ? 1f + PopGrowth * (1f - age / PopSeconds) : 1f;
        var size = radius * pop;
        ProgressRing.Glow(center, size * 2.1f, color, 0.9f);
        drawList.AddCircleFilled(center, size, ImGui.GetColorU32(color), 20);
        drawList.AddCircleFilled(center, size * 0.55f, ImGui.GetColorU32(White with { W = 0.85f }), 16);
    }

    private static void DrawPowerGlyph(ImDrawListPtr drawList, Vector2 center, float radius, PegPower power)
    {
        var ink = ImGui.GetColorU32(Ink with { W = 0.75f });
        if (power == PegPower.Multiball)
        {
            drawList.AddCircleFilled(center + new Vector2(-radius * 0.3f, 0f), radius * 0.2f, ink, 10);
            drawList.AddCircleFilled(center + new Vector2(radius * 0.3f, 0f), radius * 0.2f, ink, 10);
            return;
        }

        drawList.PathClear();
        drawList.PathArcTo(center, radius * 0.38f, 0f, MathF.PI, 12);
        drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, radius * 0.16f));
        drawList.AddLine(center + new Vector2(-radius * 0.38f, 0f), center + new Vector2(-radius * 0.38f, -radius * 0.32f),
            ink, MathF.Max(1f, radius * 0.16f));
        drawList.AddLine(center + new Vector2(radius * 0.38f, 0f), center + new Vector2(radius * 0.38f, -radius * 0.32f),
            ink, MathF.Max(1f, radius * 0.16f));
    }

    public static void DrawBucket(ImDrawListPtr drawList, in Camera2D camera, PegfallBoard board, Vector4 accent,
        float scale)
    {
        if (board.Fever)
        {
            DrawFeverSlots(drawList, camera, board, scale);
            return;
        }

        var x = board.RenderBucketX;
        var top = PegfallBoard.BucketTop;
        var half = PegfallBoard.BucketInnerHalfWidth;
        var topLeft = camera.ToScreen(new Vector2(x - half, top));
        var topRight = camera.ToScreen(new Vector2(x + half, top));
        var bottomRight = camera.ToScreen(new Vector2(x + half - BucketTaper, top + BucketDepth));
        var bottomLeft = camera.ToScreen(new Vector2(x - half + BucketTaper, top + BucketDepth));
        ProgressRing.Glow(camera.ToScreen(new Vector2(x, top + BucketDepth * 0.5f)), camera.Px(half * 1.4f), accent, 0.5f);
        drawList.AddQuadFilled(topLeft, topRight, bottomRight, bottomLeft, ImGui.GetColorU32(Glass));
        var edge = ImGui.GetColorU32(GlassEdge);
        var thickness = MathF.Max(1.5f, 2f * scale);
        drawList.AddLine(topLeft, bottomLeft, edge, thickness);
        drawList.AddLine(bottomLeft, bottomRight, edge, thickness);
        drawList.AddLine(bottomRight, topRight, edge, thickness);
        var rimHalf = new Vector2(PegfallBoard.RimHalfWidth, PegfallBoard.RimHalfHeight);
        var rimOffset = PegfallBoard.BucketInnerHalfWidth + PegfallBoard.RimHalfWidth;
        DrawRim(drawList, camera, new Vector2(x - rimOffset, top + PegfallBoard.RimHalfHeight), rimHalf, accent);
        DrawRim(drawList, camera, new Vector2(x + rimOffset, top + PegfallBoard.RimHalfHeight), rimHalf, accent);
    }

    private static void DrawRim(ImDrawListPtr drawList, in Camera2D camera, Vector2 center, Vector2 half, Vector4 accent)
    {
        var min = camera.ToScreen(center - half);
        var max = camera.ToScreen(center + half);
        var rounding = (max.X - min.X) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.2f)), rounding);
        drawList.AddRectFilled(min, new Vector2(max.X, min.Y + (max.Y - min.Y) * 0.3f),
            ImGui.GetColorU32(White with { W = 0.5f }), rounding);
    }

    private static void DrawFeverSlots(ImDrawListPtr drawList, in Camera2D camera, PegfallBoard board, float scale)
    {
        var width = PegfallBoard.FeverSlotWidth;
        var top = PegfallBoard.BucketTop;
        var wave = Pulse.Wave(Pulse.Fast);
        for (var slot = 0; slot < PegfallBoard.FeverSlotCount; slot++)
        {
            var bonus = PegfallBoard.FeverBonus(slot);
            var color = SlotColor(bonus);
            var landed = board.FeverSlot == slot;
            var min = camera.ToScreen(new Vector2(slot * width, top));
            var max = camera.ToScreen(new Vector2((slot + 1) * width, top + SlotDepth));
            var lit = ImGui.GetColorU32(color with { W = landed ? 0.75f : 0.18f + 0.14f * wave });
            var faded = ImGui.GetColorU32(color with { W = 0.04f });
            drawList.AddRectFilledMultiColor(min, max, lit, lit, faded, faded);
            drawList.AddLine(min, new Vector2(max.X, min.Y), ImGui.GetColorU32(color with { W = 0.9f }),
                MathF.Max(1.5f, 2f * scale));
            var labelCenter = new Vector2((min.X + max.X) * 0.5f, min.Y + (max.Y - min.Y) * 0.42f);
            Typography.DrawCentered(drawList, labelCenter, GameNumber.Label(bonus), White with { W = 0.92f },
                TextStyles.Caption1.Scale * (landed ? 1.25f : 1f), FontWeight.Bold);
        }

        var divider = ImGui.GetColorU32(White with { W = 0.7f });
        for (var post = 1; post < PegfallBoard.FeverSlotCount; post++)
        {
            var from = camera.ToScreen(new Vector2(post * width, top));
            var to = camera.ToScreen(new Vector2(post * width, top + SlotDepth));
            drawList.AddLine(from, to, divider, MathF.Max(2f, camera.Px(0.1f)));
        }
    }

    private static Vector4 SlotColor(int bonus)
    {
        if (bonus >= 50000)
        {
            return SlotHigh;
        }

        return bonus >= 25000 ? SlotMiddle : SlotLow;
    }

    public static void DrawLauncher(ImDrawListPtr drawList, in Camera2D camera, Vector2 aim, float kick, bool loaded,
        Vector4 accent)
    {
        var center = camera.ToScreen(PegfallBoard.Launcher);
        var radius = camera.Px(LauncherRadius);
        var side = new Vector2(-aim.Y, aim.X);
        var length = PegfallBoard.MuzzleLength - KickTravel * kick;
        var muzzle = PegfallBoard.Launcher + aim * length;
        var halfWidth = BarrelHalfWidth;
        var a = camera.ToScreen(PegfallBoard.Launcher + side * halfWidth);
        var b = camera.ToScreen(muzzle + side * halfWidth * 0.8f);
        var c = camera.ToScreen(muzzle - side * halfWidth * 0.8f);
        var d = camera.ToScreen(PegfallBoard.Launcher - side * halfWidth);
        ProgressRing.Glow(center, radius * 1.8f, accent, 0.6f + 0.4f * kick);
        drawList.AddQuadFilled(a, b, c, d, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.35f)));
        drawList.AddCircleFilled(camera.ToScreen(muzzle), camera.Px(halfWidth * 0.95f),
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.5f)), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(accent, 0.25f)), 28);
        drawList.AddCircleFilled(center, radius * 0.78f, ImGui.GetColorU32(accent), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.28f, radius * 0.3f), radius * 0.3f,
            ImGui.GetColorU32(White with { W = 0.4f }), 16);
        if (!loaded)
        {
            return;
        }

        DrawBall(drawList, camera.ToScreen(PegfallBoard.Muzzle(aim)), camera.Px(PegfallBoard.BallRadius), Steel, 0f);
    }

    public static void DrawBall(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 halo, float haloStrength)
    {
        if (haloStrength > 0f)
        {
            ProgressRing.Glow(center, radius * 2.6f, halo, haloStrength);
        }

        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.25f), radius, ImGui.GetColorU32(Shadow), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(Steel, 0.3f)), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.08f, radius * 0.1f), radius * 0.82f,
            ImGui.GetColorU32(Steel), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f, radius * 0.36f), radius * 0.3f,
            ImGui.GetColorU32(White with { W = 0.9f }), 12);
    }

    public static void DrawGuide(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> path, bool bounced,
        ReadOnlySpan<Vector2> bounce, Vector4 accent)
    {
        var dot = camera.Px(GuideDotRadius);
        var tint = GamePalette.Lighten(accent, 0.45f);
        for (var index = 0; index < path.Length; index += 2)
        {
            var fade = 1f - index / (float)path.Length * 0.5f;
            drawList.AddCircleFilled(camera.ToScreen(path[index]), dot, ImGui.GetColorU32(tint with { W = 0.75f * fade }),
                8);
        }

        if (!bounced || path.Length == 0)
        {
            return;
        }

        var contact = camera.ToScreen(path[^1]);
        drawList.AddCircle(contact, camera.Px(PegfallBoard.BallRadius), ImGui.GetColorU32(White with { W = 0.6f }), 16,
            MathF.Max(1f, dot * 0.6f));
        for (var index = 1; index < bounce.Length; index += 2)
        {
            var fade = 1f - index / (float)bounce.Length;
            drawList.AddCircleFilled(camera.ToScreen(bounce[index]), dot * 0.85f,
                ImGui.GetColorU32(tint with { W = 0.5f * fade }), 8);
        }
    }
}
