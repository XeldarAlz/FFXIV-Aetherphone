using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework.Cards;

internal enum CardKind : byte
{
    Number,
    Action,
    Modifier,
}

internal readonly struct CardDesign
{
    public readonly CardKind Kind;
    public readonly int Number;
    public readonly FontAwesomeIcon Glyph;
    public readonly Vector4 Tint;
    public readonly string Label;

    private CardDesign(CardKind kind, int number, FontAwesomeIcon glyph, Vector4 tint, string label)
    {
        Kind = kind;
        Number = number;
        Glyph = glyph;
        Tint = tint;
        Label = label;
    }

    public static CardDesign Numbered(int number)
    {
        var clamped = Math.Clamp(number, 0, CardFace.MaxNumber);
        return new CardDesign(CardKind.Number, clamped, default, CardFace.NumberTint(clamped), GameNumber.Label(clamped));
    }

    public static CardDesign Action(FontAwesomeIcon glyph, Vector4 tint) =>
        new(CardKind.Action, 0, glyph, tint, string.Empty);

    public static CardDesign Modifier(string label, Vector4 tint) => new(CardKind.Modifier, 0, default, tint, label);
}

internal static class CardFace
{
    public const int MaxNumber = 12;
    private const float RoundingFraction = 0.14f;
    private const float BorderInset = 0.07f;
    private const float PipRowFraction = 0.11f;
    private const float PipRadiusFraction = 0.036f;
    private const float PipSpanFraction = 0.44f;
    private const float PipStepFraction = 0.09f;
    private const float PipsMinWidth = 40f;
    private const float CornerMinWidth = 54f;
    private const float CornerX = 0.14f;
    private const float CornerY = 0.11f;
    private const float GlyphFraction = 0.46f;
    private const float EmblemFraction = 0.2f;
    private const float LatticeStep = 0.2f;
    private const float LatticeSize = 0.045f;
    private const float HighlightWidth = 2f;
    private const int PipSegments = 12;

    private static readonly Vector4 FaceTop = new(0.99f, 0.97f, 0.93f, 1f);
    private static readonly Vector4 FaceBottom = new(0.91f, 0.88f, 0.82f, 1f);
    private static readonly Vector4 ModifierTop = new(0.17f, 0.16f, 0.21f, 1f);
    private static readonly Vector4 ModifierBottom = new(0.09f, 0.08f, 0.12f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly Vector4[] NumberTints =
    {
        new(0.55f, 0.55f, 0.60f, 1f), new(0.85f, 0.30f, 0.32f, 1f), new(0.92f, 0.52f, 0.22f, 1f),
        new(0.86f, 0.68f, 0.12f, 1f), new(0.50f, 0.70f, 0.20f, 1f), new(0.20f, 0.66f, 0.42f, 1f),
        new(0.16f, 0.62f, 0.66f, 1f), new(0.20f, 0.52f, 0.86f, 1f), new(0.36f, 0.40f, 0.90f, 1f),
        new(0.56f, 0.36f, 0.88f, 1f), new(0.78f, 0.32f, 0.78f, 1f), new(0.88f, 0.30f, 0.56f, 1f),
        new(0.70f, 0.50f, 0.22f, 1f),
    };

    private static readonly float[] NumeralWidths = { 72f, 54f, 42f, 32f };
    private static readonly TextStyle[] NumeralStyles =
    {
        TextStyles.Hero, TextStyles.LargeTitle, TextStyles.Title1, TextStyles.Title3, TextStyles.Headline,
    };

    public static Vector4 NumberTint(int number) => NumberTints[Math.Clamp(number, 0, MaxNumber)];

    public static void Draw(ImDrawListPtr drawList, in CardPose pose, in CardDesign design, Vector4 backAccent,
        float scale, float alpha = 1f, bool highlight = false)
    {
        if (!pose.FaceUp)
        {
            DrawBack(drawList, pose, backAccent, scale, alpha);
            return;
        }

        var firstVertex = drawList.VtxBuffer.Size;
        var rect = Upright(pose);
        var rounding = pose.Width * RoundingFraction;
        Elevation.Card(drawList, rect.Min, rect.Max, rounding, scale, alpha);
        switch (design.Kind)
        {
            case CardKind.Action:
                DrawAction(drawList, rect, design, rounding, scale, alpha);
                break;
            case CardKind.Modifier:
                DrawModifier(drawList, rect, design, rounding, scale, alpha);
                break;
            default:
                DrawNumber(drawList, rect, design, rounding, scale, alpha);
                break;
        }

        if (highlight)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(White with { W = 0.9f * alpha }),
                HighlightWidth * scale);
        }

        Transform(drawList, firstVertex, pose);
    }

    public static void DrawBack(ImDrawListPtr drawList, in CardPose pose, Vector4 accent, float scale,
        float alpha = 1f)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var rect = Upright(pose);
        var rounding = pose.Width * RoundingFraction;
        Elevation.Card(drawList, rect.Min, rect.Max, rounding, scale, alpha);
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.10f) with { W = alpha }),
            ImGui.GetColorU32(GamePalette.Darken(accent, 0.35f) with { W = alpha }));
        var inset = pose.Width * BorderInset;
        var inner = new Vector2(inset, inset);
        Squircle.Stroke(drawList, rect.Min + inner, rect.Max - inner, MathF.Max(0f, rounding - inset),
            ImGui.GetColorU32(White with { W = 0.35f * alpha }), 1f * scale);
        DrawLattice(drawList, rect.Min + inner, rect.Max - inner, pose.Width, accent, alpha);
        DrawCrystal(drawList, rect.Center, pose.Width * EmblemFraction, GamePalette.Lighten(accent, 0.35f), alpha,
            scale);
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(White with { W = 0.22f * alpha }),
            1f * scale);
        Transform(drawList, firstVertex, pose);
    }

    public static void DrawSlot(ImDrawListPtr drawList, in CardPose pose, float scale, float alpha = 1f)
    {
        var firstVertex = drawList.VtxBuffer.Size;
        var rect = Upright(pose);
        var rounding = pose.Width * RoundingFraction;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(White with { W = 0.05f * alpha }));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(White with { W = 0.2f * alpha }),
            1f * scale);
        Transform(drawList, firstVertex, pose);
    }

    private static void DrawNumber(ImDrawListPtr drawList, Rect rect, in CardDesign design, float rounding,
        float scale, float alpha)
    {
        FillFace(drawList, rect, rounding, FaceTop, FaceBottom, alpha);
        var tint = design.Tint;
        var ink = GamePalette.Darken(tint, 0.12f);
        var inset = rect.Width * BorderInset;
        var inner = new Vector2(inset, inset);
        Squircle.Stroke(drawList, rect.Min + inner, rect.Max - inner, MathF.Max(0f, rounding - inset),
            ImGui.GetColorU32(tint with { W = 0.55f * alpha }), 1.5f * scale);
        var units = rect.Width / scale;
        Typography.DrawCentered(drawList, rect.Center, design.Label, ink with { W = alpha }, NumeralStyle(units, 0));
        if (units >= PipsMinWidth)
        {
            var top = (design.Number + 1) / 2;
            DrawPipRow(drawList, rect, rect.Min.Y + rect.Height * PipRowFraction, top, tint, alpha);
            DrawPipRow(drawList, rect, rect.Max.Y - rect.Height * PipRowFraction, design.Number - top, tint, alpha);
        }

        if (units < CornerMinWidth)
        {
            return;
        }

        Typography.DrawCentered(drawList,
            new Vector2(rect.Min.X + rect.Width * CornerX, rect.Min.Y + rect.Height * CornerY), design.Label,
            ink with { W = alpha }, TextStyles.Caption2);
        Typography.DrawCentered(drawList,
            new Vector2(rect.Max.X - rect.Width * CornerX, rect.Max.Y - rect.Height * CornerY), design.Label,
            ink with { W = alpha }, TextStyles.Caption2);
    }

    private static void DrawAction(ImDrawListPtr drawList, Rect rect, in CardDesign design, float rounding,
        float scale, float alpha)
    {
        var tint = design.Tint;
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(GamePalette.Lighten(tint, 0.16f) with { W = alpha }),
            ImGui.GetColorU32(GamePalette.Darken(tint, 0.22f) with { W = alpha }));
        var inset = rect.Width * BorderInset;
        var inner = new Vector2(inset, inset);
        Squircle.Stroke(drawList, rect.Min + inner, rect.Max - inner, MathF.Max(0f, rounding - inset),
            ImGui.GetColorU32(White with { W = 0.5f * alpha }), 1.5f * scale);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.34f, ImGui.GetColorU32(White with { W = 0.18f * alpha }),
            32);
        ProgressRing.CenterIcon(drawList, rect.Center, design.Glyph, White with { W = alpha }, rect.Width * GlyphFraction);
    }

    private static void DrawModifier(ImDrawListPtr drawList, Rect rect, in CardDesign design, float rounding,
        float scale, float alpha)
    {
        FillFace(drawList, rect, rounding, ModifierTop, ModifierBottom, alpha);
        var tint = design.Tint;
        var inset = rect.Width * BorderInset;
        var inner = new Vector2(inset, inset);
        Squircle.Stroke(drawList, rect.Min + inner, rect.Max - inner, MathF.Max(0f, rounding - inset),
            ImGui.GetColorU32(tint with { W = 0.8f * alpha }), 1.5f * scale);
        Typography.DrawCentered(drawList, rect.Center, design.Label, GamePalette.Lighten(tint, 0.2f) with { W = alpha },
            NumeralStyle(rect.Width / scale, 1));
    }

    private static void FillFace(ImDrawListPtr drawList, Rect rect, float rounding, Vector4 top, Vector4 bottom,
        float alpha)
    {
        Squircle.FillVerticalGradient(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(top with { W = alpha }),
            ImGui.GetColorU32(bottom with { W = alpha }));
    }

    private static void DrawPipRow(ImDrawListPtr drawList, Rect rect, float centerY, int pips, Vector4 tint,
        float alpha)
    {
        if (pips <= 0)
        {
            return;
        }

        var step = pips > 1 ? MathF.Min(rect.Width * PipStepFraction, rect.Width * PipSpanFraction / (pips - 1)) : 0f;
        var left = rect.Center.X - step * (pips - 1) * 0.5f;
        var radius = rect.Width * PipRadiusFraction;
        var color = ImGui.GetColorU32(tint with { W = alpha });
        for (var pip = 0; pip < pips; pip++)
        {
            drawList.AddCircleFilled(new Vector2(left + pip * step, centerY), radius, color, PipSegments);
        }
    }

    private static void DrawLattice(ImDrawListPtr drawList, Vector2 min, Vector2 max, float width, Vector4 accent,
        float alpha)
    {
        var step = width * LatticeStep;
        var half = width * LatticeSize;
        var color = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.3f) with { W = 0.22f * alpha });
        var columns = (int)((max.X - min.X) / step);
        var rows = (int)((max.Y - min.Y) / step);
        var origin = new Vector2((min.X + max.X - columns * step) * 0.5f + step * 0.5f,
            (min.Y + max.Y - rows * step) * 0.5f + step * 0.5f);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var center = origin + new Vector2(column * step, row * step);
                drawList.AddQuadFilled(center + new Vector2(0f, -half), center + new Vector2(half, 0f),
                    center + new Vector2(0f, half), center + new Vector2(-half, 0f), color);
            }
        }
    }

    private static void DrawCrystal(ImDrawListPtr drawList, Vector2 center, float size, Vector4 color, float alpha,
        float scale)
    {
        var top = center + new Vector2(0f, -size);
        var right = center + new Vector2(size * 0.62f, 0f);
        var bottom = center + new Vector2(0f, size);
        var left = center + new Vector2(-size * 0.62f, 0f);
        drawList.AddQuadFilled(top, right, bottom, left, ImGui.GetColorU32(color with { W = alpha }));
        drawList.AddTriangleFilled(top, center, left, ImGui.GetColorU32(White with { W = 0.35f * alpha }));
        drawList.AddQuad(top, right, bottom, left, ImGui.GetColorU32(White with { W = 0.7f * alpha }), 1f * scale);
    }

    private static TextStyle NumeralStyle(float widthUnits, int smaller)
    {
        var tier = NumeralWidths.Length;
        for (var index = 0; index < NumeralWidths.Length; index++)
        {
            if (widthUnits >= NumeralWidths[index])
            {
                tier = index;
                break;
            }
        }

        return NumeralStyles[Math.Min(NumeralStyles.Length - 1, tier + smaller)];
    }

    private static Rect Upright(in CardPose pose)
    {
        var half = new Vector2(pose.Width * 0.5f, pose.Height * 0.5f);
        return new Rect(pose.Center - half, pose.Center + half);
    }

    private static void Transform(ImDrawListPtr drawList, int firstVertex, in CardPose pose)
    {
        var squash = pose.Squash;
        if (pose.Angle == 0f && squash == 1f)
        {
            return;
        }

        var sine = MathF.Sin(pose.Angle);
        var cosine = MathF.Cos(pose.Angle);
        var pivot = pose.Center;
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = Math.Max(0, firstVertex); vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var offsetX = (vertex.Pos.X - pivot.X) * squash;
            var offsetY = vertex.Pos.Y - pivot.Y;
            vertex.Pos = new Vector2(pivot.X + offsetX * cosine - offsetY * sine,
                pivot.Y + offsetX * sine + offsetY * cosine);
        }
    }
}
