using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Emoji;

internal readonly struct EmojiTone
{
    public readonly string File;
    public readonly string Char;

    public EmojiTone(string file, string character)
    {
        File = file;
        Char = character;
    }
}

internal readonly struct EmojiGlyph
{
    public readonly string File;
    public readonly string Char;
    public readonly string Label;
    public readonly string Search;
    public readonly byte Group;
    public readonly EmojiTone[] Tones;

    public EmojiGlyph(string file, string character, string label, string search, byte group, EmojiTone[] tones)
    {
        File = file;
        Char = character;
        Label = label;
        Search = search;
        Group = group;
        Tones = tones;
    }

    public bool HasTones => Tones.Length > 0;
}

internal static class EmojiCatalog
{
    private static readonly EmojiTone[] NoTones = Array.Empty<EmojiTone>();
    private static readonly Dictionary<string, string> Lookup = new(StringComparer.Ordinal);
    private static readonly bool[] Starter = new bool[char.MaxValue + 1];

    private static string[] groups = Array.Empty<string>();
    private static EmojiGlyph[] glyphs = Array.Empty<EmojiGlyph>();
    private static int[] groupStart = Array.Empty<int>();
    private static int maxUnits;
    private static bool loaded;

    public static bool Ready => loaded && glyphs.Length > 0;

    public static string[] Groups => groups;

    public static ReadOnlySpan<EmojiGlyph> Glyphs => glyphs;

    public static int MaxMatchUnits => maxUnits;

    public static void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        var path = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Emoji",
            "catalog.json");
        if (!File.Exists(path))
        {
            AepLog.Error($"Emoji catalog missing at '{path}'.");
            return;
        }

        try
        {
            Parse(JObject.Parse(File.ReadAllText(path)));
        }
        catch (Exception exception)
        {
            AepLog.Error($"Failed to load emoji catalog: {exception.Message}");
        }
    }

    public static bool IsStarter(char value) => Starter[value];

    public static bool TryMatch(ReadOnlySpan<char> text, int start, out int length, out string file)
    {
        length = 0;
        file = string.Empty;
        var first = text[start];
        if (first < 0x0080 || !Starter[first])
        {
            return false;
        }

        var lookup = Lookup.GetAlternateLookup<ReadOnlySpan<char>>();
        var longest = Math.Min(maxUnits, text.Length - start);
        for (var span = longest; span >= 1; span--)
        {
            if (lookup.TryGetValue(text.Slice(start, span), out var candidate))
            {
                length = span;
                file = candidate;
                return true;
            }
        }

        return false;
    }

    public static ReadOnlySpan<EmojiGlyph> GlyphsInGroup(int group)
    {
        if (group < 0 || group >= groups.Length)
        {
            return ReadOnlySpan<EmojiGlyph>.Empty;
        }

        var from = groupStart[group];
        var to = groupStart[group + 1];
        return glyphs.AsSpan(from, to - from);
    }

    public static void GroupRange(int group, out int start, out int end)
    {
        if (group < 0 || group >= groups.Length)
        {
            start = 0;
            end = 0;
            return;
        }

        start = groupStart[group];
        end = groupStart[group + 1];
    }

    private static void Parse(JObject root)
    {
        var groupArray = (JArray?)root["groups"] ?? new JArray();
        groups = new string[groupArray.Count];
        for (var index = 0; index < groupArray.Count; index++)
        {
            groups[index] = groupArray[index].ToString();
        }

        var emojiArray = (JArray?)root["emoji"] ?? new JArray();
        glyphs = new EmojiGlyph[emojiArray.Count];
        for (var index = 0; index < emojiArray.Count; index++)
        {
            glyphs[index] = ParseGlyph((JObject)emojiArray[index]);
        }

        BuildGroupIndex();
    }

    private static EmojiGlyph ParseGlyph(JObject node)
    {
        var file = node["file"]!.ToString();
        var character = node["char"]?.ToString() ?? string.Empty;
        var label = node["label"]?.ToString() ?? string.Empty;
        var tags = node["tags"]?.ToString() ?? string.Empty;
        var group = (byte)(node["group"]?.Value<int>() ?? 0);
        Register(node["match"] as JArray, file);

        var tonesNode = node["tones"] as JArray;
        var tones = NoTones;
        if (tonesNode is { Count: > 0 })
        {
            tones = new EmojiTone[tonesNode.Count];
            for (var index = 0; index < tonesNode.Count; index++)
            {
                var tone = (JObject)tonesNode[index];
                var toneFile = tone["file"]!.ToString();
                tones[index] = new EmojiTone(toneFile, tone["char"]?.ToString() ?? string.Empty);
                Register(tone["match"] as JArray, toneFile);
            }
        }

        var search = string.IsNullOrEmpty(tags) ? label : string.Concat(label, " ", tags);
        return new EmojiGlyph(file, character, label, search.ToLowerInvariant(), group, tones);
    }

    private static void Register(JArray? matches, string file)
    {
        if (matches is null)
        {
            return;
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var key = matches[index].ToString();
            if (key.Length == 0)
            {
                continue;
            }

            Lookup[key] = file;
            if (key.Length > maxUnits)
            {
                maxUnits = key.Length;
            }

            if (key[0] >= 0x0080)
            {
                Starter[key[0]] = true;
            }
        }
    }

    private static void BuildGroupIndex()
    {
        groupStart = new int[groups.Length + 1];
        var cursor = 0;
        for (var group = 0; group < groups.Length; group++)
        {
            groupStart[group] = cursor;
            while (cursor < glyphs.Length && glyphs[cursor].Group == group)
            {
                cursor++;
            }
        }

        groupStart[groups.Length] = glyphs.Length;
    }
}
