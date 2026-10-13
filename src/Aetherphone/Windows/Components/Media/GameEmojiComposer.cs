using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class GameEmojiComposer
{
    private const float PanelHeightUnits = 214f;
    private const float PanelGap = 6f;
    private const float ToggleGlyphFactor = 1.5f;

    private readonly EmojiPicker picker = new() { Compact = true };
    private readonly EmojiSuggestions suggestions = new();
    private readonly AppSkin skin = new(AppPalettes.Linkpearl(PhoneTheme.Default));
    private int openedFrame = -1;
    private bool open;

    public static bool PickerEnabled
    {
        get
        {
            var configuration = Plugin.Cfg;
            return configuration is null || configuration.LinkpearlEmojiPicker;
        }
    }

    public bool Open => open;

    public bool Suggesting => suggestions.Showing;

    public void Close()
    {
        open = false;
        suggestions.Reset();
    }

    public void DrawToggle(Vector2 center, float radius, PhoneTheme theme)
    {
        if (!PickerEnabled || !EmojiCatalog.Ready)
        {
            return;
        }

        var min = center - new Vector2(radius, radius);
        var max = center + new Vector2(radius, radius);
        var hovered = UiInteract.Hover(min, max);
        var color = open ? theme.Accent : hovered ? theme.TextStrong : theme.TextMuted;
        PhoneIcon.Draw(ImGui.GetWindowDrawList(), center, PhoneIcons.MoodSmile, color, radius * ToggleGlyphFactor);
        HoverTooltip.Show(new Rect(min, max), Loc.T(L.Common.Emoji), HoverLabelSide.Above);
        if (!hovered)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (!ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return;
        }

        open = !open;
        openedFrame = ImGui.GetFrameCount();
        suggestions.Reset();
    }

    public string? DrawPanel(Rect bar, Rect host, PhoneTheme theme)
    {
        if (!open || !PickerEnabled || !EmojiCatalog.Ready)
        {
            return null;
        }

        var scale = UiScale.Current;
        var bottom = bar.Min.Y - PanelGap * scale;
        var top = MathF.Max(host.Min.Y, bottom - PanelHeightUnits * scale);
        var panel = new Rect(new Vector2(bar.Min.X, top), new Vector2(bar.Max.X, bottom));
        skin.Palette = AppPalettes.Linkpearl(theme);
        skin.Theme = theme;
        var picked = picker.Draw(panel, skin);
        if (picked is null && ImGui.GetFrameCount() != openedFrame &&
            UiInteract.ClickedOutside(panel.Min, panel.Max))
        {
            open = false;
        }

        return picked;
    }

    public bool DrawSuggestions(Rect bar, Rect host, PhoneTheme theme, ref string draft, bool typing)
    {
        if (open || !PickerEnabled || !EmojiShortcodes.Enabled)
        {
            return false;
        }

        skin.Palette = AppPalettes.Linkpearl(theme);
        skin.Theme = theme;
        return suggestions.Draw(bar, host, skin, ref draft, typing);
    }
}
