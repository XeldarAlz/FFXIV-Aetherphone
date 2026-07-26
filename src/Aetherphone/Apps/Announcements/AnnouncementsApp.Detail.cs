using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Announcements;

internal sealed partial class AnnouncementsApp
{
    private const float DetailRounding = 22f;

    private void DrawDetail(Rect area, string announcementId)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var context = new PhoneContext(area, theme, navigation);
        AppHeader.Draw(context, DisplayName, back);

        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);
        var announcement = Resolve(announcementId);
        if (announcement is null)
        {
            EmptyState.Draw(body, ui, FontAwesomeIcon.Bullhorn, Loc.T(L.Announcements.UnavailableTitle),
                Loc.T(L.Announcements.UnavailableHint));
            return;
        }

        var text = AnnouncementText.For(announcement);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
            DrawDetailHero(text.Title, announcement.CreatedAtUnix, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
            DrawDetailBody(text.Body, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }
    }

    private void DrawDetailHero(string title, long createdAtUnix, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = Metrics.Space.Lg * scale;
        var innerWidth = width - pad * 2f;
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Title2, innerWidth).Y;
        var meta = $"{TimeText.DayLabel(createdAtUnix)} · {TimeText.Clock(createdAtUnix)}";
        var metaHeight = Typography.MeasureWrappedBlock(meta, TextStyles.Footnote, innerWidth).Y;
        var height = pad + titleHeight + Metrics.Space.Sm * scale + metaHeight + pad;

        var max = new Vector2(origin.X + width, origin.Y + height);
        var rounding = DetailRounding * scale;
        Elevation.Card(drawList, origin, max, rounding, scale, 0.8f);
        Squircle.FillVerticalGradient(drawList, origin, max, rounding,
            ImGui.GetColorU32(Palette.Lighten(ui.Accent, 0.10f)),
            ImGui.GetColorU32(Palette.Darken(ui.Accent, 0.52f)));
        Squircle.Stroke(drawList, origin, max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(ui.Accent, 0.45f), 0.30f)), 1f * scale);

        var titleTop = origin.Y + pad;
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, titleTop), title, OnAccentInk, TextStyles.Title2,
            innerWidth);
        Typography.DrawWrappedLeft(new Vector2(origin.X + pad, titleTop + titleHeight + Metrics.Space.Sm * scale), meta,
            OnAccentMutedInk, TextStyles.Footnote, innerWidth);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawDetailBody(string bodyText, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var pad = Metrics.Space.Lg * scale;
        var innerWidth = width - pad * 2f;

        RichTextLayout? layout;
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            layout = LinkText.LayoutFor(bodyText, innerWidth);
        }

        var textHeight = layout?.Size.Y ?? Typography.MeasureWrappedBlock(bodyText, TextStyles.Body, innerWidth).Y;
        var height = pad + textHeight + pad;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, DetailRounding * scale, true);

        var textOrigin = new Vector2(origin.X + pad, origin.Y + pad);
        if (layout is null)
        {
            Typography.DrawWrappedLeft(textOrigin, bodyText, ui.BodyInk, TextStyles.Body, innerWidth);
        }
        else
        {
            using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
            {
                LinkText.Draw(drawList, layout, textOrigin, 1f, ui.BodyInk, Palette.Lighten(ui.Accent, 0.28f), 1f,
                    true);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }
}
