using Aetherphone.Core;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Hunts;

internal sealed partial class HuntsApp
{
    private void DrawHistoryHeader(Rect area, float scale)
    {
        var rowCenterY = area.Min.Y + area.Height * 0.5f;
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), Loc.T(L.Hunts.HistoryTab), ui.TitleInk,
            1.05f, FontWeight.SemiBold);
    }

    private void DrawHistory(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            hunts.EnsureHistoryLoaded();

            if (!hunts.HistoryLoaded)
            {
                LoadingPulse.Draw(new Vector2(body.Center.X, body.Center.Y - 14f * scale), 13f * scale, ui.Accent,
                    ui.MutedInk, Loc.T(L.Common.Loading));
                return;
            }

            var entries = hunts.History;
            if (entries.Length == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Clock, Loc.T(L.Hunts.HistoryEmpty), string.Empty);
                return;
            }

            var card = RowListCard.Begin(ui, entries.Length, RowHeight, scale);
            for (var index = 0; index < entries.Length; index++)
            {
                DrawHistoryRow(card.Row(index), entries[index], scale);
            }

            card.End();
        }
    }

    private void DrawHistoryRow(Rect row, HuntLogEntryDto entry, float scale)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            Squircle.Fill(ImGui.GetWindowDrawList(), row.Min, row.Max, Metrics.Radius.Sm * scale,
                ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, 0.05f)));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var def = mobCatalog.Find(entry.MobId);
        var mobLabel = ResolveMobLabel(def, entry.MobId);
        var zoneInstance = entry.ZoneInstance ?? 0;

        var textLeft = row.Min.X;
        var nameY = row.Center.Y - 16f * scale;
        DrawMarkNameLine("hunts.history." + entry.Id, def, mobLabel, zoneInstance, textLeft, nameY, row.Width * 0.6f,
            scale);

        var worldLabel = Prettify(entry.WorldId);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), worldLabel, ui.MutedInk, TextStyles.Footnote);

        if ((entry.KilledAt ?? entry.SpawnedAt) is { } timestamp)
        {
            var agoLabel = TimeText.Ago(timestamp);
            var agoSize = Typography.Measure(agoLabel, TextStyles.BodyEmphasized);
            Typography.Draw(new Vector2(row.Max.X - agoSize.X, nameY), agoLabel, ui.MutedInk,
                TextStyles.BodyEmphasized);
        }

        if (UiInteract.Click(row.Min, row.Max, hovered))
        {
            OpenDetailFor(entry.MobId, entry.WorldId, zoneInstance);
        }
    }
}
