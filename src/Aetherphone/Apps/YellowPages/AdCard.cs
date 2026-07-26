using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Core.YellowPages;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.YellowPages;

internal static class AdCard
{
    public const float Gap = 10f;
    private const float Pad = 12f;
    private const float TileSide = 34f;
    private const float IdentityRowHeight = 40f;
    private const float MetaRowHeight = 22f;
    private const float DescriptionGap = 6f;
    private const float HeroAspect = 0.56f;
    private const float HeroMaxHeight = 190f;
    private const int MaxDescriptionLines = 2;

    public static readonly Vector4 OpenGreen = new(0.24f, 0.82f, 0.44f, 1f);
    public static readonly Vector4 AfterDarkPink = new(0.94f, 0.42f, 0.62f, 1f);

    public static float Height(AdDto ad, float width, float scale)
    {
        var lines = DescriptionLines(ad, width, scale, out var lineHeight);
        var descriptionHeight = lines > 0 ? lines * lineHeight + DescriptionGap * scale : 0f;
        return HeroHeight(ad, width, scale) + (Pad * 2f + IdentityRowHeight + MetaRowHeight) * scale
            + descriptionHeight;
    }

    private static float HeroHeight(AdDto ad, float width, float scale) =>
        HasHero(ad) ? MathF.Min(width * HeroAspect, HeroMaxHeight * scale) : 0f;

    private static bool HasHero(AdDto ad) => !string.IsNullOrEmpty(ad.MediaUrl);

    public static bool Draw(Rect card, AdDto ad, RemoteImageCache images, LodestoneService lodestone,
        PhoneTheme theme, AppSkin ui, long nowUnix)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var rounding = Metrics.Radius.Lg * scale;
        var palette = ui.Palette;
        ui.Card(drawList, card.Min, card.Max, rounding, elevated: true);
        var pad = Pad * scale;
        var left = card.Min.X + pad;
        var right = card.Max.X - pad;
        var heroHeight = HeroHeight(ad, card.Width, scale);
        var body = new Rect(new Vector2(card.Min.X, card.Min.Y + heroHeight), card.Max);
        if (heroHeight > 0f)
        {
            DrawHero(drawList, ad, card, heroHeight, rounding, images, palette, scale);
        }

        var tileSide = TileSide * scale;
        var tileCenter = new Vector2(left + tileSide * 0.5f, body.Min.Y + pad + tileSide * 0.5f);
        var textLeft = left;
        if (heroHeight <= 0f)
        {
            IconTile.Draw(tileCenter, tileSide, IconTile.Surface(palette.Accent), AdCategories.Icon(ad.Category));
            textLeft = tileCenter.X + tileSide * 0.5f + 10f * scale;
        }

        var status = StatusText(ad, nowUnix, out var statusColor, out var live);
        var statusSize = Typography.Measure(status, TextStyles.FootnoteEmphasized);
        var statusLeft = right - statusSize.X;
        var statusTop = body.Min.Y + pad + 2f * scale;
        if (status.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(statusLeft, statusTop), status, statusColor,
                TextStyles.FootnoteEmphasized);
            if (live)
            {
                DrawOpenDot(drawList, new Vector2(statusLeft - 10f * scale, statusTop + statusSize.Y * 0.5f), scale);
            }
        }

        var titleWidth = (status.Length > 0 ? statusLeft - (live ? 20f : 8f) * scale : right) - textLeft;
        Marquee.DrawLeftAuto(drawList, "yellowpages.card.title." + ad.Id, ad.Title, textLeft, body.Min.Y + pad,
            titleWidth, TextStyles.Headline, palette.TitleInk);
        Marquee.DrawLeftAuto(drawList, "yellowpages.card.identity." + ad.Id, AdText.Identity(ad), textLeft,
            body.Min.Y + pad + 21f * scale, right - textLeft, TextStyles.Footnote, palette.MutedInk);

        var descriptionTop = body.Min.Y + (Pad + IdentityRowHeight) * scale;
        DrawDescription(drawList, ad, left, descriptionTop, card.Width, scale, palette.BodyInk);
        DrawMetaRow(drawList, ad, left, right, card.Max.Y - pad - MetaRowHeight * scale + 4f * scale, palette,
            scale);

        var hovered = UiInteract.Hover(card.Min, card.Max);
        if (hovered)
        {
            UiInteract.HoverHighlight(drawList, card.Min, card.Max, rounding);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(card.Min, card.Max, hovered);
    }

    private static void DrawHero(ImDrawListPtr drawList, AdDto ad, Rect card, float height, float rounding,
        RemoteImageCache images, in AppPalette palette, float scale)
    {
        var max = new Vector2(card.Max.X, card.Min.Y + height);
        var texture = images.Get(ad.MediaUrl!);
        if (texture is null)
        {
            Squircle.FillVerticalGradient(drawList, card.Min, max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, 0.22f)),
                ImGui.GetColorU32(Palette.WithAlpha(palette.Accent, 0.08f)));
            AppSkin.Icon(drawList, (card.Min + max) * 0.5f, AdCategories.Icon(ad.Category).ToIconString(),
                Palette.WithAlpha(palette.Accent, 0.75f), 1.4f);
            return;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, max.X - card.Min.X, height);
        drawList.AddImageRounded(texture.Handle, card.Min, max, uv0, uv1, 0xFFFFFFFFu, rounding,
            ImDrawFlags.RoundCornersTop);
        if (PostMedia.Photos(ad.MediaUrls, ad.MediaUrl).Length > 1)
        {
            MultiPhotoBadge.Draw(drawList, new Vector2(max.X - 12f * scale, card.Min.Y + 12f * scale), scale);
        }
    }

    private static string StatusText(AdDto ad, long nowUnix, out Vector4 color, out bool live)
    {
        live = false;
        if (ad.Archetype == AdArchetypes.Place)
        {
            var state = AdText.OpenState(ad, nowUnix);
            if (state.IsOpen)
            {
                live = true;
                color = OpenGreen;
                return Loc.T(L.YellowPages.OpenNow);
            }

            color = new Vector4(1f, 1f, 1f, 1f);
            if (state.NextOpeningUnix > 0)
            {
                color = OpenGreen with { W = 0.85f };
                return Loc.T(L.YellowPages.OpensAt,
                    $"{TimeText.DayLabel(state.NextOpeningUnix)} {TimeText.Clock(state.NextOpeningUnix)}");
            }

            return string.Empty;
        }

        if (ad.Archetype == AdArchetypes.Service)
        {
            color = new Vector4(0.98f, 0.72f, 0.30f, 1f);
            return AdCategories.IsLinkOnly(ad.Category)
                ? Loc.T(L.YellowPages.ModBadge)
                : AdText.PriceLine(ad);
        }

        color = new Vector4(0.98f, 0.72f, 0.30f, 1f);
        return ad.SlotsLine;
    }

    private static void DrawOpenDot(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var pulse = 0.55f + 0.45f * Pulse.Wave(Pulse.Calm);
        drawList.AddCircle(center, 5.2f * scale, ImGui.GetColorU32(Palette.WithAlpha(OpenGreen, 0.40f * pulse)), 20,
            1.3f * scale);
        drawList.AddCircleFilled(center, 3.1f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(OpenGreen, 0.65f + 0.35f * pulse)), 20);
    }

    private static void DrawDescription(ImDrawListPtr drawList, AdDto ad, float left, float top, float width,
        float scale, Vector4 ink)
    {
        if (ad.Body.Length == 0)
        {
            return;
        }

        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight))
        {
            Plugin.Fonts.NoticeText(ad.Body);
            var lines = Typography.WrapCurrent(ad.Body, textWidth);
            var count = Math.Min(lines.Length, MaxDescriptionLines);
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var font = ImGui.GetFont();
            var fontSize = ImGui.GetFontSize();
            var packed = ImGui.GetColorU32(ink);
            for (var index = 0; index < count; index++)
            {
                drawList.AddText(font, fontSize, new Vector2(left, top + index * lineHeight), packed, lines[index]);
            }
        }
    }

    private static void DrawMetaRow(ImDrawListPtr drawList, AdDto ad, float left, float right, float top,
        in AppPalette palette, float scale)
    {
        var metaRight = right;
        if (ad.AfterDark)
        {
            var label = Loc.T(L.YellowPages.AfterDarkChip);
            var textSize = Typography.Measure(label, TextStyles.Caption1);
            var chipHeight = 18f * scale;
            var chipWidth = textSize.X + 14f * scale;
            var min = new Vector2(metaRight - chipWidth, top + textSize.Y * 0.5f - chipHeight * 0.5f);
            var max = new Vector2(metaRight, min.Y + chipHeight);
            Squircle.Fill(drawList, min, max, chipHeight * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(AfterDarkPink, 0.18f)));
            Typography.Draw(drawList, new Vector2(min.X + 7f * scale, top), label,
                Palette.WithAlpha(AfterDarkPink, 0.9f), TextStyles.Caption1);
            metaRight = min.X - 8f * scale;
        }

        if (ad.MediaUrls.Length > 0)
        {
            var photos = ad.MediaUrls.Length.ToString(Loc.Culture);
            var photosSize = Typography.Measure(photos, TextStyles.Footnote);
            metaRight -= photosSize.X;
            Typography.Draw(drawList, new Vector2(metaRight, top), photos, palette.MutedInk, TextStyles.Footnote);
            AppSkin.Icon(drawList, new Vector2(metaRight - 10f * scale, top + photosSize.Y * 0.5f),
                FontAwesomeIcon.Camera.ToIconString(), Palette.WithAlpha(palette.MutedInk, 0.8f), 0.55f);
            metaRight -= 26f * scale;
        }

        var category = Loc.T(AdCategories.Label(ad.Category));
        var world = ad.WorldId > 0 ? LocationShare.WorldName((uint)ad.WorldId) : string.Empty;
        var meta = world.Length > 0 ? $"{category} · {world}" : category;
        Marquee.DrawLeftAuto(drawList, "yellowpages.card.meta." + ad.Id, meta, left, top,
            MathF.Max(1f, metaRight - left - 8f * scale), TextStyles.Footnote, palette.MutedInk);
    }

    private static int DescriptionLines(AdDto ad, float width, float scale, out float lineHeight)
    {
        var textWidth = width - Pad * 2f * scale;
        using (Plugin.Fonts.Push(TextStyles.Subheadline.Scale, TextStyles.Subheadline.Weight))
        {
            lineHeight = ImGui.GetTextLineHeightWithSpacing();
            if (ad.Body.Length == 0)
            {
                return 0;
            }

            Plugin.Fonts.NoticeText(ad.Body);
            var lines = Typography.WrapCurrent(ad.Body, textWidth);
            return Math.Min(lines.Length, MaxDescriptionLines);
        }
    }
}
