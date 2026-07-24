using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private const float HeroHeight = 208f;

    private void DrawTodayTab(Rect area)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var today = DateTime.Now;
        DrawLargeTitle(area, Loc.T(L.Store.Today),
            Loc.Culture.TextInfo.ToUpper(today.ToString("dddd d MMMM", Loc.Culture)));
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + HeaderHeight * scale), area.Max);
        using (var surface = AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                surface.JumpToTop();
                resetScroll = false;
            }

            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var top = origin.Y;
            var featured = FeaturedApp(today);
            if (featured is not null)
            {
                var heroMax = new Vector2(origin.X + width, top + HeroHeight * scale);
                DrawHero(new Rect(new Vector2(origin.X, top), heroMax), featured, scale);
                top = heroMax.Y + Metrics.Space.Xl * scale;
            }

            var fresh = Collect(app => !installer.IsInstalled(app.Id));
            var section = new Rect(new Vector2(origin.X - Metrics.Space.Lg * scale, top),
                new Vector2(origin.X + width + Metrics.Space.Lg * scale, body.Max.Y));
            if (fresh.Count > 0)
            {
                top = DrawSection(section, top, Loc.T(L.Store.NewHere), fresh, scale);
            }
            else
            {
                top = DrawAllSetCard(section, top, scale);
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, top));
            ImGui.Dummy(new Vector2(width, Metrics.Space.Lg * scale));
        }
    }

    private IPhoneApp? FeaturedApp(DateTime today)
    {
        var pool = Collect(static _ => true);
        if (pool.Count == 0)
        {
            return null;
        }

        return pool[today.DayOfYear % pool.Count];
    }

    private void DrawHero(Rect card, IPhoneApp app, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(card.Min, card.Max);
        var rounding = Metrics.Radius.Card * scale * 1.4f;
        Elevation.Floating(drawList, card.Min, card.Max, rounding, scale, hovered ? 0.85f : 0.6f);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.Lighten(app.Accent, 0.16f)),
            ImGui.GetColorU32(Palette.Darken(app.Accent, 0.55f)));
        Squircle.Stroke(drawList, card.Min, card.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(app.Accent, 0.45f), 0.30f)), 1f * scale);
        var pad = Metrics.Space.Lg * scale;
        var entry = AppStoreCatalog.For(app.Id);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), Loc.T(L.Store.AppOfTheDay),
            Palette.WithAlpha(new Vector4(1f, 1f, 1f, 1f), 0.78f), TextStyles.Caption1);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad + 20f * scale),
            Typography.FitText(app.DisplayName, card.Width - pad * 2f, TextStyles.Title1), new Vector4(1f, 1f, 1f, 1f),
            TextStyles.Title1);
        Typography.DrawWrappedLeft(new Vector2(card.Min.X + pad, card.Min.Y + pad + 52f * scale),
            Loc.T(entry.Body), new Vector4(1f, 1f, 1f, 0.86f), TextStyles.Subheadline, card.Width - pad * 2f);

        var iconSize = 56f * scale;
        var iconCenter = new Vector2(card.Min.X + pad + iconSize * 0.5f, card.Max.Y - pad - iconSize * 0.5f);
        DrawIcon(drawList, iconCenter, iconSize, app);
        var pillWidth = 74f * scale;
        var pillHeight = 30f * scale;
        var pill = new Rect(
            new Vector2(card.Max.X - pad - pillWidth, iconCenter.Y - pillHeight * 0.5f),
            new Vector2(card.Max.X - pad, iconCenter.Y + pillHeight * 0.5f));
        var overPill = UiInteract.Hover(pill.Min, pill.Max);
        DrawStatePill(pill, app, overPill, scale);
        var cardHovered = hovered && !overPill;
        if (cardHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(card.Min, card.Max, cardHovered))
        {
            router.Push(StoreView.ForApp(app.Id));
        }
    }

    private float DrawAllSetCard(Rect area, float top, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var left = area.Min.X + Metrics.Space.Lg * scale;
        var right = area.Max.X - Metrics.Space.Lg * scale;
        var cardMin = new Vector2(left, top);
        var cardMax = new Vector2(right, top + 92f * scale);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Card * scale, true);
        var center = new Vector2((cardMin.X + cardMax.X) * 0.5f, cardMin.Y + 34f * scale);
        Typography.DrawCentered(drawList, center, Loc.T(L.Store.EverythingInstalled), ui.TitleInk,
            TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, center.Y + 24f * scale),
            Loc.T(L.Store.EverythingInstalledHint), ui.MutedInk, TextStyles.Footnote);
        return cardMax.Y + Metrics.Space.Xl * scale;
    }
}
