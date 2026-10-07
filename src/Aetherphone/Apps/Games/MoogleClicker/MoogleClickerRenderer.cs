using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.MoogleClicker;

internal readonly struct MoogleClickerLayout
{
    private const float StageFraction = 0.47f;
    private const float MinStageHeight = 150f;
    private const float UpgradeRowHeight = 58f;
    private const float ShopGap = 6f;
    private const float MoogleSpan = 3.8f;
    private const float MoogleWidthFraction = 0.22f;
    private const float PomRise = 1.95f;

    public readonly Rect Full;
    public readonly Rect Stage;
    public readonly Rect Upgrades;
    public readonly Rect Shop;
    public readonly Vector2 MoogleCenter;
    public readonly float MoogleRadius;

    private MoogleClickerLayout(Rect full, Rect stage, Rect upgrades, Rect shop, Vector2 moogleCenter,
        float moogleRadius)
    {
        Full = full;
        Stage = stage;
        Upgrades = upgrades;
        Shop = shop;
        MoogleCenter = moogleCenter;
        MoogleRadius = moogleRadius;
    }

    public static MoogleClickerLayout From(Rect full, Rect safe, float scale)
    {
        var stageBottom = MathF.Max(full.Min.Y + full.Height * StageFraction, safe.Min.Y + MinStageHeight * scale);
        var stage = new Rect(safe.Min, new Vector2(safe.Max.X, stageBottom));
        var upgrades = new Rect(new Vector2(safe.Min.X, stageBottom),
            new Vector2(safe.Max.X, stageBottom + UpgradeRowHeight * scale));
        var shopTop = MathF.Min(upgrades.Max.Y + ShopGap * scale, safe.Max.Y);
        var shop = new Rect(new Vector2(safe.Min.X, shopTop), safe.Max);
        var radius = MathF.Max(1f, MathF.Min(stage.Height / MoogleSpan, stage.Width * MoogleWidthFraction));
        var spare = MathF.Max(0f, stage.Height - radius * MoogleSpan) * 0.5f;
        var center = new Vector2(stage.Center.X, stage.Min.Y + spare + radius * PomRise);
        return new MoogleClickerLayout(full, stage, upgrades, shop, center, radius);
    }

    public MoogleClickerLayout Punched(Vector2 offset, float factor) =>
        new(Full, Stage, Upgrades, Shop, MoogleCenter + offset, MoogleRadius * factor);

    public Vector2 StagePoint(Vector2 normalized) =>
        new(Stage.Min.X + Stage.Width * normalized.X, Stage.Min.Y + Stage.Height * normalized.Y);
}

internal static class MoogleClickerRenderer
{
    public const float PillHeight = 46f;
    private const int EllipseSegments = 28;
    private const float WingFlapSpeed = 7f;
    private const float WingFlapAngle = 0.28f;
    private const float BobSpeed = 2.2f;
    private const float BobAmount = 0.05f;
    private const float SquashAmount = 0.16f;
    private const float PillPadX = 16f;
    private static readonly Vector4 Fur = new(0.95f, 0.94f, 0.93f, 1f);
    private static readonly Vector4 FurShade = new(0.82f, 0.80f, 0.84f, 1f);
    private static readonly Vector4 FurLight = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 EarInner = new(0.98f, 0.70f, 0.76f, 1f);
    private static readonly Vector4 Wing = new(0.56f, 0.38f, 0.72f, 1f);
    private static readonly Vector4 WingShade = new(0.40f, 0.25f, 0.55f, 1f);
    private static readonly Vector4 Pom = new(0.95f, 0.30f, 0.42f, 1f);
    private static readonly Vector4 PomGold = new(1f, 0.82f, 0.30f, 1f);
    private static readonly Vector4 Stem = new(0.30f, 0.24f, 0.28f, 1f);
    private static readonly Vector4 Nose = new(0.88f, 0.34f, 0.40f, 1f);
    private static readonly Vector4 Blush = new(1f, 0.55f, 0.62f, 0.38f);
    private static readonly Vector4 Ink = new(0.22f, 0.18f, 0.22f, 1f);
    private static readonly Vector4 Outline = new(0.45f, 0.40f, 0.52f, 0.35f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.18f);
    private static readonly Vector4 ChickBody = new(1f, 0.84f, 0.30f, 1f);
    private static readonly Vector4 ChickBelly = new(1f, 0.94f, 0.66f, 1f);
    private static readonly Vector4 ChickWing = new(0.96f, 0.70f, 0.20f, 1f);
    private static readonly Vector4 Beak = new(0.96f, 0.52f, 0.18f, 1f);

    public static float Wobble(float squashAge) => MathF.Exp(-squashAge * 9f) * MathF.Cos(squashAge * 32f);

    public static Vector2 MoogleCenter(in MoogleClickerLayout layout, float time) =>
        layout.MoogleCenter - new Vector2(0f, MathF.Sin(time * BobSpeed) * BobAmount * layout.MoogleRadius);

    public static void DrawMoogle(ImDrawListPtr drawList, in MoogleClickerLayout layout, float time, float squashAge,
        bool frenzy, bool tapFrenzy, Vector4 accent, float scale)
    {
        var radius = layout.MoogleRadius;
        var center = MoogleCenter(layout, time);
        var wobble = Wobble(squashAge);
        var bob = MathF.Sin(time * BobSpeed);
        var shadowCenter = new Vector2(layout.MoogleCenter.X, layout.MoogleCenter.Y + radius * 1.66f);
        Ellipse(drawList, shadowCenter, new Vector2(radius * (0.82f - bob * 0.06f), radius * 0.16f),
            ImGui.GetColorU32(Shadow), EllipseSegments);
        var glow = frenzy ? PomGold : accent;
        ProgressRing.Glow(center, radius * (1.55f + 0.12f * MathF.Max(0f, wobble)), glow,
            frenzy ? 0.9f : 0.45f + 0.35f * MathF.Max(0f, wobble));
        var shape = new MoogleShape(center, radius, new Vector2(1f + SquashAmount * wobble, 1f - SquashAmount * wobble),
            center + new Vector2(0f, radius * 1.4f));
        var flap = MathF.Sin(time * WingFlapSpeed) * WingFlapAngle;
        DrawWing(drawList, shape, -1f, flap);
        DrawWing(drawList, shape, 1f, flap);
        shape.Ellipse(drawList, new Vector2(0f, 0.98f), new Vector2(0.62f, 0.5f), Fur);
        shape.Ellipse(drawList, new Vector2(-0.62f, 0.98f), new Vector2(0.17f, 0.13f), Fur);
        shape.Ellipse(drawList, new Vector2(0.62f, 0.98f), new Vector2(0.17f, 0.13f), Fur);
        shape.Ellipse(drawList, new Vector2(-0.28f, 1.36f), new Vector2(0.21f, 0.12f), FurShade);
        shape.Ellipse(drawList, new Vector2(0.28f, 1.36f), new Vector2(0.21f, 0.12f), FurShade);
        DrawEar(drawList, shape, -1f);
        DrawEar(drawList, shape, 1f);
        shape.Ellipse(drawList, Vector2.Zero, Vector2.One, FurShade);
        shape.Ellipse(drawList, new Vector2(-0.06f, -0.07f), new Vector2(0.93f, 0.92f), Fur);
        shape.Ellipse(drawList, new Vector2(-0.34f, -0.42f), new Vector2(0.26f, 0.18f), FurLight with { W = 0.7f });
        shape.Stroke(drawList, Vector2.Zero, Vector2.One, Outline, 1.5f * scale);
        DrawFace(drawList, shape, wobble, scale);
        DrawPom(drawList, shape, time, frenzy || tapFrenzy, scale);
    }

    public static void DrawMinion(ImDrawListPtr drawList, Vector2 center, float size, bool facingRight, float time,
        float alpha, float scale)
    {
        var facing = facingRight ? 1f : -1f;
        ProgressRing.Glow(center, size * 1.7f, ChickBody, 0.6f * alpha);
        var flap = MathF.Sin(time * 18f) * 0.35f;
        var wingCenter = center + new Vector2(-facing * size * 0.35f, -size * 0.05f);
        Ellipse(drawList, wingCenter + new Vector2(0f, -flap * size * 0.4f), new Vector2(size * 0.42f, size * 0.26f),
            ImGui.GetColorU32(ChickWing with { W = alpha }), EllipseSegments);
        Ellipse(drawList, center, new Vector2(size, size * 0.92f), ImGui.GetColorU32(ChickBody with { W = alpha }),
            EllipseSegments);
        Ellipse(drawList, center + new Vector2(facing * size * 0.12f, size * 0.3f), new Vector2(size * 0.55f, size * 0.45f),
            ImGui.GetColorU32(ChickBelly with { W = alpha }), EllipseSegments);
        var eye = center + new Vector2(facing * size * 0.38f, -size * 0.22f);
        drawList.AddCircleFilled(eye, size * 0.12f, ImGui.GetColorU32(Ink with { W = alpha }), 12);
        drawList.AddCircleFilled(eye - new Vector2(size * 0.04f, size * 0.04f), size * 0.04f,
            ImGui.GetColorU32(FurLight with { W = alpha }), 8);
        var beakBase = center + new Vector2(facing * size * 0.82f, -size * 0.02f);
        drawList.AddTriangleFilled(beakBase + new Vector2(0f, -size * 0.14f), beakBase + new Vector2(0f, size * 0.14f),
            beakBase + new Vector2(facing * size * 0.32f, 0f), ImGui.GetColorU32(Beak with { W = alpha }));
        var tuft = center + new Vector2(0f, -size * 0.88f);
        var tuftColor = ImGui.GetColorU32(ChickWing with { W = alpha });
        for (var feather = -1; feather <= 1; feather++)
        {
            drawList.AddLine(tuft, tuft + new Vector2(feather * size * 0.22f - facing * size * 0.06f, -size * 0.34f),
                tuftColor, MathF.Max(1f, 2f * scale));
        }
    }

    public static void DrawKupoPill(ImDrawListPtr drawList, Vector2 center, string value, string caption,
        Vector4 accent, float pop, bool glow, float scale)
    {
        var valueStyle = TextStyles.Title3;
        var captionStyle = TextStyles.Caption2;
        var valueWidth = Typography.Measure(value, valueStyle).X;
        var captionWidth = Typography.Measure(caption, captionStyle).X;
        var width = MathF.Max(valueWidth, captionWidth) + PillPadX * 2f * scale;
        var height = PillHeight * scale;
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var min = center - half;
        var max = center + half;
        if (glow)
        {
            ProgressRing.Glow(center, half.Y * 1.2f, accent, 0.5f);
        }

        Material.Frosted(drawList, min, max, half.Y, scale);
        if (glow)
        {
            Squircle.Stroke(drawList, min, max, half.Y, ImGui.GetColorU32(accent with { W = 0.85f }), 1.5f * scale);
        }

        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y - 7f * scale), value,
            glow ? GamePalette.Lighten(accent, 0.35f) : MoogleClickerText.Ink, valueStyle.Scale * pop,
            valueStyle.Weight);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 12f * scale), caption,
            MoogleClickerText.Muted, captionStyle.Scale, captionStyle.Weight);
    }

    public static void Ellipse(ImDrawListPtr drawList, Vector2 center, Vector2 radii, uint color, int segments)
    {
        drawList.PathClear();
        for (var segment = 0; segment < segments; segment++)
        {
            var angle = segment * MathF.PI * 2f / segments;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y));
        }

        drawList.PathFillConvex(color);
    }

    private static void DrawWing(ImDrawListPtr drawList, in MoogleShape shape, float side, float flap)
    {
        var root = new Vector2(side * 0.42f, 0.62f);
        var tip = Rotate(new Vector2(side * 0.86f, -0.52f), side * flap) + root;
        var lower = Rotate(new Vector2(side * 0.74f, 0.2f), side * flap * 0.6f) + root;
        var middle = Rotate(new Vector2(side * 0.62f, -0.12f), side * flap * 0.8f) + root;
        drawList.AddTriangleFilled(shape.Point(root), shape.Point(tip), shape.Point(middle),
            ImGui.GetColorU32(Wing));
        drawList.AddTriangleFilled(shape.Point(root), shape.Point(middle), shape.Point(lower),
            ImGui.GetColorU32(WingShade));
    }

    private static void DrawEar(ImDrawListPtr drawList, in MoogleShape shape, float side)
    {
        var baseInner = new Vector2(side * 0.36f, -0.82f);
        var baseOuter = new Vector2(side * 0.86f, -0.42f);
        var tip = new Vector2(side * 0.86f, -1.02f);
        drawList.AddTriangleFilled(shape.Point(baseInner), shape.Point(tip), shape.Point(baseOuter),
            ImGui.GetColorU32(Fur));
        var innerCenter = (baseInner + tip + baseOuter) / 3f;
        drawList.AddTriangleFilled(shape.Point(Vector2.Lerp(baseInner, innerCenter, 0.35f)),
            shape.Point(Vector2.Lerp(tip, innerCenter, 0.35f)), shape.Point(Vector2.Lerp(baseOuter, innerCenter, 0.35f)),
            ImGui.GetColorU32(EarInner));
    }

    private static void DrawFace(ImDrawListPtr drawList, in MoogleShape shape, float wobble, float scale)
    {
        var ink = ImGui.GetColorU32(Ink);
        var thickness = MathF.Max(1.5f, shape.Radius * 0.07f);
        var squint = wobble > 0.35f;
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = shape.Point(new Vector2(side * 0.36f, -0.06f));
            var span = shape.Radius * 0.14f;
            if (squint)
            {
                var inward = -side * span;
                drawList.AddLine(eye + new Vector2(-inward, -span * 0.8f), eye + new Vector2(inward, 0f), ink, thickness);
                drawList.AddLine(eye + new Vector2(inward, 0f), eye + new Vector2(-inward, span * 0.8f), ink, thickness);
                continue;
            }

            drawList.PathClear();
            drawList.PathArcTo(eye + new Vector2(0f, span * 0.5f), span, MathF.PI * 1.15f, MathF.PI * 1.85f, 10);
            drawList.PathStroke(ink, ImDrawFlags.None, thickness);
        }

        shape.Ellipse(drawList, new Vector2(-0.56f, 0.2f), new Vector2(0.15f, 0.1f), Blush);
        shape.Ellipse(drawList, new Vector2(0.56f, 0.2f), new Vector2(0.15f, 0.1f), Blush);
        shape.Ellipse(drawList, new Vector2(0f, 0.18f), new Vector2(0.14f, 0.11f), Nose);
        shape.Ellipse(drawList, new Vector2(-0.04f, 0.14f), new Vector2(0.05f, 0.035f), FurLight with { W = 0.75f });
        var mouth = shape.Point(new Vector2(0f, 0.38f));
        var mouthSpan = shape.Radius * 0.08f;
        drawList.PathClear();
        drawList.PathArcTo(mouth - new Vector2(mouthSpan, 0f), mouthSpan, MathF.PI, 0f, 8);
        drawList.PathArcTo(mouth + new Vector2(mouthSpan, 0f), mouthSpan, MathF.PI, 0f, 8);
        drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1f, 1.4f * scale));
    }

    private static void DrawPom(ImDrawListPtr drawList, in MoogleShape shape, float time, bool golden, float scale)
    {
        var sway = MathF.Sin(time * 3.1f) * 0.08f;
        var stemBase = shape.Point(new Vector2(0f, -0.94f));
        var stemBend = shape.Point(new Vector2(0.18f + sway * 0.5f, -1.32f));
        var pomCenter = shape.Point(new Vector2(0.1f + sway, -1.66f));
        drawList.AddBezierQuadratic(stemBase, stemBend, pomCenter, ImGui.GetColorU32(Stem),
            MathF.Max(1.5f, shape.Radius * 0.06f), 12);
        var pomRadius = shape.Radius * 0.24f;
        var color = golden ? PomGold : Pom;
        ProgressRing.Glow(pomCenter, pomRadius * (golden ? 2.6f : 1.8f), color, golden ? 0.9f : 0.5f);
        drawList.AddCircleFilled(pomCenter, pomRadius, ImGui.GetColorU32(color), 24);
        drawList.AddCircleFilled(pomCenter - new Vector2(pomRadius * 0.32f, pomRadius * 0.32f), pomRadius * 0.34f,
            ImGui.GetColorU32(FurLight with { W = 0.55f }), 14);
        drawList.AddCircle(pomCenter, pomRadius, ImGui.GetColorU32(GamePalette.Darken(color, 0.25f)), 24,
            MathF.Max(1f, scale));
    }

    private static Vector2 Rotate(Vector2 point, float angle)
    {
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        return new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
    }

    private readonly struct MoogleShape
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        private readonly Vector2 stretch;
        private readonly Vector2 pivot;

        public MoogleShape(Vector2 center, float radius, Vector2 stretch, Vector2 pivot)
        {
            Center = center;
            Radius = radius;
            this.stretch = stretch;
            this.pivot = pivot;
        }

        public Vector2 Point(Vector2 unit)
        {
            var world = Center + unit * Radius;
            return pivot + (world - pivot) * stretch;
        }

        public void Ellipse(ImDrawListPtr drawList, Vector2 unitCenter, Vector2 unitRadii, Vector4 color)
        {
            MoogleClickerRenderer.Ellipse(drawList, Point(unitCenter), unitRadii * Radius * stretch,
                ImGui.GetColorU32(color), EllipseSegments);
        }

        public void Stroke(ImDrawListPtr drawList, Vector2 unitCenter, Vector2 unitRadii, Vector4 color,
            float thickness)
        {
            var center = Point(unitCenter);
            var radii = unitRadii * Radius * stretch;
            drawList.PathClear();
            for (var segment = 0; segment < EllipseSegments; segment++)
            {
                var angle = segment * MathF.PI * 2f / EllipseSegments;
                drawList.PathLineTo(center + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y));
            }

            drawList.PathStroke(ImGui.GetColorU32(color), ImDrawFlags.Closed, thickness);
        }
    }
}
