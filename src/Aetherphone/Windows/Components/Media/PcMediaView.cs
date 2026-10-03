using Aetherphone.Core.Localization;
using Aetherphone.Core.SystemMedia;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal static class PcMediaView
{
    private const float GlyphFraction = 0.42f;
    private const float RimAlpha = 0.08f;
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 0.92f);
    private static readonly Vector4 Rim = new(1f, 1f, 1f, RimAlpha);

    private static string sourceLanguage = string.Empty;
    private static string sourceApp = string.Empty;
    private static bool sourcePlaying;
    private static string sourceText = string.Empty;

    public static string Title(in MediaSessionSnapshot snapshot) =>
        snapshot.Title.Length > 0 ? snapshot.Title : Loc.T(L.Music.PcMedia.UnknownTitle);

    public static string AppName(in MediaSessionSnapshot snapshot) =>
        snapshot.AppName.Length > 0 ? snapshot.AppName : Loc.T(L.Music.PcMedia.ThisPc);

    public static string Subtitle(in MediaSessionSnapshot snapshot) =>
        snapshot.Artist.Length > 0 ? snapshot.Artist : AppName(snapshot);

    public static string Source(in MediaSessionSnapshot snapshot)
    {
        var language = Loc.Current.Code;
        var app = AppName(snapshot);
        var playing = snapshot.IsPlaying;
        if (ReferenceEquals(app, sourceApp) && playing == sourcePlaying &&
            string.Equals(language, sourceLanguage, StringComparison.Ordinal))
        {
            return sourceText;
        }

        sourceLanguage = language;
        sourceApp = app;
        sourcePlaying = playing;
        sourceText = Loc.T(playing ? L.Music.PcMedia.PlayingOn : L.Music.PcMedia.PausedOn, app);
        return sourceText;
    }

    public static float Progress(in MediaSessionSnapshot snapshot)
    {
        if (snapshot.Duration <= TimeSpan.Zero)
        {
            return 0f;
        }

        var position = snapshot.PositionAt(DateTime.UtcNow.Ticks);
        return Math.Clamp((float)(position.Ticks / (double)snapshot.Duration.Ticks), 0f, 1f);
    }

    public static void DrawArt(ImDrawListPtr drawList, Vector2 min, float side, float radius,
        IDalamudTextureWrap? texture, in MediaSessionSnapshot snapshot, Vector4 accent, float alpha)
    {
        var max = min + new Vector2(side, side);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, min, max, radius, texture.Handle,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), uv0, uv1);
        }
        else
        {
            var surface = IconTile.Surface(accent);
            Squircle.FillVerticalGradient(drawList, min, max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(Palette.Mix(surface, accent, 0.35f), alpha)),
                ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
            ProgressRing.CenterIcon(drawList, (min + max) * 0.5f, PcMediaGlyph.For(snapshot.AppName),
                Palette.WithAlpha(GlyphInk, alpha), side * GlyphFraction);
        }

        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(Rim, alpha)), 1f);
    }

    public static void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, IDalamudTextureWrap? texture,
        in MediaSessionSnapshot snapshot, Vector4 accent, float alpha)
    {
        var extent = new Vector2(radius, radius);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            drawList.AddImageRounded(texture.Handle, center - extent, center + extent, uv0, uv1,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), radius);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(IconTile.Surface(accent), alpha)),
            32);
        ProgressRing.CenterIcon(drawList, center, PcMediaGlyph.For(snapshot.AppName),
            Palette.WithAlpha(GlyphInk, alpha), radius * 2f * GlyphFraction);
    }
}
