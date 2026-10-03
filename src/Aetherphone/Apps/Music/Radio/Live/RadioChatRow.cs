using Aetherphone.Core.Radio;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Music.Radio.Live;

internal sealed class RadioChatRow
{
    private float wrapWidth = -1f;
    private int fontGeneration = -1;
    private float nameWidth = -1f;

    public RadioChatEntry Entry { get; private set; } = null!;

    public Vector4 NameInk { get; private set; }

    public RichTextLayout? Rich { get; private set; }

    public string[] Lines { get; private set; } = Array.Empty<string>();

    public float LineHeight { get; private set; }

    public float BodyHeight { get; private set; }

    public string FittedName { get; private set; } = string.Empty;

    public int Stamp { get; set; }

    public void Assign(RadioChatEntry entry, Vector4 nameInk)
    {
        Entry = entry;
        NameInk = nameInk;
        wrapWidth = -1f;
        nameWidth = -1f;
        Rich = null;
        Lines = Array.Empty<string>();
    }

    public void EnsureBody(float width, in TextStyle style)
    {
        var generation = Plugin.Fonts.Generation;
        if (MathF.Abs(width - wrapWidth) < 0.5f && generation == fontGeneration)
        {
            return;
        }

        wrapWidth = width;
        fontGeneration = generation;
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        {
            Plugin.Fonts.NoticeText(Entry.Text);
            LineHeight = ImGui.GetTextLineHeight();
            Rich = RichText.Build(Entry.Text, ReadOnlySpan<MentionSpan>.Empty, width);
            Lines = Rich is null ? Typography.WrapCurrent(Entry.Text, width) : Array.Empty<string>();
            BodyHeight = Rich is { } rich ? rich.Size.Y : Lines.Length * LineHeight;
        }
    }

    public string NameFor(float width, in TextStyle style)
    {
        if (MathF.Abs(width - nameWidth) < 0.5f)
        {
            return FittedName;
        }

        nameWidth = width;
        FittedName = Typography.FitText(Entry.DisplayName, MathF.Max(1f, width), style);
        return FittedName;
    }
}
