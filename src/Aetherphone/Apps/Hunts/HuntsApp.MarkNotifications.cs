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
    private const float MarkNotificationRowHeight = 52f;

    private sealed class MarkNotificationEditEntry
    {
        public string MobId = string.Empty;
        public string Label = string.Empty;
        public HuntMobNotificationMode Mode;
        public string? WorldId;
    }

    private readonly List<MarkNotificationEditEntry> markNotificationEntries = new();
    private readonly List<HuntMobOverrideEntry> markNotificationScratch = new();
    private bool markNotificationsDirty;

    private void OpenMarkNotifications()
    {
        hunts.NotificationSettings.CollectMobOverrides(markNotificationScratch);
        markNotificationEntries.Clear();
        for (var index = 0; index < markNotificationScratch.Count; index++)
        {
            var source = markNotificationScratch[index];
            markNotificationEntries.Add(new MarkNotificationEditEntry
            {
                MobId = source.MobId,
                Label = ResolveMobLabel(mobCatalog.Find(source.MobId), source.MobId),
                Mode = source.Mode,
                WorldId = source.WorldId,
            });
        }

        markNotificationEntries.Sort(static (left, right) =>
            string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        markNotificationsDirty = false;
        router.Push(new HuntsView(HuntsRoute.MarkNotifications));
    }

    private void CloseMarkNotifications()
    {
        SaveMarkNotificationsIfDirty();
        router.Pop();
    }

    private void SaveMarkNotificationsIfDirty()
    {
        if (!markNotificationsDirty)
        {
            return;
        }

        markNotificationsDirty = false;
        for (var index = 0; index < markNotificationEntries.Count; index++)
        {
            var entry = markNotificationEntries[index];
            hunts.NotificationSettings.SetMobOverride(entry.MobId, entry.Mode, entry.WorldId);
        }

        hunts.SaveNotificationSettings();
    }

    private void DrawMarkNotificationsHeader(Rect content, float scale)
    {
        AppHeader.DrawTitleWithReserve(content, "hunts.markNotifications.header",
            Loc.T(L.Hunts.MarkNotificationsTitle), 12f * scale, ui.TitleInk, scale);

        if (AppHeader.DrawBack(content, scale, "hunts.markNotifications.back", ui.Accent))
        {
            CloseMarkNotifications();
        }
    }

    private void DrawMarkNotificationsBody(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            if (markNotificationEntries.Count == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Bell, Loc.T(L.Hunts.MarkNotificationsEmpty),
                    Loc.T(L.Hunts.MarkNotificationsEmptyHint));
                return;
            }

            var card = RowListCard.Begin(ui, markNotificationEntries.Count, MarkNotificationRowHeight, scale);
            for (var index = 0; index < markNotificationEntries.Count; index++)
            {
                DrawMarkNotificationRow(card.Row(index), markNotificationEntries[index], scale);
            }

            card.End();
        }
    }

    private void DrawMarkNotificationRow(Rect row, MarkNotificationEditEntry entry, float scale)
    {
        var valueText = PendingMarkNotificationValueText(entry.Mode, entry.WorldId);

        var textLeft = row.Min.X;
        var nameY = row.Center.Y - 16f * scale;
        Marquee.DrawLeftAuto("hunts.markNotifications.row." + entry.MobId, entry.Label, textLeft, nameY,
            row.Width * 0.75f, TextStyles.BodyEmphasized, ui.TitleInk);
        Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), valueText, ui.MutedInk, TextStyles.Footnote);

        var center = new Vector2(row.Max.X - 18f * scale, row.Center.Y);
        if (ui.IconButton(center, 16f * scale, NotificationIcon(entry.Mode).ToIconString(),
                NotificationTint(entry.Mode), new Vector4(0f, 0f, 0f, 0f), 1.2f, valueText, HoverLabelSide.Above))
        {
            entry.Mode = NextNotificationMode(entry.Mode);
            markNotificationsDirty = true;
        }
    }

    private static string PendingMarkNotificationValueText(HuntMobNotificationMode mode, string? worldId) => mode switch
    {
        HuntMobNotificationMode.Enabled => Loc.T(L.Hunts.NotifyModeEnabled),
        HuntMobNotificationMode.Disabled => Loc.T(L.Hunts.NotifyModeDisabled),
        HuntMobNotificationMode.EnabledOnWorld =>
            Loc.T(L.Hunts.NotifyModeEnabledOnWorldValue, Prettify(worldId ?? string.Empty)),
        _ => Loc.T(L.Hunts.NotifyModeDefault),
    };
}
