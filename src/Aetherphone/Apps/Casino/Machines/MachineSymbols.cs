using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Casino;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Machines;

internal static class MachineSymbols
{
    public const float ShimmerFrameSeconds = 0.6f;

    private static readonly Vector4 Gold = new(1f, 0.80f, 0.30f, 1f);
    private static readonly Vector4 GoldDeep = new(0.78f, 0.48f, 0.10f, 1f);
    private static readonly Vector4 Sun = new(1f, 0.62f, 0.18f, 1f);
    private static readonly Vector4 Crystal = new(0.55f, 0.95f, 1f, 1f);
    private static readonly Vector4 Orb = new(1f, 0.42f, 0.86f, 1f);
    private static readonly Vector4 Fur = new(0.97f, 0.95f, 0.92f, 1f);
    private static readonly Vector4 Pom = new(1f, 0.30f, 0.45f, 1f);
    private static readonly Vector4 Wing = new(0.62f, 0.52f, 0.98f, 1f);

    private static readonly Vector4[] GemTints =
    {
        new(0.98f, 0.36f, 0.42f, 1f),
        new(0.38f, 0.86f, 0.52f, 1f),
        new(0.40f, 0.62f, 1f, 1f),
        new(1f, 0.82f, 0.32f, 1f),
        new(0.78f, 0.52f, 1f, 1f),
    };

    private static readonly Vector4[] CascadeHighTints =
    {
        new(1f, 0.82f, 0.30f, 1f),
        new(0.95f, 0.60f, 0.30f, 1f),
        new(0.86f, 0.90f, 1f, 1f),
        new(0.60f, 0.86f, 0.98f, 1f),
    };

    public static int SymbolCount(string machineId) => machineId switch
    {
        SlotsRules.CascadeId => CrystalCascadeRules.SymbolCount,
        SlotsRules.MoogleId => MoogleMoneyRules.SymbolCount,
        _ => GoldenBirdRules.SymbolCount,
    };

    public static int BlurSymbol(string machineId, int index)
    {
        var count = SymbolCount(machineId);
        var wrapped = index % (count + 1);
        if (wrapped < 0)
        {
            wrapped += count + 1;
        }

        return wrapped == count ? Scatter(machineId) : wrapped;
    }

    public static int Scatter(string machineId) => machineId switch
    {
        SlotsRules.CascadeId => CrystalCascadeRules.Scatter,
        SlotsRules.MoogleId => MoogleMoneyRules.Scatter,
        _ => GoldenBirdRules.Scatter,
    };

    public static bool ShimmerFrame(int cell, float phase)
    {
        var frame = (int)(phase / ShimmerFrameSeconds);
        return ((frame + cell * 7) & 3) == 0;
    }

    public static void Draw(ImDrawListPtr drawList, string machineId, int symbol, Vector2 center, float extent,
        float alpha, bool shimmer)
    {
        switch (machineId)
        {
            case SlotsRules.CascadeId:
                DrawCascade(drawList, symbol, center, extent, alpha);
                break;
            case SlotsRules.MoogleId:
                DrawMoogleSymbol(drawList, symbol, center, extent, alpha);
                break;
            default:
                DrawBirdSymbol(drawList, symbol, center, extent, alpha);
                break;
        }

        if (shimmer && alpha > 0.5f)
        {
            SlotsSymbolArt.DrawSparkle(drawList, center + new Vector2(extent * 0.62f, -extent * 0.62f), extent * 0.28f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.85f * alpha)));
        }
    }

    public static void DrawBird(ImDrawListPtr drawList, Vector2 center, float extent, float alpha)
    {
        var body = ImGui.GetColorU32(Gold with { W = alpha });
        var deep = ImGui.GetColorU32(GoldDeep with { W = alpha });
        var eye = ImGui.GetColorU32(new Vector4(0.12f, 0.08f, 0.04f, alpha));
        Span<Vector2> tail = stackalloc Vector2[3]
        {
            center + new Vector2(-extent * 0.35f, extent * 0.35f),
            center + new Vector2(-extent * 0.98f, extent * 0.9f),
            center + new Vector2(-extent * 0.2f, extent * 0.62f),
        };
        Fill(drawList, deep, tail);
        drawList.AddCircleFilled(center + new Vector2(0f, extent * 0.18f), extent * 0.5f, body, 28);
        drawList.AddCircleFilled(center + new Vector2(extent * 0.36f, -extent * 0.4f), extent * 0.3f, body, 24);
        Span<Vector2> wing = stackalloc Vector2[3]
        {
            center + new Vector2(-extent * 0.1f, extent * 0.05f),
            center + new Vector2(-extent * 0.95f, -extent * 0.82f),
            center + new Vector2(-extent * 0.42f, extent * 0.42f),
        };
        Fill(drawList, deep, wing);
        Span<Vector2> beak = stackalloc Vector2[3]
        {
            center + new Vector2(extent * 0.6f, -extent * 0.5f),
            center + new Vector2(extent * 0.98f, -extent * 0.38f),
            center + new Vector2(extent * 0.6f, -extent * 0.28f),
        };
        Fill(drawList, ImGui.GetColorU32(Sun with { W = alpha }), beak);
        drawList.AddCircleFilled(center + new Vector2(extent * 0.42f, -extent * 0.46f), extent * 0.07f, eye, 10);
    }

    public static void DrawCoin(ImDrawListPtr drawList, Vector2 center, float extent, float alpha, float glow)
    {
        if (glow > 0f)
        {
            drawList.AddCircleFilled(center, extent * 1.25f, ImGui.GetColorU32(Gold with { W = 0.28f * glow * alpha }),
                32);
        }

        drawList.AddCircleFilled(center, extent, ImGui.GetColorU32(GoldDeep with { W = alpha }), 32);
        drawList.AddCircleFilled(center, extent * 0.86f, ImGui.GetColorU32(Gold with { W = alpha }), 32);
        drawList.AddCircle(center, extent * 0.7f, ImGui.GetColorU32(GoldDeep with { W = 0.6f * alpha }), 32,
            MathF.Max(1f, extent * 0.06f));
        drawList.AddCircleFilled(center + new Vector2(0f, -extent * 0.1f), extent * 0.22f,
            ImGui.GetColorU32(Pom with { W = alpha }), 18);
    }

    public static void DrawOrb(ImDrawListPtr drawList, Vector2 center, float extent, float alpha, float pulse)
    {
        drawList.AddCircleFilled(center, extent * (1.1f + 0.15f * pulse), ImGui.GetColorU32(Orb with { W = 0.22f * alpha }),
            32);
        drawList.AddCircleFilled(center, extent * 0.85f, ImGui.GetColorU32(Orb with { W = alpha }), 32);
        drawList.AddCircleFilled(center + new Vector2(-extent * 0.3f, -extent * 0.3f), extent * 0.28f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.7f * alpha)), 16);
    }

    private static void DrawBirdSymbol(ImDrawListPtr drawList, int symbol, Vector2 center, float extent, float alpha)
    {
        if (symbol == GoldenBirdRules.Bird)
        {
            DrawBird(drawList, center, extent, alpha);
            return;
        }

        if (symbol == GoldenBirdRules.Scatter)
        {
            DrawSun(drawList, center, extent, alpha);
            return;
        }

        SlotsSymbolArt.Draw(drawList, symbol, center, extent, alpha);
    }

    private static void DrawMoogleSymbol(ImDrawListPtr drawList, int symbol, Vector2 center, float extent,
        float alpha)
    {
        switch (symbol)
        {
            case MoogleMoneyRules.Moogle:
                DrawMoogle(drawList, center, extent, alpha);
                return;
            case MoogleMoneyRules.Wild:
                SlotsSymbolArt.Draw(drawList, 8, center, extent, alpha);
                return;
            case MoogleMoneyRules.Scatter:
                DrawStar(drawList, center, extent, Wing, alpha);
                return;
            case MoogleMoneyRules.Coin:
                DrawCoin(drawList, center, extent * 0.92f, alpha, 0f);
                return;
            default:
                SlotsSymbolArt.Draw(drawList, symbol, center, extent, alpha);
                return;
        }
    }

    private static void DrawCascade(ImDrawListPtr drawList, int symbol, Vector2 center, float extent, float alpha)
    {
        if (symbol == CrystalCascadeRules.Orb)
        {
            DrawOrb(drawList, center, extent, alpha, 0f);
            return;
        }

        if (symbol == CrystalCascadeRules.Scatter)
        {
            DrawStar(drawList, center, extent, Crystal, alpha);
            return;
        }

        if (symbol >= 4 && symbol < CrystalCascadeRules.SymbolCount)
        {
            DrawGem(drawList, center, extent, GemTints[symbol - 4], alpha, symbol);
            return;
        }

        var tint = CascadeHighTints[Math.Clamp(symbol, 0, CascadeHighTints.Length - 1)] with { W = alpha };
        var ink = ImGui.GetColorU32(tint);
        switch (symbol)
        {
            case 0:
                SlotsSymbolArt.Draw(drawList, 0, center, extent, alpha);
                return;
            case 1:
                DrawChalice(drawList, center, extent, ink);
                return;
            case 2:
                drawList.AddCircle(center + new Vector2(0f, extent * 0.15f), extent * 0.58f, ink, 32,
                    MathF.Max(1f, extent * 0.2f));
                DrawGem(drawList, center + new Vector2(0f, -extent * 0.55f), extent * 0.38f, Crystal, alpha, 4);
                return;
            default:
                DrawHourglass(drawList, center, extent, ink);
                return;
        }
    }

    private static void DrawGem(ImDrawListPtr drawList, Vector2 center, float extent, Vector4 tint, float alpha,
        int variant)
    {
        var ink = ImGui.GetColorU32(tint with { W = alpha });
        var glint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.55f * alpha));
        var sides = 4 + variant % 4;
        Span<Vector2> points = stackalloc Vector2[8];
        for (var index = 0; index < sides; index++)
        {
            var angle = -MathF.PI * 0.5f + MathF.PI * 2f * index / sides;
            points[index] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * extent * 0.9f;
        }

        Fill(drawList, ink, points[..sides]);
        drawList.AddLine(points[0], center, glint, MathF.Max(1f, extent * 0.08f));
        drawList.AddCircleFilled(center + new Vector2(-extent * 0.22f, -extent * 0.25f), extent * 0.14f, glint, 10);
    }

    private static void DrawChalice(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.PathClear();
        drawList.PathArcTo(center + new Vector2(0f, -extent * 0.35f), extent * 0.62f, 0f, MathF.PI, 20);
        drawList.PathFillConvex(ink);
        drawList.AddRectFilled(center + new Vector2(-extent * 0.1f, extent * 0.2f),
            center + new Vector2(extent * 0.1f, extent * 0.62f), ink);
        drawList.AddRectFilled(center + new Vector2(-extent * 0.48f, extent * 0.62f),
            center + new Vector2(extent * 0.48f, extent * 0.82f), ink, extent * 0.08f);
    }

    private static void DrawHourglass(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        Span<Vector2> top = stackalloc Vector2[3]
        {
            center + new Vector2(-extent * 0.6f, -extent * 0.75f),
            center + new Vector2(extent * 0.6f, -extent * 0.75f),
            center,
        };
        Fill(drawList, ink, top);
        Span<Vector2> bottom = stackalloc Vector2[3]
        {
            center,
            center + new Vector2(extent * 0.6f, extent * 0.75f),
            center + new Vector2(-extent * 0.6f, extent * 0.75f),
        };
        Fill(drawList, ink, bottom);
        var thickness = MathF.Max(1f, extent * 0.14f);
        drawList.AddLine(center + new Vector2(-extent * 0.72f, -extent * 0.85f),
            center + new Vector2(extent * 0.72f, -extent * 0.85f), ink, thickness);
        drawList.AddLine(center + new Vector2(-extent * 0.72f, extent * 0.85f),
            center + new Vector2(extent * 0.72f, extent * 0.85f), ink, thickness);
    }

    private static void DrawSun(ImDrawListPtr drawList, Vector2 center, float extent, float alpha)
    {
        var ray = ImGui.GetColorU32(Sun with { W = alpha });
        for (var index = 0; index < 8; index++)
        {
            var angle = MathF.PI * 2f * index / 8f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(center + direction * extent * 0.5f, center + direction * extent * 0.95f, ray,
                MathF.Max(1f, extent * 0.14f));
        }

        drawList.AddCircleFilled(center, extent * 0.52f, ImGui.GetColorU32(Gold with { W = alpha }), 28);
        drawList.AddCircleFilled(center, extent * 0.3f, ImGui.GetColorU32(new Vector4(1f, 0.96f, 0.8f, alpha)), 20);
    }

    private static void DrawStar(ImDrawListPtr drawList, Vector2 center, float extent, Vector4 tint, float alpha)
    {
        drawList.AddCircleFilled(center, extent * 0.95f, ImGui.GetColorU32(tint with { W = 0.18f * alpha }), 28);
        SlotsSymbolArt.DrawSparkle(drawList, center, extent * 0.95f, ImGui.GetColorU32(tint with { W = alpha }));
        drawList.AddCircleFilled(center, extent * 0.18f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), 12);
    }

    private static void DrawMoogle(ImDrawListPtr drawList, Vector2 center, float extent, float alpha)
    {
        var fur = ImGui.GetColorU32(Fur with { W = alpha });
        var dark = ImGui.GetColorU32(new Vector4(0.25f, 0.18f, 0.22f, alpha));
        var wing = ImGui.GetColorU32(Wing with { W = alpha });
        drawList.AddLine(center + new Vector2(0f, -extent * 0.45f), center + new Vector2(0f, -extent * 0.78f), dark,
            MathF.Max(1f, extent * 0.07f));
        drawList.AddCircleFilled(center + new Vector2(0f, -extent * 0.82f), extent * 0.18f,
            ImGui.GetColorU32(Pom with { W = alpha }), 16);
        Span<Vector2> leftWing = stackalloc Vector2[3]
        {
            center + new Vector2(-extent * 0.45f, extent * 0.2f),
            center + new Vector2(-extent * 0.98f, -extent * 0.15f),
            center + new Vector2(-extent * 0.55f, extent * 0.55f),
        };
        Fill(drawList, wing, leftWing);
        Span<Vector2> rightWing = stackalloc Vector2[3]
        {
            center + new Vector2(extent * 0.45f, extent * 0.2f),
            center + new Vector2(extent * 0.55f, extent * 0.55f),
            center + new Vector2(extent * 0.98f, -extent * 0.15f),
        };
        Fill(drawList, wing, rightWing);
        drawList.AddCircleFilled(center + new Vector2(0f, extent * 0.1f), extent * 0.58f, fur, 28);
        drawList.AddCircleFilled(center + new Vector2(-extent * 0.2f, 0f), extent * 0.07f, dark, 10);
        drawList.AddCircleFilled(center + new Vector2(extent * 0.2f, 0f), extent * 0.07f, dark, 10);
        drawList.AddCircleFilled(center + new Vector2(0f, extent * 0.18f), extent * 0.1f,
            ImGui.GetColorU32(Pom with { W = 0.8f * alpha }), 12);
    }

    private static void Fill(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }
}
