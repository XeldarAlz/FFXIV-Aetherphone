using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal enum ScratchMotif : byte
{
    Star,
    Clover,
    Heart,
    Coin,
    Gem,
}

internal readonly record struct ScratchTheme(Vector4 Accent, Vector4 Paper, Vector4 FoilTop, Vector4 FoilBottom,
    ScratchMotif Motif);

internal static class ScratchTicketArt
{
    public const float HeaderShare = 0.24f;

    private const float GridInsetShare = 0.07f;
    private const float PaperAccentMix = 0.22f;
    private const float PatternAlpha = 0.08f;
    private const float MotifBobSpeed = 2.2f;
    private const float MotifBobShare = 0.04f;
    private const float StampWidthShare = 0.6f;
    private const float StampTilt = -0.18f;

    private static readonly Vector4 Silver = new(0.70f, 0.72f, 0.80f, 1f);
    private static readonly Vector4 SilverShade = new(0.38f, 0.40f, 0.48f, 1f);

    private static readonly ScratchTheme[] Themes =
    {
        Theme(CasinoColors.LightB, new Vector4(0.04f, 0.08f, 0.14f, 1f), ScratchMotif.Star),
        Theme(new Vector4(0.30f, 0.86f, 0.55f, 1f), new Vector4(0.04f, 0.11f, 0.08f, 1f), ScratchMotif.Clover),
        Theme(CasinoColors.LightA, new Vector4(0.13f, 0.04f, 0.10f, 1f), ScratchMotif.Heart),
        Theme(CasinoColors.Money, new Vector4(0.13f, 0.09f, 0.03f, 1f), ScratchMotif.Coin),
        Theme(new Vector4(0.72f, 0.56f, 1f, 1f), new Vector4(0.08f, 0.05f, 0.15f, 1f), ScratchMotif.Gem),
    };

    private static readonly Vector2[] PatternSpots =
    {
        new(0.12f, 0.30f),
        new(0.88f, 0.22f),
        new(0.18f, 0.82f),
        new(0.84f, 0.76f),
        new(0.50f, 0.12f),
    };

    public static ScratchTheme For(int tier) => Themes[Math.Clamp(tier, 0, Themes.Length - 1)];

    public static Rect GridRect(Rect ticket)
    {
        var header = ticket.Height - ticket.Width;
        var inset = ticket.Width * GridInsetShare;
        return new Rect(new Vector2(ticket.Min.X + inset, ticket.Min.Y + header + inset * 0.2f),
            new Vector2(ticket.Max.X - inset, ticket.Max.Y - inset));
    }

    public static void Shell(ImDrawListPtr drawList, Rect ticket, int tier, float phase, float scale)
    {
        var theme = For(tier);
        var rounding = Metrics.Radius.Grouped * scale;
        Elevation.Card(drawList, ticket.Min, ticket.Max, rounding, scale, 1f);
        Squircle.FillVerticalGradient(drawList, ticket.Min, ticket.Max, rounding,
            ImGui.GetColorU32(Palette.Mix(theme.Paper, theme.Accent, PaperAccentMix)), ImGui.GetColorU32(theme.Paper));
        DrawPattern(drawList, ticket, theme, scale);
        Squircle.Stroke(drawList, ticket.Min, ticket.Max, rounding, ImGui.GetColorU32(theme.Accent with { W = 0.6f }),
            MathF.Max(1f, 1.4f * scale));
        var header = ticket.Height - ticket.Width;
        var signCenter = new Vector2(ticket.Center.X, ticket.Min.Y + header * 0.42f);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Scratch, ticket.Width * 0.62f, header * 0.42f);
        CasinoSigns.Draw(drawList, CasinoSign.Scratch, signCenter, signHeight, theme.Accent, 1f);
        var motifExtent = header * 0.16f;
        var bob = MathF.Sin(phase * MotifBobSpeed) * ticket.Height * MotifBobShare * 0.25f;
        var ink = ImGui.GetColorU32(theme.Accent);
        DrawMotif(drawList, theme.Motif, new Vector2(ticket.Min.X + ticket.Width * 0.11f, signCenter.Y + bob),
            motifExtent, ink);
        DrawMotif(drawList, theme.Motif, new Vector2(ticket.Max.X - ticket.Width * 0.11f, signCenter.Y - bob),
            motifExtent, ink);
        var price = NumberText.Compact(ScratchRules.Prices[Math.Clamp(tier, 0, ScratchRules.TierCount - 1)]);
        var priceSize = CurrencyGlyph.MeasureAmount(price, TextStyles.FootnoteEmphasized);
        CurrencyGlyph.DrawAmount(drawList, new Vector2(ticket.Center.X - priceSize.X * 0.5f,
            ticket.Min.Y + header * 0.72f), price, CurrencyKind.Chips, CasinoColors.Money, TextStyles.FootnoteEmphasized);
    }

    public static void Foil(ImDrawListPtr drawList, Vector2 cellMin, Vector2 cellMax, float rounding, int tier,
        float amount, float shimmer, float scale)
    {
        var theme = For(tier);
        var alpha = Math.Clamp(amount, 0f, 1f);
        Squircle.FillVerticalGradient(drawList, cellMin, cellMax, rounding,
            ImGui.GetColorU32(theme.FoilTop with { W = alpha }), ImGui.GetColorU32(theme.FoilBottom with { W = alpha }));
        var size = cellMax.X - cellMin.X;
        var sheen = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, (0.06f + 0.12f * shimmer) * alpha));
        drawList.AddLine(new Vector2(cellMin.X + size * 0.2f, cellMax.Y - size * 0.15f),
            new Vector2(cellMax.X - size * 0.15f, cellMin.Y + size * 0.2f), sheen, 3f * scale);
        DrawMotif(drawList, theme.Motif, (cellMin + cellMax) * 0.5f, size * 0.16f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f * alpha)));
    }

    public static void Stamp(ImDrawListPtr drawList, Rect ticket, string label, float progress, float scale)
    {
        if (progress <= 0f)
        {
            return;
        }

        var header = ticket.Height - ticket.Width;
        var pop = 1f + 0.35f * (1f - MathF.Min(1f, progress * 3f));
        var style = TextStyles.Title2;
        var textScale = style.Scale * pop;
        var size = Typography.Measure(label, textScale, style.Weight);
        if (size.X > ticket.Width * StampWidthShare)
        {
            return;
        }

        var center = new Vector2(ticket.Max.X - ticket.Width * 0.2f, ticket.Min.Y + header * 0.92f);
        var padding = new Vector2(8f, 4f) * scale;
        var half = size * 0.5f + padding;
        var alpha = MathF.Min(1f, progress * 4f);
        var firstVertex = drawList.VtxBuffer.Size;
        Squircle.Fill(drawList, center - half, center + half, half.Y,
            ImGui.GetColorU32(CasinoColors.Money with { W = 0.92f * alpha }));
        Typography.Draw(drawList, center - size * 0.5f, label, new Vector4(0.10f, 0.06f, 0.02f, alpha), textScale,
            style.Weight);
        Rotate(drawList, firstVertex, center, StampTilt);
    }

    public static void DrawMotif(ImDrawListPtr drawList, ScratchMotif motif, Vector2 center, float extent, uint ink)
    {
        switch (motif)
        {
            case ScratchMotif.Star:
                SlotsSymbolArt.DrawSparkle(drawList, center, extent, ink);
                break;
            case ScratchMotif.Clover:
                DrawClover(drawList, center, extent, ink);
                break;
            case ScratchMotif.Heart:
                DrawHeart(drawList, center, extent, ink);
                break;
            case ScratchMotif.Coin:
                DrawCoin(drawList, center, extent, ink);
                break;
            default:
                DrawGem(drawList, center, extent, ink);
                break;
        }
    }

    private static ScratchTheme Theme(Vector4 accent, Vector4 paper, ScratchMotif motif) =>
        new(accent, paper, Vector4.Lerp(Silver, accent, 0.18f), Vector4.Lerp(SilverShade, accent, 0.22f), motif);

    private static void DrawPattern(ImDrawListPtr drawList, Rect ticket, in ScratchTheme theme, float scale)
    {
        var ink = ImGui.GetColorU32(theme.Accent with { W = PatternAlpha });
        var extent = ticket.Width * 0.08f;
        for (var spotIndex = 0; spotIndex < PatternSpots.Length; spotIndex++)
        {
            var spot = PatternSpots[spotIndex];
            DrawMotif(drawList, theme.Motif,
                new Vector2(ticket.Min.X + ticket.Width * spot.X, ticket.Min.Y + ticket.Height * spot.Y), extent, ink);
        }

        var inner = ticket.Inset(4f * scale);
        Squircle.Stroke(drawList, inner.Min, inner.Max, Metrics.Radius.Grouped * scale * 0.8f,
            ImGui.GetColorU32(theme.Accent with { W = 0.14f }), MathF.Max(1f, scale));
    }

    private static void DrawClover(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var leaf = extent * 0.42f;
        drawList.AddCircleFilled(center + new Vector2(0f, -leaf), leaf, ink, 16);
        drawList.AddCircleFilled(center + new Vector2(leaf, 0f), leaf, ink, 16);
        drawList.AddCircleFilled(center + new Vector2(0f, leaf), leaf, ink, 16);
        drawList.AddCircleFilled(center + new Vector2(-leaf, 0f), leaf, ink, 16);
        drawList.AddLine(center, center + new Vector2(extent * 0.5f, extent * 1.05f), ink, MathF.Max(1f, extent * 0.16f));
    }

    private static void DrawHeart(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        var lobe = extent * 0.5f;
        drawList.AddCircleFilled(center + new Vector2(-lobe * 0.95f, -lobe * 0.35f), lobe, ink, 16);
        drawList.AddCircleFilled(center + new Vector2(lobe * 0.95f, -lobe * 0.35f), lobe, ink, 16);
        drawList.AddTriangleFilled(center + new Vector2(-extent * 0.97f, -lobe * 0.05f),
            center + new Vector2(extent * 0.97f, -lobe * 0.05f), center + new Vector2(0f, extent), ink);
    }

    private static void DrawCoin(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.AddCircleFilled(center, extent, ink, 20);
        drawList.AddCircle(center, extent * 0.72f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), 20,
            MathF.Max(1f, extent * 0.12f));
        SlotsSymbolArt.DrawSparkle(drawList, center, extent * 0.45f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.6f)));
    }

    private static void DrawGem(ImDrawListPtr drawList, Vector2 center, float extent, uint ink)
    {
        drawList.PathClear();
        drawList.PathLineTo(center + new Vector2(-extent, -extent * 0.3f));
        drawList.PathLineTo(center + new Vector2(-extent * 0.55f, -extent * 0.8f));
        drawList.PathLineTo(center + new Vector2(extent * 0.55f, -extent * 0.8f));
        drawList.PathLineTo(center + new Vector2(extent, -extent * 0.3f));
        drawList.PathLineTo(center + new Vector2(0f, extent));
        drawList.PathFillConvex(ink);
        drawList.AddLine(center + new Vector2(-extent, -extent * 0.3f), center + new Vector2(extent, -extent * 0.3f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f)), MathF.Max(1f, extent * 0.1f));
    }

    private static void Rotate(ImDrawListPtr drawList, int firstVertex, Vector2 pivot, float angle)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        var vertices = drawList.VtxBuffer.AsSpan();
        for (var vertexIndex = firstVertex; vertexIndex < vertices.Length; vertexIndex++)
        {
            ref var vertex = ref vertices[vertexIndex];
            var offset = vertex.Pos - pivot;
            vertex.Pos = new Vector2(pivot.X + offset.X * cosine - offset.Y * sine,
                pivot.Y + offset.X * sine + offset.Y * cosine);
        }
    }
}
