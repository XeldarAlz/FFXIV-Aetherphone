using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class GameEmojiComposer
{
    private const float PanelHeightUnits = 214f;
    private const float PanelGap = 6f;
    private const float SuggestionRowUnits = 24f;
    private const float SuggestionPadUnits = 6f;
    private const float SuggestionGapUnits = 4f;
    private const float SuggestionIconUnits = 16f;
    private const int SuggestionLimit = 6;
    private const float ToggleGlyphFactor = 1.5f;

    private readonly EmojiPicker picker = new() { Compact = true };
    private readonly EmojiShortcode[] suggestions = new EmojiShortcode[SuggestionLimit];
    private readonly string[] labels = new string[SuggestionLimit];
    private readonly AppSkin skin = new(AppPalettes.Linkpearl(PhoneTheme.Default));
    private string query = string.Empty;
    private int suggestionCount;
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

    public void Close()
    {
        open = false;
        query = string.Empty;
        suggestionCount = 0;
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
        query = string.Empty;
        suggestionCount = 0;
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

    public bool DrawSuggestions(Rect bar, Rect host, PhoneTheme theme, ref string draft)
    {
        if (open || !PickerEnabled || !EmojiShortcodes.Enabled || !EmojiCatalog.Ready)
        {
            return false;
        }

        if (!EmojiAutocomplete.TryToken(draft, draft.Length, out var start, out var length) ||
            length < EmojiAutocomplete.MinimumQuery)
        {
            query = string.Empty;
            suggestionCount = 0;
            return false;
        }

        Refresh(draft.AsSpan(start + 1, length));
        var scale = UiScale.Current;
        var rowHeight = SuggestionRowUnits * scale;
        var pad = SuggestionPadUnits * scale;
        var bottom = bar.Min.Y - SuggestionGapUnits * scale;
        var room = (int)((bottom - host.Min.Y - pad * 2f) / rowHeight);
        var rows = Math.Min(suggestionCount, room);
        if (rows <= 0)
        {
            return false;
        }

        var popup = new Rect(new Vector2(bar.Min.X + Metrics.Space.Md * scale, bottom - rows * rowHeight - pad * 2f),
            new Vector2(bar.Max.X - Metrics.Space.Md * scale, bottom));
        var picked = DrawPopup(popup, theme, rows, rowHeight, pad);
        if (picked < 0)
        {
            return false;
        }

        draft = string.Concat(draft.AsSpan(0, start), labels[picked], draft.AsSpan(start + 1 + length));
        query = string.Empty;
        suggestionCount = 0;
        return true;
    }

    private void Refresh(ReadOnlySpan<char> current)
    {
        if (current.Equals(query, StringComparison.Ordinal))
        {
            return;
        }

        query = new string(current);
        suggestionCount = EmojiAutocomplete.Rank(current, suggestions);
        for (var index = 0; index < suggestionCount; index++)
        {
            labels[index] = string.Concat(":", suggestions[index].Code, ":");
        }
    }

    private int DrawPopup(Rect popup, PhoneTheme theme, int rows, float rowHeight, float pad)
    {
        var picked = -1;
        var scale = UiScale.Current;
        ImGui.SetCursorScreenPos(popup.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var child = ImRaii.Child("##emojiSuggest", popup.Size, false,
                   ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return -1;
            }

            var drawList = ImGui.GetWindowDrawList();
            var rounding = Metrics.Radius.Md * scale;
            Squircle.Fill(drawList, popup.Min, popup.Max, rounding, ImGui.GetColorU32(theme.GroupedCard));
            Squircle.Stroke(drawList, popup.Min, popup.Max, rounding, ImGui.GetColorU32(theme.Separator),
                Metrics.Stroke.Hairline * scale);
            var icon = SuggestionIconUnits * scale;
            var textLeft = popup.Min.X + pad + icon + Metrics.Space.Sm * scale;
            for (var index = 0; index < rows; index++)
            {
                var min = new Vector2(popup.Min.X + pad * 0.5f, popup.Min.Y + pad + index * rowHeight);
                var max = new Vector2(popup.Max.X - pad * 0.5f, min.Y + rowHeight);
                var hovered = UiInteract.Hover(min, max);
                if (hovered)
                {
                    Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale,
                        ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f)));
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                var iconMin = new Vector2(popup.Min.X + pad, min.Y + (rowHeight - icon) * 0.5f);
                EmojiImages.TryDraw(drawList, suggestions[index].File, iconMin, iconMin + new Vector2(icon, icon),
                    0xFFFFFFFF);
                var label = Typography.FitText(labels[index], max.X - textLeft - pad, TextStyles.Caption1);
                var textTop = min.Y + (rowHeight - Typography.LineHeight(TextStyles.Caption1)) * 0.5f;
                Typography.Draw(drawList, new Vector2(textLeft, textTop), label,
                    hovered ? theme.TextStrong : theme.TextMuted, TextStyles.Caption1);
                if (UiInteract.Click(min, max, hovered))
                {
                    picked = index;
                }
            }
        }

        return picked;
    }
}
