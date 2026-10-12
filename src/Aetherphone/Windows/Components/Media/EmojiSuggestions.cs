using Aetherphone.Core;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class EmojiSuggestions
{
    private const float SuggestionRowUnits = 24f;
    private const float SuggestionPadUnits = 6f;
    private const float SuggestionGapUnits = 4f;
    private const float SuggestionIconUnits = 16f;
    private const int SuggestionLimit = 6;

    private readonly EmojiShortcode[] suggestions = new EmojiShortcode[SuggestionLimit];
    private readonly string[] labels = new string[SuggestionLimit];
    private string query = string.Empty;
    private int suggestionCount;
    private int selected;
    private int shownFrame = int.MinValue / 2;

    public bool Showing => ImGui.GetFrameCount() - shownFrame <= 1;

    public void Reset()
    {
        query = string.Empty;
        suggestionCount = 0;
        selected = 0;
    }

    public bool Draw(Rect bar, Rect host, PhoneTheme theme, ref string draft, bool below = false)
    {
        var accepting = Showing && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter) ||
                                    ImGui.IsKeyPressed(ImGuiKey.Tab));
        var text = accepting && draft.EndsWith('\n') ? draft.AsSpan(0, draft.Length - 1) : draft.AsSpan();
        if (!EmojiCatalog.Ready || !EmojiAutocomplete.TryToken(text, text.Length, out var start, out var length) ||
            length < EmojiAutocomplete.MinimumQuery)
        {
            Reset();
            return false;
        }

        Refresh(text.Slice(start + 1, length));
        var scale = UiScale.Current;
        var rowHeight = SuggestionRowUnits * scale;
        var pad = SuggestionPadUnits * scale;
        var gap = SuggestionGapUnits * scale;
        var space = below ? host.Max.Y - bar.Max.Y : bar.Min.Y - host.Min.Y;
        var rows = Math.Min(suggestionCount, (int)((space - gap - pad * 2f) / rowHeight));
        if (rows <= 0)
        {
            return false;
        }

        shownFrame = ImGui.GetFrameCount();
        selected = Math.Min(selected, rows - 1);
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
        {
            selected = (selected + 1) % rows;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
        {
            selected = (selected + rows - 1) % rows;
        }

        var height = rows * rowHeight + pad * 2f;
        var top = below ? bar.Max.Y + gap : bar.Min.Y - gap - height;
        var popup = new Rect(new Vector2(bar.Min.X + Metrics.Space.Md * scale, top),
            new Vector2(bar.Max.X - Metrics.Space.Md * scale, top + height));
        var picked = DrawPopup(popup, theme, rows, rowHeight, pad);
        if (picked < 0 && accepting)
        {
            picked = selected;
        }

        if (picked < 0)
        {
            return false;
        }

        draft = string.Concat(text[..start], labels[picked], " ");
        Reset();
        return true;
    }

    private void Refresh(ReadOnlySpan<char> current)
    {
        if (current.Equals(query, StringComparison.Ordinal))
        {
            return;
        }

        query = new string(current);
        selected = 0;
        suggestionCount = EmojiAutocomplete.Rank(current, suggestions);
        for (var index = 0; index < suggestionCount; index++)
        {
            labels[index] = string.Concat(":", suggestions[index].Code, ":");
        }
    }

    private int DrawPopup(Rect popup, PhoneTheme theme, int rows, float rowHeight, float pad)
    {
        UiInteract.HoverOverlay(popup);
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
                var hovered = !UiInteract.InputBlocked && UiInteract.HoverWindowOnly(min, max);
                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    if (ImGui.GetIO().MouseDelta != Vector2.Zero)
                    {
                        selected = index;
                    }
                }

                var lit = index == selected;
                if (lit)
                {
                    Squircle.Fill(drawList, min, max, Metrics.Radius.Sm * scale,
                        ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.10f)));
                }

                var iconMin = new Vector2(popup.Min.X + pad, min.Y + (rowHeight - icon) * 0.5f);
                EmojiImages.TryDraw(drawList, suggestions[index].File, iconMin, iconMin + new Vector2(icon, icon),
                    0xFFFFFFFF);
                var label = Typography.FitText(labels[index], max.X - textLeft - pad, TextStyles.Caption1);
                var textTop = min.Y + (rowHeight - Typography.LineHeight(TextStyles.Caption1)) * 0.5f;
                Typography.Draw(drawList, new Vector2(textLeft, textTop), label,
                    lit ? theme.TextStrong : theme.TextMuted, TextStyles.Caption1);
                if (UiInteract.Click(min, max, hovered))
                {
                    picked = index;
                }
            }
        }

        return picked;
    }
}
