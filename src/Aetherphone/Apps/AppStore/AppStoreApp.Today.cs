using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float HeroAspect = 1.08f;
    private const float HeroMinHeight = 300f;
    private const float HeroMaxHeight = 420f;
    private const float HeroPad = 18f;
    private const float HeroEyebrowGap = 4f;
    private const float HeroStripHeight = 66f;
    private const float HeroStripInset = 10f;
    private const float HeroStripIcon = 42f;
    private const float HeroStageGap = 14f;
    private const float HeroIconSize = 108f;
    private const float HeroHaloScale = 1.9f;
    private const float HeroHaloAlpha = 0.16f;
    private const float HeroTopLift = 0.16f;
    private const float HeroBottomDarken = 0.55f;
    private const float HeroEdgeAlpha = 0.30f;
    private const float HeroEyebrowAlpha = 0.78f;
    private const float HeroSubtitleAlpha = 0.86f;
    private const float HeroStripDarken = 0.38f;
    private const float HeroStripOpacity = 0.62f;
    private const float WidgetMediumAspect = 0.47f;
    private const float WidgetSmallMax = 150f;
    private const float DateGap = 6f;
    private const float AllSetHeight = 92f;
    private const int TodayUpdateRows = 5;

    private void DrawTodayTab(in PhoneContext context)
    {
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("appstore.today"))
        using (var surface = AppSurface.Begin(navBar.Body))
        {
            TakeScrollReset(surface);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var top = origin.Y;
            Typography.Draw(drawList, new Vector2(origin.X, top), texts.Today(), ui.MutedInk,
                TextStyles.FootnoteEmphasized);
            top += Typography.LineHeight(TextStyles.FootnoteEmphasized) + DateGap * scale;
            if (index.Featured is { } featured)
            {
                var heroHeight = Math.Clamp(width * HeroAspect, HeroMinHeight * scale, HeroMaxHeight * scale);
                var hero = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + heroHeight));
                DrawHero(drawList, hero, featured, scale);
                top = hero.Max.Y + SectionGap * scale;
            }

            if (index.Fresh.Count > 0)
            {
                top = DrawSectionHeader(drawList, new Vector2(origin.X, top), width, Loc.T(L.Store.NewHere), false,
                    out _);
                top = DrawRowCard(drawList, new Vector2(origin.X, top), width, index.Fresh, int.MaxValue, scale);
            }
            else
            {
                top = DrawAllSetCard(drawList, new Vector2(origin.X, top), width, scale);
            }

            if (index.Updates.Count > 0)
            {
                top += SectionGap * scale;
                top = DrawSectionHeader(drawList, new Vector2(origin.X, top), width,
                    texts.UpdatedIn(index.LatestVersion), index.Updates.Count > TodayUpdateRows, out var seeAll);
                top = DrawUpdateCard(drawList, new Vector2(origin.X, top), width, index.Updates, TodayUpdateRows,
                    scale);
                if (seeAll)
                {
                    router.Push(StoreView.ForUpdates(NextSerial()));
                }
            }

            Reserve(origin, width, top);
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.today.nav", Loc.T(L.Store.Today),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty);
    }

    private void DrawHero(ImDrawListPtr drawList, Rect card, IPhoneApp app, float scale)
    {
        var pad = HeroPad * scale;
        var strip = new Rect(
            new Vector2(card.Min.X + HeroStripInset * scale, card.Max.Y - (HeroStripInset + HeroStripHeight) * scale),
            new Vector2(card.Max.X - HeroStripInset * scale, card.Max.Y - HeroStripInset * scale));
        var pillCenter = new Vector2(strip.Max.X - pad - StatePillWidth * 0.5f * scale, strip.Center.Y);
        var pill = new Rect(pillCenter - new Vector2(StatePillWidth, StatePillHeight) * 0.5f * scale,
            pillCenter + new Vector2(StatePillWidth, StatePillHeight) * 0.5f * scale);
        var overPill = UiInteract.Hover(pill.Min, pill.Max);
        var hovered = UiInteract.Hover(card.Min, card.Max) && !overPill;
        float press;
        using (UiScope.PushId("appstore.hero"))
        {
            press = PressFx.Scale(ImGui.GetID(app.Id), hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left),
                PressFx.CardPressedScale);
        }

        var center = card.Center;
        var half = card.Size * 0.5f * press;
        var body = new Rect(center - half, center + half);
        var rounding = Metrics.Radius.Grouped * scale;
        Squircle.FillVerticalGradient(drawList, body.Min, body.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(app.Accent, HeroTopLift)),
            ImGui.GetColorU32(Palette.Darken(app.Accent, HeroBottomDarken)));
        Squircle.Stroke(drawList, body.Min, body.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(app.Accent, 0.45f), HeroEdgeAlpha)), 1f * scale);

        var entry = AppStoreCatalog.For(app.Id);
        var textLeft = body.Min.X + pad;
        var textWidth = body.Width - pad * 2f;
        var cursorY = body.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(textLeft, cursorY), Loc.T(L.Store.AppOfTheDay),
            Palette.WithAlpha(White, HeroEyebrowAlpha), TextStyles.FootnoteEmphasized);
        cursorY += Typography.LineHeight(TextStyles.FootnoteEmphasized) + HeroEyebrowGap * scale;
        Marquee.DrawLeft(new MarqueeId("appstore.hero.name.", app.Id), app.DisplayName, textLeft, cursorY, textWidth,
            TextStyles.Title1, White, hovered);
        cursorY += Typography.LineHeight(TextStyles.Title1);
        Typography.Draw(drawList, new Vector2(textLeft, cursorY),
            Typography.FitText(Loc.T(entry.Subtitle), textWidth, TextStyles.Subheadline),
            Palette.WithAlpha(White, HeroSubtitleAlpha), TextStyles.Subheadline);
        cursorY += Typography.LineHeight(TextStyles.Subheadline) + HeroStageGap * scale;

        var stage = new Rect(new Vector2(body.Min.X + pad, cursorY),
            new Vector2(body.Max.X - pad, strip.Min.Y - HeroStageGap * scale));
        DrawHeroStage(drawList, stage, app, scale);

        Material.AccentGlass(drawList, strip.Min, strip.Max, strip.Height * 0.5f, scale,
            Palette.Darken(app.Accent, HeroStripDarken), HeroStripOpacity);
        var iconCenter = new Vector2(strip.Min.X + pad * 0.5f + HeroStripIcon * 0.5f * scale, strip.Center.Y);
        DrawIcon(drawList, iconCenter, HeroStripIcon * scale, app);
        var nameLeft = iconCenter.X + (HeroStripIcon * 0.5f + RowTextGap) * scale;
        var nameWidth = MathF.Max(1f, pill.Min.X - nameLeft - RowTextGap * scale);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var nameY = strip.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(nameLeft, nameY),
            Typography.FitText(app.DisplayName, nameWidth, TextStyles.Headline), White, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(nameLeft, nameY + nameHeight),
            Typography.FitText(Loc.T(AppStoreCatalog.Name(entry.Category)), nameWidth, TextStyles.Footnote),
            Palette.WithAlpha(White, HeroEyebrowAlpha), TextStyles.Footnote);
        DrawStatePill(drawList, pill, app, overPill, ui.Ink with { Ink = White }, White, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, hovered))
        {
            OpenDetail(app.Id);
        }
    }

    private void DrawHeroStage(ImDrawListPtr drawList, Rect stage, IPhoneApp app, float scale)
    {
        if (stage.Height <= 0f || stage.Width <= 0f)
        {
            return;
        }

        var widget = FirstLiveWidget(app.Id);
        if (widget is not null && widgetHost is not null)
        {
            var size = WidgetSizes.Contains(widget.Sizes, WidgetSize.Medium)
                ? WidgetSize.Medium
                : WidgetSizes.Smallest(widget.Sizes);
            var rect = WidgetRect(stage, size, scale);
            if (rect.Height <= stage.Height)
            {
                WidgetGalleryPreview.Draw(drawList, widgetHost, widget, size, rect, theme, scale,
                    ImGui.GetIO().DeltaTime);
                return;
            }
        }

        var iconSize = MathF.Min(HeroIconSize * scale, stage.Height);
        var halo = ImGui.GetColorU32(Palette.WithAlpha(White, HeroHaloAlpha));
        drawList.AddCircleFilled(stage.Center, iconSize * HeroHaloScale * 0.5f, halo, 48);
        drawList.AddCircleFilled(stage.Center, iconSize * (HeroHaloScale + 1f) * 0.25f, halo, 48);
        DrawIcon(drawList, stage.Center, iconSize, app);
    }

    private static Rect WidgetRect(Rect stage, WidgetSize size, float scale)
    {
        if (size == WidgetSize.Small)
        {
            var side = MathF.Min(MathF.Min(stage.Width, stage.Height), WidgetSmallMax * scale);
            return new Rect(stage.Center - new Vector2(side, side) * 0.5f, stage.Center + new Vector2(side, side) * 0.5f);
        }

        var width = stage.Width;
        var height = size == WidgetSize.Medium ? width * WidgetMediumAspect : width;
        var top = stage.Center.Y - height * 0.5f;
        return new Rect(new Vector2(stage.Min.X, top), new Vector2(stage.Max.X, top + height));
    }

    private float DrawAllSetCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var cardMax = new Vector2(origin.X + width, origin.Y + AllSetHeight * scale);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        var iconCenter = new Vector2(origin.X + Metrics.Space.Lg * scale + RowIconSize * 0.5f * scale,
            (origin.Y + cardMax.Y) * 0.5f);
        var surface = IconTile.Surface(ui.Accent);
        var iconHalf = RowIconSize * 0.5f * scale;
        IconTile.FillShaded(drawList, iconCenter - new Vector2(iconHalf), iconCenter + new Vector2(iconHalf),
            RowIconSize * Metrics.Radius.TileFactor * scale, surface);
        AppSkin.Icon(drawList, iconCenter, IconGlyph.Of(FontAwesomeIcon.Check), White, 1.4f);
        var textLeft = iconCenter.X + iconHalf + RowTextGap * scale;
        var textWidth = MathF.Max(1f, cardMax.X - Metrics.Space.Lg * scale - textLeft);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var hint = Loc.T(L.Store.EverythingInstalledHint);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        var titleY = iconCenter.Y - (titleHeight + hintHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, titleY),
            Typography.FitText(Loc.T(L.Store.EverythingInstalled), textWidth, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawWrappedLeft(new Vector2(textLeft, titleY + titleHeight), hint, ui.MutedInk,
            TextStyles.Subheadline, textWidth);
        return cardMax.Y;
    }
}
