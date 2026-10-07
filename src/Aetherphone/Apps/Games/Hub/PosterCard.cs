using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Hub;

internal struct ClampedLines
{
    private string? source;
    private float width;
    private float lineHeight;
    private string? first;
    private string? second;

    public readonly string First => first ?? string.Empty;

    public readonly string Second => second ?? string.Empty;

    public readonly int Count => string.IsNullOrEmpty(second) ? string.IsNullOrEmpty(first) ? 0 : 1 : 2;

    public void Update(string text, float maxWidth, in TextStyle style)
    {
        var height = Typography.LineHeight(style);
        if (ReferenceEquals(text, source) && maxWidth == width && height == lineHeight)
        {
            return;
        }

        source = text;
        width = maxWidth;
        lineHeight = height;
        first = string.Empty;
        second = string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        var lines = Typography.WrapText(text, style, maxWidth);
        if (lines.Length == 0)
        {
            return;
        }

        first = lines[0];
        if (lines.Length == 1)
        {
            return;
        }

        second = lines.Length == 2
            ? lines[1]
            : Typography.FitText(Remainder(text, lines[0], lines[1]), maxWidth, style);
    }

    public static string Remainder(string text, string firstLine, string fallback)
    {
        if (firstLine.Length == 0 || !text.StartsWith(firstLine, StringComparison.Ordinal))
        {
            return fallback;
        }

        return text.AsSpan(firstLine.Length).TrimStart().ToString();
    }
}

internal sealed class PosterCard
{
    public const float NightSky = 0.75f;
    public const float ResumeWidth = 156f;
    public const float ResumeHeight = 100f;
    public const float ResumeGap = 12f;
    public const float EditorialHeight = 172f;
    public const float EditorialGap = 12f;
    public const int EditorialSlots = 8;
    private const float EditorialFraction = 0.84f;
    private const float ResumeInset = 10f;
    private const float ResumeIcon = 40f;
    private const float ResumeCapsule = 20f;
    private const float ResumeScrim = 0.55f;
    private const float ProgressHeight = 4f;
    private const float ProgressGap = 6f;
    private const float EditorialPad = 16f;
    private const float EditorialIcon = 56f;
    private const float EditorialScrim = 0.6f;
    private const float EditorialTopScrim = 0.32f;
    private const float TopScrimTo = 0.5f;
    private const float ScrimFrom = 0.4f;
    private const float CapsulePadFraction = 0.4f;
    private const float CapsuleGap = 4f;
    private const float CapsuleGlyphFraction = 0.5f;
    private const float CapsuleGlyphGap = 4f;
    private const float CapsuleFill = 0.32f;
    private const float RankFill = 0.55f;
    private const float TrackAlpha = 0.24f;
    private const float FillAlpha = 0.95f;
    private const float EyebrowAlpha = 0.78f;
    private const float HookAlpha = 0.86f;
    private const float ButtonMinWidth = 64f;

    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private readonly StageBackdrop backdrop = new();
    private readonly ClampedLines[] hooks = new ClampedLines[EditorialSlots];

    public static Backdrop PosterBackdrop(Backdrop preset) =>
        preset is Backdrop.Paper or Backdrop.Meadow ? Backdrop.Nebula : preset;

    public static float EditorialWidth(float width) => MathF.Round(width * EditorialFraction);

    public static float ResumeTileHeight(float scale) =>
        ResumeHeight * scale + HubMetrics.CaptionGap * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized)
        + Typography.LineHeight(TextStyles.Footnote);

    public static float CapsuleWidth(string label, string glyph, float height)
    {
        var glyphSpan = glyph.Length > 0 ? height * CapsuleGlyphFraction + CapsuleGlyphGap * UiScale.Current : 0f;
        return Typography.Measure(label, TextStyles.FootnoteEmphasized).X + glyphSpan
               + height * CapsulePadFraction * 2f;
    }

    public static float DrawCapsule(ImDrawListPtr drawList, Vector2 anchor, bool alignRight, string label,
        string glyph, Vector4 glyphInk, float height, float fillAlpha)
    {
        var width = CapsuleWidth(label, glyph, height);
        var left = alignRight ? anchor.X - width : anchor.X;
        var min = new Vector2(left, anchor.Y);
        var max = new Vector2(left + width, anchor.Y + height);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, fillAlpha)));
        var centerY = anchor.Y + height * 0.5f;
        var textLeft = left + height * CapsulePadFraction;
        if (glyph.Length > 0)
        {
            var glyphSize = height * CapsuleGlyphFraction;
            PhoneIcon.Draw(drawList, new Vector2(textLeft + glyphSize * 0.5f, centerY), glyph, glyphInk, glyphSize);
            textLeft += glyphSize + CapsuleGlyphGap * UiScale.Current;
        }

        var textHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, centerY - textHeight * 0.5f), label, White,
            TextStyles.FootnoteEmphasized);
        return width;
    }

    public void DrawBackground(ImDrawListPtr drawList, Rect card, Backdrop preset, Vector4 accent, float topScrim,
        float bottomScrim, float scale)
    {
        backdrop.Set(PosterBackdrop(preset));
        backdrop.SetSky(NightSky);
        backdrop.Draw(drawList, card, accent, scale);
        drawList.PushClipRect(card.Min, card.Max, true);
        if (topScrim > 0f)
        {
            var shade = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, topScrim));
            drawList.AddRectFilledMultiColor(card.Min,
                new Vector2(card.Max.X, card.Min.Y + card.Height * TopScrimTo), shade, shade, 0u, 0u);
        }

        LivePreview.Scrim(drawList, card, ScrimFrom, bottomScrim);
        drawList.PopClipRect();
    }

    public bool DrawResume(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex, Backdrop preset,
        Vector2 topLeft, bool daily, bool interactive, in HubGround ground, out Rect source)
    {
        var scale = UiScale.Current;
        var tileId = library.TileIds[entryIndex];
        var rest = new Rect(topLeft, topLeft + new Vector2(ResumeWidth, ResumeHeight) * scale);
        var tileMax = new Vector2(rest.Max.X, topLeft.Y + ResumeTileHeight(scale));
        var hovered = interactive && UiInteract.Hover(topLeft, tileMax);
        var card = rest.Scaled(Pose(tileId, hovered));
        source = card;
        var accent = library.Accent(entryIndex);
        DrawBackground(drawList, card, preset, accent, 0f, ResumeScrim, scale);
        var unit = scale * card.Width / rest.Width;
        var inset = ResumeInset * unit;
        var iconMin = card.Min + new Vector2(inset);
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], accent, iconMin,
            iconMin + new Vector2(ResumeIcon * unit), IconAppearance.Default, true);
        var rank = library.RankLabel(entryIndex);
        var capsuleHeight = ResumeCapsule * unit;
        if (rank.Length > 0)
        {
            DrawCapsule(drawList, new Vector2(card.Max.X - inset, card.Min.Y + inset), true, rank, string.Empty,
                White, capsuleHeight, RankFill);
        }

        var progress = library.Progress(entryIndex);
        var bottom = card.Max.Y - inset;
        if (progress >= 0f)
        {
            DrawProgress(drawList, new Rect(new Vector2(card.Min.X + inset, bottom - ProgressHeight * unit),
                new Vector2(card.Max.X - inset, bottom)), progress);
            bottom -= (ProgressHeight + ProgressGap) * unit;
        }

        if (daily)
        {
            DrawCapsule(drawList, new Vector2(card.Min.X + inset, bottom - capsuleHeight), false,
                Loc.T(L.GamesHub.Today), string.Empty, White, capsuleHeight, CapsuleFill);
        }

        LivePreview.CoverCorners(drawList, card, HubMetrics.SmallCardRadius * unit, ground, scale);
        DrawCaption(drawList, ui, library, entryIndex, rest, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(topLeft, tileMax, hovered);
    }

    public bool DrawEditorial(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex, int slot,
        Backdrop preset, Vector2 topLeft, float width, bool interactive, in HubGround ground, out Rect source)
    {
        var scale = UiScale.Current;
        var tileId = library.TileIds[entryIndex];
        var rest = new Rect(topLeft, new Vector2(topLeft.X + width, topLeft.Y + EditorialHeight * scale));
        var label = Loc.T(L.Games.Play);
        var button = PlayButtonRect(rest, label, scale);
        var overButton = interactive && UiInteract.Hover(button.Min, button.Max);
        var hovered = interactive && !overButton && UiInteract.Hover(rest.Min, rest.Max);
        var card = rest.Scaled(Pose(tileId, hovered));
        source = card;
        var accent = library.Accent(entryIndex);
        var unit = scale * card.Width / rest.Width;
        var pad = EditorialPad * unit;
        DrawBackground(drawList, card, preset, accent, EditorialTopScrim, EditorialScrim, scale);
        var iconSide = EditorialIcon * unit;
        var iconMin = card.Min + new Vector2(pad);
        GameIconArt.Draw(drawList, library.IconIds[entryIndex], accent, iconMin, iconMin + new Vector2(iconSide),
            IconAppearance.Default, true);
        DrawEditorialHeading(drawList, library, entryIndex, card, iconMin, iconSide, pad, scale);
        DrawEditorialHook(drawList, library, entryIndex, slot, card, button, pad, scale);
        LivePreview.CoverCorners(drawList, card, HubMetrics.CardRadius * unit, ground, scale);
        var play = Button.Draw(drawList, button, label, ui.Ink with { Ink = White }, ButtonStyle.Gray,
            id: library.PlayIds[entryIndex]);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tapped = UiInteract.Click(rest.Min, rest.Max, hovered);
        return tapped || (play && interactive);
    }

    private static Rect PlayButtonRect(Rect rest, string label, float scale)
    {
        var pad = EditorialPad * scale;
        var height = Button.SmallHeight * scale;
        var width = MathF.Max(ButtonMinWidth * scale, Button.WidthFor(label, ButtonSize.Small));
        return new Rect(new Vector2(rest.Max.X - pad - width, rest.Max.Y - pad - height),
            new Vector2(rest.Max.X - pad, rest.Max.Y - pad));
    }

    public static float Pose(string id, bool hovered)
    {
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var hover = HoverFx.Amount(id, hovered);
        var press = PressFx.Scale(id, pressed, Motion.PressScaleCard);
        return press * (1f + Motion.HoverLiftCard * hover);
    }

    private static void DrawCaption(ImDrawListPtr drawList, AppSkin ui, GamesLibrary library, int entryIndex,
        Rect rest, float scale)
    {
        var titleTop = rest.Max.Y + HubMetrics.CaptionGap * scale;
        Typography.Draw(drawList, new Vector2(rest.Min.X, titleTop),
            Typography.FitText(library.Title(entryIndex), rest.Width, TextStyles.FootnoteEmphasized), ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList,
            new Vector2(rest.Min.X, titleTop + Typography.LineHeight(TextStyles.FootnoteEmphasized)),
            Typography.FitText(library.ProgressLabel(entryIndex), rest.Width, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
    }

    private static void DrawEditorialHeading(ImDrawListPtr drawList, GamesLibrary library, int entryIndex, Rect card,
        Vector2 iconMin, float iconSide, float pad, float scale)
    {
        var textLeft = iconMin.X + iconSide + Metrics.Space.Md * scale;
        var textWidth = MathF.Max(1f, card.Max.X - pad - textLeft);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var textTop = iconMin.Y + MathF.Max(0f, (iconSide - eyebrowHeight - titleHeight) * 0.5f);
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(library.Eyebrow(entryIndex), textWidth, TextStyles.FootnoteEmphasized),
            White with { W = EyebrowAlpha }, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + eyebrowHeight),
            Typography.FitText(library.Title(entryIndex), textWidth, TextStyles.Title3), White, TextStyles.Title3);
    }

    private void DrawEditorialHook(ImDrawListPtr drawList, GamesLibrary library, int entryIndex, int slot, Rect card,
        Rect button, float pad, float scale)
    {
        var left = card.Min.X + pad;
        var hookWidth = MathF.Max(1f, button.Min.X - Metrics.Space.Md * scale - left);
        ref var hook = ref hooks[slot % EditorialSlots];
        hook.Update(library.Hook(entryIndex), hookWidth, TextStyles.Footnote);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = card.Max.Y - pad - hook.Count * lineHeight;
        var ink = White with { W = HookAlpha };
        Typography.Draw(drawList, new Vector2(left, top), hook.First, ink, TextStyles.Footnote);
        if (hook.Count < 2)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, top + lineHeight), hook.Second, ink, TextStyles.Footnote);
    }

    private static void DrawProgress(ImDrawListPtr drawList, Rect bar, float progress)
    {
        var radius = bar.Height * 0.5f;
        Squircle.Fill(drawList, bar.Min, bar.Max, radius, ImGui.GetColorU32(White with { W = TrackAlpha }));
        var fill = Math.Clamp(progress, 0f, 1f) * bar.Width;
        if (fill <= 0f)
        {
            return;
        }

        Squircle.Fill(drawList, bar.Min, new Vector2(bar.Min.X + MathF.Max(fill, bar.Height), bar.Max.Y), radius,
            ImGui.GetColorU32(White with { W = FillAlpha }));
    }
}
