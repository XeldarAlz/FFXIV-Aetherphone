using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.LoveIsland;

internal sealed partial class LoveIslandApp
{
    private const float HeroAspect = 0.66f;
    private const float HeroMaxHeight = 330f;
    private const float HeroParallax = 0.5f;
    private const float HeroScrollFade = 0.55f;
    private const float HeroTopFade = 0.10f;
    private const float HeroBottomFade = 0.42f;
    private const float HeroZoom = 0.07f;
    private const float HeroPan = 0.015f;
    private const float HeroDriftSeconds = 26f;
    private const float EmblemSide = 66f;
    private const float EmblemRim = 3f;
    private const float EmblemGap = 10f;
    private const float EmblemOrbitPad = 5f;
    private const int EmblemMotes = 3;
    private const float EmblemMoteOrbitSeconds = 9f;
    private const double EmblemBreath = 2800.0;
    private const float FallbackMoonShare = 0.22f;

    private static readonly Vector2[] HeroSparkles =
    {
        new(0.14f, 0.30f), new(0.86f, 0.24f), new(0.30f, 0.14f), new(0.70f, 0.70f), new(0.08f, 0.66f),
        new(0.93f, 0.55f), new(0.52f, 0.10f),
    };

    private static float HeroHeight(float width, float scale) => MathF.Min(width * HeroAspect, HeroMaxHeight * scale);

    private void DrawHeroBackdrop(Rect area, float heroHeight, float scroll, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = area.Min.Y - scroll * HeroParallax;
        var hero = new Rect(new Vector2(area.Min.X, top), new Vector2(area.Max.X, top + heroHeight));
        if (hero.Max.Y <= area.Min.Y)
        {
            return;
        }

        var alpha = 1f - Math.Clamp(scroll / heroHeight, 0f, 1f) * HeroScrollFade;
        drawList.PushClipRect(area.Min, area.Max, true);
        var campaign = frameCampaign;
        var url = campaign?.HeroUrl;
        var texture = string.IsNullOrEmpty(url)
            ? null
            : images.Sized(url, MathF.Max(hero.Width, hero.Height) * LoveIslandKit.ThumbOversample);
        if (texture is null)
        {
            DrawHeroFallback(drawList, hero, alpha, scale);
        }
        else
        {
            DrawHeroImage(drawList, texture, hero, alpha);
        }

        DrawHeroSparkles(drawList, hero, alpha, scale);
        drawList.PopClipRect();
    }

    private void DrawHeroImage(ImDrawListPtr drawList, IDalamudTextureWrap texture, Rect hero, float alpha)
    {
        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, hero.Width, hero.Height);
        var phase = (float)ImGui.GetTime() * MathF.Tau / HeroDriftSeconds;
        var zoom = 1f + HeroZoom * (0.5f - 0.5f * MathF.Cos(phase));
        var span = (uv1 - uv0) / zoom;
        var center = (uv0 + uv1) * 0.5f + new Vector2(MathF.Sin(phase * 0.5f) * HeroPan, 0f);
        var low = Vector2.Max(uv0, center - span * 0.5f);
        var high = low + span;
        if (high.X > uv1.X)
        {
            low.X -= high.X - uv1.X;
            high.X = uv1.X;
        }

        DrawFadedImage(drawList, texture, hero, low, high, alpha, HeroTopFade, HeroBottomFade);
    }

    private static void DrawFadedImage(ImDrawListPtr drawList, IDalamudTextureWrap texture, Rect frame, Vector2 uv0,
        Vector2 uv1, float alpha, float topShare, float bottomShare)
    {
        var topFade = frame.Min.Y + frame.Height * topShare;
        var bottomFade = frame.Max.Y - frame.Height * bottomShare;
        drawList.PushTextureID(texture.Handle);
        FadedBand(drawList, frame, uv0, uv1, frame.Min.Y, topFade, 0f, alpha);
        FadedBand(drawList, frame, uv0, uv1, topFade, bottomFade, alpha, alpha);
        FadedBand(drawList, frame, uv0, uv1, bottomFade, frame.Max.Y, alpha, 0f);
        drawList.PopTextureID();
    }

    private static void FadedBand(ImDrawListPtr drawList, Rect hero, Vector2 uv0, Vector2 uv1, float fromY, float toY,
        float fromAlpha, float toAlpha)
    {
        if (toY <= fromY)
        {
            return;
        }

        var fromV = uv0.Y + (uv1.Y - uv0.Y) * ((fromY - hero.Min.Y) / hero.Height);
        var toV = uv0.Y + (uv1.Y - uv0.Y) * ((toY - hero.Min.Y) / hero.Height);
        var upper = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fromAlpha));
        var lower = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, toAlpha));
        var topLeft = new Vector2(hero.Min.X, fromY);
        var topRight = new Vector2(hero.Max.X, fromY);
        var bottomRight = new Vector2(hero.Max.X, toY);
        var bottomLeft = new Vector2(hero.Min.X, toY);
        drawList.PrimReserve(6, 6);
        drawList.PrimVtx(topLeft, new Vector2(uv0.X, fromV), upper);
        drawList.PrimVtx(topRight, new Vector2(uv1.X, fromV), upper);
        drawList.PrimVtx(bottomRight, new Vector2(uv1.X, toV), lower);
        drawList.PrimVtx(topLeft, new Vector2(uv0.X, fromV), upper);
        drawList.PrimVtx(bottomRight, new Vector2(uv1.X, toV), lower);
        drawList.PrimVtx(bottomLeft, new Vector2(uv0.X, toV), lower);
    }

    private void DrawHeroFallback(ImDrawListPtr drawList, Rect hero, float alpha, float scale)
    {
        var radius = hero.Width * FallbackMoonShare;
        var center = new Vector2(hero.Center.X, hero.Min.Y + hero.Height * 0.46f);
        var firstVertex = CardEntrance.Mark(drawList);
        EclipseSky.Moon(drawList, center, radius, ui.Accent, scale);
        CardEntrance.Fade(drawList, firstVertex, alpha);
    }

    private void DrawHeroSparkles(ImDrawListPtr drawList, Rect hero, float alpha, float scale)
    {
        var time = (float)ImGui.GetTime();
        for (var index = 0; index < HeroSparkles.Length; index++)
        {
            var twinkle = MathF.Sin(time * (0.9f + index * 0.17f) + index * 1.9f);
            if (twinkle <= 0f)
            {
                continue;
            }

            var point = HeroSparkles[index];
            var center = new Vector2(hero.Min.X + point.X * hero.Width, hero.Min.Y + point.Y * hero.Height);
            EclipseFx.Sparkle(drawList, center, (2.5f + 3.5f * twinkle) * scale,
                LoveIslandKit.White with { W = 0.85f * twinkle * alpha });
        }
    }

    private float DrawEmblem(ImDrawListPtr drawList, float centerX, float lineY, float width, float scale)
    {
        var firstVertex = CardEntrance.Mark(drawList);
        var side = EmblemSide * scale;
        var center = new Vector2(centerX, lineY);
        var half = new Vector2(side * 0.5f, side * 0.5f);
        var logo = new Rect(center - half, center + half);
        var radius = side * Metrics.Radius.TileFactor;
        var breath = EclipseFx.Breath(EmblemBreath);
        SoftGlow.Draw(drawList, center, side * (1.15f + 0.12f * breath), ui.Accent with { W = 0.55f + 0.2f * breath });
        var rim = EmblemRim * scale;
        var campaign = frameCampaign;
        var logoUrl = campaign?.LogoUrl;
        var logoTexture = string.IsNullOrEmpty(logoUrl)
            ? null
            : images.Sized(logoUrl, side * LoveIslandKit.ThumbOversample);
        if (logoTexture is null)
        {
            Squircle.FillVerticalGradient(drawList, logo.Min, logo.Max, radius,
                ImGui.GetColorU32(Palette.Lighten(ui.Accent, 0.12f)),
                ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.35f)));
            PhoneIcon.Draw(drawList, logo.Center, PhoneIcons.MoonStars, LoveIslandKit.White, side * 0.5f);
        }
        else
        {
            var (uv0, uv1) = ImageFit.CoverSquare(logoTexture.Size);
            Squircle.FillImage(drawList, logo.Min, logo.Max, radius, logoTexture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        var pad = EmblemOrbitPad * scale;
        var padding = new Vector2(pad, pad);
        EclipseFx.OrbitBorder(drawList, logo.Min - padding, logo.Max + padding, radius + pad,
            EclipseFx.Rose(ui.Accent, 0.6f), scale);
        DrawEmblemMotes(drawList, center, side * 0.5f + pad * 2.4f, scale);
        UiAnchors.Report("loveisland.hero", logo);
        var cursorY = logo.Max.Y + rim + EmblemGap * scale;
        if (string.IsNullOrEmpty(campaign?.HeroUrl))
        {
            var nameStyle = TextStyles.Title1;
            var name = Typography.FitText(DisplayName, width, nameStyle);
            var nameWidth = Typography.Measure(name, nameStyle).X;
            Typography.Draw(drawList, new Vector2(centerX - nameWidth * 0.5f, cursorY), name, ui.TitleInk, nameStyle,
                ChromeEffect());
            cursorY += Typography.LineHeight(nameStyle) + Metrics.Space.Xxs * scale;
        }

        var windowLine = labels.WindowLine;
        if (windowLine.Length > 0)
        {
            var lineStyle = TextStyles.FootnoteEmphasized;
            var line = Typography.FitText(windowLine, width, lineStyle);
            var lineWidth = Typography.Measure(line, lineStyle).X;
            Typography.Draw(drawList, new Vector2(centerX - lineWidth * 0.5f, cursorY), line,
                EclipseFx.Rose(ui.Accent, 0.72f), lineStyle);
            cursorY += Typography.LineHeight(lineStyle);
        }

        entrance.Settle(drawList, firstVertex, scale);
        return cursorY;
    }

    private void DrawEmblemMotes(ImDrawListPtr drawList, Vector2 center, float orbit, float scale)
    {
        var time = (float)ImGui.GetTime();
        var spin = time * MathF.Tau / EmblemMoteOrbitSeconds;
        for (var index = 0; index < EmblemMotes; index++)
        {
            var angle = spin + index * MathF.Tau / EmblemMotes;
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 2.3f + index * 2.1f);
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbit;
            EclipseFx.Sparkle(drawList, position, (2.2f + 2f * pulse) * scale,
                EclipseFx.Rose(ui.Accent, 0.7f) with { W = 0.55f + 0.45f * pulse });
        }
    }
}
