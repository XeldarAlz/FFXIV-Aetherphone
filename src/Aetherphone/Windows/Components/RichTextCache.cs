using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class RichTextCache
{
    private sealed class Entry
    {
        public string Text = string.Empty;
        public MentionDto[]? Source;
        public MentionSpan[] Mentions = Array.Empty<MentionSpan>();
        public RichTextLayout? Layout;
    }

    private const int CacheLimit = 256;

    private static readonly MentionSpan[] NoMentions = Array.Empty<MentionSpan>();

    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public RichTextLayout? LayoutFor(string key, string text, MentionDto[]? mentions, float wrapWidth)
    {
        if (entries.TryGetValue(key, out var entry) && Matches(entry, text, mentions, wrapWidth))
        {
            return entry.Layout;
        }

        if (entries.Count > CacheLimit)
        {
            entries.Clear();
            entry = null;
        }

        entry ??= new Entry();
        entry.Text = text;
        entry.Source = mentions;
        entry.Mentions = Convert(mentions);
        entry.Layout = RichText.Build(text, entry.Mentions, wrapWidth);
        entries[key] = entry;
        return entry.Layout;
    }

    public void Clear()
    {
        entries.Clear();
    }

    private static MentionSpan[] Convert(MentionDto[]? mentions)
    {
        if (mentions is null || mentions.Length == 0)
        {
            return NoMentions;
        }

        var spans = new MentionSpan[mentions.Length];
        for (var index = 0; index < mentions.Length; index++)
        {
            var mention = mentions[index];
            spans[index] = new MentionSpan(mention.Handle, mention.UserId, mention.DisplayName);
        }

        return spans;
    }

    private static bool Matches(Entry entry, string text, MentionDto[]? mentions, float wrapWidth)
    {
        if (!ReferenceEquals(entry.Source, mentions) && !SameMentions(entry.Source, mentions))
        {
            return false;
        }

        if (!ReferenceEquals(entry.Text, text) && !string.Equals(entry.Text, text, StringComparison.Ordinal))
        {
            return false;
        }

        if (entry.Layout is null)
        {
            return true;
        }

        return entry.Layout.WrapWidth == wrapWidth
            && entry.Layout.FontSize == ImGui.GetFontSize()
            && entry.Layout.FontGeneration == Plugin.Fonts.Generation;
    }

    private static bool SameMentions(MentionDto[]? cached, MentionDto[]? current)
    {
        var cachedLength = cached?.Length ?? 0;
        var currentLength = current?.Length ?? 0;
        if (cachedLength != currentLength)
        {
            return false;
        }

        for (var index = 0; index < cachedLength; index++)
        {
            if (!string.Equals(cached![index].Handle, current![index].Handle, StringComparison.Ordinal)
                || !string.Equals(cached[index].UserId, current[index].UserId, StringComparison.Ordinal)
                || !string.Equals(cached[index].DisplayName, current[index].DisplayName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
