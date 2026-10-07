using System.Runtime.InteropServices;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Message;

internal sealed partial class MessagePopoutWindow
{
    private const float PanelBottomPad = 24f;
    private const float TextScaleLabelEpsilon = 0.001f;

    private readonly string textSizeMenuId;
    private readonly PopoutSettingIds settingIds;
    private string textSizeLabel = string.Empty;
    private float textSizeLabelValue = -1f;

    private void ToggleSettings()
    {
        screen = screen == PopoutScreen.Settings ? PopoutScreen.Thread : PopoutScreen.Settings;
        CloseMenus();
    }

    private void DrawSettingsPanel(Rect body, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        using (ImRaii.PushId(settingIds.Panel))
        using (AppSurface.Begin(body))
        {
            DrawPopoutCard(theme, scale);
            ImGui.Dummy(new Vector2(0f, PanelBottomPad * scale));
        }
    }

    private void DrawPopoutCard(PhoneTheme theme, float scale)
    {
        SettingsSection.Header(Loc.T(L.Linkpearl.PopoutSection), theme);
        var fade = configuration.MessagePopoutFade;
        var card = GroupCard.Begin(theme, fade ? 4 : 3);
        var textSizeRow = card.NextRow();
        if (SettingsRow.Disclosure(textSizeRow, Loc.T(L.Linkpearl.PopoutTextSize), TextSizeLabel(), theme,
                settingIds.TextSize))
        {
            OpenTextSizeMenu(textSizeRow);
        }

        configuration.MessagePopoutOpacity = LinkpearlSettingRows.OpacitySlider(card.NextRow(), scale,
            settingIds.Opacity, Loc.T(L.Linkpearl.PopoutOpacity), configuration.MessagePopoutOpacity,
            LinkpearlSettingRows.PopoutOpacityMinimum, theme, out var opacityReleased);
        if (opacityReleased)
        {
            configuration.Save();
        }

        var nextFade = SettingsRow.Bool(card.NextRow(), Loc.T(L.Linkpearl.PopoutFade), fade, theme, settingIds.Fade);
        if (nextFade != fade)
        {
            configuration.MessagePopoutFade = nextFade;
            configuration.Save();
        }

        if (fade)
        {
            configuration.MessagePopoutIdleOpacity = LinkpearlSettingRows.OpacitySlider(card.NextRow(), scale,
                settingIds.IdleOpacity, Loc.T(L.Linkpearl.PopoutIdleOpacity),
                configuration.MessagePopoutIdleOpacity, LinkpearlSettingRows.IdleOpacityMinimum, theme,
                out var idleReleased);
            if (idleReleased)
            {
                configuration.Save();
            }
        }

        card.End();
    }

    private void OpenTextSizeMenu(Rect anchor)
    {
        switchItems.Clear();
        var choices = LinkpearlSettingRows.TextScaleChoices;
        for (var index = 0; index < choices.Length; index++)
        {
            switchItems.Add(new DropdownMenu.Item(LinkpearlSettingRows.PercentLabel(choices[index]), string.Empty,
                false,
                MathF.Abs(choices[index] - configuration.MessagePopoutTextScale) < LinkpearlSettingRows.ScaleEpsilon));
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

        configuration.MessagePopoutTextScale = LinkpearlSettingRows.TextScaleChoices[picked];
        configuration.Save();
    }

    private string TextSizeLabel()
    {
        var value = configuration.MessagePopoutTextScale;
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
        public readonly string TextSize;
        public readonly string Opacity;
        public readonly string Fade;
        public readonly string IdleOpacity;

        public PopoutSettingIds(string slotText)
        {
            var prefix = string.Concat("message.popout.settings.", slotText, ".");
            Panel = prefix + "panel";
            TextSize = prefix + "textSize";
            Opacity = prefix + "opacity";
            Fade = prefix + "fade";
            IdleOpacity = prefix + "idleOpacity";
        }
    }
}
