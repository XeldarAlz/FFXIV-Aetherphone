using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float DetailIconSize = 96f;
    private const float PreviewHeight = 148f;
    private const int LanguageCount = 9;

    private void DrawDetail(Rect area, string appId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var app = Find(appId);
        if (app is null)
        {
            router.Pop();
            return;
        }

        DrawNavBar(area, app.DisplayName, scale);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var entry = AppStoreCatalog.For(app.Id);
            var top = DrawDetailHead(origin, width, app, entry, scale);
            top = DrawPreviewStrip(origin, width, top, app, entry, scale);
            top = DrawAbout(origin, width, top, entry, scale);
            top = DrawInformation(origin, width, top, app, entry, scale);
            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private void DrawNavBar(Rect area, string title, float scale)
    {
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fitted = Typography.FitText(title, area.Width - 96f * scale, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), fitted, ui.TitleInk, TextStyles.Title3);
        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + AppHeader.Height * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var center = new Vector2(area.Min.X + 17f * scale, rowCenterY);
        if (BackButton.Draw("appstore.back", center, 15f * scale, ui.TitleInk, hovered, scale))
        {
            router.Pop();
        }
    }

    private float DrawDetailHead(Vector2 origin, float width, IPhoneApp app, in StoreEntry entry, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var top = origin.Y + Metrics.Space.Sm * scale;
        var iconCenter = new Vector2(origin.X + DetailIconSize * 0.5f * scale, top + DetailIconSize * 0.5f * scale);
        DrawIcon(drawList, iconCenter, DetailIconSize * scale, app);
        var textLeft = origin.X + (DetailIconSize + 14f) * scale;
        var textWidth = origin.X + width - textLeft;
        var nameY = top + 6f * scale;
        var nameHovering = ImGui.IsMouseHoveringRect(new Vector2(textLeft, nameY),
            new Vector2(textLeft + textWidth, nameY + Typography.Measure(app.DisplayName, TextStyles.Title2).Y));
        Marquee.DrawLeft("appstore.detail.name." + app.Id, app.DisplayName, textLeft, nameY, textWidth,
            TextStyles.Title2, ui.TitleInk, nameHovering);
        Typography.DrawWrappedLeft(new Vector2(textLeft, top + 32f * scale), Loc.T(entry.Subtitle), ui.MutedInk,
            TextStyles.Footnote, textWidth);

        var pillHeight = 30f * scale;
        var pillWidth = 78f * scale;
        var pillTop = top + (DetailIconSize - 34f) * scale;
        var pill = new Rect(new Vector2(textLeft, pillTop), new Vector2(textLeft + pillWidth, pillTop + pillHeight));
        if (app.IsAvailable)
        {
            DrawStatePill(pill, app, UiInteract.Hover(pill.Min, pill.Max), scale);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(textLeft, pillTop + 8f * scale), Loc.T(L.Store.Unavailable),
                ui.MutedInk, TextStyles.Footnote);
        }

        if (installer.IsInstalled(app.Id) && AppInstaller.CanUninstall(app.Id))
        {
            var removeWidth = 92f * scale;
            var remove = new Rect(new Vector2(pill.Max.X + 10f * scale, pillTop),
                new Vector2(pill.Max.X + 10f * scale + removeWidth, pillTop + pillHeight));
            if (ui.DangerGhostButton(remove, Loc.T(L.Store.Remove)))
            {
                installer.Uninstall(app.Id);
            }
        }

        return top + (DetailIconSize + Metrics.Space.Xl) * scale;
    }

    private float DrawPreviewStrip(Vector2 origin, float width, float top, IPhoneApp app, in StoreEntry entry,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, new Vector2(origin.X, top), Loc.T(L.Store.Preview), ui.TitleInk, TextStyles.Title3);
        top += 28f * scale;
        const int cards = 3;
        var gap = 10f * scale;
        var cardWidth = (width - gap * (cards - 1)) / cards;
        var rounding = Metrics.Radius.Card * scale;
        for (var index = 0; index < cards; index++)
        {
            var min = new Vector2(origin.X + index * (cardWidth + gap), top);
            var max = new Vector2(min.X + cardWidth, top + PreviewHeight * scale);
            var tint = index switch
            {
                0 => Palette.Darken(app.Accent, 0.35f),
                1 => Palette.Darken(app.Accent, 0.52f),
                _ => Palette.Darken(app.Accent, 0.66f),
            };
            Squircle.FillVerticalGradient(drawList, min, max, rounding,
                ImGui.GetColorU32(Palette.Lighten(tint, 0.10f)), ImGui.GetColorU32(Palette.Darken(tint, 0.35f)));
            Squircle.Stroke(drawList, min, max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.08f)), 1f);
            var center = new Vector2((min.X + max.X) * 0.5f, min.Y + PreviewHeight * 0.42f * scale);
            DrawIcon(drawList, center, cardWidth * 0.46f, app);
            var caption = index == 0 ? app.DisplayName : Loc.T(index == 1 ? entry.Subtitle : AppStoreCatalog.Name(entry.Category));
            Typography.DrawCentered(drawList,
                new Vector2(center.X, max.Y - 22f * scale),
                Typography.FitText(caption, cardWidth - 12f * scale, TextStyles.Caption1),
                Palette.WithAlpha(ui.TitleInk, 0.82f), TextStyles.Caption1);
        }

        return top + (PreviewHeight + Metrics.Space.Xl) * scale;
    }

    private float DrawAbout(Vector2 origin, float width, float top, in StoreEntry entry, float scale)
    {
        Typography.Draw(new Vector2(origin.X, top), Loc.T(L.Store.Description), ui.TitleInk, TextStyles.Title3);
        top += 28f * scale;
        var height = Typography.DrawWrappedLeft(new Vector2(origin.X, top), Loc.T(entry.Body), ui.BodyInk,
            TextStyles.Callout, width);
        return top + height + Metrics.Space.Xl * scale;
    }

    private float DrawInformation(Vector2 origin, float width, float top, IPhoneApp app, in StoreEntry entry,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Typography.Draw(drawList, new Vector2(origin.X, top), Loc.T(L.Store.Information), ui.TitleInk,
            TextStyles.Title3);
        top += 28f * scale;
        const int rowCount = 3;
        var rowHeight = 44f * scale;
        var cardMin = new Vector2(origin.X, top);
        var cardMax = new Vector2(origin.X + width, top + rowCount * rowHeight);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Card * scale, true);
        DrawInfoRow(drawList, cardMin, cardMax, 0, rowHeight, Loc.T(L.Store.Developer), Loc.T(L.Store.DeveloperName),
            scale);
        DrawInfoRow(drawList, cardMin, cardMax, 1, rowHeight, Loc.T(L.Store.Category),
            Loc.T(AppStoreCatalog.Name(entry.Category)), scale);
        DrawInfoRow(drawList, cardMin, cardMax, 2, rowHeight, Loc.T(L.Store.Languages),
            Loc.T(L.Store.LanguageCount, LanguageCount), scale);
        return cardMax.Y + Metrics.Space.Xl * scale;
    }

    private void DrawInfoRow(ImDrawListPtr drawList, Vector2 cardMin, Vector2 cardMax, int index, float rowHeight,
        string label, string value, float scale)
    {
        var rowTop = cardMin.Y + index * rowHeight;
        if (index > 0)
        {
            drawList.AddLine(new Vector2(cardMin.X + 14f * scale, rowTop), new Vector2(cardMax.X - 14f * scale, rowTop),
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
        }

        var centerY = rowTop + rowHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(cardMin.X + 14f * scale, centerY - 8f * scale), label, ui.MutedInk,
            TextStyles.Footnote);
        var size = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(cardMax.X - 14f * scale - size.X, centerY - 8f * scale), value,
            ui.TitleInk, TextStyles.FootnoteEmphasized);
    }
}
