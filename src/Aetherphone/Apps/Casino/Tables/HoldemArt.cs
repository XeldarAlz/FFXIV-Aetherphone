using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemArt
{
    public const float RailWidth = 9f;

    private const int LampLayers = 5;
    private const float LampAlpha = 0.05f;
    private const float SignAlpha = 0.16f;
    private const float StitchInset = 8f;

    private static readonly Vector4 RailHighlight = new(0.36f, 0.22f, 0.14f, 1f);
    private static readonly Vector4 Lamp = new(1f, 0.92f, 0.74f, 1f);
    private static readonly Vector4 Stitch = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 ButtonFace = new(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Vector4 ButtonInk = new(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Vector4 Shade = new(0f, 0f, 0f, 0.45f);
    private static readonly Vector4 PracticeFeltTop = new(0.15f, 0.17f, 0.19f, 1f);
    private static readonly Vector4 PracticeFeltBottom = new(0.07f, 0.08f, 0.09f, 1f);
    private static readonly Vector4 EdgeFelt = new(0.016f, 0.075f, 0.055f, 1f);
    private static readonly string PlusGlyph = IconGlyph.Of(Dalamud.Interface.FontAwesomeIcon.Plus);

    public static void DrawFeltStage(ImDrawListPtr drawList, in Rect full, in Rect ring, bool practice, float phase,
        float scale)
    {
        if (full.Width <= 0f || full.Height <= 0f)
        {
            return;
        }

        drawList.PushClipRect(full.Min, full.Max, true);
        var top = practice ? PracticeFeltTop : CasinoColors.FeltTop;
        var bottom = practice ? PracticeFeltBottom : EdgeFelt;
        drawList.AddRectFilledMultiColor(full.Min, full.Max, ImGui.GetColorU32(top), ImGui.GetColorU32(top),
            ImGui.GetColorU32(bottom), ImGui.GetColorU32(bottom));
        var pool = ring.Center;
        var span = MathF.Max(ring.Width, ring.Height) * 0.62f;
        var breath = 0.92f + 0.08f * MathF.Sin(phase * 0.7f);
        for (var layer = LampLayers; layer >= 1; layer--)
        {
            drawList.AddCircleFilled(pool, span * layer / LampLayers,
                ImGui.GetColorU32(Lamp with { W = LampAlpha * breath * (LampLayers - layer + 1) / LampLayers }), 48);
        }

        var edge = RailWidth * scale;
        drawList.AddRectFilledMultiColor(full.Min, new Vector2(full.Min.X + edge * 3f, full.Max.Y),
            ImGui.GetColorU32(Shade), ImGui.GetColorU32(Shade with { W = 0f }), ImGui.GetColorU32(Shade with { W = 0f }),
            ImGui.GetColorU32(Shade));
        drawList.AddRectFilledMultiColor(new Vector2(full.Max.X - edge * 3f, full.Min.Y), full.Max,
            ImGui.GetColorU32(Shade with { W = 0f }), ImGui.GetColorU32(Shade), ImGui.GetColorU32(Shade),
            ImGui.GetColorU32(Shade with { W = 0f }));
        var radii = new Vector2(ring.Width * 0.5f + StitchInset * 4f * scale, ring.Height * 0.5f + StitchInset * 4f * scale);
        Games.Framework.Shapes.StrokeEllipse(drawList, ring.Center, radii, ImGui.GetColorU32(RailHighlight with { W = 0.55f }),
            edge, 72);
        Games.Framework.Shapes.StrokeEllipse(drawList, ring.Center, radii - new Vector2(edge, edge),
            ImGui.GetColorU32(Stitch), Metrics.Stroke.Thin * scale, 72);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Holdem, ring.Width * 0.5f, 24f * scale);
        CasinoSigns.Draw(drawList, CasinoSign.Holdem, new Vector2(ring.Center.X, ring.Min.Y + ring.Height * 0.30f),
            signHeight, (practice ? CasinoColors.Practice : CasinoColors.MoneyHighlight) with { W = SignAlpha }, 0.35f);
        drawList.PopClipRect();
    }

    public static float StateLine(ImDrawListPtr drawList, Vector2 center, string text, float maxWidth, Vector4 ink)
    {
        if (text.Length == 0)
        {
            return 0f;
        }

        var style = TextStyles.Title2;
        var fitted = Typography.FitText(text, maxWidth, style);
        var size = Typography.Measure(fitted, style);
        var origin = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
        var shadow = new Vector2(0f, MathF.Max(1f, size.Y * 0.06f));
        Typography.Draw(drawList, origin + shadow, fitted, new Vector4(0f, 0f, 0f, 0.65f), style);
        Typography.Draw(drawList, origin, fitted, ink, style);
        return size.Y;
    }

    public static void DrawSitSpot(ImDrawListPtr drawList, Vector2 center, float radius, string label, bool hovered,
        float scale)
    {
        var fill = hovered ? CasinoColors.LightA with { W = 0.28f } : new Vector4(0f, 0f, 0f, 0.38f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 40);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.LightA with { W = hovered ? 1f : 0.7f }), 40,
            MathF.Max(1.5f, 2f * scale));
        PhoneIcon.Draw(drawList, new Vector2(center.X, center.Y - radius * 0.18f), PlusGlyph, CasinoColors.InkTitle,
            radius * 0.62f);
        var style = TextStyles.Footnote;
        var fitted = Typography.FitText(label, radius * 1.8f, style);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + radius * 0.42f), fitted,
            CasinoColors.InkTitle, style);
    }

    public static void DrawCard(ImDrawListPtr drawList, Vector2 center, float width, int card, bool faceUp,
        float squash, float scale, float alpha = 1f, bool highlight = false)
    {
        var height = PlayingCards.HeightFor(width);
        var half = new Vector2(width * 0.5f * MathF.Max(0.02f, squash), height * 0.5f);
        var rect = new Rect(center - half, center + half);
        var rounding = PlayingCards.RoundingFor(width);
        if (highlight)
        {
            var glow = new Vector2(3f * scale, 3f * scale);
            Squircle.Fill(drawList, rect.Min - glow, rect.Max + glow, rounding + glow.X,
                ImGui.GetColorU32(CasinoColors.Money with { W = 0.55f }));
        }

        if (faceUp && PlayingCards.IsCard(card))
        {
            PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
        }
        else
        {
            PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
        }

        if (alpha < 1f)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
                ImGui.GetColorU32(new Vector4(0.02f, 0.02f, 0.04f, 1f - alpha)));
        }
    }

    public static void DrawDealerButton(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var radius = 8f * scale;
        drawList.AddCircleFilled(center + new Vector2(0f, 1.5f * scale), radius, ImGui.GetColorU32(Shade), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ButtonFace), 20);
        drawList.AddCircle(center, radius * 0.72f, ImGui.GetColorU32(ButtonInk with { W = 0.25f }), 20,
            MathF.Max(1f, scale));
        Typography.DrawCentered(drawList, center, "D", ButtonInk, TextStyles.Caption2);
    }

    public static void DrawTag(ImDrawListPtr drawList, Vector2 center, string label, Vector4 tint, float scale)
    {
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var half = new Vector2(size.X * 0.5f + 5f * scale, size.Y * 0.5f + 1.5f * scale);
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(tint with { W = 0.22f }));
        Squircle.Stroke(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(tint with { W = 0.75f }),
            MathF.Max(1f, scale));
        Typography.DrawCentered(drawList, center, label, tint, TextStyles.FootnoteEmphasized);
    }

    public static void DrawBar(ImDrawListPtr drawList, in Rect rect, float fraction, Vector4 fill)
    {
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
        var width = rect.Width * Math.Clamp(fraction, 0f, 1f);
        if (width < rect.Height)
        {
            return;
        }

        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Min.X + width, rect.Max.Y), radius,
            ImGui.GetColorU32(fill));
    }

    public static void DrawAmount(ImDrawListPtr drawList, Vector2 center, string text, Vector4 ink, bool practice,
        float scale, in TextStyle style)
    {
        var size = Typography.Measure(text, style);
        var lineHeight = Typography.LineHeight(style);
        var glyph = lineHeight * CurrencyGlyph.GlyphFraction;
        var width = size.X + CurrencyGlyph.Reserve(lineHeight);
        var half = new Vector2(width * 0.5f + 6f * scale, lineHeight * 0.5f + 1f * scale);
        Squircle.Fill(drawList, center - half, center + half, half.Y, ImGui.GetColorU32(Shade));
        var left = center.X - width * 0.5f;
        var glyphCenter = new Vector2(left + glyph * 0.5f, center.Y);
        if (practice)
        {
            ChipStack.DrawPractice(drawList, glyphCenter, glyph * 0.5f);
        }
        else
        {
            CurrencyGlyph.Draw(drawList, CurrencyKind.Chips, glyphCenter, glyph);
        }

        Typography.Draw(drawList, new Vector2(left + CurrencyGlyph.Reserve(lineHeight), center.Y - size.Y * 0.5f), text,
            practice ? CasinoColors.Practice : ink, style);
    }

    public static void DrawBubble(ImDrawListPtr drawList, Vector2 anchor, string glyph, float alpha, float scale)
    {
        var radius = 15f * scale;
        var center = new Vector2(anchor.X, anchor.Y - radius);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.08f, 0.06f, 0.14f, 0.92f * alpha)), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.LightA with { W = 0.8f * alpha }), 28,
            MathF.Max(1f, 1.4f * scale));
        PhoneIcon.Draw(drawList, center, glyph, CasinoColors.InkTitle with { W = alpha }, radius * 1.05f);
    }
}
