using Aetherphone.Core;
using Aetherphone.Core.Media;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Components;

internal static class ArtworkTile
{
    public const float LargeCard = 150f;
    public const float Standard = 120f;
    public const float RowArt = 44f;
    public const float TileRadiusFraction = 0.08f;
    public const float HeroRadiusFraction = 0.12f;
    public const float CaptionGap = 6f;
    private const float PlayRadiusFraction = 0.13f;
    private const float PlayInsetFraction = 0.07f;
    private const float RimAlpha = 0.06f;
    private const float PressDim = 0.18f;
    private static readonly Vector4 Rim = new(1f, 1f, 1f, RimAlpha);
    private static readonly Vector4 PlayInk = new(0.05f, 0.05f, 0.06f, 1f);

    public static float Side(float units) => units * UiScale.Current;

    public static float CaptionHeight() =>
        CaptionGap * UiScale.Current + Typography.LineHeight(TextStyles.FootnoteEmphasized) +
        Typography.LineHeight(TextStyles.Footnote);

    public static float CardHeight(float side) => side + CaptionHeight();

    public static void Draw(ImDrawListPtr drawList, RemoteImageCache images, Vector2 min, float side, string url,
        string seed, float radiusFraction = TileRadiusFraction)
    {
        var max = min + new Vector2(side, side);
        var radius = side * radiusFraction;
        var texture = string.IsNullOrEmpty(url) ? null : images.Sized(url, side);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            var swatch = ArtGradient.FromName(seed);
            Squircle.FillVerticalGradient(drawList, min, max, radius, ImGui.GetColorU32(swatch.Top),
                ImGui.GetColorU32(swatch.Bottom));
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Rim), 1f);
    }

    public static void DrawPressed(ImDrawListPtr drawList, Vector2 min, float side, bool hovered,
        float radiusFraction = TileRadiusFraction)
    {
        if (!hovered)
        {
            return;
        }

        var max = min + new Vector2(side, side);
        var alpha = ImGui.IsMouseDown(ImGuiMouseButton.Left) ? PressDim : PressDim * 0.5f;
        Squircle.Fill(drawList, min, max, side * radiusFraction, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, alpha)));
    }

    public static Rect PlayButtonRect(Vector2 min, float side)
    {
        var radius = side * PlayRadiusFraction;
        var inset = side * PlayInsetFraction;
        var center = new Vector2(min.X + side - inset - radius, min.Y + side - inset - radius);
        return new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
    }

    public static bool PlayOverlay(string id, Vector2 min, float side, Vector4 fill, bool playing)
    {
        var rect = PlayButtonRect(min, side);
        return MusicRenderer.PlayButton(id, rect.Center, rect.Width * 0.5f, fill, PlayInk, playing);
    }

    public static void DrawCaption(ImDrawListPtr drawList, AppSkin ui, Vector2 artMin, float width, string title,
        string subtitle, bool highlighted = false)
    {
        var scale = UiScale.Current;
        var top = artMin.Y + width + CaptionGap * scale;
        var fittedTitle = Typography.FitText(title, width, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(artMin.X, top), fittedTitle, highlighted ? ui.Accent : ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        if (subtitle.Length == 0)
        {
            return;
        }

        var subtitleTop = top + Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var fittedSubtitle = Typography.FitText(subtitle, width, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(artMin.X, subtitleTop), fittedSubtitle, ui.MutedInk,
            TextStyles.Footnote);
    }
}
