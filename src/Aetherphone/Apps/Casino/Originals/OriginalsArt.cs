using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal static class OriginalsArt
{
    public static readonly Vector4 Gem = new(0.30f, 0.90f, 0.62f, 1f);
    public static readonly Vector4 GemHighlight = new(0.72f, 1f, 0.86f, 1f);
    public static readonly Vector4 Boom = new(0.96f, 0.36f, 0.38f, 1f);
    public static readonly Vector4 TileTop = new(0.21f, 0.18f, 0.34f, 1f);
    public static readonly Vector4 TileBottom = new(0.13f, 0.11f, 0.22f, 1f);
    public static readonly Vector4 TileOpen = new(0.07f, 0.06f, 0.12f, 1f);
    public static readonly Vector4 Board = new(0.05f, 0.04f, 0.10f, 0.72f);

    private const int CircleSegments = 24;

    public static void Panel(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Board));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(CasinoColors.LightA with { W = 0.28f }), MathF.Max(1f, scale));
    }

    public static void HiddenTile(ImDrawListPtr drawList, Rect rect, float rounding, bool hovered, float lift,
        float scale)
    {
        var raised = new Rect(rect.Min - new Vector2(0f, lift), rect.Max - new Vector2(0f, lift));
        var shadow = new Vector2(0f, 2f * scale);
        Squircle.Fill(drawList, rect.Min + shadow, rect.Max + shadow, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)));
        Squircle.FillVerticalGradient(drawList, raised.Min, raised.Max, rounding,
            ImGui.GetColorU32(hovered ? Palette.Mix(TileTop, CasinoColors.LightB, 0.18f) : TileTop),
            ImGui.GetColorU32(TileBottom));
        Material.Sheen(drawList, raised.Min, raised.Max, rounding,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, hovered ? 0.22f : 0.12f)), 1f * scale, 1f * scale);
    }

    public static void OpenTile(ImDrawListPtr drawList, Rect rect, float rounding, Vector4 glow, float glowAlpha)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(TileOpen));
        if (glowAlpha <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(glow with { W = glowAlpha }));
    }

    public static void DrawGem(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var top = center + new Vector2(0f, -radius);
        var right = center + new Vector2(radius * 0.86f, -radius * 0.25f);
        var bottom = center + new Vector2(0f, radius);
        var left = center + new Vector2(-radius * 0.86f, -radius * 0.25f);
        drawList.AddCircleFilled(center, radius * 1.35f, ImGui.GetColorU32(Gem with { W = 0.16f * alpha }),
            CircleSegments);
        drawList.AddQuadFilled(top, right, bottom, left, ImGui.GetColorU32(Gem with { W = alpha }));
        drawList.AddTriangleFilled(top, right, center + new Vector2(0f, -radius * 0.1f),
            ImGui.GetColorU32(GemHighlight with { W = 0.85f * alpha }));
        drawList.AddTriangleFilled(left, center + new Vector2(0f, -radius * 0.1f), bottom,
            ImGui.GetColorU32(Palette.Mix(Gem, TileOpen, 0.25f) with { W = alpha }));
    }

    public static void DrawMine(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 body, float alpha)
    {
        var color = ImGui.GetColorU32(body with { W = body.W * alpha });
        var spike = radius * 1.35f;
        for (var spokeIndex = 0; spokeIndex < 4; spokeIndex++)
        {
            var angle = spokeIndex * MathF.PI * 0.25f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * spike;
            drawList.AddLine(center - direction, center + direction, color, radius * 0.22f);
        }

        drawList.AddCircleFilled(center, radius, color, CircleSegments);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.32f, radius * 0.32f), radius * 0.24f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f * alpha)), 12);
    }

    public static int PlayingCard(int wireCard) =>
        PlayingCards.Encode(OriginalsRules.HiLoRank(wireCard) - 1, OriginalsRules.HiLoSuit(wireCard));

    public static void Card(ImDrawListPtr drawList, Vector2 center, float width, int wireCard, bool faceUp,
        float squash, float scale, bool shadowed = true)
    {
        var height = PlayingCards.HeightFor(width);
        var half = new Vector2(width * MathF.Max(0.02f, squash) * 0.5f, height * 0.5f);
        var rect = new Rect(center - half, center + half);
        var rounding = MathF.Min(PlayingCards.RoundingFor(width), rect.Width * 0.5f);
        if (faceUp && OriginalsRules.IsHiLoCard(wireCard))
        {
            PlayingCards.DrawFace(drawList, rect, PlayingCard(wireCard), rounding, scale, shadowed);
            return;
        }

        PlayingCards.DrawBack(drawList, rect, rounding, scale, shadowed);
    }
}
