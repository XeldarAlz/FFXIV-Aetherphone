using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Collections;

internal sealed partial class CollectionsApp
{
    private void DrawDetail(Rect area, CollectionCategory category, CollectionItem item)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, item.Name, back);
        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawDetailHero(item, category);
            DrawDetailDescription(item);
            DrawDetailInfo(item, category);
            DrawDetailSources(item);
        }
    }

    private void DrawDetailHero(CollectionItem item, CollectionCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var iconBox = 72f * scale;
        var iconMin = origin;
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        DrawIcon(drawList, item, iconMin, iconMax, 14f * scale);
        var textLeft = iconMax.X + 14f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 6f * scale), item.Name, frameTheme.TextStrong, 1.05f,
            FontWeight.SemiBold);
        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;
        if (owned is { State: OwnedState.Ready })
        {
            var isOwned = owned.Ids.Contains(item.Id);
            var label = isOwned ? Loc.T(L.Collections.Owned) : Loc.T(L.Collections.Missing);
            var color = isOwned ? frameTheme.ToggleOn : frameTheme.TextMuted;
            Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), label, color, 0.82f, FontWeight.SemiBold);
        }

        if (item.Stars > 0)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 52f * scale),
                new string('★', Math.Clamp(item.Stars, 1, 5)), frameTheme.Accent, 0.82f, FontWeight.Medium);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, iconBox + 14f * scale));
    }

    private void DrawDetailDescription(CollectionItem item)
    {
        if (item.Description.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.About), frameTheme);
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.88f))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(item.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawDetailInfo(CollectionItem item, CollectionCategory category)
    {
        var rows = 0;
        if (item.Patch.Length > 0)
        {
            rows++;
        }

        if (item.HasTradeable)
        {
            rows++;
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            rows++;
        }

        if (item.Community.Length > 0)
        {
            rows++;
        }

        if (category == CollectionCategory.TriadCards && item.Stats is not null)
        {
            rows++;
        }

        if (rows == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.Details), frameTheme);
        var card = GroupCard.Begin(frameTheme, rows);
        if (item.Patch.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Patch), item.Patch, frameTheme);
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Points), item.Points.ToString(), frameTheme);
        }

        if (category == CollectionCategory.TriadCards && item.Stats is { } stats)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.CardStats),
                $"{stats.Top} · {stats.Right} · {stats.Bottom} · {stats.Left}", frameTheme);
        }

        if (item.HasTradeable)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Tradeable),
                item.Tradeable ? Loc.T(L.Collections.Yes) : Loc.T(L.Collections.No), frameTheme);
        }

        if (item.Community.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Community), item.Community, frameTheme);
        }

        card.End();
    }

    private void DrawDetailSources(CollectionItem item)
    {
        if (item.Sources.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.HowToObtain), frameTheme);
        var card = GroupCard.Begin(frameTheme, item.Sources.Length, 52f);
        for (var index = 0; index < item.Sources.Length; index++)
        {
            var source = item.Sources[index];
            var row = card.NextRow();
            var scale = ImGuiHelpers.GlobalScale;
            var type = source.Type ?? string.Empty;
            var text = source.Text ?? string.Empty;
            Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 9f * scale),
                type.Length > 0 ? type : Loc.T(L.Collections.Source), frameTheme.TextStrong, 0.86f, FontWeight.Medium);
            if (text.Length > 0)
            {
                Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 28f * scale), text, frameTheme.TextMuted, 0.78f);
            }
        }

        card.End();
    }
}
