using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly record struct BarkeepBottle(float Across, float Tall, int Color, int Shape);

internal static class BarkeepSceneArt
{
    public const int ShelfCount = 2;

    private const float BottleWidthShare = 0.055f;
    private const float GlowAlpha = 0.28f;
    private const float FeverGlowAlpha = 0.5f;
    private const float FeverCycleSpeed = 1.6f;
    private const int BubbleGlyphStep = 22;
    private const float HatClearance = 1.12f;

    private static readonly Vector4 WallTop = new(0.07f, 0.035f, 0.03f, 0.96f);
    private static readonly Vector4 WallBottom = new(0.17f, 0.085f, 0.045f, 0.96f);
    private static readonly Vector4 Plank = new(0.34f, 0.19f, 0.09f, 1f);
    private static readonly Vector4 PlankLip = new(0.62f, 0.40f, 0.20f, 1f);
    private static readonly Vector4 Amber = new(1f, 0.62f, 0.28f, 1f);
    private static readonly Vector4 CounterTop = new(0.42f, 0.23f, 0.11f, 1f);
    private static readonly Vector4 CounterFront = new(0.13f, 0.07f, 0.04f, 1f);
    private static readonly Vector4 Brass = new(0.92f, 0.74f, 0.40f, 1f);
    private static readonly Vector4 BubbleFill = new(0.97f, 0.95f, 0.90f, 0.96f);
    private static readonly Vector4 BubbleInk = new(0.16f, 0.10f, 0.08f, 1f);
    private static readonly Vector4 Eye = new(0.08f, 0.06f, 0.08f, 1f);

    private static readonly Vector4[] BottleColors =
    {
        new(0.85f, 0.48f, 0.15f, 1f),
        new(0.30f, 0.62f, 0.35f, 1f),
        new(0.75f, 0.85f, 0.92f, 1f),
        new(0.70f, 0.15f, 0.20f, 1f),
        new(0.20f, 0.40f, 0.80f, 1f),
        new(0.95f, 0.75f, 0.30f, 1f),
        new(0.50f, 0.25f, 0.60f, 1f),
    };

    private static readonly BarkeepBottle[] Bottles =
    {
        new(0.07f, 0.82f, 0, 0), new(0.15f, 0.64f, 2, 1), new(0.23f, 0.92f, 1, 0), new(0.31f, 0.56f, 3, 2),
        new(0.40f, 0.78f, 5, 0), new(0.60f, 0.86f, 4, 0), new(0.69f, 0.60f, 6, 2), new(0.77f, 0.90f, 0, 1),
        new(0.86f, 0.70f, 2, 0), new(0.94f, 0.80f, 3, 0), new(0.11f, 0.74f, 5, 1), new(0.20f, 0.58f, 6, 2),
        new(0.29f, 0.88f, 4, 0), new(0.38f, 0.66f, 1, 1), new(0.62f, 0.72f, 0, 0), new(0.71f, 0.94f, 2, 0),
        new(0.80f, 0.54f, 3, 2), new(0.89f, 0.84f, 5, 1),
    };

    private static readonly Vector4[] Outfits =
    {
        new(0.28f, 0.34f, 0.62f, 1f),
        new(0.60f, 0.22f, 0.36f, 1f),
        new(0.20f, 0.48f, 0.42f, 1f),
        new(0.66f, 0.46f, 0.22f, 1f),
        new(0.36f, 0.30f, 0.46f, 1f),
        new(0.54f, 0.20f, 0.20f, 1f),
    };

    private static readonly Vector4[] Skins =
    {
        new(0.96f, 0.80f, 0.66f, 1f),
        new(0.86f, 0.64f, 0.48f, 1f),
        new(0.66f, 0.46f, 0.32f, 1f),
        new(0.45f, 0.31f, 0.22f, 1f),
        new(0.92f, 0.86f, 0.80f, 1f),
        new(0.78f, 0.58f, 0.42f, 1f),
    };

    private static readonly Vector4[] Hairs =
    {
        new(0.12f, 0.08f, 0.06f, 1f),
        new(0.52f, 0.30f, 0.14f, 1f),
        new(0.90f, 0.78f, 0.46f, 1f),
        new(0.70f, 0.70f, 0.74f, 1f),
        new(0.62f, 0.18f, 0.16f, 1f),
    };

    public static Vector4 FeverTint(float phase)
    {
        var cycle = phase * FeverCycleSpeed;
        var blend = 0.5f + 0.5f * MathF.Sin(cycle);
        return Vector4.Lerp(CasinoColors.LightA, CasinoColors.LightB, blend);
    }

    public static void Wall(ImDrawListPtr drawList, Rect wall)
    {
        drawList.AddRectFilledMultiColor(wall.Min, wall.Max, ImGui.GetColorU32(WallTop), ImGui.GetColorU32(WallTop),
            ImGui.GetColorU32(WallBottom), ImGui.GetColorU32(WallBottom));
    }

    public static void Shelves(ImDrawListPtr drawList, Rect shelves, float phase, float fever, float scale)
    {
        var gap = shelves.Height / ShelfCount;
        var glowTint = Vector4.Lerp(Amber, FeverTint(phase), fever);
        var glowAlpha = GlowAlpha + (FeverGlowAlpha - GlowAlpha) * fever;
        var bottlesPerShelf = Bottles.Length / ShelfCount;
        for (var shelf = 0; shelf < ShelfCount; shelf++)
        {
            var plankY = shelves.Min.Y + gap * (shelf + 1);
            var glowTop = plankY - gap * 0.92f;
            drawList.AddRectFilledMultiColor(new Vector2(shelves.Min.X, glowTop), new Vector2(shelves.Max.X, plankY),
                ImGui.GetColorU32(glowTint with { W = 0f }), ImGui.GetColorU32(glowTint with { W = 0f }),
                ImGui.GetColorU32(glowTint with { W = glowAlpha }), ImGui.GetColorU32(glowTint with { W = glowAlpha }));
            for (var bottleIndex = 0; bottleIndex < bottlesPerShelf; bottleIndex++)
            {
                var bottle = Bottles[shelf * bottlesPerShelf + bottleIndex];
                var flicker = fever > 0f ? 0.5f + 0.5f * MathF.Sin(phase * 7f + bottleIndex * 1.3f + shelf) : 0f;
                DrawBottle(drawList, bottle, shelves, plankY, gap, flicker * fever, scale);
            }

            var plankHeight = MathF.Max(3f, 5f * scale);
            drawList.AddRectFilled(new Vector2(shelves.Min.X, plankY), new Vector2(shelves.Max.X, plankY + plankHeight),
                ImGui.GetColorU32(Plank));
            drawList.AddLine(new Vector2(shelves.Min.X, plankY), new Vector2(shelves.Max.X, plankY),
                ImGui.GetColorU32(PlankLip), MathF.Max(1f, scale));
        }
    }

    public static void Sign(ImDrawListPtr drawList, Vector2 center, float maxWidth, float maxHeight, float phase,
        float fever)
    {
        var height = CasinoSigns.HeightToFit(CasinoSign.Bar, maxWidth, maxHeight);
        var flicker = MathF.Sin(phase * 13f) > 0.93f ? 0.55f : 1f;
        var color = Vector4.Lerp(CasinoColors.LightA, FeverTint(phase), fever);
        CasinoSigns.Draw(drawList, CasinoSign.Bar, center, height, color, fever > 0f ? 1f : 0.85f * flicker);
    }

    public static void Counter(ImDrawListPtr drawList, Rect top, Rect front, float scale)
    {
        drawList.AddRectFilledMultiColor(front.Min, front.Max, ImGui.GetColorU32(CounterFront),
            ImGui.GetColorU32(CounterFront), ImGui.GetColorU32(CounterFront with { W = 0.92f }),
            ImGui.GetColorU32(CounterFront with { W = 0.92f }));
        drawList.AddRectFilledMultiColor(top.Min, top.Max, ImGui.GetColorU32(Vector4.Lerp(CounterTop, PlankLip, 0.35f)),
            ImGui.GetColorU32(Vector4.Lerp(CounterTop, PlankLip, 0.35f)), ImGui.GetColorU32(CounterTop),
            ImGui.GetColorU32(CounterTop));
        drawList.AddLine(new Vector2(top.Min.X, top.Max.Y + 2f * scale), new Vector2(top.Max.X, top.Max.Y + 2f * scale),
            ImGui.GetColorU32(Brass), MathF.Max(1.5f, 2.4f * scale));
        drawList.AddLine(top.Min, new Vector2(top.Max.X, top.Min.Y), ImGui.GetColorU32(Brass with { W = 0.5f }),
            MathF.Max(1f, scale));
    }

    public static void Patron(ImDrawListPtr drawList, in BarkeepPatronLook look, Vector2 feet, float unit, float bob,
        float alpha)
    {
        var height = unit * look.Height;
        var width = height * 0.52f * look.Width;
        var shoulders = feet.Y - height * 0.58f + bob;
        var outfit = Outfits[look.Tone % Outfits.Length] with { W = alpha };
        var skin = Skins[(look.Tone + look.Hair) % Skins.Length] with { W = alpha };
        var hair = Hairs[look.Hair % Hairs.Length] with { W = alpha };
        Squircle.Fill(drawList, new Vector2(feet.X - width * 0.5f, shoulders), new Vector2(feet.X + width * 0.5f, feet.Y),
            width * 0.34f, ImGui.GetColorU32(outfit));
        drawList.AddTriangleFilled(new Vector2(feet.X - width * 0.12f, shoulders),
            new Vector2(feet.X + width * 0.12f, shoulders), new Vector2(feet.X, shoulders + height * 0.12f),
            ImGui.GetColorU32(skin));
        var headRadius = height * 0.17f;
        var head = new Vector2(feet.X, shoulders - headRadius * 0.92f);
        if (look.Hair == 2)
        {
            drawList.AddRectFilled(new Vector2(head.X - headRadius * 1.05f, head.Y - headRadius * 0.2f),
                new Vector2(head.X + headRadius * 1.05f, head.Y + headRadius * 1.2f), ImGui.GetColorU32(hair),
                headRadius * 0.4f);
        }

        drawList.AddCircleFilled(head, headRadius, ImGui.GetColorU32(skin), 20);
        DrawHair(drawList, look.Hair, head, headRadius, ImGui.GetColorU32(hair));
        DrawHat(drawList, look.Hat, head, headRadius, ImGui.GetColorU32(Vector4.Lerp(outfit, Eye, 0.45f) with { W = alpha }));
        var eyeInk = ImGui.GetColorU32(Eye with { W = alpha });
        drawList.AddCircleFilled(head + new Vector2(-headRadius * 0.36f, headRadius * 0.08f), headRadius * 0.11f, eyeInk, 8);
        drawList.AddCircleFilled(head + new Vector2(headRadius * 0.36f, headRadius * 0.08f), headRadius * 0.11f, eyeInk, 8);
    }

    public static float HeadTop(in BarkeepPatronLook look, Vector2 feet, float unit)
    {
        return feet.Y - unit * look.Height * HatClearance;
    }

    public static void OrderBubble(ImDrawListPtr drawList, BarkeepShift shift, Vector2 tip, Vector4 accent, float pulse,
        float scale)
    {
        var steps = shift.CurrentStepCount;
        if (steps <= 0)
        {
            return;
        }

        var step = BubbleGlyphStep * scale;
        var width = steps * step + 14f * scale;
        var height = 26f * scale;
        var tail = 7f * scale;
        var min = new Vector2(tip.X - width * 0.5f, tip.Y - tail - height);
        var max = new Vector2(tip.X + width * 0.5f, tip.Y - tail);
        var fill = ImGui.GetColorU32(BubbleFill);
        Squircle.Fill(drawList, min, max, height * 0.5f, fill);
        drawList.AddTriangleFilled(new Vector2(tip.X - tail, max.Y - 1f), new Vector2(tip.X + tail, max.Y - 1f), tip, fill);
        for (var stepIndex = 0; stepIndex < steps; stepIndex++)
        {
            var center = new Vector2(min.X + 7f * scale + step * (stepIndex + 0.5f), (min.Y + max.Y) * 0.5f);
            var done = stepIndex < shift.CurrentStepIndex;
            var active = stepIndex == shift.CurrentStepIndex;
            var ink = done
                ? BarkeepArt.GradeTint(shift.CurrentOrderGrade(stepIndex), accent)
                : active ? BubbleInk : Palette.WithAlpha(BubbleInk, 0.35f);
            if (active)
            {
                drawList.AddCircle(center, step * 0.46f, ImGui.GetColorU32(accent with { W = 0.4f + 0.4f * pulse }), 20,
                    MathF.Max(1f, 1.6f * scale));
            }

            BarkeepArt.DrawVerbGlyph(drawList, shift.StepKindAt(shift.CurrentPatronIndex, stepIndex), center, 7f * scale,
                ImGui.GetColorU32(ink));
        }
    }

    private static void DrawBottle(ImDrawListPtr drawList, in BarkeepBottle bottle, Rect shelves, float plankY,
        float gap, float sparkle, float scale)
    {
        var color = BottleColors[bottle.Color % BottleColors.Length];
        var width = shelves.Width * BottleWidthShare * (bottle.Shape == 2 ? 1.25f : 1f);
        var tall = gap * 0.78f * bottle.Tall;
        var neck = bottle.Shape == 2 ? tall * 0.12f : tall * 0.32f;
        var x = shelves.Min.X + shelves.Width * bottle.Across;
        var bodyTop = plankY - tall + neck;
        var bodyMin = new Vector2(x - width * 0.5f, bodyTop);
        var bodyMax = new Vector2(x + width * 0.5f, plankY);
        drawList.AddCircleFilled(new Vector2(x, plankY - tall * 0.45f), width * (1.2f + sparkle * 0.6f),
            ImGui.GetColorU32(color with { W = 0.10f + 0.18f * sparkle }), 16);
        var rounding = bottle.Shape == 1 ? width * 0.12f : width * 0.4f;
        drawList.AddRectFilled(bodyMin, bodyMax, ImGui.GetColorU32(color with { W = 0.55f }), rounding);
        var liquidTop = bodyTop + (plankY - bodyTop) * 0.35f;
        drawList.AddRectFilled(new Vector2(bodyMin.X, liquidTop), bodyMax, ImGui.GetColorU32(color with { W = 0.85f }),
            rounding);
        var neckWidth = width * 0.32f;
        drawList.AddRectFilled(new Vector2(x - neckWidth * 0.5f, plankY - tall), new Vector2(x + neckWidth * 0.5f, bodyTop + 1f),
            ImGui.GetColorU32(color with { W = 0.7f }));
        drawList.AddRectFilled(new Vector2(x - neckWidth * 0.6f, plankY - tall - 2f * scale),
            new Vector2(x + neckWidth * 0.6f, plankY - tall + 2f * scale), ImGui.GetColorU32(Brass with { W = 0.9f }));
        drawList.AddLine(new Vector2(bodyMin.X + width * 0.22f, bodyTop + (plankY - bodyTop) * 0.15f),
            new Vector2(bodyMin.X + width * 0.22f, plankY - (plankY - bodyTop) * 0.2f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f + 0.4f * sparkle)), MathF.Max(1f, width * 0.12f));
    }

    private static void DrawHair(ImDrawListPtr drawList, int style, Vector2 head, float radius, uint ink)
    {
        switch (style)
        {
            case 0:
                return;
            case 3:
                drawList.AddCircleFilled(head - new Vector2(0f, radius * 1.05f), radius * 0.42f, ink, 14);
                break;
            case 4:
                drawList.AddTriangleFilled(head + new Vector2(-radius, -radius * 0.2f), head + new Vector2(radius * 0.4f, -radius),
                    head + new Vector2(-radius * 0.2f, -radius * 1.05f), ink);
                break;
        }

        drawList.PathClear();
        drawList.PathArcTo(head, radius * 1.04f, MathF.PI, MathF.PI * 2f, 14);
        drawList.PathFillConvex(ink);
    }

    private static void DrawHat(ImDrawListPtr drawList, int style, Vector2 head, float radius, uint ink)
    {
        switch (style)
        {
            case 1:
                drawList.AddRectFilled(head + new Vector2(-radius * 0.7f, -radius * 2.1f),
                    head + new Vector2(radius * 0.7f, -radius * 0.7f), ink, radius * 0.1f);
                drawList.AddRectFilled(head + new Vector2(-radius * 1.2f, -radius * 0.82f),
                    head + new Vector2(radius * 1.2f, -radius * 0.62f), ink, radius * 0.1f);
                break;
            case 2:
                Shapes.FillEllipse(drawList, head + new Vector2(radius * 0.2f, -radius * 0.82f),
                    radius * 1.1f, radius * 0.42f, ink);
                break;
            case 3:
                drawList.AddTriangleFilled(head + new Vector2(-radius * 0.9f, -radius * 0.7f),
                    head + new Vector2(radius * 0.9f, -radius * 0.7f), head + new Vector2(radius * 0.1f, -radius * 2f), ink);
                break;
        }
    }
}
