using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private const float SectionGap = 24f;
    private const float HeaderGap = 10f;
    private const float SearchGap = 16f;
    private const float BottomBreathing = 28f;
    private const float HeroHeight = 128f;
    private const float HeroPad = 18f;
    private const float HeroRingRadius = 44f;
    private const float HeroRingThickness = 9f;
    private const float HeroTextGap = 18f;
    private const float HeroLineGap = 2f;
    private const float HeroGlowCoverage = 0.7f;
    private const float HeroGlowStrength = 0.12f;
    private const float TileGap = 12f;
    private const float TileHeight = 116f;
    private const float TilePad = 14f;
    private const float TileIconSize = 38f;
    private const float TileRingRadius = 22f;
    private const float TileRingThickness = 4.5f;
    private const float TileStateGlyph = 15f;
    private const float TileGlowCoverage = 0.55f;
    private const float TileGlowRest = 0.07f;
    private const float TileGlowHover = 0.12f;
    private const float RingTrackAlpha = 0.14f;
    private const int TileColumns = 2;
    private const string WidestPercent = "100%";

    private readonly Spring[] tileFills = new Spring[CollectionCategories.All.Length];
    private Spring heroFill;
    private string rootQuery = string.Empty;

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawSearchField(drawList, new Vector2(origin.X, origin.Y), width, ref rootQuery,
                "##collectionsSearchAll", Loc.T(L.Collections.SearchAll), scale);
            cursorY += SearchGap * scale;
            if (rootQuery.Length > 0)
            {
                cursorY = DrawGlobalResults(drawList, new Vector2(origin.X, cursorY), width, navBar.Body, scale);
            }
            else
            {
                cursorY = DrawHome(drawList, new Vector2(origin.X, cursorY), width, scale);
            }

            ReserveTo(origin, width, cursorY + BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "collections.nav", DisplayName, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty);
    }

    private float DrawSearchField(ImDrawListPtr drawList, Vector2 origin, float width, ref string query, string id,
        string hint, float scale)
    {
        var field = new Rect(origin, new Vector2(origin.X + width, origin.Y + GlassField.HeightUnits * scale));
        Material.ThemedGlass(drawList, field.Min, field.Max, GlassField.Radius(field), scale, theme);
        GlassField.Search(drawList, field, id, hint, ref query, theme, scale, SearchMaxLength, false);
        return field.Max.Y;
    }

    private float DrawHome(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var snapshot = Digest();
        var cursorY = origin.Y;
        if (!tracking)
        {
            cursorY += CollectionsArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width,
                FontAwesomeIcon.UserLock, ui.Accent, Loc.T(L.Collections.SignedOutTitle),
                Loc.T(L.Collections.SignedOutHint), scale);
            cursorY += SectionGap * scale;
        }
        else if (snapshot.HasOverall)
        {
            cursorY = DrawHero(drawList, new Vector2(origin.X, cursorY), width, snapshot, scale);
            cursorY += SectionGap * scale;
        }

        cursorY = DrawTiles(drawList, new Vector2(origin.X, cursorY), width, snapshot, scale);
        cursorY = DrawRowSection(drawList, origin.X, cursorY, width, Loc.T(L.Collections.RecentTitle), string.Empty,
            snapshot.Recent, "collections.recent", scale);
        cursorY = DrawRowSection(drawList, origin.X, cursorY, width, Loc.T(L.Collections.WishlistTitle), string.Empty,
            snapshot.Wishlist, "collections.wishlist", scale);
        cursorY = DrawRowSection(drawList, origin.X, cursorY, width, Loc.T(L.Collections.UpNextTitle),
            Loc.T(L.Collections.UpNextHint), snapshot.UpNext, "collections.upnext", scale);
        return cursorY;
    }

    private float DrawRowSection(ImDrawListPtr drawList, float left, float top, float width, string title,
        string hint, List<DigestRow> rows, string anchor, float scale)
    {
        if (rows.Count == 0)
        {
            return top;
        }

        var cursorY = top + SectionGap * scale;
        cursorY += CollectionsArt.SectionHeader(drawList, new Vector2(left, cursorY), width, title, ui.TitleInk);
        if (hint.Length > 0)
        {
            cursorY += HeroLineGap * scale;
            cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), hint, ui.MutedInk, TextStyles.Footnote,
                width);
        }

        cursorY += HeaderGap * scale;
        return DrawRowGroup(drawList, new Vector2(left, cursorY), width, rows, anchor);
    }

    private float DrawHero(ImDrawListPtr drawList, Vector2 origin, float width, CollectionsDigest snapshot,
        float scale)
    {
        var max = new Vector2(origin.X + width, origin.Y + HeroHeight * scale);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, ui.Accent, HeroGlowCoverage, HeroGlowStrength);
        UiAnchors.Report("collections.hero", new Rect(origin, max));

        var pad = HeroPad * scale;
        var ringRadius = HeroRingRadius * scale;
        var center = new Vector2(origin.X + pad + ringRadius, (origin.Y + max.Y) * 0.5f);
        var fill = heroFill.Step(snapshot.OverallFraction, Motion.Sheet, FrameDelta());
        CollectionsArt.Ring(drawList, center, ringRadius, HeroRingThickness * scale, fill,
            Palette.WithAlpha(ui.TitleInk, RingTrackAlpha), ui.Accent, snapshot.OverallPercent, ui.TitleInk,
            TextStyles.Title2, WidestPercent);

        var textLeft = center.X + ringRadius + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var name = snapshot.CharacterName;
        var caption = snapshot.NewThisWeek.Length > 0 ? snapshot.NewThisWeek : Loc.T(L.Collections.HeroCaption);
        var nameFitted = Typography.FitText(name, textWidth, TextStyles.Headline);
        var countFitted = Typography.FitText(snapshot.OverallCount, textWidth, TextStyles.Title2);
        var nameHeight = name.Length > 0
            ? Typography.Measure(nameFitted, TextStyles.Headline).Y + HeroLineGap * scale
            : 0f;
        var countHeight = Typography.Measure(countFitted, TextStyles.Title2).Y;
        var captionHeight = Typography.MeasureWrappedBlock(caption, TextStyles.Footnote, textWidth).Y;
        var top = center.Y - (nameHeight + countHeight + captionHeight + HeroLineGap * scale) * 0.5f;
        if (name.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(textLeft, top), nameFitted, ui.MutedInk, TextStyles.Headline);
            top += nameHeight;
        }

        Typography.Draw(drawList, new Vector2(textLeft, top), countFitted, ui.TitleInk, TextStyles.Title2);
        top += countHeight + HeroLineGap * scale;
        var captionInk = snapshot.NewThisWeek.Length > 0 ? ui.Accent : ui.MutedInk;
        Typography.DrawWrappedLeft(new Vector2(textLeft, top), caption, captionInk, TextStyles.Footnote, textWidth);
        return max.Y;
    }

    private float DrawTiles(ImDrawListPtr drawList, Vector2 origin, float width, CollectionsDigest snapshot,
        float scale)
    {
        var gap = TileGap * scale;
        var tileWidth = (width - gap) / TileColumns;
        var tileHeight = TileHeight * scale;
        var categories = CollectionCategories.All;
        var rows = (categories.Length + TileColumns - 1) / TileColumns;
        for (var index = 0; index < categories.Length; index++)
        {
            var column = index % TileColumns;
            var row = index / TileColumns;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var rect = new Rect(min, min + new Vector2(tileWidth, tileHeight));
            var category = categories[index];
            if (category == CollectionCategory.Mounts)
            {
                UiAnchors.Report("collections.tile.mounts", rect);
            }

            if (DrawTile(drawList, rect, category, snapshot.Tiles[index], scale))
            {
                OpenCategory(category, string.Empty);
            }
        }

        return origin.Y + rows * tileHeight + (rows - 1) * gap;
    }

    private bool DrawTile(ImDrawListPtr drawList, Rect rect, CollectionCategory category, TileState tile, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID($"collections.tile.{(int)category}"), pressed,
            PressFx.CardPressedScale);
        var half = rect.Size * 0.5f * press;
        var min = rect.Center - half;
        var max = rect.Center + half;
        var radius = Metrics.Radius.Grouped * scale;
        var tint = CollectionsArt.Tint(category);
        ui.Card(drawList, min, max, radius, true);
        Material.TopGlow(drawList, min, max, radius, tint, TileGlowCoverage, hovered ? TileGlowHover : TileGlowRest);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var pad = TilePad * scale;
        var iconSize = TileIconSize * scale;
        CollectionsArt.CategoryTile(drawList, new Vector2(min.X + pad + iconSize * 0.5f, min.Y + pad + iconSize * 0.5f),
            iconSize, category);
        DrawTileState(drawList, new Vector2(max.X - pad - TileRingRadius * scale, min.Y + pad + iconSize * 0.5f),
            category, tile, scale);

        var textWidth = MathF.Max(1f, max.X - min.X - pad * 2f);
        var countHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var countTop = max.Y - pad - countHeight;
        var nameTop = countTop - nameHeight;
        var name = Typography.FitText(Loc.T(CollectionText.Label(category)), textWidth, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(min.X + pad, nameTop), name, ui.TitleInk, TextStyles.Headline);
        if (tile.Count.Length > 0)
        {
            var count = Typography.FitText(tile.Count, textWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(min.X + pad, countTop), count, ui.MutedInk, TextStyles.Footnote);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private void DrawTileState(ImDrawListPtr drawList, Vector2 center, CollectionCategory category, TileState tile,
        float scale)
    {
        var radius = TileRingRadius * scale;
        var thickness = TileRingThickness * scale;
        var track = Palette.WithAlpha(ui.TitleInk, RingTrackAlpha);
        ref var fill = ref tileFills[(int)category];
        switch (tile.Mode)
        {
            case TileMode.Ring:
                var value = fill.Step(tile.Fraction, Motion.Sheet, FrameDelta());
                CollectionsArt.Ring(drawList, center, radius, thickness, value, track, ui.Accent, tile.Percent,
                    ui.TitleInk, TextStyles.FootnoteEmphasized, WidestPercent);
                return;
            case TileMode.Loading:
                ProgressRing.Track(drawList, center, radius, thickness, track);
                ProgressRing.Sweep(center, radius, thickness, ui.Accent, 900.0, 1.6f, 0.85f);
                return;
            case TileMode.Private:
                ProgressRing.Track(drawList, center, radius, thickness, track);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Lock, ui.MutedInk, TileStateGlyph * scale);
                return;
            case TileMode.NotLinked:
                ProgressRing.Track(drawList, center, radius, thickness, track);
                ProgressRing.CenterIcon(drawList, center, FontAwesomeIcon.Link, ui.MutedInk, TileStateGlyph * scale);
                return;
            default:
                fill.SnapTo(0f);
                return;
        }
    }

    private static float FrameDelta() => MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
}
