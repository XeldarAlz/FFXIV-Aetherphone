using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Slice;

internal struct SliceSplat
{
    public Vector2 Position;
    public Vector4 Color;
    public float Size;
    public float Life;
    public float Rotation;
}

internal static class SliceRenderer
{
    public const float MissMarksWidth = 66f;
    public const float PowerWidth = 70f;
    public const float SplatSeconds = 1.8f;
    private const int CircleSides = 22;
    private const int MaxPoints = 32;
    private const int SplatDrops = 5;
    private const float InnerScale = 0.68f;
    private const float HalfFadeSeconds = 0.45f;
    private const float CapsulePad = 10f;
    private const float MarkSize = 12f;
    private const float MarkGap = 5f;
    private const float PowerIcon = 12f;
    private static readonly Vector2 ShadowOffset = new(0.1f, 0.16f);
    private static readonly Vector2[] CircleOutline = BuildCircle(1f, 1f, 0f);
    private static readonly Vector2[] EggOutline = BuildCircle(0.86f, 1.08f, 0.16f);
    private static readonly Vector2[] CrystalOutline =
    {
        new(0f, -1.15f), new(0.6f, -0.5f), new(0.68f, 0.22f), new(0f, 1.1f), new(-0.68f, 0.22f), new(-0.6f, -0.5f),
    };

    private static readonly Vector2[] CrystalTopFacet =
    {
        new(0f, -1.15f), new(0.6f, -0.5f), new(0f, -0.2f), new(-0.6f, -0.5f),
    };

    private static readonly Vector2[] CrystalSideFacet =
    {
        new(0.6f, -0.5f), new(0.68f, 0.22f), new(0f, 1.1f), new(0f, -0.2f),
    };

    private static readonly Vector2[] EggSpots =
    {
        new(0.22f, -0.42f), new(-0.32f, 0.08f), new(0.28f, 0.36f), new(-0.06f, 0.66f),
    };

    private static readonly float[] EggSpotSizes = { 0.1f, 0.13f, 0.09f, 0.11f };

    public static readonly Vector4[] CrystalColors =
    {
        new(0.97f, 0.36f, 0.32f, 1f), new(0.50f, 0.82f, 1f, 1f), new(0.44f, 0.88f, 0.52f, 1f),
        new(0.96f, 0.78f, 0.32f, 1f), new(0.74f, 0.52f, 1f, 1f), new(0.30f, 0.62f, 0.98f, 1f),
    };

    public static readonly Vector4 EggShell = new(0.99f, 0.93f, 0.72f, 1f);
    public static readonly Vector4 Yolk = new(1f, 0.74f, 0.20f, 1f);
    public static readonly Vector4 MoogleFur = new(0.98f, 0.97f, 0.96f, 1f);
    public static readonly Vector4 Stuffing = new(1f, 0.70f, 0.80f, 1f);
    public static readonly Vector4 Pompom = new(0.96f, 0.28f, 0.38f, 1f);
    public static readonly Vector4 FreezeColor = new(0.56f, 0.86f, 1f, 1f);
    public static readonly Vector4 FrenzyColor = new(1f, 0.58f, 0.22f, 1f);
    public static readonly Vector4 DoubleColor = new(1f, 0.82f, 0.30f, 1f);
    public static readonly Vector4 BombGlow = new(1f, 0.30f, 0.24f, 1f);
    public static readonly Vector4 Spark = new(1f, 0.86f, 0.42f, 1f);
    private static readonly Vector4 EggSpot = new(0.62f, 0.60f, 0.30f, 1f);
    private static readonly Vector4 Wing = new(0.62f, 0.46f, 0.88f, 1f);
    private static readonly Vector4 FaceInk = new(0.26f, 0.20f, 0.28f, 1f);
    private static readonly Vector4 Nose = new(0.96f, 0.42f, 0.48f, 1f);
    private static readonly Vector4 BombBody = new(0.17f, 0.15f, 0.22f, 1f);
    private static readonly Vector4 BombRim = new(0.52f, 0.48f, 0.64f, 1f);
    private static readonly Vector4 Fuse = new(0.76f, 0.62f, 0.42f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.26f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Dim = new(1f, 1f, 1f, 0.22f);

    private static LabelSlot doubleLabel;

    public static Vector4 ColorOf(SliceKind kind, byte tint) => kind switch
    {
        SliceKind.Crystal => CrystalColors[tint % CrystalColors.Length],
        SliceKind.Egg => EggShell,
        SliceKind.Moogle => MoogleFur,
        SliceKind.Bomb => BombBody,
        SliceKind.Freeze => FreezeColor,
        SliceKind.Frenzy => FrenzyColor,
        _ => DoubleColor,
    };

    public static Vector4 JuiceOf(SliceKind kind, byte tint) => kind switch
    {
        SliceKind.Crystal => GamePalette.Lighten(CrystalColors[tint % CrystalColors.Length], 0.2f),
        SliceKind.Egg => Yolk,
        SliceKind.Moogle => Stuffing,
        _ => GamePalette.Lighten(ColorOf(kind, tint), 0.25f),
    };

    private static Vector4 InnerOf(SliceKind kind, byte tint) => kind switch
    {
        SliceKind.Crystal => GamePalette.Lighten(CrystalColors[tint % CrystalColors.Length], 0.5f),
        SliceKind.Egg => Yolk,
        SliceKind.Moogle => Stuffing,
        _ => GamePalette.Lighten(ColorOf(kind, tint), 0.45f),
    };

    public static void DrawSplats(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<SliceSplat> splats)
    {
        for (var index = 0; index < splats.Length; index++)
        {
            ref readonly var splat = ref splats[index];
            if (splat.Life <= 0f)
            {
                continue;
            }

            var fade = Math.Clamp(splat.Life / SplatSeconds, 0f, 1f);
            var color = ImGui.GetColorU32(splat.Color with { W = 0.34f * fade });
            var center = camera.ToScreen(splat.Position);
            var size = camera.Px(splat.Size);
            drawList.AddCircleFilled(center, size, color, 18);
            for (var drop = 0; drop < SplatDrops; drop++)
            {
                var angle = splat.Rotation + drop * 1.31f;
                var reach = size * (1.15f + 0.18f * (drop % 3));
                var dropCenter = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
                drawList.AddCircleFilled(dropCenter, size * (0.16f + 0.05f * (drop % 3)), color, 10);
            }

            var drip = center + new Vector2(0f, size * (0.9f + 1.4f * (1f - fade)));
            drawList.AddLine(center, drip, color, size * 0.22f);
            drawList.AddCircleFilled(drip, size * 0.16f, color, 10);
        }
    }

    public static void DrawObjects(ImDrawListPtr drawList, in Camera2D camera, SliceBoard board, float time)
    {
        var frozen = board.FreezeLeft > 0f;
        for (var index = 0; index < board.ObjectCount; index++)
        {
            var item = board.Object(index);
            DrawObject(drawList, in camera, in item, time, frozen);
        }
    }

    public static void DrawHalves(ImDrawListPtr drawList, in Camera2D camera, SliceBoard board)
    {
        Span<Vector2> outline = stackalloc Vector2[MaxPoints];
        Span<Vector2> clipped = stackalloc Vector2[MaxPoints];
        for (var index = 0; index < board.HalfCount; index++)
        {
            var half = board.Half(index);
            var alpha = Math.Clamp(half.Life / HalfFadeSeconds, 0f, 1f);
            var cosine = MathF.Cos(half.Rotation);
            var sine = MathF.Sin(half.Rotation);
            var offset = -half.CutNormal * (half.Side * half.Radius * SliceBoard.CentroidOffset);
            var count = Outline(half.Kind, half.Radius, 1f, outline);
            var clippedCount = Clip(outline[..count], half.CutNormal, half.Side, clipped, out var cutStart, out var cutEnd);
            if (clippedCount < 3)
            {
                continue;
            }

            var outer = ColorOf(half.Kind, half.Tint);
            Fill(drawList, in camera, clipped[..clippedCount], half.Position + ShadowOffset, cosine, sine, offset,
                ImGui.GetColorU32(Shadow with { W = Shadow.W * alpha }));
            Fill(drawList, in camera, clipped[..clippedCount], half.Position, cosine, sine, offset,
                ImGui.GetColorU32(outer with { W = alpha }));
            var inner = InnerOf(half.Kind, half.Tint);
            count = Outline(half.Kind, half.Radius, InnerScale, outline);
            clippedCount = Clip(outline[..count], half.CutNormal, half.Side, clipped, out _, out _);
            if (clippedCount >= 3)
            {
                Fill(drawList, in camera, clipped[..clippedCount], half.Position, cosine, sine, offset,
                    ImGui.GetColorU32(inner with { W = alpha }));
            }

            var start = camera.ToScreen(half.Position + Rotate(cutStart + offset, cosine, sine));
            var end = camera.ToScreen(half.Position + Rotate(cutEnd + offset, cosine, sine));
            drawList.AddLine(start, end, ImGui.GetColorU32(GamePalette.Lighten(inner, 0.35f) with { W = alpha }),
                MathF.Max(1f, camera.Px(0.07f)));
        }
    }

    public static void DrawMissMarks(ImDrawListPtr drawList, Rect rect, int misses, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var size = MarkSize * scale;
        var gap = MarkGap * scale;
        var total = SliceBoard.MissesPerLife * size + (SliceBoard.MissesPerLife - 1) * gap;
        var left = rect.Center.X - total * 0.5f;
        var thickness = MathF.Max(1.5f, 2.4f * scale);
        for (var mark = 0; mark < SliceBoard.MissesPerLife; mark++)
        {
            var center = new Vector2(left + mark * (size + gap) + size * 0.5f, rect.Center.Y);
            var color = ImGui.GetColorU32(mark < misses ? Danger : Dim);
            var arm = size * 0.38f;
            drawList.AddLine(center - new Vector2(arm, arm), center + new Vector2(arm, arm), color, thickness);
            drawList.AddLine(center + new Vector2(-arm, arm), center + new Vector2(arm, -arm), color, thickness);
        }
    }

    public static void DrawPower(ImDrawListPtr drawList, Rect rect, SliceKind kind, float fraction, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var color = ColorOf(kind, 0);
        var iconRadius = PowerIcon * scale * 0.5f;
        var iconCenter = new Vector2(rect.Min.X + CapsulePad * scale + iconRadius, rect.Center.Y - scale);
        DrawPickupGlyph(drawList, iconCenter, iconRadius, kind, 0f, 1f);
        var barLeft = iconCenter.X + iconRadius + 6f * scale;
        var barRight = rect.Max.X - CapsulePad * scale;
        var barHeight = 4f * scale;
        var barTop = rect.Center.Y - barHeight * 0.5f;
        drawList.AddRectFilled(new Vector2(barLeft, barTop), new Vector2(barRight, barTop + barHeight),
            ImGui.GetColorU32(color with { W = 0.2f }), barHeight * 0.5f);
        drawList.AddRectFilled(new Vector2(barLeft, barTop),
            new Vector2(barLeft + (barRight - barLeft) * Math.Clamp(fraction, 0f, 1f), barTop + barHeight),
            ImGui.GetColorU32(color), barHeight * 0.5f);
    }

    private static void DrawObject(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item, float time,
        bool frozen)
    {
        Span<Vector2> outline = stackalloc Vector2[MaxPoints];
        var cosine = MathF.Cos(item.Rotation);
        var sine = MathF.Sin(item.Rotation);
        var count = Outline(item.Kind, item.Radius, 1f, outline);
        var points = outline[..count];
        var center = camera.ToScreen(item.Position);
        var radius = camera.Px(item.Radius);
        if (item.Kind == SliceKind.Moogle)
        {
            DrawWings(drawList, in camera, in item, cosine, sine, time);
        }

        if (item.Kind == SliceKind.Bomb)
        {
            ProgressRing.Glow(center, radius * 1.7f, BombGlow, 0.45f + 0.35f * Flicker(time, 9f));
        }
        else if (SliceBoard.IsPickup(item.Kind))
        {
            ProgressRing.Glow(center, radius * 1.8f, ColorOf(item.Kind, 0), 0.6f + 0.25f * Flicker(time, 5f));
        }
        else
        {
            ProgressRing.Glow(center, radius * 1.45f, ColorOf(item.Kind, item.Tint), 0.22f);
        }

        Fill(drawList, in camera, points, item.Position + ShadowOffset, cosine, sine, Vector2.Zero,
            ImGui.GetColorU32(Shadow));
        Fill(drawList, in camera, points, item.Position, cosine, sine, Vector2.Zero,
            ImGui.GetColorU32(ColorOf(item.Kind, item.Tint)));
        switch (item.Kind)
        {
            case SliceKind.Crystal:
                DrawCrystalFacets(drawList, in camera, in item, cosine, sine);
                break;
            case SliceKind.Egg:
                DrawEggSpots(drawList, in camera, in item, cosine, sine);
                break;
            case SliceKind.Moogle:
                DrawMoogleFace(drawList, in camera, in item, cosine, sine);
                break;
            case SliceKind.Bomb:
                DrawBombDetail(drawList, in camera, in item, cosine, sine, time);
                break;
            default:
                DrawPickupGlyph(drawList, center, radius, item.Kind, item.Rotation, 1f);
                break;
        }

        var sheen = center + new Vector2(-radius * 0.34f, -radius * 0.38f);
        drawList.AddCircleFilled(sheen, radius * 0.16f, ImGui.GetColorU32(White with { W = 0.55f }), 12);
        if (!frozen)
        {
            return;
        }

        Fill(drawList, in camera, points, item.Position, cosine, sine, Vector2.Zero,
            ImGui.GetColorU32(FreezeColor with { W = 0.32f }));
    }

    private static void DrawCrystalFacets(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item,
        float cosine, float sine)
    {
        Span<Vector2> facet = stackalloc Vector2[4];
        var color = ColorOf(item.Kind, item.Tint);
        Scale(CrystalTopFacet, item.Radius, facet);
        Fill(drawList, in camera, facet, item.Position, cosine, sine, Vector2.Zero,
            ImGui.GetColorU32(GamePalette.Lighten(color, 0.32f)));
        Scale(CrystalSideFacet, item.Radius, facet);
        Fill(drawList, in camera, facet, item.Position, cosine, sine, Vector2.Zero,
            ImGui.GetColorU32(GamePalette.Darken(color, 0.22f)));
        var glintStart = camera.ToScreen(item.Position + Rotate(new Vector2(-0.36f, -0.62f) * item.Radius, cosine, sine));
        var glintEnd = camera.ToScreen(item.Position + Rotate(new Vector2(-0.12f, -0.9f) * item.Radius, cosine, sine));
        drawList.AddLine(glintStart, glintEnd, ImGui.GetColorU32(White with { W = 0.8f }), MathF.Max(1f, camera.Px(0.06f)));
    }

    private static void DrawEggSpots(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item, float cosine,
        float sine)
    {
        var color = ImGui.GetColorU32(EggSpot);
        for (var spot = 0; spot < EggSpots.Length; spot++)
        {
            var world = item.Position + Rotate(EggSpots[spot] * item.Radius, cosine, sine);
            drawList.AddCircleFilled(camera.ToScreen(world), camera.Px(EggSpotSizes[spot] * item.Radius), color, 10);
        }
    }

    private static void DrawWings(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item, float cosine,
        float sine, float time)
    {
        var flap = MathF.Sin(time * 14f + item.Rotation) * 0.18f;
        var color = ImGui.GetColorU32(Wing);
        for (var side = -1; side <= 1; side += 2)
        {
            var root = new Vector2(side * 0.62f, -0.2f) * item.Radius;
            var tip = new Vector2(side * 1.32f, -0.72f + flap) * item.Radius;
            var low = new Vector2(side * 1.05f, 0.08f - flap * 0.5f) * item.Radius;
            drawList.AddTriangleFilled(camera.ToScreen(item.Position + Rotate(root, cosine, sine)),
                camera.ToScreen(item.Position + Rotate(tip, cosine, sine)),
                camera.ToScreen(item.Position + Rotate(low, cosine, sine)), color);
        }
    }

    private static void DrawMoogleFace(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item, float cosine,
        float sine)
    {
        var radius = item.Radius;
        var ink = ImGui.GetColorU32(FaceInk);
        var thickness = MathF.Max(1f, camera.Px(0.075f * radius));
        for (var side = -1; side <= 1; side += 2)
        {
            var eyeLeft = camera.ToScreen(item.Position + Rotate(new Vector2(side * 0.42f, -0.08f) * radius, cosine, sine));
            var eyeRight = camera.ToScreen(item.Position + Rotate(new Vector2(side * 0.18f, -0.08f) * radius, cosine, sine));
            drawList.AddLine(eyeLeft, eyeRight, ink, thickness);
        }

        var nose = camera.ToScreen(item.Position + Rotate(new Vector2(0f, 0.16f) * radius, cosine, sine));
        drawList.AddCircleFilled(nose, camera.Px(0.13f * radius), ImGui.GetColorU32(Nose), 12);
        var stalkBase = camera.ToScreen(item.Position + Rotate(new Vector2(0f, -0.96f) * radius, cosine, sine));
        var ball = camera.ToScreen(item.Position + Rotate(new Vector2(0.1f, -1.42f) * radius, cosine, sine));
        drawList.AddLine(stalkBase, ball, ink, thickness);
        var pompomRadius = camera.Px(0.27f * radius);
        drawList.AddCircleFilled(ball, pompomRadius, ImGui.GetColorU32(Pompom), 16);
        drawList.AddCircleFilled(ball - new Vector2(pompomRadius * 0.3f, pompomRadius * 0.3f), pompomRadius * 0.32f,
            ImGui.GetColorU32(White with { W = 0.6f }), 10);
    }

    private static void DrawBombDetail(ImDrawListPtr drawList, in Camera2D camera, in SliceObject item, float cosine,
        float sine, float time)
    {
        var radius = item.Radius;
        var center = camera.ToScreen(item.Position);
        var pixels = camera.Px(radius);
        drawList.PathArcTo(center, pixels * 0.78f, MathF.PI * 1.05f, MathF.PI * 1.45f, 12);
        drawList.PathStroke(ImGui.GetColorU32(BombRim), ImDrawFlags.None, MathF.Max(1f, pixels * 0.12f));
        var capLeft = camera.ToScreen(item.Position + Rotate(new Vector2(-0.24f, -1.04f) * radius, cosine, sine));
        var capRight = camera.ToScreen(item.Position + Rotate(new Vector2(0.24f, -1.04f) * radius, cosine, sine));
        drawList.AddLine(capLeft, capRight, ImGui.GetColorU32(BombRim), MathF.Max(1f, pixels * 0.26f));
        var fuseStart = camera.ToScreen(item.Position + Rotate(new Vector2(0f, -1.12f) * radius, cosine, sine));
        var fuseBend = camera.ToScreen(item.Position + Rotate(new Vector2(0.18f, -1.42f) * radius, cosine, sine));
        var fuseTip = camera.ToScreen(item.Position + Rotate(new Vector2(0.46f, -1.5f) * radius, cosine, sine));
        drawList.AddBezierQuadratic(fuseStart, fuseBend, fuseTip, ImGui.GetColorU32(Fuse), MathF.Max(1f, pixels * 0.1f), 8);
        var flicker = Flicker(time, 23f);
        var sparkRadius = pixels * (0.2f + 0.12f * flicker);
        ProgressRing.Glow(fuseTip, sparkRadius * 2.2f, Spark, 0.8f);
        drawList.AddCircleFilled(fuseTip, sparkRadius, ImGui.GetColorU32(Spark), 10);
        var arm = sparkRadius * 1.8f;
        var sparkColor = ImGui.GetColorU32(White with { W = 0.85f });
        drawList.AddLine(fuseTip - new Vector2(arm, 0f), fuseTip + new Vector2(arm, 0f), sparkColor, MathF.Max(1f, sparkRadius * 0.3f));
        drawList.AddLine(fuseTip - new Vector2(0f, arm), fuseTip + new Vector2(0f, arm), sparkColor, MathF.Max(1f, sparkRadius * 0.3f));
        var warning = ImGui.GetColorU32(BombGlow with { W = 0.5f + 0.4f * Flicker(time, 6f) });
        drawList.AddCircle(center, pixels * 1.08f, warning, 28, MathF.Max(1f, pixels * 0.08f));
    }

    private static void DrawPickupGlyph(ImDrawListPtr drawList, Vector2 center, float radius, SliceKind kind,
        float rotation, float alpha)
    {
        var ink = ImGui.GetColorU32(White with { W = alpha });
        var thickness = MathF.Max(1f, radius * 0.14f);
        switch (kind)
        {
            case SliceKind.Freeze:
                for (var arm = 0; arm < 3; arm++)
                {
                    var angle = rotation + arm * MathF.PI / 3f;
                    var reach = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.66f;
                    drawList.AddLine(center - reach, center + reach, ink, thickness);
                }

                drawList.AddCircleFilled(center, radius * 0.16f, ink, 10);
                return;
            case SliceKind.Frenzy:
                for (var point = 0; point < 5; point++)
                {
                    var angle = rotation - MathF.PI * 0.5f + point * MathF.Tau / 5f;
                    var tip = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.7f;
                    var left = center + new Vector2(MathF.Cos(angle - 0.63f), MathF.Sin(angle - 0.63f)) * radius * 0.3f;
                    var right = center + new Vector2(MathF.Cos(angle + 0.63f), MathF.Sin(angle + 0.63f)) * radius * 0.3f;
                    drawList.AddTriangleFilled(tip, right, left, ink);
                }

                drawList.AddCircleFilled(center, radius * 0.32f, ink, 12);
                return;
            default:
                drawList.AddCircle(center, radius * 0.74f, ImGui.GetColorU32(GamePalette.Darken(DoubleColor, 0.3f) with { W = alpha }),
                    20, thickness);
                Typography.DrawCentered(drawList, center, doubleLabel.Get(L.Stage.Times, 2),
                    GamePalette.Darken(DoubleColor, 0.55f) with { W = alpha }, Math.Clamp(radius / 14f, 0.45f, 1.6f),
                    FontWeight.Bold);
                return;
        }
    }

    private static int Outline(SliceKind kind, float radius, float scale, Span<Vector2> output)
    {
        var source = kind switch
        {
            SliceKind.Crystal => CrystalOutline,
            SliceKind.Egg => EggOutline,
            _ => CircleOutline,
        };

        var size = radius * scale;
        for (var index = 0; index < source.Length; index++)
        {
            output[index] = source[index] * size;
        }

        return source.Length;
    }

    private static void Scale(ReadOnlySpan<Vector2> source, float radius, Span<Vector2> output)
    {
        for (var index = 0; index < source.Length; index++)
        {
            output[index] = source[index] * radius;
        }
    }

    private static int Clip(ReadOnlySpan<Vector2> input, Vector2 normal, float side, Span<Vector2> output,
        out Vector2 cutStart, out Vector2 cutEnd)
    {
        cutStart = Vector2.Zero;
        cutEnd = Vector2.Zero;
        var cuts = 0;
        var count = 0;
        for (var index = 0; index < input.Length; index++)
        {
            var current = input[index];
            var previous = input[(index + input.Length - 1) % input.Length];
            var currentSide = side * Vector2.Dot(normal, current);
            var previousSide = side * Vector2.Dot(normal, previous);
            if ((currentSide >= 0f) != (previousSide >= 0f) && count < output.Length)
            {
                var amount = previousSide / (previousSide - currentSide);
                var crossing = Vector2.Lerp(previous, current, amount);
                output[count++] = crossing;
                if (cuts == 0)
                {
                    cutStart = crossing;
                }
                else
                {
                    cutEnd = crossing;
                }

                cuts++;
            }

            if (currentSide >= 0f && count < output.Length)
            {
                output[count++] = current;
            }
        }

        return count;
    }

    private static void Fill(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<Vector2> points, Vector2 center,
        float cosine, float sine, Vector2 offset, uint color)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(camera.ToScreen(center + Rotate(points[index] + offset, cosine, sine)));
        }

        drawList.PathFillConvex(color);
    }

    private static Vector2 Rotate(Vector2 point, float cosine, float sine) =>
        new(point.X * cosine - point.Y * sine, point.X * sine + point.Y * cosine);

    private static float Flicker(float time, float rate) => 0.5f + 0.5f * MathF.Sin(time * rate);

    private static Vector2[] BuildCircle(float radiusX, float radiusY, float taper)
    {
        var points = new Vector2[CircleSides];
        for (var index = 0; index < CircleSides; index++)
        {
            var angle = index * MathF.Tau / CircleSides;
            var y = MathF.Sin(angle);
            var width = radiusX * (1f + taper * y);
            points[index] = new Vector2(MathF.Cos(angle) * width, y * radiusY);
        }

        return points;
    }
}
