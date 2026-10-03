using Aetherphone.Apps.Music.Components;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music;

internal sealed partial class MusicApp
{
    private const float HeroAdvanceSeconds = 6f;
    private const float HeroImageAspect = 0.56f;
    private const float HeroDragSlop = 6f;
    private const float HeroFlingFraction = 0.18f;
    private const float HeroDotsGap = 10f;
    private const float HeroTextGap = 2f;
    private const float HeroDotsHeight = 14f;
    private const float HeroScrimAlpha = 0.55f;

    private Spring heroPosition;
    private int heroPage;
    private float heroTimer;
    private bool heroPressed;
    private bool heroDragging;
    private float heroPressX;
    private float heroPressPosition;

    private void DrawHeroCarousel(float scale)
    {
        var heroes = MusicCatalogShelves.Heroes;
        var heroCatalog = Catalog;
        for (var index = 0; index < heroes.Length; index++)
        {
            heroCatalog.Ensure(heroes[index].Shelf.Key, heroes[index].Shelf.Request);
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var inset = MusicUi.Inset * scale;
        var cardWidth = width - inset * 2f;
        var gap = ShelfRail.TileGap * scale;
        var kickerHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textHeight = kickerHeight + titleHeight + subtitleHeight + HeroTextGap * 2f * scale;
        var imageHeight = cardWidth * HeroImageAspect;
        var cardHeight = textHeight + Metrics.Space.Sm * scale + imageHeight;
        var rail = new Rect(origin, origin + new Vector2(width, cardHeight));
        var stride = cardWidth + gap;
        UpdateHeroGesture(rail, stride, heroes.Length);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(rail.Min, rail.Max, true);
        var tappedIndex = -1;
        for (var index = 0; index < heroes.Length; index++)
        {
            var left = origin.X + inset + (index - heroPosition.Value) * stride;
            if (left > rail.Max.X || left + cardWidth < rail.Min.X)
            {
                continue;
            }

            var cardMin = new Vector2(left, origin.Y);
            var cardMax = new Vector2(left + cardWidth, origin.Y + cardHeight);
            DrawHeroCard(drawList, heroes[index], cardMin, cardWidth, textHeight, imageHeight, scale);
            var cardHovered = !heroDragging && UiInteract.Hover(cardMin, cardMax);
            if (UiInteract.Click(cardMin, cardMax, cardHovered))
            {
                tappedIndex = index;
            }
        }

        drawList.PopClipRect();
        var dotsCenter = new Vector2(origin.X + width * 0.5f, rail.Max.Y + HeroDotsGap * scale);
        PhotoCarousel.DrawDots(drawList, dotsCenter, heroes.Length, heroPage, cardWidth, ui.TitleInk);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, cardHeight + (HeroDotsGap + HeroDotsHeight) * scale));
        if (tappedIndex >= 0)
        {
            var shelf = heroes[tappedIndex].Shelf;
            Push(MusicRoute.Genre(shelf.Key, Loc.T(shelf.Title)));
        }
    }

    private void UpdateHeroGesture(Rect rail, float stride, int count)
    {
        var io = ImGui.GetIO();
        var delta = MathF.Min(io.DeltaTime, MaxFrameSeconds);
        var hovered = UiInteract.Hover(rail.Min, rail.Max);
        var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        if (!heroPressed && hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            heroPressed = true;
            heroDragging = false;
            heroPressX = io.MousePos.X;
            heroPressPosition = heroPosition.Value;
        }

        if (heroPressed && down)
        {
            var travel = io.MousePos.X - heroPressX;
            if (!heroDragging && MathF.Abs(travel) > HeroDragSlop * UiScale.Current)
            {
                heroDragging = true;
                UiInteract.CancelPendingTap();
            }

            if (heroDragging)
            {
                var position = Math.Clamp(heroPressPosition - travel / stride, 0f, count - 1);
                heroPosition.SnapTo(position);
            }
        }
        else if (heroPressed)
        {
            heroPressed = false;
            if (heroDragging)
            {
                var moved = heroPosition.Value - heroPressPosition;
                var target = heroPage;
                if (moved > HeroFlingFraction)
                {
                    target = (int)MathF.Ceiling(heroPosition.Value - HeroFlingFraction + 0.5f);
                }
                else if (moved < -HeroFlingFraction)
                {
                    target = (int)MathF.Floor(heroPosition.Value + HeroFlingFraction - 0.5f);
                }

                heroPage = Math.Clamp(target, 0, count - 1);
                heroDragging = false;
            }

            heroTimer = 0f;
        }

        if (!heroPressed)
        {
            heroTimer = hovered ? 0f : heroTimer + delta;
            if (heroTimer >= HeroAdvanceSeconds && count > 1)
            {
                heroTimer = 0f;
                heroPage = (heroPage + 1) % count;
            }

            heroPosition.Step(heroPage, Motion.Sheet, delta);
        }
    }

    private void DrawHeroCard(ImDrawListPtr drawList, in CatalogHero hero, Vector2 min, float cardWidth,
        float textHeight, float imageHeight, float scale)
    {
        var kickerHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.LineHeight(TextStyles.Title3);
        var gap = HeroTextGap * scale;
        Typography.Draw(drawList, min, Typography.FitText(Loc.T(L.Music.New.Featured), cardWidth,
            TextStyles.FootnoteEmphasized), ui.MutedInk, TextStyles.FootnoteEmphasized);
        var titleTop = min.Y + kickerHeight + gap;
        Typography.Draw(drawList, new Vector2(min.X, titleTop),
            Typography.FitText(Loc.T(hero.Shelf.Title), cardWidth, TextStyles.Title3), ui.TitleInk,
            TextStyles.Title3);
        Typography.Draw(drawList, new Vector2(min.X, titleTop + titleHeight + gap),
            Typography.FitText(Loc.T(hero.Subtitle), cardWidth, TextStyles.Subheadline), ui.MutedInk,
            TextStyles.Subheadline);
        var imageMin = new Vector2(min.X, min.Y + textHeight + Metrics.Space.Sm * scale);
        var imageMax = imageMin + new Vector2(cardWidth, imageHeight);
        var radius = imageHeight * ArtworkTile.TileRadiusFraction;
        var songs = Catalog.Songs(hero.Shelf.Key);
        var url = songs.Length > 0 ? songs[0].ThumbnailUrl : string.Empty;
        var texture = url.Length == 0 ? null : images.Sized(url, cardWidth);
        if (texture is not null)
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, cardWidth, imageHeight);
            Squircle.FillImage(drawList, imageMin, imageMax, radius, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }
        else if (songs.Length == 0)
        {
            Skeleton.Bar(drawList, imageMin, imageMax, radius);
        }
        else
        {
            var swatch = ArtGradient.FromName(hero.Shelf.Key);
            Squircle.FillVerticalGradient(drawList, imageMin, imageMax, radius, ImGui.GetColorU32(swatch.Top),
                ImGui.GetColorU32(swatch.Bottom));
        }

        if (songs.Length == 0)
        {
            return;
        }

        var scrimTop = new Vector2(imageMin.X, imageMin.Y + imageHeight * 0.55f);
        Squircle.FillVerticalGradient(drawList, scrimTop, imageMax, radius, ImGui.GetColorU32(Vector4.Zero),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, HeroScrimAlpha)));
        var pad = Metrics.Space.Md * scale;
        var captionHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var caption = Typography.FitText(songs[0].Title, cardWidth - pad * 2f, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(imageMin.X + pad, imageMax.Y - pad - captionHeight), caption, BrowseTileInk,
            TextStyles.FootnoteEmphasized);
    }
}
