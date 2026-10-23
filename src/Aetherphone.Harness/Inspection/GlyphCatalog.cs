using System.Reflection;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Harness.Inspection;

internal sealed unsafe class GlyphCatalog
{
    private const float FallbackSpaceEm = 0.25f;
    private const uint PrivateUseStart = 0xE000;
    private const uint PrivateUseEnd = 0xF8FF;
    private static readonly Dictionary<uint, string> IconNames = ReadIconNames();
    private readonly Dictionary<GlyphKey, GlyphEntry> byStart = new();
    private readonly Dictionary<GlyphKey, GlyphEntry> byEnd = new();

    public int Generation { get; private set; } = -1;

    public void Rebuild(ImFontAtlasPtr atlas, int generation)
    {
        byStart.Clear();
        byEnd.Clear();
        Generation = generation;
        var fonts = atlas.Fonts;
        var textures = atlas.Textures;
        for (var fontIndex = 0; fontIndex < fonts.Size; fontIndex++)
        {
            var font = fonts[fontIndex];
            ImFontGlyphPtr space = font.FindGlyphNoFallback(' ');
            var spaceAdvance = space.IsNull ? font.FontSize * FallbackSpaceEm : space.AdvanceX;
            var glyphs = font.Glyphs;
            for (var glyphIndex = 0; glyphIndex < glyphs.Size; glyphIndex++)
            {
                var glyph = glyphs.Data[glyphIndex];
                if (glyph.Visible == 0 || glyph.TextureIndex >= textures.Size)
                {
                    continue;
                }

                var texture = (nint)textures.Data[glyph.TextureIndex].TexID.Handle;
                var entry = new GlyphEntry(Describe(glyph.Codepoint), font.FontSize, glyph.X0, glyph.Y0, glyph.X1,
                    glyph.Y1, glyph.U0, glyph.V0, glyph.U1, glyph.V1, glyph.AdvanceX, spaceAdvance);
                byStart.TryAdd(new GlyphKey(texture, glyph.U0, glyph.V0), entry);
                byEnd.TryAdd(new GlyphKey(texture, glyph.U1, glyph.V1), entry);
            }
        }
    }

    public bool TryGet(nint texture, Vector2 start, Vector2 end, out GlyphEntry entry) =>
        byStart.TryGetValue(new GlyphKey(texture, start.X, start.Y), out entry) ||
        byEnd.TryGetValue(new GlyphKey(texture, end.X, end.Y), out entry);

    private static string Describe(uint codepoint)
    {
        if (IconNames.TryGetValue(codepoint, out var name))
        {
            return name;
        }

        if (codepoint is >= PrivateUseStart and <= PrivateUseEnd)
        {
            return $"[icon:U+{codepoint:X4}]";
        }

        return codepoint <= char.MaxValue ? ((char)codepoint).ToString() : char.ConvertFromUtf32((int)codepoint);
    }

    private static Dictionary<uint, string> ReadIconNames()
    {
        var names = new Dictionary<uint, string>();
        var fields = typeof(PhoneIcons).GetFields(BindingFlags.Public | BindingFlags.Static);
        for (var index = 0; index < fields.Length; index++)
        {
            if (fields[index].IsLiteral && fields[index].GetRawConstantValue() is string { Length: > 0 } glyph)
            {
                names.TryAdd((uint)char.ConvertToUtf32(glyph, 0), $"[icon:{fields[index].Name}]");
            }
        }

        var awesome = Enum.GetValues<FontAwesomeIcon>();
        for (var index = 0; index < awesome.Length; index++)
        {
            names.TryAdd((uint)awesome[index], $"[icon:{awesome[index]}]");
        }

        return names;
    }

    private readonly record struct GlyphKey(nint Texture, float U, float V);
}

internal readonly record struct GlyphEntry(string Text, float FontSize, float X0, float Y0, float X1, float Y1, float U0,
    float V0, float U1, float V1, float Advance, float SpaceAdvance);
