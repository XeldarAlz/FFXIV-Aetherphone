using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Shortcuts;

internal interface IShortcutSource
{
    ShortcutEntry? Find(Guid id);
}

internal enum ShortcutStepKind : byte
{
    Command,
    Wait,
    OpenPlugin,
    OpenUrl,
}

[Serializable]
internal sealed class ShortcutStep
{
    public ShortcutStepKind Kind { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Seconds { get; set; } = 1f;
}

[Serializable]
internal sealed class ShortcutEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int Glyph { get; set; }
    public string IconPlugin { get; set; } = string.Empty;
    public string Tint { get; set; } = string.Empty;
    public List<ShortcutStep> Steps { get; set; } = new();

    public ShortcutEntry Copy()
    {
        var copy = new ShortcutEntry
        {
            Name = Name,
            Glyph = Glyph,
            IconPlugin = IconPlugin,
            Tint = Tint,
        };

        for (var index = 0; index < Steps.Count; index++)
        {
            var step = Steps[index];
            copy.Steps.Add(new ShortcutStep { Kind = step.Kind, Text = step.Text, Seconds = step.Seconds });
        }

        return copy;
    }

    public void CopyFrom(ShortcutEntry source)
    {
        Name = source.Name;
        Glyph = source.Glyph;
        IconPlugin = source.IconPlugin;
        Tint = source.Tint;
        Steps.Clear();
        for (var index = 0; index < source.Steps.Count; index++)
        {
            var step = source.Steps[index];
            Steps.Add(new ShortcutStep { Kind = step.Kind, Text = step.Text, Seconds = step.Seconds });
        }
    }
}

internal static class ShortcutTint
{
    public static Vector4 Resolve(string tint) =>
        HexColor.TryParse(tint, out var parsed) ? parsed : AppAccents.For("shortcuts");
}
