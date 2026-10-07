using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Mahjong;

internal static class MahjongArt
{
    private const float InnerInset = 0.13f;
    private const int RayCount = 8;
    private const int WaveSteps = 10;
    private const int PetalCount = 5;
    private static readonly Vector4 Ink = new(0.16f, 0.22f, 0.42f, 1f);
    private static readonly Vector4 Snow = new(0.80f, 0.90f, 1f, 1f);
    private static readonly Vector4 Gold = new(0.93f, 0.72f, 0.22f, 1f);
    private static readonly Vector4 GoldRim = new(0.62f, 0.43f, 0.10f, 1f);
    private static readonly Vector4 Rose = new(0.88f, 0.42f, 0.58f, 1f);
    private static readonly Vector4 Leaf = new(0.30f, 0.66f, 0.34f, 1f);
    private static readonly Vector4 Ember = new(0.94f, 0.52f, 0.20f, 1f);
    private static readonly Vector4 Maple = new(0.86f, 0.34f, 0.18f, 1f);
    private static readonly Vector4 Frost = new(0.36f, 0.66f, 0.92f, 1f);
    private static readonly Vector4 Pollen = new(0.98f, 0.82f, 0.30f, 1f);
    private static readonly Vector4[] Elements =
    {
        new(0.92f, 0.30f, 0.25f, 1f), new(0.36f, 0.74f, 0.95f, 1f), new(0.34f, 0.76f, 0.42f, 1f),
        new(0.84f, 0.60f, 0.24f, 1f), new(0.64f, 0.42f, 0.92f, 1f), new(0.24f, 0.44f, 0.90f, 1f),
    };
    private static readonly Vector4[] Roles =
    {
        new(0.24f, 0.44f, 0.84f, 1f), new(0.28f, 0.66f, 0.34f, 1f), new(0.84f, 0.28f, 0.28f, 1f),
    };
    private static readonly Vector4[] Dragons =
    {
        new(0.88f, 0.24f, 0.22f, 1f), new(0.20f, 0.62f, 0.38f, 1f), new(0.70f, 0.82f, 0.97f, 1f),
    };
    private static readonly Vector4[] Flowers =
    {
        new(0.86f, 0.36f, 0.56f, 1f), new(0.62f, 0.42f, 0.86f, 1f), new(0.95f, 0.56f, 0.66f, 1f),
        new(0.95f, 0.70f, 0.24f, 1f),
    };
    private static readonly float[] PipSizes = { 0.62f, 0.44f, 0.38f, 0.38f, 0.34f, 0.32f, 0.28f, 0.27f, 0.27f };
    private static readonly int[] PipStart = { 0, 1, 3, 6, 10, 15, 21, 28, 36 };
    private static readonly Vector2[] Pips =
    {
        new(0f, 0f),
        new(0f, -0.5f), new(0f, 0.5f),
        new(-0.5f, -0.55f), new(0f, 0f), new(0.5f, 0.55f),
        new(-0.45f, -0.5f), new(0.45f, -0.5f), new(-0.45f, 0.5f), new(0.45f, 0.5f),
        new(-0.48f, -0.56f), new(0.48f, -0.56f), new(0f, 0f), new(-0.48f, 0.56f), new(0.48f, 0.56f),
        new(-0.45f, -0.62f), new(0.45f, -0.62f), new(-0.45f, 0f), new(0.45f, 0f), new(-0.45f, 0.62f),
        new(0.45f, 0.62f),
        new(-0.55f, -0.68f), new(0f, -0.48f), new(0.55f, -0.28f), new(-0.45f, 0.22f), new(0.45f, 0.22f),
        new(-0.45f, 0.68f), new(0.45f, 0.68f),
        new(-0.45f, -0.72f), new(0.45f, -0.72f), new(-0.45f, -0.24f), new(0.45f, -0.24f), new(-0.45f, 0.24f),
        new(0.45f, 0.24f), new(-0.45f, 0.72f), new(0.45f, 0.72f),
        new(-0.56f, -0.64f), new(0f, -0.64f), new(0.56f, -0.64f), new(-0.56f, 0f), new(0f, 0f), new(0.56f, 0f),
        new(-0.56f, 0.64f), new(0f, 0.64f), new(0.56f, 0.64f),
    };

    public static Vector4 Tint(int face)
    {
        var variant = MahjongTiles.Variant(face);
        return MahjongTiles.KindOf(face) switch
        {
            TileKind.Crystal => Elements[variant % Elements.Length],
            TileKind.Role => Roles[variant % Roles.Length],
            TileKind.Gil => Gold,
            TileKind.Wind => Frost,
            TileKind.Dragon => Dragons[variant],
            TileKind.Season => Gold,
            _ => Flowers[variant],
        };
    }

    public static void Draw(ImDrawListPtr drawList, Rect face, int faceId, Vector4 paper, float alpha, float scale)
    {
        var inset = face.Width * InnerInset;
        var inner = new Rect(face.Min + new Vector2(inset, inset), face.Max - new Vector2(inset, inset));
        var variant = MahjongTiles.Variant(faceId);
        switch (MahjongTiles.KindOf(faceId))
        {
            case TileKind.Crystal:
            case TileKind.Role:
            case TileKind.Gil:
                DrawPips(drawList, inner, faceId, paper, alpha, scale);
                return;
            case TileKind.Wind:
                DrawWind(drawList, inner, variant, paper, alpha, scale);
                return;
            case TileKind.Dragon:
                DrawDragon(drawList, inner, variant, alpha, scale);
                return;
            case TileKind.Season:
                DrawFrame(drawList, inner, Gold, alpha, scale);
                DrawSeason(drawList, inner, variant, alpha, scale);
                return;
            default:
                DrawFrame(drawList, inner, Rose, alpha, scale);
                DrawFlower(drawList, inner, Flowers[variant], alpha);
                return;
        }
    }

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static void DrawPips(ImDrawListPtr drawList, Rect inner, int faceId, Vector4 paper, float alpha,
        float scale)
    {
        var rank = MahjongTiles.Rank(faceId);
        var kind = MahjongTiles.KindOf(faceId);
        var half = inner.Size * 0.5f;
        var center = inner.Center;
        var size = MathF.Min(half.X, half.Y * 0.75f) * PipSizes[rank - 1];
        var start = PipStart[rank - 1];
        for (var pip = 0; pip < rank; pip++)
        {
            var point = Pips[start + pip];
            var position = new Vector2(center.X + point.X * half.X, center.Y + point.Y * half.Y);
            switch (kind)
            {
                case TileKind.Crystal:
                    DrawCrystal(drawList, position, size * 0.55f, size, Elements[(rank + pip) % Elements.Length],
                        alpha, scale);
                    break;
                case TileKind.Role:
                    DrawRole(drawList, position, size, (rank + pip) % Roles.Length, alpha);
                    break;
                default:
                    DrawCoin(drawList, position, size, paper, alpha, scale);
                    break;
            }
        }
    }

    private static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float halfWidth, float halfHeight,
        Vector4 color, float alpha, float scale)
    {
        var top = center + new Vector2(0f, -halfHeight);
        var right = center + new Vector2(halfWidth, 0f);
        var bottom = center + new Vector2(0f, halfHeight);
        var left = center + new Vector2(-halfWidth, 0f);
        drawList.AddQuadFilled(top, right, bottom, left, Color(color, alpha));
        drawList.AddTriangleFilled(top, center, left, Color(GamePalette.Lighten(color, 0.45f), alpha));
        drawList.AddQuad(top, right, bottom, left, Color(GamePalette.Darken(color, 0.35f), alpha * 0.8f),
            MathF.Max(1f, 0.8f * scale));
    }

    private static void DrawRole(ImDrawListPtr drawList, Vector2 center, float size, int role, float alpha)
    {
        var color = Color(Roles[role], alpha);
        switch (role)
        {
            case 0:
            {
                var width = size * 0.72f;
                var height = size * 0.88f;
                drawList.PathClear();
                drawList.PathLineTo(center + new Vector2(-width, -height));
                drawList.PathLineTo(center + new Vector2(width, -height));
                drawList.PathLineTo(center + new Vector2(width, height * 0.1f));
                drawList.PathLineTo(center + new Vector2(0f, height));
                drawList.PathLineTo(center + new Vector2(-width, height * 0.1f));
                drawList.PathFillConvex(color);
                drawList.AddLine(center + new Vector2(0f, -height * 0.7f), center + new Vector2(0f, height * 0.6f),
                    Color(GamePalette.Lighten(Roles[role], 0.5f), alpha), MathF.Max(1f, size * 0.16f));
                return;
            }
            case 1:
            {
                var arm = size * 0.85f;
                var thick = size * 0.3f;
                drawList.AddRectFilled(center - new Vector2(thick, arm), center + new Vector2(thick, arm), color);
                drawList.AddRectFilled(center - new Vector2(arm, thick), center + new Vector2(arm, thick), color);
                return;
            }
            default:
            {
                var bladeTop = center + new Vector2(0f, -size * 0.95f);
                var guard = center + new Vector2(0f, size * 0.42f);
                drawList.AddTriangleFilled(bladeTop, guard + new Vector2(size * 0.24f, 0f),
                    guard - new Vector2(size * 0.24f, 0f), color);
                drawList.AddLine(guard - new Vector2(size * 0.55f, 0f), guard + new Vector2(size * 0.55f, 0f), color,
                    MathF.Max(1f, size * 0.22f));
                drawList.AddLine(guard, center + new Vector2(0f, size * 0.85f), color, MathF.Max(1f, size * 0.2f));
                return;
            }
        }
    }

    private static void DrawCoin(ImDrawListPtr drawList, Vector2 center, float size, Vector4 paper, float alpha,
        float scale)
    {
        var radius = size * 0.82f;
        drawList.AddCircleFilled(center, radius, Color(Gold, alpha), 16);
        drawList.AddCircle(center, radius * 0.86f, Color(GoldRim, alpha), 16, MathF.Max(1f, radius * 0.16f));
        var hole = radius * 0.26f;
        drawList.AddRectFilled(center - new Vector2(hole, hole), center + new Vector2(hole, hole), Color(paper, alpha));
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.38f, -radius * 0.42f), radius * 0.16f,
            Color(new Vector4(1f, 1f, 1f, 0.6f), alpha), 8);
    }

    private static void DrawWind(ImDrawListPtr drawList, Rect inner, int variant, Vector4 paper, float alpha,
        float scale)
    {
        var center = inner.Center;
        var glyph = MathF.Min(inner.Width, inner.Height) * 0.46f;
        var ink = Color(Ink, alpha);
        switch (variant)
        {
            case 0:
                drawList.AddCircleFilled(center, glyph * 0.42f, ink, 20);
                for (var ray = 0; ray < RayCount; ray++)
                {
                    var angle = ray * MathF.PI * 2f / RayCount;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    drawList.AddLine(center + direction * glyph * 0.6f, center + direction * glyph * 0.95f, ink,
                        MathF.Max(1f, glyph * 0.13f));
                }

                return;
            case 1:
                drawList.AddCircleFilled(center, glyph * 0.82f, ink, 24);
                drawList.AddCircleFilled(center + new Vector2(glyph * 0.4f, -glyph * 0.22f), glyph * 0.7f,
                    Color(paper, alpha), 24);
                return;
            case 2:
                for (var wave = 0; wave < 2; wave++)
                {
                    var rowY = center.Y + (wave == 0 ? -0.3f : 0.3f) * glyph;
                    drawList.PathClear();
                    for (var step = 0; step <= WaveSteps; step++)
                    {
                        var progress = step / (float)WaveSteps;
                        drawList.PathLineTo(new Vector2(center.X + (progress * 2f - 1f) * glyph * 0.9f,
                            rowY + MathF.Sin(progress * MathF.PI * 2f) * glyph * 0.2f));
                    }

                    drawList.PathStroke(ink, ImDrawFlags.None, MathF.Max(1.2f * scale, glyph * 0.17f));
                }

                return;
            default:
                drawList.AddTriangleFilled(center + new Vector2(-glyph * 0.9f, glyph * 0.62f),
                    center + new Vector2(0f, -glyph * 0.78f), center + new Vector2(glyph * 0.9f, glyph * 0.62f), ink);
                drawList.AddTriangleFilled(center + new Vector2(-glyph * 0.3f, -glyph * 0.32f),
                    center + new Vector2(0f, -glyph * 0.78f), center + new Vector2(glyph * 0.3f, -glyph * 0.32f),
                    Color(Snow, alpha));
                return;
        }
    }

    private static void DrawDragon(ImDrawListPtr drawList, Rect inner, int variant, float alpha, float scale)
    {
        var center = inner.Center;
        var glyph = MathF.Min(inner.Width, inner.Height * 0.8f) * 0.5f;
        var color = Dragons[variant];
        DrawCrystal(drawList, center + new Vector2(-glyph * 0.48f, glyph * 0.28f), glyph * 0.26f, glyph * 0.62f, color,
            alpha, scale);
        DrawCrystal(drawList, center + new Vector2(glyph * 0.48f, glyph * 0.28f), glyph * 0.26f, glyph * 0.62f, color,
            alpha, scale);
        DrawCrystal(drawList, center + new Vector2(0f, -glyph * 0.08f), glyph * 0.34f, glyph * 1.0f, color, alpha,
            scale);
        var ground = center + new Vector2(0f, glyph * 0.92f);
        drawList.AddLine(ground - new Vector2(glyph * 0.8f, 0f), ground + new Vector2(glyph * 0.8f, 0f),
            Color(Ink, alpha * 0.6f), MathF.Max(1f, glyph * 0.08f));
    }

    private static void DrawFrame(ImDrawListPtr drawList, Rect inner, Vector4 color, float alpha, float scale)
    {
        var inset = inner.Width * 0.04f;
        drawList.AddRect(inner.Min - new Vector2(inset, inset), inner.Max + new Vector2(inset, inset),
            Color(color, alpha), inner.Width * 0.12f, ImDrawFlags.None, MathF.Max(1f, 1.4f * scale));
    }

    private static void DrawSeason(ImDrawListPtr drawList, Rect inner, int variant, float alpha, float scale)
    {
        var center = inner.Center;
        var glyph = MathF.Min(inner.Width, inner.Height) * 0.42f;
        switch (variant)
        {
            case 0:
            {
                var leaf = Color(Leaf, alpha);
                drawList.AddLine(center + new Vector2(0f, glyph * 0.85f), center + new Vector2(0f, -glyph * 0.1f), leaf,
                    MathF.Max(1f, glyph * 0.14f));
                drawList.AddCircleFilled(center + new Vector2(-glyph * 0.36f, -glyph * 0.3f), glyph * 0.34f, leaf, 14);
                drawList.AddCircleFilled(center + new Vector2(glyph * 0.36f, -glyph * 0.5f), glyph * 0.34f, leaf, 14);
                return;
            }
            case 1:
            {
                var ember = Color(Ember, alpha);
                drawList.AddCircleFilled(center, glyph * 0.48f, ember, 20);
                for (var ray = 0; ray < RayCount; ray++)
                {
                    var angle = ray * MathF.PI * 2f / RayCount + MathF.PI / RayCount;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    drawList.AddLine(center + direction * glyph * 0.66f, center + direction * glyph * 0.95f, ember,
                        MathF.Max(1f, glyph * 0.12f));
                }

                return;
            }
            case 2:
            {
                var maple = Color(Maple, alpha);
                drawList.AddQuadFilled(center + new Vector2(0f, -glyph * 0.9f), center + new Vector2(glyph * 0.7f, 0f),
                    center + new Vector2(0f, glyph * 0.6f), center + new Vector2(-glyph * 0.7f, 0f), maple);
                drawList.AddLine(center + new Vector2(0f, -glyph * 0.6f), center + new Vector2(0f, glyph * 0.95f),
                    Color(GamePalette.Darken(Maple, 0.4f), alpha), MathF.Max(1f, glyph * 0.1f));
                return;
            }
            default:
            {
                var frost = Color(Frost, alpha);
                for (var arm = 0; arm < 3; arm++)
                {
                    var angle = arm * MathF.PI / 3f + MathF.PI * 0.5f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * glyph * 0.9f;
                    drawList.AddLine(center - direction, center + direction, frost, MathF.Max(1f, glyph * 0.14f));
                    drawList.AddCircleFilled(center + direction, glyph * 0.1f, frost, 8);
                    drawList.AddCircleFilled(center - direction, glyph * 0.1f, frost, 8);
                }

                return;
            }
        }
    }

    private static void DrawFlower(ImDrawListPtr drawList, Rect inner, Vector4 color, float alpha)
    {
        var center = inner.Center;
        var glyph = MathF.Min(inner.Width, inner.Height) * 0.42f;
        var petal = Color(color, alpha);
        for (var index = 0; index < PetalCount; index++)
        {
            var angle = index * MathF.PI * 2f / PetalCount - MathF.PI * 0.5f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddCircleFilled(center + direction * glyph * 0.48f, glyph * 0.36f, petal, 14);
        }

        drawList.AddCircleFilled(center, glyph * 0.24f, Color(Pollen, alpha), 12);
    }
}
