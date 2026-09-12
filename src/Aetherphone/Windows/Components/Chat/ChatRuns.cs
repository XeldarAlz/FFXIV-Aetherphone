using Aetherphone.Core.Emoji;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class ChatRunSet
{
    public TextRun[] Runs = Array.Empty<TextRun>();
    public ChatChunk[] Targets = Array.Empty<ChatChunk>();
    public TextRun[] LogRuns = Array.Empty<TextRun>();
    public ChatChunk[] LogTargets = Array.Empty<ChatChunk>();
    public string NamePrefix = string.Empty;
    public string NameWorldPrefix = string.Empty;
    public string LogKey = string.Empty;
    public string LogWorldKey = string.Empty;
    public bool HasLinks;
    public bool HasEmoji;
    public int LastUsedFrame;
}

internal static class ChatRuns
{
    private const int SweepThreshold = 192;
    private const string NameSeparator = ": ";
    private const string WorldSeparator = "@";
    private const string LogKeySuffix = ":log";
    private const string LogWorldKeySuffix = ":logw";

    private static readonly Dictionary<string, ChatRunSet> Cache = new(StringComparer.Ordinal);
    private static int lastSweepFrame = -1;
    private static readonly List<TextRun> RunScratch = new(16);
    private static readonly List<ChatChunk> TargetScratch = new(8);
    private static readonly List<EmojiSpan> EmojiScratch = new(8);

    public static void Reset() => Cache.Clear();

    public static ChatRunSet For(ChatEntry entry) => For(entry, 0);

    public static ChatRunSet For(ChatEntry entry, int frame)
    {
        if (Cache.TryGetValue(entry.Id, out var cached))
        {
            cached.LastUsedFrame = frame;
            return cached;
        }

        if (Cache.Count > SweepThreshold)
        {
            SweepIdle(frame);
        }

        var set = Build(entry);
        set.LastUsedFrame = frame;
        Cache[entry.Id] = set;
        return set;
    }

    private static void SweepIdle(int frame)
    {
        if (lastSweepFrame == frame)
        {
            return;
        }

        lastSweepFrame = frame;
        foreach (var pair in Cache)
        {
            if (pair.Value.LastUsedFrame < frame - 1)
            {
                Cache.Remove(pair.Key);
            }
        }
    }

    public static Vector4 TintFor(ChatChunkKind kind) => kind switch
    {
        ChatChunkKind.Url => LinkTints.Url,
        ChatChunkKind.Item => LinkTints.Item,
        ChatChunkKind.Map => LinkTints.Map,
        ChatChunkKind.Player => LinkTints.Player,
        ChatChunkKind.Status => LinkTints.Status,
        ChatChunkKind.Quest => LinkTints.Quest,
        ChatChunkKind.PartyFinder => LinkTints.PartyFinder,
        _ => LinkTints.Plugin,
    };

    private static ChatRunSet Build(ChatEntry entry)
    {
        RunScratch.Clear();
        TargetScratch.Clear();
        var chunks = entry.Chunks;
        for (var index = 0; index < chunks.Length; index++)
        {
            var chunk = chunks[index];
            if (chunk.IsLink)
            {
                AddLink(chunk);
                continue;
            }

            SplitUrls(chunk.Text);
        }

        var set = new ChatRunSet
        {
            Runs = RunScratch.ToArray(),
            Targets = TargetScratch.ToArray(),
        };
        for (var index = 0; index < set.Runs.Length; index++)
        {
            set.HasLinks |= set.Runs[index].Interactive;
            set.HasEmoji |= set.Runs[index].IsEmoji;
        }

        BuildLogVariant(entry, set);
        return set;
    }

    private static void BuildLogVariant(ChatEntry entry, ChatRunSet set)
    {
        if (entry.AuthorName.Length == 0)
        {
            set.LogRuns = set.Runs;
            set.LogTargets = set.Targets;
            set.LogKey = entry.Id;
            set.LogWorldKey = entry.Id;
            return;
        }

        var author = NameMask.Display(entry.AuthorName);
        set.NamePrefix = string.Concat(author, NameSeparator);
        set.NameWorldPrefix = entry.AuthorWorld.Length > 0
            ? string.Concat(author, WorldSeparator, entry.AuthorWorld, NameSeparator)
            : set.NamePrefix;
        set.LogKey = string.Concat(entry.Id, LogKeySuffix);
        set.LogWorldKey = string.Concat(entry.Id, LogWorldKeySuffix);
        var runs = new TextRun[set.Runs.Length + 1];
        runs[0] = TextRun.Name(set.NamePrefix, LinkTints.Player, 0);
        for (var index = 0; index < set.Runs.Length; index++)
        {
            var run = set.Runs[index];
            runs[index + 1] = run.Interactive
                ? new TextRun(run.Text, run.Tint, run.Target + 1, true, run.Underlined, run.EmojiFile)
                : run;
        }

        var targets = new ChatChunk[set.Targets.Length + 1];
        targets[0] = ChatChunk.Player(entry.AuthorName, entry.AuthorWorld);
        Array.Copy(set.Targets, 0, targets, 1, set.Targets.Length);
        set.LogRuns = runs;
        set.LogTargets = targets;
    }

    private static void AddLink(ChatChunk chunk)
    {
        RunScratch.Add(TextRun.Link(chunk.Text, TintFor(chunk.Kind), TargetScratch.Count));
        TargetScratch.Add(chunk);
    }

    private static void SplitUrls(string text)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            var start = FindUrl(text, cursor, out var length);
            if (start < 0)
            {
                AddPlain(text[cursor..]);
                return;
            }

            if (start > cursor)
            {
                AddPlain(text[cursor..start]);
            }

            AddLink(ChatChunk.Url(text.Substring(start, length)));
            cursor = start + length;
        }
    }

    private static void AddPlain(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (!EmojiShortcodes.MightContain(text))
        {
            RunScratch.Add(TextRun.Plain(text));
            return;
        }

        EmojiScratch.Clear();
        EmojiShortcodes.Collect(text, EmojiScratch);
        if (EmojiScratch.Count == 0)
        {
            RunScratch.Add(TextRun.Plain(text));
            return;
        }

        var cursor = 0;
        for (var index = 0; index < EmojiScratch.Count; index++)
        {
            var span = EmojiScratch[index];
            if (span.Start > cursor)
            {
                RunScratch.Add(TextRun.Plain(text[cursor..span.Start]));
            }

            RunScratch.Add(TextRun.Emoji(span.File));
            cursor = span.Start + span.Length;
        }

        if (cursor < text.Length)
        {
            RunScratch.Add(TextRun.Plain(text[cursor..]));
        }
    }

    private static int FindUrl(string text, int from, out int length)
    {
        length = 0;
        for (var index = from; index < text.Length; index++)
        {
            if (text[index] != 'h' && text[index] != 'w' && text[index] != 'H' && text[index] != 'W')
            {
                continue;
            }

            if (index > 0 && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (!Starts(text, index, "http://") && !Starts(text, index, "https://") && !Starts(text, index, "www."))
            {
                continue;
            }

            var end = index;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            while (end > index && IsTrailingPunctuation(text[end - 1]))
            {
                end--;
            }

            length = end - index;
            return length > 0 ? index : -1;
        }

        return -1;
    }

    private static bool Starts(string text, int at, string prefix)
    {
        if (at + prefix.Length > text.Length)
        {
            return false;
        }

        return text.AsSpan(at, prefix.Length).Equals(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrailingPunctuation(char value) =>
        value is '.' or ',' or ')' or ']' or '}' or '!' or '?' or ':' or ';' or '"' or '\'';
}
