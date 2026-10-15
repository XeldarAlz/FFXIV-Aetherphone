using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Emoji;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class EmojiSuggestions
{
    private const float SuggestionRowUnits = 34f;
    private const float SuggestionPadUnits = 6f;
    private const float SuggestionGapUnits = 6f;
    private const float SuggestionIconUnits = 22f;
    private const float SuggestionRadiusUnits = 14f;
    private const float RowRadiusUnits = 9f;
    private const float RevealSlideUnits = 6f;
    private const float SelectedTint = 0.18f;
    private const int SuggestionLimit = 6;

    private readonly EmojiShortcode[] suggestions = new EmojiShortcode[SuggestionLimit];
    private readonly string[] labels = new string[SuggestionLimit];
    private Spring revealSpring;
    private string query = string.Empty;
    private int suggestionCount;
    private int selected;
    private int shownFrame = int.MinValue / 2;

    public bool Showing => ImGui.GetFrameCount() - shownFrame <= 1;

    public static bool FieldTyping() => ImGui.IsItemActive() || ImGui.IsItemDeactivated();

    internal static bool StaysOpen(bool typing, bool showing, bool pointerHeld) => typing || (showing && pointerHeld);

    internal static bool TakesKeys(bool typing, bool blocked, bool shielded) => typing && !blocked && !shielded;

    public void Reset()
    {
        query = string.Empty;
        suggestionCount = 0;
        selected = 0;
    }

    public bool Draw(Rect bar, Rect host, AppSkin ui, ref string draft, bool typing, bool below = false)
    {
        var pointerHeld = ImGui.IsMouseDown(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Left);
        if (!StaysOpen(typing, Showing, pointerHeld))
        {
            Reset();
            return false;
        }

        var keys = TakesKeys(typing, UiInteract.InputBlocked, InputShield.Active);
        var accepting = keys && Showing && (ImGui.IsKeyPressed(ImGuiKey.Enter) ||
                                            ImGui.IsKeyPressed(ImGuiKey.KeypadEnter) ||
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

        if (!Showing)
        {
            revealSpring.SnapTo(0f);
        }

        shownFrame = ImGui.GetFrameCount();
        selected = Math.Min(selected, rows - 1);
        if (keys && ImGui.IsKeyPressed(ImGuiKey.DownArrow, true))
        {
            selected = (selected + 1) % rows;
        }

        if (keys && ImGui.IsKeyPressed(ImGuiKey.UpArrow, true))
        {
            selected = (selected + rows - 1) % rows;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var reveal = Math.Clamp(revealSpring.Step(1f, Motion.Appear, delta), 0f, 1f);
        var slide = (1f - reveal) * RevealSlideUnits * scale * (below ? -1f : 1f);
        var height = rows * rowHeight + pad * 2f;
        var top = (below ? bar.Max.Y + gap : bar.Min.Y - gap - height) + slide;
        var popup = new Rect(new Vector2(bar.Min.X + Metrics.Space.Md * scale, top),
            new Vector2(bar.Max.X - Metrics.Space.Md * scale, top + height));
        var picked = DrawPopup(popup, ui, rows, rowHeight, pad, reveal);
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

    private int DrawPopup(Rect popup, AppSkin ui, int rows, float rowHeight, float pad, float alpha)
    {
        var theme = ui.Theme;
        UiInteract.HoverOverlay(popup);
        var picked = -1;
        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        var rounding = SuggestionRadiusUnits * scale;
        Elevation.Floating(drawList, popup.Min, popup.Max, rounding, scale, alpha);
        Squircle.Fill(drawList, popup.Min, popup.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(theme.GroupedCard,
                MathF.Min(0.98f, theme.GroupedCard.W + 0.4f) * alpha)));
        Material.EdgeSquircle(drawList, popup.Min, popup.Max, rounding, scale, alpha);
        var icon = SuggestionIconUnits * scale;
        var inset = Metrics.Space.Sm * scale;
        var textLeft = popup.Min.X + pad + inset + icon + Metrics.Space.Sm * scale;
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        for (var index = 0; index < rows; index++)
        {
            var min = new Vector2(popup.Min.X + pad, popup.Min.Y + pad + index * rowHeight);
            var max = new Vector2(popup.Max.X - pad, min.Y + rowHeight);
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
                Squircle.Fill(drawList, min, max, RowRadiusUnits * scale,
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, SelectedTint * alpha)));
            }

            var iconMin = new Vector2(min.X + inset, min.Y + (rowHeight - icon) * 0.5f);
            EmojiImages.TryDraw(drawList, suggestions[index].File, iconMin, iconMin + new Vector2(icon, icon), tint);
            var label = Typography.FitText(labels[index], max.X - textLeft - inset, TextStyles.Subheadline);
            var ink = lit ? theme.TextStrong : theme.TextMuted;
            Typography.Draw(drawList, new Vector2(textLeft, min.Y + (rowHeight - lineHeight) * 0.5f), label,
                Palette.WithAlpha(ink, ink.W * alpha), TextStyles.Subheadline);
            if (UiInteract.Click(min, max, hovered))
            {
                picked = index;
            }
        }

        return picked;
    }
}
