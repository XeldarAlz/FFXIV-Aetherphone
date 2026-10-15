using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Live;

internal static class ChallengeCard
{
    private const float Pad = 12f;
    private const float Tile = 46f;
    private const float TextGap = 12f;
    private const float LineGap = 3f;
    private const float ChevronSlot = 16f;
    private const float DotGap = 5f;
    private const float WashAlpha = 0.18f;
    private const float RimAlpha = 0.45f;

    public static float Height(in LiveChallengeView view, bool running, float width, float scale)
    {
        var textWidth = TextWidth(width, scale);
        var text = Typography.LineHeight(TextStyles.FootnoteEmphasized)
                   + Line(view.Title, TextStyles.Headline, textWidth, scale)
                   + Line(view.Goal, TextStyles.Footnote, textWidth, scale)
                   + Line(view.Stake, TextStyles.Footnote, textWidth, scale)
                   + Line(view.Prize, TextStyles.SubheadlineEmphasized, textWidth, scale);
        if (running)
        {
            text += Line(view.Leader, TextStyles.Footnote, textWidth, scale)
                    + Line(view.Yours, TextStyles.FootnoteEmphasized, textWidth, scale);
        }

        return Pad * 2f * scale + MathF.Max(Tile * scale, text);
    }

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, Rect rect, in LiveChallengeView view, string clock,
        bool running, bool interactive, float scale)
    {
        var hovered = CasinoArt.PressCard(ImGui.GetID("challenge"), rect.Min, rect.Max, out var min, out var max,
            interactive);
        var radius = Metrics.Radius.Lg * scale;
        ui.Card(drawList, min, max, radius);
        if (running)
        {
            Squircle.FillVerticalGradient(drawList, min, max, radius, Color(CasinoColors.Money, WashAlpha),
                Color(CasinoColors.Money, 0f));
            Squircle.Stroke(drawList, min, max, radius, Color(CasinoColors.Money, RimAlpha), MathF.Max(1f, scale));
        }

        var pad = Pad * scale;
        var tile = Tile * scale;
        var tileCenter = new Vector2(min.X + pad + tile * 0.5f, min.Y + pad + tile * 0.5f);
        CasinoArt.GameTile(drawList, view.GameId, tileCenter, tile);
        CasinoArt.Chevron(drawList, new Vector2(max.X - pad - ChevronSlot * 0.5f * scale, (min.Y + max.Y) * 0.5f),
            ui.MutedInk);
        var left = tileCenter.X + tile * 0.5f + TextGap * scale;
        var textWidth = TextWidth(rect.Width, scale);
        var top = DrawClock(drawList, ui, clock, running, left, min.Y + pad, textWidth, scale);
        top = DrawLine(drawList, view.Title, TextStyles.Headline, ui.TitleInk, left, top, textWidth, scale);
        top = DrawLine(drawList, view.Goal, TextStyles.Footnote, ui.BodyInk, left, top, textWidth, scale);
        top = DrawLine(drawList, view.Stake, TextStyles.Footnote, ui.MutedInk, left, top, textWidth, scale);
        if (running)
        {
            top = DrawLine(drawList, view.Leader, TextStyles.Footnote, ui.TitleInk, left, top, textWidth, scale);
            top = DrawLine(drawList, view.Yours, TextStyles.FootnoteEmphasized, ui.Accent, left, top, textWidth,
                scale);
        }

        DrawLine(drawList, view.Prize, TextStyles.SubheadlineEmphasized, CasinoColors.Money, left, top, textWidth,
            scale);
        return interactive && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static float TextWidth(float width, float scale) =>
        MathF.Max(1f, width - (Pad * 2f + Tile + TextGap * 1.5f + ChevronSlot) * scale);

    private static float Line(string text, in TextStyle style, float width, float scale) =>
        text.Length == 0 ? 0f : LineGap * scale + PitText.Height(text, style, width);

    private static float DrawLine(ImDrawListPtr drawList, string text, in TextStyle style, Vector4 ink, float left,
        float top, float width, float scale)
    {
        if (text.Length == 0)
        {
            return top;
        }

        return PitText.Draw(drawList, text, style, ink, left, top + LineGap * scale, width, false);
    }

    private static float DrawClock(ImDrawListPtr drawList, AppSkin ui, string clock, bool running, float left,
        float top, float width, float scale)
    {
        var style = TextStyles.FootnoteEmphasized;
        var lineHeight = Typography.LineHeight(style);
        var textLeft = left;
        if (running)
        {
            CasinoArt.LiveDot(drawList, new Vector2(left + CasinoArt.LiveDotRadius * scale, top + lineHeight * 0.5f),
                scale, CasinoColors.LightA, true);
            textLeft += (CasinoArt.LiveDotRadius * 2f + DotGap) * scale;
        }

        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(clock, MathF.Max(1f, left + width - textLeft), style),
            running ? CasinoColors.LightA : ui.MutedInk, style);
        return top + lineHeight;
    }

    private static uint Color(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = alpha });
}
