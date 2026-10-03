using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Notifications;

internal sealed partial class NotificationsApp
{
    private const float AppIconSize = 44f;
    private const float AppTextGap = 12f;
    private const float AppLineGap = 2f;
    private const float AppActionGap = 8f;
    private const float AppActionHeight = 34f;
    private const float AppActionRowGap = 14f;
    private const float ActionHoverMix = 0.5f;
    private const float FilterGap = 12f;

    private readonly ChipRail filterRail = new();
    private string[] filterLabels = Array.Empty<string>();
    private bool[] filterActive = Array.Empty<bool>();
    private CachedText appCardText;

    private enum AppAction : byte
    {
        None,
        MuteHour,
        MuteToday,
        Unmute,
        Settings,
    }

    private void RebuildFilterLabels()
    {
        var count = appCounts.Count + 1;
        if (filterLabels.Length != count)
        {
            filterLabels = new string[count];
            filterActive = new bool[count];
        }

        filterLabels[0] = Loc.T(L.Notifications.AllApps);
        filterActive[0] = appFilter is null;
        for (var index = 0; index < appCounts.Count; index++)
        {
            var entry = appCounts[index];
            filterLabels[index + 1] = string.Concat(AppName(entry.AppId), " ",
                entry.Count.ToString(CultureInfo.CurrentCulture));
            filterActive[index + 1] = string.Equals(entry.AppId, appFilter, StringComparison.Ordinal);
        }
    }

    private void DrawFilters(float scale)
    {
        if (appCounts.Count == 0 && appFilter is null)
        {
            return;
        }

        var tapped = filterRail.Draw(ui, filterLabels, filterActive, "notifications.filters",
            ChipRail.CompactLabelPadding + Metrics.Space.Md);
        ImGui.Dummy(new Vector2(0f, MathF.Max(0f, FilterGap * scale - ImGui.GetStyle().ItemSpacing.Y * 2f)));
        if (tapped < 0)
        {
            return;
        }

        var selected = tapped == 0 ? null : appCounts[tapped - 1].AppId;
        if (string.Equals(selected, appFilter, StringComparison.Ordinal))
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        appFilter = selected;
        EnsureBuilt();
    }

    private void DrawAppCard(float width, float scale)
    {
        if (appFilter is null || !TryNewest(appFilter, out var newest))
        {
            return;
        }

        var pad = Metrics.Space.Lg * scale;
        var iconSize = AppIconSize * scale;
        var actionHeight = AppActionHeight * scale;
        var height = pad * 2f + iconSize + AppActionRowGap * scale + actionHeight;
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + width, origin.Y + height);
        var drawList = ImGui.GetWindowDrawList();
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);

        var iconMin = new Vector2(origin.X + pad, origin.Y + pad);
        NotificationCard.DrawAppIcon(drawList, newest, iconMin, iconSize, 1f);
        var textLeft = iconMin.X + iconSize + AppTextGap * scale;
        var textWidth = MathF.Max(1f, max.X - pad - textLeft);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var textTop = iconMin.Y + (iconSize - nameHeight - AppLineGap * scale - lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, textTop),
            Typography.FitText(AppName(appFilter), textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var muted = notifications.IsMuted(appFilter);
        Typography.Draw(drawList, new Vector2(textLeft, textTop + nameHeight + AppLineGap * scale),
            Typography.FitText(AppStatus(muted), textWidth, TextStyles.Subheadline),
            muted ? ui.HeaderInk : ui.MutedInk, TextStyles.Subheadline);

        var rowTop = iconMin.Y + iconSize + AppActionRowGap * scale;
        var action = muted
            ? DrawActionRow(drawList, origin.X + pad, max.X - pad, rowTop, actionHeight, scale,
                AppAction.Unmute, AppAction.Settings, AppAction.None)
            : DrawActionRow(drawList, origin.X + pad, max.X - pad, rowTop, actionHeight, scale,
                AppAction.MuteHour, AppAction.MuteToday, AppAction.Settings);
        Advance(origin, width, height, BlockGap, scale);
        Apply(action, appFilter, nowUnix);
    }

    private bool TryNewest(string appId, out PhoneNotification newest)
    {
        var list = groups.Groups;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Newest.AppId, appId, StringComparison.Ordinal))
            {
                newest = list[index].Newest;
                return true;
            }
        }

        newest = null!;
        return false;
    }

    private string AppStatus(bool muted)
    {
        var until = muted && configuration.NotificationSettings.TryGetValue(appFilter!, out var setting)
            ? setting.MutedUntilUnix
            : 0L;
        var key = muted ? until : -1L - visibleCount;
        if (appCardText.IsCurrent(key))
        {
            return appCardText.Value;
        }

        var text = muted
            ? Loc.T(L.Notifications.MutedUntil, TimeText.Clock(until))
            : Loc.Plural(L.Notifications.Count, visibleCount);
        return appCardText.Store(key, text);
    }

    private AppAction DrawActionRow(ImDrawListPtr drawList, float left, float right, float top, float height,
        float scale, AppAction first, AppAction second, AppAction third)
    {
        var slots = third == AppAction.None ? 2 : 3;
        var gap = AppActionGap * scale;
        var slotWidth = (right - left - gap * (slots - 1)) / slots;
        var result = AppAction.None;
        for (var index = 0; index < slots; index++)
        {
            var action = index == 0 ? first : index == 1 ? second : third;
            var slotLeft = left + index * (slotWidth + gap);
            var rect = new Rect(new Vector2(slotLeft, top), new Vector2(slotLeft + slotWidth, top + height));
            if (ActionPill(drawList, rect, ActionLabel(action), action == AppAction.Unmute))
            {
                result = action;
            }
        }

        return result;
    }

    private bool ActionPill(ImDrawListPtr drawList, Rect rect, string label, bool prominent)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var fill = prominent
            ? hovered ? Palette.Mix(ui.Accent, White, LinkHoverLift) : ui.Accent
            : hovered ? Palette.Mix(ui.FieldSurface, ui.HoverTint, ActionHoverMix) : ui.FieldSurface;
        drawList.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(fill), rect.Height * 0.5f);
        var fitted = Typography.FitText(label, MathF.Max(1f, rect.Width - rect.Height * 0.5f),
            TextStyles.FootnoteEmphasized);
        Typography.DrawCentered(drawList, rect.Center, fitted, prominent ? White : ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static string ActionLabel(AppAction action) => action switch
    {
        AppAction.MuteHour => Loc.T(L.Notifications.MuteHour),
        AppAction.MuteToday => Loc.T(L.Notifications.MuteToday),
        AppAction.Unmute => Loc.T(L.Notifications.Unmute),
        _ => Loc.T(L.Notifications.AppSettings),
    };

    private void Apply(AppAction action, string appId, long nowUnix)
    {
        switch (action)
        {
            case AppAction.MuteHour:
                SetMute(appId, NotificationMutes.ForAnHour(nowUnix));
                break;
            case AppAction.MuteToday:
                SetMute(appId, NotificationMutes.UntilTomorrow(DateTimeOffset.Now));
                break;
            case AppAction.Unmute:
                SetMute(appId, 0L);
                break;
            case AppAction.Settings:
                OpenSettings(appId);
                break;
        }
    }

    private void SetMute(string appId, long untilUnix)
    {
        configuration.NotificationSettingFor(appId).MutedUntilUnix = untilUnix;
        configuration.Save();
        appCardText.Reset();
        UiFeedback.Play(untilUnix > 0 ? UiSound.ToggleOn : UiSound.ToggleOff);
    }
}
