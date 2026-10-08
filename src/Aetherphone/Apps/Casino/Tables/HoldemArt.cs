using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemArt
{
    public const float RailWidth = 9f;

    private const float SignAlpha = 0.16f;
    private const float StitchInset = 8f;

    private static readonly Vector4 RailHighlight = new(0.36f, 0.22f, 0.14f, 1f);
    private static readonly Vector4 Stitch = new(1f, 1f, 1f, 0.07f);
    private static readonly Vector4 ButtonFace = new(0.97f, 0.96f, 0.92f, 1f);
    private static readonly Vector4 ButtonInk = new(0.12f, 0.12f, 0.14f, 1f);
    private static readonly Vector4 Shade = new(0f, 0f, 0f, 0.45f);

    public static void DrawFeltStage(ImDrawListPtr drawList, in Rect full, in Rect ring, bool practice, float phase,
        float scale)
    {
        if (full.Width <= 0f || full.Height <= 0f)
        {
            return;
        }

        FeltTable.DrawCloth(drawList, full, ring.Center, false, practice, scale);
        drawList.PushClipRect(full.Min, full.Max, true);
        var edge = RailWidth * scale;
        var breath = 0.85f + 0.15f * MathF.Sin(phase * 0.7f);
        var radii = new Vector2(ring.Width * 0.5f + StitchInset * 4f * scale, ring.Height * 0.5f + StitchInset * 4f * scale);
        Games.Framework.Shapes.StrokeEllipse(drawList, ring.Center, radii,
            ImGui.GetColorU32(RailHighlight with { W = 0.55f * breath }), edge, 72);
        Games.Framework.Shapes.StrokeEllipse(drawList, ring.Center, radii - new Vector2(edge, edge),
            ImGui.GetColorU32(Stitch), Metrics.Stroke.Thin * scale, 72);
        var signHeight = CasinoSigns.HeightToFit(CasinoSign.Holdem, ring.Width * 0.5f, 24f * scale);
        CasinoSigns.Draw(drawList, CasinoSign.Holdem, new Vector2(ring.Center.X, ring.Min.Y + ring.Height * 0.30f),
            signHeight, (practice ? CasinoColors.Practice : CasinoColors.MoneyHighlight) with { W = SignAlpha }, 0.35f);
        drawList.PopClipRect();
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
