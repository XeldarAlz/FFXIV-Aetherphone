using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Linkpearl;

internal sealed partial class LinkpearlApp
{
    private const int TabNameMaxLength = 24;
    private const float EditorNameRowHeight = 50f;
    private const float EditorTintRowHeight = 44f;
    private const float EditorChannelRowHeight = 44f;
    private const float EditorSwatchRadius = 11f;

    private static readonly ChannelCategory[] PickerOrder =
    {
        ChannelCategory.Community,
        ChannelCategory.Group,
        ChannelCategory.Linkshell,
        ChannelCategory.CrossWorld,
        ChannelCategory.Local,
        ChannelCategory.System,
    };

    private static readonly HistoryPolicy[] HistoryChoices =
    {
        HistoryPolicy.Off,
        HistoryPolicy.Session,
        HistoryPolicy.Days30,
        HistoryPolicy.Forever,
    };

    private readonly List<DropdownMenu.Item> editorItems = new(24);
    private readonly List<string> editorKeys = new(24);
    private string editorName = string.Empty;
    private string editorTabId = string.Empty;
    private bool editorNameFocus;

    private void OpenTabEditor(ChatTab tab)
    {
        editorTabId = tab.Id;
        editorName = tab.Name;
        router.Push(LinkpearlRoute.TabEditor(tab.Id));
    }

    private void CreateTab()
    {
        if (!tabs.CanAdd)
        {
            ShellToast.Show(Loc.T(L.Linkpearl.TabLimit, TabStore.MaxTabs));
            return;
        }

        var tab = tabs.Create(Loc.T(L.Linkpearl.NewTab), Array.Empty<string>());
        inbox.Invalidate();
        editorNameFocus = true;
        OpenTabEditor(tab);
    }

    private void DrawTabEditor(Rect area, string tabId)
    {
        var tab = tabs.Find(tabId);
        if (tab is null)
        {
            router.Reset();
            return;
        }

        var scale = UiScale.Current;
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, Loc.T(L.Linkpearl.EditTab), leaveTabEditor);
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawIdentityCard(tab, scale);
            SettingsSection.Header(Loc.T(L.Linkpearl.Channels), frameTheme,
                tab.Channels.Count == 0 ? Loc.T(L.Linkpearl.NoChannels) : null);
            for (var index = 0; index < PickerOrder.Length; index++)
            {
                DrawCategory(tab, PickerOrder[index], scale);
            }

            SettingsSection.Header(Loc.T(L.Linkpearl.TabSettings), frameTheme);
            DrawTabSettings(tab);
            SettingsSection.Hint(Loc.T(L.Linkpearl.StoredOnThisPc), frameTheme);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            var dangerCard = GroupCard.Begin(frameTheme, 1);
            if (SettingsRow.Action(dangerCard.NextRow(), Loc.T(L.Linkpearl.DeleteTab), frameTheme.Danger, frameTheme))
            {
                AskDeleteTab(tab);
            }

            dangerCard.End();
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxl * scale));
        }

        DrawEditorMenu(area, tab);
    }

    private void DrawIdentityCard(ChatTab tab, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.TabName), frameTheme);
        var card = GroupCard.Begin(frameTheme, 2, EditorNameRowHeight);
        DrawNameRow(tab, card.NextRow(), scale);
        DrawTintRow(tab, card.NextRow(), scale);
        card.End();
    }

    private void DrawNameRow(ChatTab tab, Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var palette = ChannelTints.TabPalette;
        var tint = palette[Math.Clamp(tab.Tint, 0, palette.Length - 1)];
        var badgeRadius = 15f * scale;
        var badgeCenter = new Vector2(row.Min.X + badgeRadius, row.Center.Y);
        var badgeMin = badgeCenter - new Vector2(badgeRadius, badgeRadius);
        var badgeMax = badgeCenter + new Vector2(badgeRadius, badgeRadius);
        Squircle.Fill(drawList, badgeMin, badgeMax, badgeRadius * 0.62f,
            ImGui.GetColorU32(Palette.WithAlpha(tint, 0.22f)));
        Typography.DrawCentered(drawList, badgeCenter, Initials.Of(editorName.Length > 0 ? editorName : tab.Name),
            tint, TextStyles.Caption1);
        var fieldLeft = badgeMax.X + Metrics.Space.Md * scale;
        ImGui.SetCursorScreenPos(new Vector2(fieldLeft, row.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(row.Max.X - fieldLeft);
        if (editorNameFocus)
        {
            ImGui.SetKeyboardFocusHere();
            editorNameFocus = false;
        }

        Plugin.Fonts.NoticeText(editorName);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.InputTextWithHint("##linkpearl.tabname", Loc.T(L.Linkpearl.TabName), ref editorName,
                TabNameMaxLength);
        }

        var trimmed = editorName.Trim();
        if (trimmed.Length > 0 && !string.Equals(trimmed, tab.Name, StringComparison.Ordinal))
        {
            tab.Name = trimmed;
            tabs.Update(tab);
            inbox.Invalidate();
        }
    }

    private void DrawTintRow(ChatTab tab, Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var palette = ChannelTints.TabPalette;
        var radius = EditorSwatchRadius * scale;
        var spacing = row.Width / palette.Length;
        for (var index = 0; index < palette.Length; index++)
        {
            var center = new Vector2(row.Min.X + spacing * (index + 0.5f), row.Center.Y);
            var selected = tab.Tint == index;
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(palette[index]), 24);
            if (selected)
            {
                drawList.AddCircle(center, radius + 3.5f * scale, ImGui.GetColorU32(frameTheme.TextStrong), 24,
                    1.6f * scale);
            }

            if (!UiInteract.HoverClickCircle(center, radius + 4f * scale) || selected)
            {
                continue;
            }

            tab.Tint = index;
            tabs.Update(tab);
            inbox.Invalidate();
        }
    }

    private void DrawCategory(ChatTab tab, ChannelCategory category, float scale)
    {
        var all = GameChannels.All;
        var count = 0;
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Category == category)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        SettingsSection.Hint(Loc.T(CategoryLabel(category)), frameTheme);
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
        var card = GroupCard.Begin(frameTheme, count, EditorChannelRowHeight);
        for (var index = 0; index < all.Length; index++)
        {
            if (all[index].Category == category)
            {
                DrawChannelRow(tab, all[index], card.NextRow(), scale);
            }
        }

        card.End();
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }

    private void DrawChannelRow(ChatTab tab, GameChannel channel, Rect row, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            SettingsRow.DrawRowHighlight(row, frameTheme);
        }

        var dotCenter = new Vector2(row.Min.X + 5f * scale, row.Center.Y);
        drawList.AddCircleFilled(dotCenter, 4.5f * scale, ImGui.GetColorU32(channel.Tint), 12);
        var joined = LinkshellNames.Joined(channel);
        var label = LinkshellNames.Label(channel);
        var textLeft = dotCenter.X + 14f * scale;
        var selected = tab.Includes(channel.Key);
        var ink = joined ? frameTheme.TextStrong : frameTheme.TextMuted;
        var labelWidth = row.Max.X - Metrics.Space.Xl * scale - textLeft;
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var fitted = Typography.FitText(label, labelWidth, TextStyles.BodyEmphasized);
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - labelSize.Y * 0.5f), fitted, ink,
            TextStyles.BodyEmphasized);
        if (channel.IsSlotted && !joined)
        {
            var fittedWidth = Typography.Measure(fitted, TextStyles.BodyEmphasized).X;
            var hint = Loc.T(L.Linkpearl.EmptySlot);
            Typography.Draw(drawList, new Vector2(textLeft + fittedWidth + Metrics.Space.Sm * scale,
                row.Center.Y - labelSize.Y * 0.5f + 1f * scale), hint, frameTheme.TextMuted, TextStyles.Caption1);
        }

        DrawCheck(drawList, new Vector2(row.Max.X - 10f * scale, row.Center.Y), selected, scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!UiInteract.Click(row.Min, row.Max, hovered))
        {
            return;
        }

        if (selected)
        {
            tab.Channels.Remove(channel.Key);
        }
        else
        {
            tab.Channels.Add(channel.Key);
        }

        tabs.Update(tab);
        inbox.Invalidate();
        threadKey = string.Empty;
    }

    private void DrawCheck(ImDrawListPtr drawList, Vector2 center, bool selected, float scale)
    {
        var radius = 10f * scale;
        if (!selected)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(frameTheme.TextMuted, 0.5f)), 20,
                1.4f * scale);
            return;
        }

        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(frameTheme.Accent), 20);
        var color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
        var tip = new Vector2(center.X - 1.5f * scale, center.Y + 3.5f * scale);
        drawList.AddLine(tip - new Vector2(4f * scale, 4f * scale), tip, color, 2f * scale);
        drawList.AddLine(tip, new Vector2(tip.X + 7f * scale, tip.Y - 8f * scale), color, 2f * scale);
    }

    private void DrawTabSettings(ChatTab tab)
    {
        var card = GroupCard.Begin(frameTheme, 4);
        var sendRow = card.NextRow();
        if (SettingsRow.Disclosure(sendRow, Loc.T(L.Linkpearl.RepliesGoTo), SendChannelLabel(tab), frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.send", sendRow);
        }

        var layoutRow = card.NextRow();
        if (SettingsRow.Disclosure(layoutRow, Loc.T(L.Linkpearl.Layout), Loc.T(
                tab.Density == ChatDensity.Bubbles ? L.Linkpearl.LayoutBubbles : L.Linkpearl.LayoutLog),
                frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.layout", layoutRow);
        }

        var alertsRow = card.NextRow();
        if (SettingsRow.Disclosure(alertsRow, Loc.T(L.Linkpearl.Alerts), Loc.T(AlertLabel(tab.Alerts)), frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.alerts", alertsRow);
        }

        var historyRow = card.NextRow();
        if (SettingsRow.Disclosure(historyRow, Loc.T(L.Linkpearl.KeepHistory), HistoryLabel(tab), frameTheme))
        {
            editorMenu.Toggle("linkpearl.editor.history", historyRow);
        }

        card.End();
    }

    private void DrawEditorMenu(Rect area, ChatTab tab)
    {
        if (editorMenu.IsOpenFor("linkpearl.editor.send"))
        {
            editorItems.Clear();
            editorKeys.Clear();
            for (var index = 0; index < tab.Channels.Count; index++)
            {
                if (!GameChannels.TryByKey(tab.Channels[index], out var channel) || !channel.CanSend)
                {
                    continue;
                }

                editorItems.Add(new DropdownMenu.Item(LinkshellNames.Label(channel), string.Empty, false,
                    string.Equals(channel.Key, tab.SendChannel, StringComparison.Ordinal)));
                editorKeys.Add(channel.Key);
            }

            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                tab.SendChannel = editorKeys[picked];
                tabs.Update(tab);
                threadKey = string.Empty;
            }

            return;
        }

        if (editorMenu.IsOpenFor("linkpearl.editor.layout"))
        {
            editorItems.Clear();
            editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutLog), string.Empty, false,
                tab.Density == ChatDensity.Log));
            editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.LayoutBubbles), string.Empty, false,
                tab.Density == ChatDensity.Bubbles));
            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                tab.Density = picked == 1 ? ChatDensity.Bubbles : ChatDensity.Log;
                tabs.Update(tab);
                threadKey = string.Empty;
            }

            return;
        }

        if (editorMenu.IsOpenFor("linkpearl.editor.history"))
        {
            editorItems.Clear();
            var current = TabHistory(tab);
            for (var index = 0; index < HistoryChoices.Length; index++)
            {
                editorItems.Add(new DropdownMenu.Item(Loc.T(HistoryLabelFor(HistoryChoices[index])), string.Empty,
                    false, current == HistoryChoices[index]));
            }

            var picked = DrawEditorList(area);
            if (picked >= 0)
            {
                ApplyHistory(tab, HistoryChoices[picked]);
            }

            return;
        }

        if (!editorMenu.IsOpenFor("linkpearl.editor.alerts"))
        {
            return;
        }

        editorItems.Clear();
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsAll), string.Empty, false,
            tab.Alerts == AlertPolicy.All));
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsMentions), string.Empty, false,
            tab.Alerts == AlertPolicy.Mentions));
        editorItems.Add(new DropdownMenu.Item(Loc.T(L.Linkpearl.AlertsOff), string.Empty, false,
            tab.Alerts == AlertPolicy.Off));
        var choice = DrawEditorList(area);
        if (choice < 0)
        {
            return;
        }

        tab.Alerts = choice switch
        {
            0 => AlertPolicy.All,
            1 => AlertPolicy.Mentions,
            _ => AlertPolicy.Off,
        };
        tabs.Update(tab);
        inbox.Invalidate();
    }

    private int DrawEditorList(Rect area) =>
        editorItems.Count == 0 ? -1 : editorMenu.Draw(area, frameTheme, CollectionsMarshal.AsSpan(editorItems));

    private void LeaveTabEditor()
    {
        editorMenu.Close();
        var tab = tabs.Find(editorTabId);
        if (tab is { Channels.Count: 0 })
        {
            popouts.Close(ChatInbox.KeyForTab(tab));
            tabs.Delete(tab);
            inbox.Invalidate();
        }

        editorTabId = string.Empty;
        router.Pop();
    }

    private string SendChannelLabel(ChatTab tab) =>
        GameChannels.TryByKey(tab.SendChannel, out var channel)
            ? LinkshellNames.Label(channel)
            : Loc.T(L.Linkpearl.ChannelReadOnly);

    private string HistoryLabel(ChatTab tab)
    {
        var policy = TabHistory(tab);
        return policy is null ? Loc.T(L.Linkpearl.HistoryMixed) : Loc.T(HistoryLabelFor(policy.Value));
    }

    private HistoryPolicy? TabHistory(ChatTab tab)
    {
        HistoryPolicy? shared = null;
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            var policy = archive.PolicyFor(tab.Channels[index]);
            if (shared is null)
            {
                shared = policy;
                continue;
            }

            if (shared != policy)
            {
                return null;
            }
        }

        return shared ?? HistoryPolicy.Days30;
    }

    private void ApplyHistory(ChatTab tab, HistoryPolicy policy)
    {
        for (var index = 0; index < tab.Channels.Count; index++)
        {
            archive.SetPolicy(tab.Channels[index], policy);
        }
    }

    private static LocString HistoryLabelFor(HistoryPolicy policy) => policy switch
    {
        HistoryPolicy.Off => L.Linkpearl.HistoryOff,
        HistoryPolicy.Session => L.Linkpearl.HistorySession,
        HistoryPolicy.Forever => L.Linkpearl.HistoryForever,
        _ => L.Linkpearl.HistoryDays30,
    };

    private static LocString AlertLabel(AlertPolicy policy) => policy switch
    {
        AlertPolicy.All => L.Linkpearl.AlertsAll,
        AlertPolicy.Mentions => L.Linkpearl.AlertsMentions,
        _ => L.Linkpearl.AlertsOff,
    };

    private static LocString CategoryLabel(ChannelCategory category) => category switch
    {
        ChannelCategory.Local => L.Linkpearl.CategoryLocal,
        ChannelCategory.Group => L.Linkpearl.CategoryGroup,
        ChannelCategory.Community => L.Linkpearl.CategoryCommunity,
        ChannelCategory.Linkshell => L.Linkpearl.CategoryLinkshell,
        ChannelCategory.CrossWorld => L.Linkpearl.CategoryCrossWorld,
        _ => L.Linkpearl.CategorySystem,
    };
}
