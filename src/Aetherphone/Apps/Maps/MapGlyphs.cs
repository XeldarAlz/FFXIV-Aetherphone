using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal readonly record struct MapSearchHit(string Prefix, string Match, string Suffix, bool InName)
{
    public bool Active => Match.Length > 0;
}

internal static class MapGlyphs
{
    private const float HighlightBleed = 8f;
    private const float MatchBleed = 1f;
    private const float MatchRounding = 3f;
    private const float MatchWashAlpha = 0.16f;
    public static readonly Vector4 FavoriteStar = new(1f, 0.78f, 0.25f, 1f);
    private static readonly Vector4[] ExpansionTints =
    {
        new(0.20f, 0.42f, 0.82f, 1f),
        new(0.27f, 0.56f, 0.78f, 1f),
        new(0.76f, 0.22f, 0.25f, 1f),
        new(0.43f, 0.31f, 0.72f, 1f),
        new(0.16f, 0.55f, 0.56f, 1f),
        new(0.84f, 0.53f, 0.16f, 1f),
    };

    public static Vector4 ExpansionTint(byte order, Vector4 fallback) =>
        order < ExpansionTints.Length ? ExpansionTints[order] : fallback;

    public static void Highlight(ImDrawListPtr drawList, Rect row, Vector4 color, float verticalInset, float scale)
    {
        var min = new Vector2(row.Min.X - HighlightBleed * scale, row.Min.Y + verticalInset * scale);
        var max = new Vector2(row.Max.X + HighlightBleed * scale, row.Max.Y - verticalInset * scale);
        Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(color));
    }

    public static void ChevronRight(ImDrawListPtr drawList, Vector2 tip, float size, float thickness, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    public static void Line(ImDrawListPtr drawList, Vector2 position, string text, float maxWidth, in TextStyle style,
        Vector4 ink, Vector4 matchInk, in MapSearchHit hit, bool useHit)
    {
        if (!useHit || !hit.Active)
        {
            Typography.Draw(drawList, position, Typography.FitText(text, maxWidth, style), ink, style);
            return;
        }

        var prefixWidth = Typography.Measure(hit.Prefix, style).X;
        var matchWidth = Typography.Measure(hit.Match, style).X;
        var suffixWidth = Typography.Measure(hit.Suffix, style).X;
        if (prefixWidth + matchWidth + suffixWidth > maxWidth)
        {
            Typography.Draw(drawList, position, Typography.FitText(text, maxWidth, style), ink, style);
            return;
        }

        Typography.Draw(drawList, position, hit.Prefix, ink, style);
        var matchPosition = new Vector2(position.X + prefixWidth, position.Y);
        var lineHeight = Typography.Measure(hit.Match, style).Y;
        var scale = UiScale.Current;
        drawList.AddRectFilled(new Vector2(matchPosition.X - MatchBleed * scale, matchPosition.Y),
            new Vector2(matchPosition.X + matchWidth + MatchBleed * scale, matchPosition.Y + lineHeight),
            ImGui.GetColorU32(matchInk with { W = MatchWashAlpha }), MatchRounding * scale);
        Typography.Draw(drawList, matchPosition, hit.Match, matchInk, style);
        Typography.Draw(drawList, new Vector2(matchPosition.X + matchWidth, position.Y), hit.Suffix, ink, style);
    }
}
