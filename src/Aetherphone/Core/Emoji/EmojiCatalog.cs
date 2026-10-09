using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
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

internal readonly struct EmojiShortcode
{
    public readonly string Code;
    public readonly string File;

    public EmojiShortcode(string code, string file)
    {
        Code = code;
        File = file;
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
    public const int BareSequenceFloor = 0x2190;
    public const char PresentationSelector = '\uFE0F';
    public const char KeycapMark = '\u20E3';

    private const string PresentationSegment = "-fe0f";
    private const int TriggerCapacity = 2304;

    private static readonly EmojiTone[] NoTones = Array.Empty<EmojiTone>();
    private static readonly Dictionary<string, string> ShortcodeToFile = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> SequenceToFile = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> ShortcodeSpans =
        ShortcodeToFile.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> SequenceSpans =
        SequenceToFile.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly SearchValues<char> NoTriggers = SearchValues.Create(ReadOnlySpan<char>.Empty);

    private static readonly Comparison<EmojiShortcode> ShortcodeOrder = CompareShortcodes;

    private static string[] groups = Array.Empty<string>();
    private static EmojiGlyph[] glyphs = Array.Empty<EmojiGlyph>();
    private static EmojiShortcode[] shortcodes = Array.Empty<EmojiShortcode>();
    private static int[] groupStart = Array.Empty<int>();
    private static SearchValues<char> sequenceTriggers = NoTriggers;
    private static bool loaded;

    public static bool Ready => loaded && glyphs.Length > 0;

    public static string[] Groups => groups;

    public static ReadOnlySpan<EmojiGlyph> Glyphs => glyphs;

    public static ReadOnlySpan<EmojiShortcode> Shortcodes => shortcodes;

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
            LoadJson(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, "Failed to load emoji catalog");
        }
    }

    public static void LoadJson(string json)
    {
        loaded = true;
        ShortcodeToFile.Clear();
        SequenceToFile.Clear();
        sequenceTriggers = NoTriggers;
        groups = Array.Empty<string>();
        glyphs = Array.Empty<EmojiGlyph>();
        shortcodes = Array.Empty<EmojiShortcode>();
        groupStart = Array.Empty<int>();
        Parse(JObject.Parse(json));
    }

    public static bool TryResolve(string shortcode, out string file) =>
        ShortcodeToFile.TryGetValue(shortcode, out file!);

    public static bool TryResolve(ReadOnlySpan<char> shortcode, out string file) =>
        ShortcodeSpans.TryGetValue(shortcode, out file!);

    public static bool TryResolveSequence(ReadOnlySpan<char> key, out string file) =>
        SequenceSpans.TryGetValue(key, out file!);

    public static bool MightHaveSequence(ReadOnlySpan<char> text) => text.ContainsAny(sequenceTriggers);

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
        var codes = new List<EmojiShortcode>(emojiArray.Count * 2);
        glyphs = new EmojiGlyph[emojiArray.Count];
        for (var index = 0; index < emojiArray.Count; index++)
        {
            glyphs[index] = ParseGlyph((JObject)emojiArray[index], codes);
        }

        shortcodes = codes.ToArray();
        Array.Sort(shortcodes, ShortcodeOrder);
        BuildGroupIndex();
        BuildSequenceIndex();
    }

    private static void BuildSequenceIndex()
    {
        var triggers = new List<char>(TriggerCapacity);
        for (var surrogate = '\uD800'; surrogate <= '\uDFFF'; surrogate++)
        {
            triggers.Add(surrogate);
        }

        triggers.Add(PresentationSelector);
        triggers.Add(KeycapMark);
        for (var glyphIndex = 0; glyphIndex < glyphs.Length; glyphIndex++)
        {
            var glyph = glyphs[glyphIndex];
            IndexSequence(glyph.File, triggers);
            var tones = glyph.Tones;
            for (var toneIndex = 0; toneIndex < tones.Length; toneIndex++)
            {
                IndexSequence(tones[toneIndex].File, triggers);
            }
        }

        sequenceTriggers = SearchValues.Create(CollectionsMarshal.AsSpan(triggers));
    }

    private static void IndexSequence(string file, List<char> triggers)
    {
        SequenceToFile[file.Replace(PresentationSegment, string.Empty, StringComparison.Ordinal)] = file;
        var separator = file.IndexOf('-');
        var lead = int.Parse(separator < 0 ? file.AsSpan() : file.AsSpan(0, separator), NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture);
        if (lead is >= BareSequenceFloor and <= char.MaxValue)
        {
            triggers.Add((char)lead);
        }
    }

    private static int CompareShortcodes(EmojiShortcode left, EmojiShortcode right)
    {
        if (left.Code.Length != right.Code.Length)
        {
            return left.Code.Length - right.Code.Length;
        }

        return string.CompareOrdinal(left.Code, right.Code);
    }

    private static EmojiGlyph ParseGlyph(JObject node, List<EmojiShortcode> codes)
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
                codes.Add(new EmojiShortcode(code, file));
                search = string.Concat(search, " ", code);
            }
        }
        else
        {
            ShortcodeToFile[file] = file;
            codes.Add(new EmojiShortcode(file, file));
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
