using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallpapers;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.YellowPages;

internal readonly record struct AdCardContext(RemoteImageCache Images, long NowUnix, bool Compact,
    WallpaperImageCache? Local = null)
{
    public IDalamudTextureWrap? Texture(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (Local is not null && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Local.Get(url);
        }

        return Images.Get(url);
    }
}

internal static class AdCard
{
    private const float HeroAspect = 0.5625f;
    private const float HeroMaxHeight = 220f;
    private const float PadX = SocialChrome.CellPadX;
    private const float PadY = 12f;
    private const float TileSide = 46f;
    private const float TileGap = 12f;
    private const float TitleGap = 2f;
    private const float BodyGap = 6f;
    private const float FooterGap = 8f;
    private const float FooterHeight = 22f;
    private const float CompactHeight = 78f;
    private const float CompactThumb = 54f;
    private const float OverlayInset = 12f;
    private const float ScrimShare = 0.5f;
    private const int MaxBodyLines = 2;

    private static readonly TextStyle TitleStyle = TextStyles.Headline;
    private static readonly TextStyle MetaStyle = TextStyles.Footnote;
    private static readonly TextStyle BodyStyle = TextStyles.Subheadline;
    private static readonly TextStyle FooterStyle = TextStyles.FootnoteEmphasized;
    private static readonly TextStyle StatStyle = TextStyles.Caption1;

    private static readonly SocialInk Ink = YellowPagesInk.Shared;

    public static float Height(AdDto ad, float width, float scale, in AdCardContext context)
    {
        if (context.Compact)
        {
            return CompactHeight * scale;
        }

        var hasHero = HasHero(ad);
        var textWidth = width - PadX * 2f * scale;
        var bodyLines = BodyLines(ad, hasHero ? textWidth : textWidth - (TileSide + TileGap) * scale);
        var bodyHeight = bodyLines > 0 ? bodyLines * Typography.LineHeight(BodyStyle) + BodyGap * scale : 0f;
        var headerHeight = Typography.LineHeight(TitleStyle) + TitleGap * scale + Typography.LineHeight(MetaStyle);
        if (!hasHero)
        {
            headerHeight = MathF.Max(headerHeight, TileSide * scale);
        }

        return HeroHeight(ad, width, scale) + PadY * 2f * scale + headerHeight + bodyHeight
            + FooterGap * scale + FooterHeight * scale;
    }

    public static bool Draw(AdDto ad, in AdCardContext context)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var width = ScrollLayout.StableContentWidth();
        var height = Height(ad, width, scale, context);
        var origin = ImGui.GetCursorScreenPos();
        if (!ImGui.IsRectVisible(origin, origin + new Vector2(width, height)))
        {
            ImGui.Dummy(new Vector2(width, height));
            return false;
        }

        var cell = FeedCell.Begin(drawList, height, Ink.HoverTint);
        if (context.Compact)
        {
            PaintCompact(drawList, ad, cell.Bounds, scale, context);
        }
        else
        {
            PaintFull(drawList, ad, cell.Bounds, scale, context);
        }

        FeedCell.End(drawList, cell, Ink.Hairline);
        return cell.Tapped;
    }

    private static void PaintFull(ImDrawListPtr drawList, AdDto ad, Rect bounds, float scale,
        in AdCardContext context)
    {
        var pad = PadX * scale;
        var left = bounds.Min.X + pad;
        var right = bounds.Max.X - pad;
        var heroHeight = HeroHeight(ad, bounds.Width, scale);
        var status = YellowPagesKit.StatusOf(ad, context.NowUnix);
        var accent = YellowPagesKit.AccentOf(ad);
        var textLeft = left;
        var cursorY = bounds.Min.Y + heroHeight + PadY * scale;
        if (heroHeight > 0f)
        {
            PaintHero(drawList, ad, new Rect(bounds.Min, new Vector2(bounds.Max.X, bounds.Min.Y + heroHeight)), scale,
                context, status, accent);
        }
        else
        {
            var tileSide = TileSide * scale;
            var tileMin = new Vector2(left, cursorY);
            YellowPagesKit.Tile(drawList, tileMin, tileMin + new Vector2(tileSide, tileSide), accent,
                AdCategories.Icon(ad.Category), 13f * scale, 1.05f);
            textLeft = left + tileSide + TileGap * scale;
        }

        var textWidth = MathF.Max(1f, right - textLeft);
        var titleHeight = Typography.LineHeight(TitleStyle);
        var trailingWidth = 0f;
        if (heroHeight <= 0f && (ad.Wanted || status.Live))
        {
            var label = status.Live ? Loc.T(L.YellowPages.OpenNow) : Loc.T(L.YellowPages.WantedChip);
            var pillWidth = YellowPagesKit.PillWidth(label, scale, false, true);
            var pillMin = new Vector2(right - pillWidth, cursorY);
            if (status.Live)
            {
                YellowPagesKit.LivePill(drawList, pillMin, label, scale);
            }
            else
            {
                YellowPagesKit.Pill(drawList, pillMin, label, Palette.WithAlpha(YellowPagesKit.WantedTint, 0.22f),
                    Palette.Lighten(YellowPagesKit.WantedTint, 0.35f), scale, string.Empty, true);
            }

            trailingWidth = pillWidth + 8f * scale;
        }

        Marquee.DrawLeftAuto(drawList, new MarqueeId("yellowpages.card.title.", ad.Id), ad.Title, textLeft, cursorY,
            MathF.Max(1f, textWidth - trailingWidth), TitleStyle, Ink.TitleInk);
        cursorY += titleHeight + TitleGap * scale;
        var meta = MetaLine(ad);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), Typography.FitText(meta, textWidth, MetaStyle),
            Ink.MutedInk, MetaStyle);
        cursorY += Typography.LineHeight(MetaStyle);
        if (heroHeight <= 0f)
        {
            cursorY = MathF.Max(cursorY, bounds.Min.Y + PadY * scale + TileSide * scale);
        }

        var bodyWidth = right - left;
        var bodyLines = BodyLines(ad, heroHeight > 0f ? bodyWidth : textWidth);
        if (bodyLines > 0)
        {
            cursorY += BodyGap * scale;
            PaintBody(drawList, ad, new Vector2(left, cursorY), heroHeight > 0f ? bodyWidth : textWidth, bodyLines,
                heroHeight > 0f ? left : textLeft);
            cursorY += bodyLines * Typography.LineHeight(BodyStyle);
        }

        cursorY += FooterGap * scale;
        PaintFooter(drawList, ad, left, right, cursorY, scale, context, status, accent);
    }

    private static void PaintHero(ImDrawListPtr drawList, AdDto ad, Rect hero, float scale, in AdCardContext context,
        in AdStatus status, Vector4 accent)
    {
        var texture = context.Texture(ad.MediaUrl);
        if (texture is null)
        {
            YellowPagesKit.CoverFallback(drawList, hero.Min, hero.Max, 0f, accent, ad.Category, 1.6f);
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, hero.Width, hero.Height);
            drawList.AddImage(texture.Handle, hero.Min, hero.Max, uv0, uv1, 0xFFFFFFFFu);
        }

        YellowPagesKit.BottomScrim(drawList, hero.Min, hero.Max, ScrimShare);
        var inset = OverlayInset * scale;
        var cursorX = hero.Min.X + inset;
        var overlayTop = hero.Min.Y + inset;
        cursorX += YellowPagesKit.Pill(drawList, new Vector2(cursorX, overlayTop), Loc.T(AdCategories.Label(ad.Category)),
            YellowPagesKit.OverlayFill, YellowPagesKit.White, scale) + 6f * scale;
        if (ad.Wanted)
        {
            YellowPagesKit.Pill(drawList, new Vector2(cursorX, overlayTop), Loc.T(L.YellowPages.WantedChip),
                Palette.WithAlpha(YellowPagesKit.WantedTint, 0.85f), YellowPagesKit.White, scale, string.Empty, true);
        }

        if (status.Live)
        {
            var label = Loc.T(L.YellowPages.OpenNow);
            var pillWidth = YellowPagesKit.PillWidth(label, scale, false, true) + 12f * scale;
            YellowPagesKit.LivePill(drawList, new Vector2(hero.Max.X - inset - pillWidth, overlayTop), label, scale);
        }

        if (PostMedia.Photos(ad.MediaUrls, ad.MediaUrl).Length > 1)
        {
            MultiPhotoBadge.Draw(drawList, new Vector2(hero.Max.X - inset, hero.Max.Y - inset - 14f * scale), scale);
        }
    }

    private static void PaintBody(ImDrawListPtr drawList, AdDto ad, Vector2 topLeft, float width, int lines,
        float left)
    {
        using (Plugin.Fonts.Push(BodyStyle.Scale, BodyStyle.Weight))
        {
            Plugin.Fonts.NoticeText(ad.Body);
            var wrapped = Typography.WrapCurrent(ad.Body, width);
            var count = Math.Min(wrapped.Length, lines);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(Ink.BodyInk);
            for (var index = 0; index < count; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(left, topLeft.Y + index * lineHeight), packed,
                    wrapped[index]);
            }
        }
    }

    private static void PaintFooter(ImDrawListPtr drawList, AdDto ad, float left, float right, float top,
        float scale, in AdCardContext context, in AdStatus status, Vector4 accent)
    {
        var centerY = top + FooterHeight * scale * 0.5f;
        var cursorRight = right;
        var remaining = AdText.RemainingShort(ad, context.NowUnix);
        var remainingSize = Typography.Measure(remaining, StatStyle);
        cursorRight -= remainingSize.X;
        Typography.Draw(drawList, new Vector2(cursorRight, centerY - remainingSize.Y * 0.5f), remaining,
            AdText.ExpiresSoon(ad, context.NowUnix) ? Ink.Danger : Ink.MutedInk, StatStyle);
        cursorRight -= 16f * scale;
        PhoneIcon.Draw(drawList, new Vector2(cursorRight + 6f * scale, centerY), PhoneIcons.Clock, Ink.FaintInk,
            13f * scale);
        cursorRight -= 12f * scale;
        if (ad.Views > 0)
        {
            var views = ad.Views.ToString(Loc.Culture);
            var viewsSize = Typography.Measure(views, StatStyle);
            cursorRight -= viewsSize.X;
            Typography.Draw(drawList, new Vector2(cursorRight, centerY - viewsSize.Y * 0.5f), views, Ink.MutedInk,
                StatStyle);
            cursorRight -= 16f * scale;
            PhoneIcon.Draw(drawList, new Vector2(cursorRight + 6f * scale, centerY), PhoneIcons.Eye, Ink.FaintInk,
                13f * scale);
            cursorRight -= 12f * scale;
        }

        if (ad.Saved)
        {
            cursorRight -= 8f * scale;
            PhoneIcon.Draw(drawList, new Vector2(cursorRight, centerY), PhoneIcons.BookmarkFilled, Ink.AccentLink,
                14f * scale);
            cursorRight -= 12f * scale;
        }

        if (ad.AfterDark)
        {
            var label = Loc.T(L.YellowPages.AfterDarkChip);
            var pillWidth = YellowPagesKit.PillWidth(label, scale, false);
            cursorRight -= pillWidth;
            YellowPagesKit.Pill(drawList, new Vector2(cursorRight, centerY - YellowPagesKit.PillHeight * scale * 0.5f),
                label, Palette.WithAlpha(YellowPagesKit.AfterDarkPink, 0.18f),
                Palette.WithAlpha(YellowPagesKit.AfterDarkPink, 0.95f), scale);
            cursorRight -= 8f * scale;
        }

        var statusLeft = left;
        if (status.Live)
        {
            YellowPagesKit.LiveDot(drawList, new Vector2(left + 5f * scale, centerY), YellowPagesKit.OpenGreen, scale);
            statusLeft += 16f * scale;
        }

        if (status.Label.Length > 0)
        {
            var maxWidth = MathF.Max(1f, cursorRight - 8f * scale - statusLeft);
            var fitted = Typography.FitText(status.Label, maxWidth, FooterStyle);
            var size = Typography.Measure(fitted, FooterStyle);
            Typography.Draw(drawList, new Vector2(statusLeft, centerY - size.Y * 0.5f), fitted,
                status.Live ? YellowPagesKit.OpenGreen : status.Tint, FooterStyle);
        }
    }

    private static void PaintCompact(ImDrawListPtr drawList, AdDto ad, Rect bounds, float scale,
        in AdCardContext context)
    {
        var pad = PadX * scale;
        var thumbSide = CompactThumb * scale;
        var thumbMin = new Vector2(bounds.Min.X + pad, bounds.Center.Y - thumbSide * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSide, thumbSide);
        var accent = YellowPagesKit.AccentOf(ad);
        var rounding = 14f * scale;
        var texture = HasHero(ad) ? context.Texture(ad.MediaUrl) : null;
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.CoverSquare(texture.Size);
            Squircle.FillImage(drawList, thumbMin, thumbMax, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else
        {
            YellowPagesKit.Tile(drawList, thumbMin, thumbMax, accent, AdCategories.Icon(ad.Category), rounding, 1.1f);
        }

        var status = YellowPagesKit.StatusOf(ad, context.NowUnix);
        var textLeft = thumbMax.X + TileGap * scale;
        var right = bounds.Max.X - pad;
        var remaining = AdText.RemainingShort(ad, context.NowUnix);
        var remainingSize = Typography.Measure(remaining, StatStyle);
        var titleTop = bounds.Min.Y + 13f * scale;
        Typography.Draw(drawList, new Vector2(right - remainingSize.X, titleTop + 2f * scale), remaining,
            AdText.ExpiresSoon(ad, context.NowUnix) ? Ink.Danger : Ink.MutedInk, StatStyle);
        var titleWidth = MathF.Max(1f, right - remainingSize.X - 10f * scale - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("yellowpages.compact.title.", ad.Id), ad.Title, textLeft, titleTop,
            titleWidth, TitleStyle, Ink.TitleInk);
        var metaTop = titleTop + Typography.LineHeight(TitleStyle) + 2f * scale;
        var meta = Typography.FitText(MetaLine(ad), right - textLeft, MetaStyle);
        Typography.Draw(drawList, new Vector2(textLeft, metaTop), meta, Ink.MutedInk, MetaStyle);
        var statusTop = metaTop + Typography.LineHeight(MetaStyle) + 3f * scale;
        var statusLeft = textLeft;
        if (status.Live)
        {
            YellowPagesKit.LiveDot(drawList, new Vector2(statusLeft + 5f * scale, statusTop + 7f * scale),
                YellowPagesKit.OpenGreen, scale);
            statusLeft += 16f * scale;
        }

        var statusLabel = status.Label;
        if (ad.Wanted)
        {
            statusLabel = statusLabel.Length > 0
                ? $"{Loc.T(L.YellowPages.WantedChip)} · {statusLabel}"
                : Loc.T(L.YellowPages.WantedChip);
        }

        if (statusLabel.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(statusLeft, statusTop),
                Typography.FitText(statusLabel, right - statusLeft, FooterStyle),
                status.Live ? YellowPagesKit.OpenGreen : status.Tint, FooterStyle);
        }
    }

    private static string MetaLine(AdDto ad)
    {
        var identity = AdText.Identity(ad);
        var world = AdText.WorldLine(ad);
        return world.Length > 0 ? $"{identity} · {world}" : identity;
    }

    private static float HeroHeight(AdDto ad, float width, float scale) =>
        HasHero(ad) ? MathF.Min(width * HeroAspect, HeroMaxHeight * scale) : 0f;

    private static bool HasHero(AdDto ad) => !string.IsNullOrEmpty(ad.MediaUrl);

    private static int BodyLines(AdDto ad, float width)
    {
        if (ad.Body.Length == 0)
        {
            return 0;
        }

        return Math.Min(Typography.CountWrappedLines(ad.Body, BodyStyle, width), MaxBodyLines);
    }
}
