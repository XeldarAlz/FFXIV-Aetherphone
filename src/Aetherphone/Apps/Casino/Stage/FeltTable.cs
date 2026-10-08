using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct FeltTableOptions(
    int Seats,
    string Print = "",
    bool Cloth = false,
    bool Rail = true,
    bool Circles = true);

internal sealed class FeltTable
{
    public const float PrintAlpha = 0.34f;
    public const float LineAlpha = 0.22f;
    public const float CircleAlpha = 0.30f;
    public const int MaxPrintGlyphs = 48;

    private const int ArcSegments = 40;

    private readonly string[] glyphs = new string[MaxPrintGlyphs];
    private readonly float[] widths = new float[MaxPrintGlyphs];
    private string printText = string.Empty;
    private float printScale = -1f;
    private int glyphCount;
    private float printWidth;
    private float printHeight;

    public static FeltTable Shared { get; } = new();

    public FeltTableGeometry Draw(ImDrawListPtr drawList, Rect full, Rect table, in FeltTableOptions options,
        float scale)
    {
        var geometry = FeltTableGeometry.Compute(table, options.Seats, scale);
        if (options.Cloth)
        {
            var lamp = new Vector2(full.Center.X, full.Min.Y + full.Height * StageBackdrop.NightFeltLampY);
            StageBackdrop.NightFelt(drawList, full, lamp, Vector2.Zero, options.Rail, scale);
        }

        DrawInsuranceLine(drawList, geometry, scale);
        if (options.Print.Length > 0)
        {
            DrawPrint(drawList, geometry, options.Print, scale);
        }

        if (options.Circles)
        {
            DrawCircles(drawList, geometry, scale);
        }

        return geometry;
    }

    private static void DrawInsuranceLine(ImDrawListPtr drawList, in FeltTableGeometry geometry, float scale)
    {
        var inset = geometry.SeatRadius + FeltTableGeometry.CircleGap * scale * 0.5f;
        var radii = new Vector2(MathF.Max(0f, geometry.ArcRadii.X - inset), MathF.Max(0f, geometry.ArcRadii.Y - inset));
        var color = ImGui.GetColorU32(CasinoColors.Money with { W = LineAlpha });
        var start = MathF.PI * FeltTableGeometry.ArcStart;
        var span = MathF.PI * (FeltTableGeometry.ArcEnd - FeltTableGeometry.ArcStart);
        for (var step = 0; step <= ArcSegments; step++)
        {
            drawList.PathLineTo(geometry.PointOn(radii, start + span * step / ArcSegments));
        }

        drawList.PathStroke(color, ImDrawFlags.None, MathF.Max(1f, 1.5f * scale));
    }

    private static void DrawCircles(ImDrawListPtr drawList, in FeltTableGeometry geometry, float scale)
    {
        var color = ImGui.GetColorU32(CasinoColors.Money with { W = CircleAlpha });
        var thickness = MathF.Max(1f, 1.5f * scale);
        for (var seat = 0; seat < geometry.SeatCount; seat++)
        {
            drawList.AddCircle(geometry.BettingCircle(seat), geometry.CircleRadius, color, ArcSegments, thickness);
        }
    }

    private void DrawPrint(ImDrawListPtr drawList, in FeltTableGeometry geometry, string text, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        Prepare(text, style, scale);
        var radii = geometry.PrintRadii;
        if (radii.X <= 0f || glyphCount == 0)
        {
            return;
        }

        var span = printWidth / radii.X;
        var limit = MathF.PI * (FeltTableGeometry.ArcEnd - FeltTableGeometry.ArcStart);
        if (span > limit)
        {
            return;
        }

        var color = CasinoColors.Money with { W = PrintAlpha };
        var angle = MathF.PI * 0.5f + span * 0.5f;
        for (var index = 0; index < glyphCount; index++)
        {
            var half = widths[index] * 0.5f / radii.X;
            angle -= half;
            var point = geometry.PointOn(radii, angle);
            Typography.Draw(drawList, new Vector2(point.X - widths[index] * 0.5f, point.Y - printHeight * 0.5f),
                glyphs[index], color, style);
            angle -= half;
        }
    }

    private void Prepare(string text, in TextStyle style, float scale)
    {
        if (string.Equals(text, printText, StringComparison.Ordinal) && scale == printScale)
        {
            return;
        }

        printText = text;
        printScale = scale;
        glyphCount = Math.Min(text.Length, MaxPrintGlyphs);
        printWidth = 0f;
        printHeight = 0f;
        for (var index = 0; index < glyphCount; index++)
        {
            glyphs[index] = text.Substring(index, 1);
            var size = Typography.Measure(glyphs[index], style);
            widths[index] = size.X;
            printWidth += size.X;
            printHeight = MathF.Max(printHeight, size.Y);
        }
    }
}
