using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppIconTile
{
    private const float StencilFill = 0.83f;
    private const float PaintedFocal = 0.60f;
    private const float MaskCanvasScale = StencilFill / PaintedFocal;
    private const float MinimumSide = 0.5f;

    public static bool TryDraw(ImDrawListPtr drawList, string appId, Vector4 accent, Vector2 min, Vector2 max,
        float radius, float alpha, bool elevated = true, float scale = 0f) =>
        TryDraw(drawList, appId, accent, min, max, radius, alpha, Plugin.Cfg.IconAppearance, elevated, scale);

    public static bool TryDraw(ImDrawListPtr drawList, string appId, Vector4 accent, Vector2 min, Vector2 max,
        float radius, float alpha, IconAppearance appearance, bool elevated, float scale)
    {
        try
        {
            if (!AppIconCache.IsPainted(appId))
            {
                return false;
            }

            AppIconCache.Prepare();
            var side = MathF.Max(max.X - min.X, max.Y - min.Y);
            if (side <= MinimumSide)
            {
                return true;
            }

            var texture = AppIconCache.Resolve(appId, appearance, TextureSizes.LevelFor(side), accent);
            if (texture is null || texture.Handle == nint.Zero)
            {
                return false;
            }

            if (alpha <= 0.001f)
            {
                return true;
            }

            var uiScale = scale > 0f ? scale : UiScale.Current;
            if (elevated)
            {
                Elevation.IconRest(drawList, min, max, radius, uiScale, alpha);
            }

            if (appearance == IconAppearance.Clear)
            {
                Material.LiquidGlass(drawList, min, max, radius, uiScale, GlassTone.Light, AppIconCache.GlassBrightness,
                    alpha);
            }

            Squircle.FillImage(drawList, min, max, radius, texture.Handle,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
            if (elevated)
            {
                Material.EdgeSquircle(drawList, min, max, radius, uiScale, alpha);
            }

            return true;
        }
        catch (Exception exception)
        {
            AppIconCache.Disable(appId);
            AepLog.Warning(exception, $"[Icons] painted tile for {appId} fell back to the stencil");
            return false;
        }
    }

    public static bool TryDrawGlyph(ImDrawListPtr drawList, string appId, Vector2 center, float size, Vector4 tint)
    {
        if (TryDrawMask(drawList, appId, center, size, tint))
        {
            return true;
        }

        return AppIconTextures.TryDrawArtwork(drawList, appId, center, size, tint);
    }

    private static bool TryDrawMask(ImDrawListPtr drawList, string appId, Vector2 center, float size, Vector4 tint)
    {
        try
        {
            if (!AppIconCache.IsPainted(appId))
            {
                return false;
            }

            AppIconCache.Prepare();
            var canvas = size * MaskCanvasScale;
            if (canvas <= MinimumSide)
            {
                return true;
            }

            var texture = AppIconCache.Resolve(appId, IconAppearance.Clear, TextureSizes.LevelFor(canvas),
                AppAccents.For(appId));
            if (texture is null || texture.Handle == nint.Zero)
            {
                return false;
            }

            var half = canvas * 0.5f;
            drawList.AddImage(texture.Handle, new Vector2(center.X - half, center.Y - half),
                new Vector2(center.X + half, center.Y + half), Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));
            return true;
        }
        catch (Exception exception)
        {
            AppIconCache.Disable(appId);
            AepLog.Warning(exception, $"[Icons] painted glyph for {appId} fell back to the stencil");
            return false;
        }
    }
}
