using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float DetailIconSize = 104f;
    private const float DetailTextGap = 16f;
    private const float DetailPillWidth = 84f;
    private const float DetailPillHeight = Button.SmallHeight;
    private const float DetailButtonGap = 10f;
    private const float RemoveRowHeight = 50f;
    private const float StripHeight = 82f;
    private const float StripCellPad = 6f;
    private const float StripGlyphScale = 1.25f;
    private const float StripDividerInset = 18f;
    private const float BulletGap = 8f;
    private const float BulletRadius = 2.5f;
    private const float BulletIndent = 14f;
    private const float WidgetGap = 12f;
    private const float WidgetCaptionGap = 6f;
    private const float NoteGlyphScale = 1.1f;
    private const float NotePad = 14f;
    private const int MaxReleaseLines = 4;
    private const int LanguageCount = 9;

    private void DrawDetail(in PhoneContext context, in StoreView view, int depth)
    {
        var app = Find(view.AppId);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId("appstore.detail"))
        using (UiScope.PushId(view.Serial))
        using (AppSurface.Begin(navBar.Body))
        {
            if (app is not null)
            {
                DrawDetailBody(app);
            }
        }

        AppHeader.EndLargeTitle(in navBar, context, "appstore.detail.nav", app?.DisplayName ?? Loc.T(L.Store.Apps),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, BackTitle(depth), back);
        if (app is null)
        {
            router.Pop(false);
        }
    }

    private void DrawDetailBody(IPhoneApp app)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var entry = AppStoreCatalog.For(app.Id);
        var top = DrawDetailHead(drawList, origin, width, app, entry, scale);
        top = DrawInfoStrip(drawList, new Vector2(origin.X, top + SectionGap * scale), width, app, entry, scale);
        if (index.TryRelease(app.Id, out var release))
        {
            top = DrawWhatsNew(drawList, new Vector2(origin.X, top + SectionGap * scale), width, release, scale);
        }

        top = DrawWidgetShelf(drawList, new Vector2(origin.X, top), width, app, scale);
        top = DrawSectionHeader(drawList, new Vector2(origin.X, top + SectionGap * scale), width,
            Loc.T(L.Store.Description), false, out _);
        top += Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(entry.Body), ui.BodyInk,
            TextStyles.Callout, width);
        if (!AppInstaller.CanUninstall(app.Id))
        {
            top = DrawBuiltInNote(drawList, new Vector2(origin.X, top + SectionGap * scale), width, scale);
        }
        else if (installer.IsInstalled(app.Id) && !installing.ContainsKey(app.Id))
        {
            top = DrawRemoveRow(new Vector2(origin.X, top + SectionGap * scale), width, app, scale);
        }

        Reserve(origin, width, top);
    }

    private float DrawDetailHead(ImDrawListPtr drawList, Vector2 origin, float width, IPhoneApp app,
        in StoreEntry entry, float scale)
    {
        var iconSize = DetailIconSize * scale;
        DrawIcon(drawList, new Vector2(origin.X + iconSize * 0.5f, origin.Y + iconSize * 0.5f), iconSize, app);
        var textLeft = origin.X + iconSize + DetailTextGap * scale;
        var textWidth = MathF.Max(1f, origin.X + width - textLeft);
        var subtitleHeight = Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y), Loc.T(entry.Subtitle),
            ui.MutedInk, TextStyles.Subheadline, textWidth);
        Typography.Draw(drawList, new Vector2(textLeft, origin.Y + subtitleHeight),
            Typography.FitText(Loc.T(AppStoreCatalog.Name(entry.Category)), textWidth, TextStyles.Footnote),
            Palette.Lighten(app.Accent, PillInkLift), TextStyles.Footnote);

        var pillTop = origin.Y + iconSize - DetailPillHeight * scale;
        var pill = new Rect(new Vector2(textLeft, pillTop),
            new Vector2(textLeft + DetailPillWidth * scale, pillTop + DetailPillHeight * scale));
        if (!app.IsAvailable)
        {
            Typography.Draw(drawList,
                new Vector2(textLeft, pill.Center.Y - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                Typography.FitText(Loc.T(L.Store.Unavailable), textWidth, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
            return origin.Y + iconSize;
        }

        UiAnchors.Report("appstore.detail.get", pill);
        DrawStatePill(drawList, pill, app, UiInteract.Hover(pill.Min, pill.Max), ui.Ink,
            Palette.Lighten(app.Accent, PillInkLift), scale);
        if (!AppInstaller.CanUninstall(app.Id))
        {
            DrawBuiltInTag(drawList, new Vector2(pill.Max.X + DetailButtonGap * scale, pill.Center.Y), scale);
        }

        return origin.Y + iconSize;
    }

    private float DrawRemoveRow(Vector2 origin, float width, IPhoneApp app, float scale)
    {
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + RemoveRowHeight * scale));
        var hovered = UiInteract.Hover(row.Min, row.Max);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            Squircle.Fill(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha)));
        }

        Typography.DrawCentered(drawList, row.Center,
            Typography.FitText(Loc.T(L.Store.Remove), row.Width - DetailButtonGap * 2f * scale, TextStyles.Body),
            ui.Theme.Danger, TextStyles.Body);
        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            AskRemove(app);
        }

        return row.Max.Y;
    }

    private void DrawBuiltInTag(ImDrawListPtr drawList, Vector2 leftCenter, float scale)
    {
        var glyph = IconGlyph.Of(FontAwesomeIcon.Lock);
        var glyphSize = Typography.LineHeight(TextStyles.Footnote);
        AppSkin.Icon(drawList, new Vector2(leftCenter.X + glyphSize * 0.5f, leftCenter.Y), glyph, ui.MutedInk, 0.8f);
        Typography.Draw(drawList,
            new Vector2(leftCenter.X + glyphSize + Metrics.Space.Xxs * scale, leftCenter.Y - glyphSize * 0.5f),
            Loc.T(L.Store.BuiltIn), ui.MutedInk, TextStyles.Footnote);
    }

    private float DrawInfoStrip(ImDrawListPtr drawList, Vector2 origin, float width, IPhoneApp app,
        in StoreEntry entry, float scale)
    {
        var widgetCount = WidgetsFor(app.Id).Count;
        var cells = widgetCount > 0 ? 4 : 3;
        var cardMax = new Vector2(origin.X + width, origin.Y + StripHeight * scale);
        var hairline = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha * 2f));
        drawList.AddLine(origin, new Vector2(cardMax.X, origin.Y), hairline, 1f);
        drawList.AddLine(new Vector2(origin.X, cardMax.Y), cardMax, hairline, 1f);
        var cellWidth = width / cells;
        var category = entry.Category;
        DrawStripCell(drawList, origin, cellWidth, 0, Loc.T(L.Store.Category),
            IconGlyph.Of(AppStoreCatalog.Icon(category)), true, Loc.T(AppStoreCatalog.Name(category)), scale);
        DrawStripCell(drawList, origin, cellWidth, 1, Loc.T(L.Store.Developer),
            IconGlyph.Of(FontAwesomeIcon.UserCircle), true, DeveloperName(app), scale);
        DrawStripCell(drawList, origin, cellWidth, 2, Loc.T(L.Store.Languages), texts.Count(LanguageCount), false,
            string.Empty, scale);
        if (widgetCount > 0)
        {
            DrawStripCell(drawList, origin, cellWidth, 3, Loc.T(L.Store.Widgets), texts.Count(widgetCount), false,
                string.Empty, scale);
        }

        return cardMax.Y;
    }

    private void DrawStripCell(ImDrawListPtr drawList, Vector2 origin, float cellWidth, int cellIndex, string caption,
        string value, bool glyph, string footer, float scale)
    {
        var left = origin.X + cellIndex * cellWidth;
        var centerX = left + cellWidth * 0.5f;
        var innerWidth = MathF.Max(1f, cellWidth - StripCellPad * 2f * scale);
        if (cellIndex > 0)
        {
            drawList.AddLine(new Vector2(left, origin.Y + StripDividerInset * scale),
                new Vector2(left, origin.Y + (StripHeight - StripDividerInset) * scale),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HairlineAlpha * 2f)), 1f);
        }

        var captionHeight = Typography.LineHeight(TextStyles.Caption1);
        var valueHeight = Typography.LineHeight(TextStyles.Title3);
        var footerHeight = footer.Length > 0 ? Typography.LineHeight(TextStyles.Caption1) : 0f;
        var top = origin.Y + (StripHeight * scale - captionHeight - valueHeight - footerHeight) * 0.5f;
        Typography.DrawCentered(drawList, new Vector2(centerX, top + captionHeight * 0.5f),
            Typography.FitText(caption, innerWidth, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
        var valueCenter = new Vector2(centerX, top + captionHeight + valueHeight * 0.5f);
        if (glyph)
        {
            AppSkin.Icon(drawList, valueCenter, value, ui.MutedInk, StripGlyphScale);
        }
        else
        {
            Typography.DrawCentered(drawList, valueCenter, value, ui.MutedInk, TextStyles.Title3);
        }

        if (footer.Length == 0)
        {
            return;
        }

        Typography.DrawCentered(drawList,
            new Vector2(centerX, top + captionHeight + valueHeight + footerHeight * 0.5f),
            Typography.FitText(footer, innerWidth, TextStyles.Caption1), ui.MutedInk, TextStyles.Caption1);
    }

    private float DrawWhatsNew(ImDrawListPtr drawList, Vector2 origin, float width, in StoreRelease release,
        float scale)
    {
        var top = DrawSectionHeader(drawList, origin, width, Loc.T(L.Store.WhatsNew), false, out _);
        var version = texts.Version(release.Version);
        var date = texts.ReleaseDate(release.Date);
        var metaHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(origin.X, top), version, ui.MutedInk, TextStyles.Subheadline);
        var dateWidth = Typography.Measure(date, TextStyles.Subheadline).X;
        Typography.Draw(drawList, new Vector2(origin.X + width - dateWidth, top), date, ui.MutedInk,
            TextStyles.Subheadline);
        top += metaHeight + BulletGap * scale;
        var lines = Math.Min(release.Highlights.Count, MaxReleaseLines);
        var textLeft = origin.X + BulletIndent * scale;
        var textWidth = MathF.Max(1f, width - BulletIndent * scale);
        var bodyLine = Typography.LineHeight(TextStyles.Body);
        for (var lineIndex = 0; lineIndex < lines; lineIndex++)
        {
            drawList.AddCircleFilled(new Vector2(origin.X + BulletRadius * scale, top + bodyLine * 0.5f),
                BulletRadius * scale, ImGui.GetColorU32(ui.MutedInk), 12);
            top += Typography.DrawWrappedLeft(new Vector2(textLeft, top), Loc.T(release.Highlights[lineIndex]),
                ui.BodyInk, TextStyles.Body, textWidth);
            top += BulletGap * scale;
        }

        return top;
    }

    private float DrawWidgetShelf(ImDrawListPtr drawList, Vector2 origin, float width, IPhoneApp app, float scale)
    {
        var widgets = WidgetsFor(app.Id);
        if (widgets.Count == 0 || widgetHost is null || widgetRegistry is null)
        {
            return origin.Y;
        }

        var top = DrawSectionHeader(drawList, new Vector2(origin.X, origin.Y + SectionGap * scale), width,
            Loc.T(L.Store.Widgets), false, out _);
        var gap = WidgetGap * scale;
        var smallSide = (width - gap) * 0.5f;
        var captionHeight = Typography.LineHeight(TextStyles.Footnote) + WidgetCaptionGap * scale;
        var column = 0;
        var rowBottom = top;
        var delta = ImGui.GetIO().DeltaTime;
        for (var widgetIndex = 0; widgetIndex < widgets.Count; widgetIndex++)
        {
            var widget = widgets[widgetIndex];
            if (!widgetRegistry.IsAvailable(widget))
            {
                continue;
            }

            var size = WidgetSizes.Smallest(widget.Sizes);
            Rect rect;
            if (size == WidgetSize.Small)
            {
                var left = origin.X + column * (smallSide + gap);
                rect = new Rect(new Vector2(left, top), new Vector2(left + smallSide, top + smallSide));
                column++;
            }
            else
            {
                if (column > 0)
                {
                    top = rowBottom + gap;
                    column = 0;
                }

                var height = size == WidgetSize.Medium ? width * WidgetMediumAspect : width;
                rect = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
                column = CategoryColumns;
            }

            WidgetGalleryPreview.Draw(drawList, widgetHost, widget, size, rect, theme, scale, delta);
            Typography.DrawCentered(drawList,
                new Vector2(rect.Center.X, rect.Max.Y + captionHeight * 0.5f + WidgetCaptionGap * 0.5f * scale),
                Typography.FitText(widget.DisplayName, rect.Width, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
            rowBottom = MathF.Max(rowBottom, rect.Max.Y + captionHeight);
            if (column >= CategoryColumns)
            {
                top = rowBottom + gap;
                column = 0;
            }
        }

        return MathF.Max(rowBottom, top - gap);
    }

    private float DrawBuiltInNote(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = NotePad * scale;
        var glyphSize = Typography.LineHeight(TextStyles.Subheadline);
        var textLeft = origin.X + pad + glyphSize + Metrics.Space.Sm * scale;
        var textWidth = MathF.Max(1f, origin.X + width - pad - textLeft);
        var hint = Loc.T(L.Store.BuiltInHint);
        var textHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, textWidth).Y;
        var cardMax = new Vector2(origin.X + width, origin.Y + textHeight + pad * 2f);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        AppSkin.Icon(drawList, new Vector2(origin.X + pad + glyphSize * 0.5f, origin.Y + pad + glyphSize * 0.5f),
            IconGlyph.Of(FontAwesomeIcon.Lock), ui.MutedInk, NoteGlyphScale);
        Typography.DrawWrappedLeft(new Vector2(textLeft, origin.Y + pad), hint, ui.MutedInk, TextStyles.Subheadline,
            textWidth);
        return cardMax.Y;
    }

    private static string DeveloperName(IPhoneApp app) => app.Id switch
    {
        "health" => "Yozora",
        "housing" => "Yozora",
        "hunts" => "Deldee",
        "jobs" => "K.I.R.O",
        _ => Loc.T(L.Store.DeveloperName),
    };
}
