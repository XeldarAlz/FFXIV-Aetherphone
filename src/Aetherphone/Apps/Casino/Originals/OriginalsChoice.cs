using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class OriginalsChoice
{
    private readonly string id;
    private readonly DropdownMenu menu = new();
    private readonly DropdownMenu.Item[] items;
    private readonly string[] shown;

    private string header = string.Empty;
    private int count;
    private int selected;
    private int picked = -1;

    public OriginalsChoice(string id, int capacity)
    {
        this.id = id;
        items = new DropdownMenu.Item[capacity];
        shown = new string[capacity];
    }

    public bool Open => menu.IsOpenFor(id);

    public void Gate()
    {
        menu.Gate();
    }

    public void Close()
    {
        menu.Close();
        picked = -1;
    }

    public static float PickerWidth(string caption, ReadOnlySpan<string> labels, float height, float scale)
    {
        var widest = 0f;
        for (var index = 0; index < labels.Length; index++)
        {
            widest = MathF.Max(widest, KnobPicker.Width(caption, labels[index], height, scale));
        }

        return widest;
    }

    public int Draw(ImDrawListPtr drawList, Rect rect, string caption, ReadOnlySpan<string> labels, int current,
        AppSkin ui, bool enabled)
    {
        if (KnobSegments.Fits(labels, rect.Width, rect.Height, UiScale.Current))
        {
            if (Open)
            {
                Close();
            }

            return KnobSegments.Draw(drawList, id, rect, labels, current, ui, enabled);
        }

        return DrawPicker(drawList, rect, caption, labels, current, ui, enabled);
    }

    public int DrawPicker(ImDrawListPtr drawList, Rect rect, string caption, ReadOnlySpan<string> labels, int current,
        AppSkin ui, bool enabled)
    {
        if (labels.Length == 0)
        {
            return current;
        }

        var clamped = Math.Clamp(current, 0, labels.Length - 1);
        Remember(caption, labels, clamped);
        if (!enabled && Open)
        {
            Close();
        }

        if (KnobPicker.Draw(drawList, id, rect, caption, labels[clamped], ui, enabled, Open))
        {
            menu.Toggle(id, rect);
        }

        var result = enabled && picked >= 0 && picked < labels.Length ? picked : current;
        picked = -1;
        return result;
    }

    public void DrawOverlay(Rect screen, AppSkin ui)
    {
        if (!Open)
        {
            return;
        }

        for (var index = 0; index < count; index++)
        {
            items[index] = new DropdownMenu.Item(shown[index], Selected: index == selected);
        }

        menu.Header = header;
        var chosen = menu.Draw(screen, ui.Theme, items.AsSpan(0, count));
        if (chosen >= 0)
        {
            picked = chosen;
        }
    }

    private void Remember(string caption, ReadOnlySpan<string> labels, int current)
    {
        header = caption;
        selected = current;
        count = Math.Min(labels.Length, shown.Length);
        for (var index = 0; index < count; index++)
        {
            shown[index] = labels[index];
        }
    }
}
