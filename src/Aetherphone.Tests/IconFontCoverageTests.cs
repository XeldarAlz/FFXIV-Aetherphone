using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.RegularExpressions;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IconFontCoverageTests
{
    [Fact]
    public void TablerIconsProvidesEveryGlyphPhoneIconsDeclares()
    {
        var covered = TablerCodepoints();
        var declared = DeclaredIconCodepoints();

        Assert.NotEmpty(declared);
        for (var index = 0; index < declared.Count; index++)
        {
            var declaration = declared[index];
            Assert.True(covered.Contains(declaration.Codepoint),
                $"PhoneIcons.{declaration.Name} is U+{declaration.Codepoint:X4} but TablerIcons.ttf has no glyph there.");
        }
    }

    [Fact]
    public void TheSeededIconRangeMatchesTheFontFile()
    {
        var covered = TablerCodepoints();
        var lowest = int.MaxValue;
        var highest = int.MinValue;
        foreach (var codepoint in covered)
        {
            lowest = Math.Min(lowest, codepoint);
            highest = Math.Max(highest, codepoint);
        }

        Assert.Equal(IconPlan.FirstTablerCodepoint, lowest);
        Assert.Equal(IconPlan.LastTablerCodepoint, highest);
    }

    [Fact]
    public void TheIconCatalogIsSortedAndDistinct()
    {
        var catalog = IconPlan.FontAwesome;

        Assert.NotEmpty(catalog.ToArray());
        for (var index = 1; index < catalog.Length; index++)
        {
            Assert.True(catalog[index] > catalog[index - 1],
                $"IconPlan.FontAwesome is binary searched, but U+{catalog[index]:X4} follows U+{catalog[index - 1]:X4}.");
        }
    }

    [Fact]
    public void TheIconCatalogDeclaresEveryFontAwesomeIconTheSourceDraws()
    {
        var used = FontAwesomeIconsUsedInSource();

        Assert.NotEmpty(used);
        foreach (var (name, codepoint) in used)
        {
            Assert.True(IconPlan.IsDeclared(codepoint),
                $"FontAwesomeIcon.{name} (U+{codepoint:X4}) is drawn but missing from IconPlan, so its first draw "
                + "rebuilds the whole font atlas. Add it to IconPlan.FontAwesomeCodepoints.");
        }
    }

    [Fact]
    public void AFreshInstallComposesAnIconRangeBothFontsCanSatisfy()
    {
        var service = ComposedIconService(new HashSet<ushort>());
        var tabler = Coverage((ushort[])StaticField("TablerRanges").GetValue(null)!);
        var fontAwesome = Coverage((ushort[])Field("fontAwesomeRanges").GetValue(service)!);

        var covered = TablerCodepoints();
        foreach (var codepoint in covered)
        {
            Assert.True(tabler.Contains(codepoint),
                $"A fresh install asks TablerIcons.ttf for a range that omits U+{codepoint:X4}, which the file supplies.");
        }

        var catalog = IconPlan.FontAwesome;
        for (var index = 0; index < catalog.Length; index++)
        {
            Assert.True(fontAwesome.Contains(catalog[index]),
                $"A fresh install asks FontAwesome for a range that omits U+{catalog[index]:X4}.");
        }
    }

    [Fact]
    public void FontAwesomeIsNeverAskedForTheTablerBlock()
    {
        var service = ComposedIconService(new HashSet<ushort>());
        var fontAwesome = Coverage((ushort[])Field("fontAwesomeRanges").GetValue(service)!);

        for (var codepoint = IconPlan.FirstTablerCodepoint; codepoint <= IconPlan.LastTablerCodepoint; codepoint++)
        {
            Assert.False(fontAwesome.Contains(codepoint),
                $"FontAwesome is asked for U+{codepoint:X4}; the first font in a merge wins, so a FontAwesome glyph "
                + "there would replace the Tabler icon.");
        }
    }

    [Fact]
    public void NoFontAwesomeIconTheSourceDrawsSitsInTheTablerBlock()
    {
        foreach (var (name, codepoint) in FontAwesomeIconsUsedInSource())
        {
            Assert.False(codepoint >= IconPlan.FirstTablerCodepoint && codepoint <= IconPlan.LastTablerCodepoint,
                $"FontAwesomeIcon.{name} (U+{codepoint:X4}) sits in the Tabler block, so it would draw a Tabler glyph.");
        }
    }

    [Fact]
    public void AFreshInstallDrawingEveryDeclaredIconNeverDirtiesTheLedger()
    {
        var learnedIcons = new HashSet<ushort>();
        var service = ComposedIconService(learnedIcons);

        var notice = typeof(FontService).GetMethod("NoticeIcon", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var used = FontAwesomeIconsUsedInSource();
        foreach (var (_, codepoint) in used)
        {
            notice.Invoke(service, new object[] { (char)codepoint });
        }

        var declared = DeclaredIconCodepoints();
        for (var index = 0; index < declared.Count; index++)
        {
            notice.Invoke(service, new object[] { (char)declared[index].Codepoint });
        }

        Assert.Empty(learnedIcons);
        Assert.Equal(0L, (long)Field("learnDirtySince").GetValue(service)!);
    }

    private static List<(string Name, int Codepoint)> FontAwesomeIconsUsedInSource()
    {
        var pattern = new Regex(@"FontAwesomeIcon\.(?<name>[A-Za-z0-9_]+)", RegexOptions.Compiled);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(PluginSourceDirectory(), "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                var name = match.Groups["name"].Value;
                if (seen.ContainsKey(name) || !Enum.TryParse<FontAwesomeIcon>(name, false, out var icon))
                {
                    continue;
                }

                seen.Add(name, (int)icon);
            }
        }

        var used = new List<(string Name, int Codepoint)>(seen.Count);
        foreach (var pair in seen)
        {
            used.Add((pair.Key, pair.Value));
        }

        return used;
    }

    private static string PluginSourceDirectory()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException("Could not locate src/Aetherphone from the test assembly.");
    }

    private static FontService ComposedIconService(HashSet<ushort> learnedIcons)
    {
        var service = (FontService)RuntimeHelpers.GetUninitializedObject(typeof(FontService));
        Field("iconCoverage").SetValue(service, new GlyphCoverage());
        Field("learnedIcons").SetValue(service, learnedIcons);

        typeof(FontService).GetMethod("ComposeIconRanges", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, null);
        return service;
    }

    private static GlyphCoverage Coverage(ushort[] ranges)
    {
        var coverage = new GlyphCoverage();
        coverage.AddRanges(ranges);
        return coverage;
    }

    private static FieldInfo Field(string name)
    {
        var field = typeof(FontService).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        return field!;
    }

    private static FieldInfo StaticField(string name)
    {
        var field = typeof(FontService).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return field!;
    }

    private static List<(string Name, int Codepoint)> DeclaredIconCodepoints()
    {
        var declared = new List<(string Name, int Codepoint)>();
        var fields = typeof(PhoneIcons).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (var index = 0; index < fields.Length; index++)
        {
            var field = fields[index];
            if (field.FieldType != typeof(string))
            {
                continue;
            }

            var glyph = (string)field.GetRawConstantValue()!;
            Assert.Equal(1, glyph.Length);
            declared.Add((field.Name, glyph[0]));
        }

        return declared;
    }

    private static HashSet<int> TablerCodepoints()
    {
        var font = File.ReadAllBytes(TablerIconPath());
        var cmap = TableOffset(font, "cmap");
        var subtableCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(cmap + 2));
        var codepoints = new HashSet<int>();
        for (var index = 0; index < subtableCount; index++)
        {
            var record = cmap + 4 + 8 * index;
            var subtable = cmap + (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 4));
            if (BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(subtable)) == 4)
            {
                ReadFormatFour(font, subtable, codepoints);
            }
        }

        Assert.NotEmpty(codepoints);
        return codepoints;
    }

    private static void ReadFormatFour(byte[] font, int subtable, HashSet<int> codepoints)
    {
        var segmentCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(subtable + 6)) / 2;
        var endCodes = subtable + 14;
        var startCodes = endCodes + segmentCount * 2 + 2;
        var deltas = startCodes + segmentCount * 2;
        var rangeOffsets = deltas + segmentCount * 2;
        for (var segment = 0; segment < segmentCount; segment++)
        {
            var start = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(startCodes + segment * 2));
            var end = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(endCodes + segment * 2));
            if (start > end || end == 0xFFFF)
            {
                continue;
            }

            var delta = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(deltas + segment * 2));
            var rangeOffsetAddress = rangeOffsets + segment * 2;
            var rangeOffset = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(rangeOffsetAddress));
            for (var codepoint = start; codepoint <= end; codepoint++)
            {
                if (rangeOffset == 0)
                {
                    if ((ushort)(codepoint + delta) != 0)
                    {
                        codepoints.Add(codepoint);
                    }

                    continue;
                }

                var glyphAddress = rangeOffsetAddress + rangeOffset + (codepoint - start) * 2;
                if (BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(glyphAddress)) != 0)
                {
                    codepoints.Add(codepoint);
                }
            }
        }
    }

    private static int TableOffset(byte[] font, string tag)
    {
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (var index = 0; index < tableCount; index++)
        {
            var record = 12 + 16 * index;
            if (font[record] == tag[0] && font[record + 1] == tag[1] && font[record + 2] == tag[2] &&
                font[record + 3] == tag[3])
            {
                return (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
            }
        }

        throw new InvalidDataException($"TablerIcons.ttf has no '{tag}' table.");
    }

    private static string TablerIconPath()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone", "Fonts", "TablerIcons.ttf");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new FileNotFoundException("Could not locate src/Aetherphone/Fonts/TablerIcons.ttf from the test assembly.");
    }
}
