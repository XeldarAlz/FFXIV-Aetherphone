using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Home;

internal readonly struct WidgetChoice
{
    public readonly string Value;
    public readonly LocString Label;
    public readonly string Text;

    public WidgetChoice(string value, LocString label)
    {
        Value = value;
        Label = label;
        Text = string.Empty;
    }

    public WidgetChoice(string value, string text)
    {
        Value = value;
        Label = default;
        Text = text;
    }

    public string Display => Text.Length > 0 || Label.Key is null ? Text : Loc.T(Label);
}

internal sealed class WidgetOption
{
    public static readonly IReadOnlyList<WidgetOption> None = Array.Empty<WidgetOption>();

    private readonly WidgetChoice[] fixedChoices;
    private readonly Action<List<WidgetChoice>>? source;

    public WidgetOption(string key, LocString label, WidgetChoice[] choices, string defaultValue = "")
    {
        Key = key;
        Label = label;
        fixedChoices = choices;
        DefaultValue = defaultValue;
    }

    public WidgetOption(string key, LocString label, Action<List<WidgetChoice>> source, string defaultValue = "")
    {
        Key = key;
        Label = label;
        fixedChoices = Array.Empty<WidgetChoice>();
        this.source = source;
        DefaultValue = defaultValue;
    }

    public string Key { get; }
    public LocString Label { get; }
    public string DefaultValue { get; }

    public void Choices(List<WidgetChoice> target)
    {
        target.Clear();
        if (source is not null)
        {
            source(target);
            return;
        }

        for (var index = 0; index < fixedChoices.Length; index++)
        {
            target.Add(fixedChoices[index]);
        }
    }
}
