using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows;

internal sealed partial class LinkpearlPopoutWindow
{
    private const float LayoutSegmentFraction = 0.56f;
    private const float PanelBottomPad = 24f;
    private const float TextScaleLabelEpsilon = 0.001f;

    private readonly string[] layoutOptions = new string[2];
    private readonly string textSizeMenuId;
    private readonly PopoutSettingIds settingIds;
    private string textSizeLabel = string.Empty;
    private float textSizeLabelValue = -1f;
    private bool settingsOpen;

    private void ToggleSettings()
    {
        settingsOpen = !settingsOpen;
        CloseMenus();
        Touch();
    }

    private void DrawSettingsPanel(Rect body, PhoneTheme theme, InboxRow row)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId(settingIds.Panel))
        using (AppSurface.Begin(body))
        {
            DrawChatCard(theme, row, scale);
            ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
            DrawPopoutCard(theme, scale);
            if (row.Density == ChatDensity.Log)
            {
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
                DrawLogCard(theme, row);
            }

            ImGui.Dummy(new Vector2(0f, PanelBottomPad * scale));
        }
    }

    private void DrawChatCard(PhoneTheme theme, InboxRow row, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.ThisChat), theme);
        var card = GroupCard.Begin(theme, 1);
        var layoutRow = card.NextRow();
        var label = Loc.T(L.Linkpearl.Layout);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(layoutRow.Min.X, layoutRow.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong,
            TextStyles.BodyEmphasized);
        var segmentLeft = MathF.Max(layoutRow.Min.X + labelSize.X + Metrics.Space.Md * scale,
            layoutRow.Max.X - layoutRow.Width * LayoutSegmentFraction);
        layoutOptions[0] = Loc.T(L.Linkpearl.LayoutLog);
        layoutOptions[1] = Loc.T(L.Linkpearl.LayoutBubbles);
        var selected = row.Density == ChatDensity.Bubbles ? 1 : 0;
        var picked = SegmentStrip.Draw(settingIds.Layout,
            new Rect(new Vector2(segmentLeft, layoutRow.Min.Y), layoutRow.Max), layoutOptions, selected, theme);
        if (picked != selected)
        {
            inbox.SetDensity(row, picked == 1 ? ChatDensity.Bubbles : ChatDensity.Log);
        }

        card.End();
    }

    private void DrawPopoutCard(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PopoutSection), theme);
        var fade = configuration.LinkpearlPopoutFade;
        var card = GroupCard.Begin(theme, fade ? 4 : 3);
        var textSizeRow = card.NextRow();
        if (SettingsRow.Disclosure(textSizeRow, Loc.T(L.Linkpearl.PopoutTextSize), TextSizeLabel(), theme,
                settingIds.TextSize))
        {
            OpenTextSizeMenu(textSizeRow);
        }

        configuration.LinkpearlPopoutOpacity = LinkpearlSettingRows.OpacitySlider(card.NextRow(), scale,
            settingIds.Opacity, Loc.T(L.Linkpearl.PopoutOpacity), configuration.LinkpearlPopoutOpacity,
            LinkpearlSettingRows.PopoutOpacityMinimum, theme, out var opacityReleased);
        if (opacityReleased)
        {
            configuration.Save();
        }

        var nextFade = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.PopoutFade), fade, theme, settingIds.Fade);
        if (nextFade != fade)
        {
            configuration.LinkpearlPopoutFade = nextFade;
            configuration.Save();
        }

        if (fade)
        {
            configuration.LinkpearlPopoutIdleOpacity = LinkpearlSettingRows.OpacitySlider(card.NextRow(), scale,
                settingIds.IdleOpacity, Loc.T(L.Linkpearl.PopoutIdleOpacity),
                configuration.LinkpearlPopoutIdleOpacity, LinkpearlSettingRows.IdleOpacityMinimum, theme,
                out var idleReleased);
            if (idleReleased)
            {
                configuration.Save();
            }
        }

        card.End();
    }

    private void DrawLogCard(PhoneTheme theme, InboxRow row)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.LayoutLog), theme);
        var card = GroupCard.Begin(theme, 4);
        var timestamps = row.Tab?.Timestamps ?? configuration.LinkpearlLogTimestamps;
        var nextTimestamps = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.LogTimestamps), timestamps, theme,
            settingIds.Timestamps);
        if (nextTimestamps != timestamps)
        {
            SetTimestamps(row, nextTimestamps);
        }

        DrawLogSwitch(card.NextRow(), theme, L.Linkpearl.LogWorldNames, configuration.LinkpearlLogWorldNames,
            settingIds.WorldNames, static (configuration, value) => configuration.LinkpearlLogWorldNames = value);
        DrawLogSwitch(card.NextRow(), theme, L.Linkpearl.LogGameColors, configuration.LinkpearlLogGameColors,
            settingIds.GameColors, static (configuration, value) => configuration.LinkpearlLogGameColors = value);
        DrawLogSwitch(card.NextRow(), theme, L.Linkpearl.LogGroupLines, configuration.LinkpearlLogGroupLines,
            settingIds.GroupLines, static (configuration, value) => configuration.LinkpearlLogGroupLines = value);
        card.End();
    }

    private void DrawLogSwitch(Rect row, PhoneTheme theme, LocString label, bool value, string id,
        Action<Configuration, bool> apply)
    {
        var next = SettingsRow.Bool(row, Loc.T(label), value, theme, id);
        if (next == value)
        {
            return;
        }

        apply(configuration, next);
        configuration.Save();
    }

    private void SetTimestamps(InboxRow row, bool value)
    {
        if (row.Tab is { Timestamps: not null } tab)
        {
            tab.Timestamps = value;
            tabs.Update(tab);
            return;
        }

        configuration.LinkpearlLogTimestamps = value;
        configuration.Save();
    }

    private void OpenTextSizeMenu(Rect anchor)
    {
        switchItems.Clear();
        switchKeys.Clear();
        switchActions.Clear();
        var choices = LinkpearlSettingRows.TextScaleChoices;
        for (var index = 0; index < choices.Length; index++)
        {
            switchItems.Add(new DropdownMenu.Item(LinkpearlSettingRows.PercentLabel(choices[index]), string.Empty,
                false,
                MathF.Abs(choices[index] - configuration.LinkpearlPopoutTextScale) < LinkpearlSettingRows.ScaleEpsilon));
        }

        switchMenu.Header = Loc.T(L.Linkpearl.PopoutTextSize);
        switchMenu.Toggle(textSizeMenuId, anchor);
    }

    private void DrawTextSizeMenu(PhoneTheme theme)
    {
        var picked = switchMenu.Draw(PhoneBounds.Viewport(), theme, CollectionsMarshal.AsSpan(switchItems));
        if (picked < 0)
        {
            return;
        }

        configuration.LinkpearlPopoutTextScale = LinkpearlSettingRows.TextScaleChoices[picked];
        configuration.Save();
    }

    private string TextSizeLabel()
    {
        var value = configuration.LinkpearlPopoutTextScale;
        if (MathF.Abs(value - textSizeLabelValue) < TextScaleLabelEpsilon)
        {
            return textSizeLabel;
        }

        textSizeLabelValue = value;
        textSizeLabel = LinkpearlSettingRows.PercentLabel(value);
        return textSizeLabel;
    }

    private readonly struct PopoutSettingIds
    {
        public readonly string Panel;
        public readonly string Layout;
        public readonly string TextSize;
        public readonly string Opacity;
        public readonly string Fade;
        public readonly string IdleOpacity;
        public readonly string Timestamps;
        public readonly string WorldNames;
        public readonly string GameColors;
        public readonly string GroupLines;

        public PopoutSettingIds(string slotText)
        {
            var prefix = string.Concat("linkpearl.popout.settings.", slotText, ".");
            Panel = prefix + "panel";
            Layout = prefix + "layout";
            TextSize = prefix + "textSize";
            Opacity = prefix + "opacity";
            Fade = prefix + "fade";
            IdleOpacity = prefix + "idleOpacity";
            Timestamps = prefix + "timestamps";
            WorldNames = prefix + "worldNames";
            GameColors = prefix + "gameColors";
            GroupLines = prefix + "groupLines";
        }
    }
}
