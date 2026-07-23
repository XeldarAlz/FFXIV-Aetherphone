using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.AppStore;

internal sealed partial class AppStoreApp
{
    private void DrawIcon(ImDrawListPtr drawList, Vector2 center, float size, IPhoneApp app)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * Metrics.Radius.TileFactor;
        var surface = IconTile.Surface(app.Accent);
        Elevation.IconRest(drawList, min, max, radius, scale);
        IconTile.FillShaded(drawList, min, max, radius, surface);
        Material.EdgeSquircle(drawList, min, max, radius, scale);
        if (!AppIconArt.TryDraw(drawList, app.Id, center, size * 0.62f, GlyphInk, Palette.Darken(surface, 0.25f)))
        {
            Typography.DrawCentered(drawList, center, app.Glyph, GlyphInk, TextStyles.Headline);
        }
    }

    private bool DrawAppRow(Rect row, IPhoneApp app, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pillWidth = 68f * scale;
        var pillHeight = 28f * scale;
        var pill = new Rect(
            new Vector2(row.Max.X - pillWidth, row.Center.Y - pillHeight * 0.5f),
            new Vector2(row.Max.X, row.Center.Y + pillHeight * 0.5f));
        var overPill = UiInteract.Hover(pill.Min, pill.Max);
        var hovered = UiInteract.Hover(row.Min, row.Max) && !overPill;
        if (hovered)
        {
            Squircle.Fill(drawList, new Vector2(row.Min.X - 6f * scale, row.Min.Y + 2f * scale),
                new Vector2(row.Max.X + 6f * scale, row.Max.Y - 2f * scale), 12f * scale,
                ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var iconCenter = new Vector2(row.Min.X + RowIconSize * 0.5f * scale, row.Center.Y);
        DrawIcon(drawList, iconCenter, RowIconSize * scale, app);
        var textLeft = row.Min.X + (RowIconSize + 12f) * scale;
        var textWidth = pill.Min.X - textLeft - 10f * scale;
        var entry = AppStoreCatalog.For(app.Id);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - 18f * scale),
            Typography.FitText(app.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y + 2f * scale),
            Typography.FitText(Loc.T(entry.Subtitle), textWidth, TextStyles.Footnote), ui.MutedInk,
            TextStyles.Footnote);
        DrawStatePill(pill, app, overPill, scale);
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawStatePill(Rect pill, IPhoneApp app, bool hovered, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (installing.TryGetValue(app.Id, out var progress))
        {
            DrawInstallProgress(drawList, pill.Center, progress, app.Accent, scale);
            return;
        }

        var installed = installer.IsInstalled(app.Id);
        var label = Loc.T(installed ? L.Store.Open : L.Store.Get);
        var radius = pill.Height * 0.5f;
        Squircle.Fill(drawList, pill.Min, pill.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, hovered ? 0.20f : 0.12f)));
        Typography.DrawCentered(drawList, pill.Center, label, Palette.Lighten(app.Accent, 0.30f),
            TextStyles.FootnoteEmphasized);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        if (installed)
        {
            OpenApp(app.Id);
            return;
        }

        BeginInstall(app.Id);
    }

    private void DrawInstallProgress(ImDrawListPtr drawList, Vector2 center, float progress, Vector4 accent,
        float scale)
    {
        var radius = 11f * scale;
        ProgressRing.Track(center, radius, 2.5f * scale, Palette.WithAlpha(ui.TitleInk, 0.18f));
        ProgressRing.Fill(center, radius, 2.5f * scale, Math.Clamp(progress, 0f, 1f), Palette.Lighten(accent, 0.28f));
        var stop = 3f * scale;
        drawList.AddRectFilled(center - new Vector2(stop), center + new Vector2(stop),
            ImGui.GetColorU32(Palette.Lighten(accent, 0.28f)), 1f * scale);
    }

    private float DrawSection(Rect area, float top, string title, List<IPhoneApp> entries, float scale)
    {
        if (entries.Count == 0)
        {
            return top;
        }

        var left = area.Min.X + Metrics.Space.Lg * scale;
        Typography.Draw(new Vector2(left, top), title, ui.TitleInk, TextStyles.Title3);
        top += 30f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var cardMin = new Vector2(left, top);
        var cardMax = new Vector2(area.Max.X - Metrics.Space.Lg * scale, top + entries.Count * RowHeight * scale);
        ui.Card(drawList, cardMin, cardMax, Metrics.Radius.Card * scale, true);
        for (var index = 0; index < entries.Count; index++)
        {
            var rowTop = cardMin.Y + index * RowHeight * scale;
            if (index > 0)
            {
                drawList.AddLine(new Vector2(cardMin.X + (RowIconSize + 26f) * scale, rowTop),
                    new Vector2(cardMax.X - 14f * scale, rowTop),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.06f)), 1f);
            }

            var row = new Rect(new Vector2(cardMin.X + 14f * scale, rowTop),
                new Vector2(cardMax.X - 14f * scale, rowTop + RowHeight * scale));
            if (DrawAppRow(row, entries[index], scale))
            {
                router.Push(StoreView.ForApp(entries[index].Id));
            }
        }

        return cardMax.Y + Metrics.Space.Xl * scale;
    }
}
