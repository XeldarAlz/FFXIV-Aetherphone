using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Emoji;

internal readonly struct EmojiTone
{
    public readonly string File;
    public readonly string Shortcode;

    public EmojiTone(string file, string shortcode)
    {
        File = file;
        Shortcode = shortcode;
    }
}

internal readonly struct EmojiGlyph
{
    public readonly string File;
    public readonly string Shortcode;
    public readonly string Label;
    public readonly string Search;
    public readonly byte Group;
    public readonly EmojiTone[] Tones;

    public EmojiGlyph(string file, string shortcode, string label, string search, byte group, EmojiTone[] tones)
    {
        File = file;
        Shortcode = shortcode;
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
    private static readonly Dictionary<string, string> ShortcodeToFile = new(StringComparer.OrdinalIgnoreCase);

    private static string[] groups = Array.Empty<string>();
    private static EmojiGlyph[] glyphs = Array.Empty<EmojiGlyph>();
    private static int[] groupStart = Array.Empty<int>();
    private static bool loaded;

    public static bool Ready => loaded && glyphs.Length > 0;

    public static string[] Groups => groups;

    public static ReadOnlySpan<EmojiGlyph> Glyphs => glyphs;

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

    public static bool TryResolve(string shortcode, out string file) =>
        ShortcodeToFile.TryGetValue(shortcode, out file!);

    public static ReadOnlySpan<EmojiGlyph> GlyphsInGroup(int group)
    {
        if (group < 0 || group >= groups.Length)
        {
            return ReadOnlySpan<EmojiGlyph>.Empty;
        }

        return glyphs.AsSpan(groupStart[group], groupStart[group + 1] - groupStart[group]);
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
        var label = node["label"]?.ToString() ?? string.Empty;
        var tags = node["tags"]?.ToString() ?? string.Empty;
        var group = (byte)(node["group"]?.Value<int>() ?? 0);
        var shortArray = node["short"] as JArray;
        var primary = file;
        var search = string.IsNullOrEmpty(tags) ? label : string.Concat(label, " ", tags);
        if (shortArray is { Count: > 0 })
        {
            primary = shortArray[0].ToString();
            for (var index = 0; index < shortArray.Count; index++)
            {
                var code = shortArray[index].ToString();
                if (code.Length == 0)
                {
                    continue;
                }

                ShortcodeToFile[code] = file;
                search = string.Concat(search, " ", code);
            }
        }
        else
        {
            ShortcodeToFile[file] = file;
        }

        var tonesNode = node["tones"] as JArray;
        var tones = NoTones;
        if (tonesNode is { Count: > 0 })
        {
            tones = new EmojiTone[tonesNode.Count];
            for (var index = 0; index < tonesNode.Count; index++)
            {
                var tone = (JObject)tonesNode[index];
                var toneFile = tone["file"]!.ToString();
                var toneNumber = tone["tone"]?.Value<int>() ?? index + 1;
                var toneCode = string.Concat(primary, "_tone", toneNumber.ToString());
                tones[index] = new EmojiTone(toneFile, toneCode);
                ShortcodeToFile[toneCode] = toneFile;
            }
        }

        return new EmojiGlyph(file, primary, label, search.ToLowerInvariant(), group, tones);
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
