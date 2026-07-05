using System.IO;
using System.Numerics;
using Aetherphone.Core.Localization;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace Aetherphone.Core;

internal enum FontWeight : byte
{
    Regular,
    Medium,
    SemiBold,
    Bold,
}

internal sealed class FontService : IDisposable
{
    private static readonly string[] WeightFiles =
    {
        "Inter-Regular.ttf",
        "Inter-Medium.ttf",
        "Inter-SemiBold.ttf",
        "Inter-Bold.ttf",
    };

    private static readonly float[] SizeMultipliers =
    {
        0.60f, 0.72f, 0.80f, 0.88f, 0.95f, 1.00f, 1.10f, 1.20f, 1.32f, 1.45f, 1.65f, 1.90f,
    };

    private static readonly ushort[] BaseGlyphRanges =
    {
        0x0020, 0x00FF, // Basic Latin and Latin-1 Supplement
        0x0100, 0x017F, // Latin Extended-A for European name accents and Turkish glyphs
        0x2000, 0x206F, // General Punctuation: ellipsis, em dash, curly quotes
        0x2200, 0x22FF, // Mathematical Operators for the market alert glyphs
        0x25A0, 0x27BF, // Geometric Shapes, Misc Symbols, Dingbats for game and gender glyphs
    };

    private const float TrackingThreshold = 1.20f;
    private const float TrackingRatio = -0.02f;

    private readonly IFontAtlas atlas;
    private readonly string fontDirectory;
    private readonly float baseSize;

    private readonly ushort[] glyphRanges;
    private IFontHandle[,] handles;
    private float zoom;

    public FontService(IDalamudPluginInterface pluginInterface, float zoom)
    {
        atlas = pluginInterface.UiBuilder.FontAtlas;
        fontDirectory = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Fonts");
        baseSize = UiBuilder.DefaultFontSizePx;
        this.zoom = zoom;
        glyphRanges = ComposeRanges();
        handles = Build(zoom);
    }

    public float Zoom => zoom;

    public void SetZoom(float value)
    {
        if (MathF.Abs(value - zoom) < 0.001f)
        {
            return;
        }

        var previous = handles;
        zoom = value;
        handles = Build(value);
        DisposeHandles(previous);
    }

    public IDisposable Push(float scale) => Push(scale, FontWeight.Regular);

    public IDisposable Push(float scale, FontWeight weight) => handles[(int)weight, NearestSize(scale)].Push();

    private IFontHandle[,] Build(float scale)
    {
        var built = new IFontHandle[WeightFiles.Length, SizeMultipliers.Length];
        for (var weightIndex = 0; weightIndex < WeightFiles.Length; weightIndex++)
        {
            var path = Path.Combine(fontDirectory, WeightFiles[weightIndex]);
            for (var sizeIndex = 0; sizeIndex < SizeMultipliers.Length; sizeIndex++)
            {
                built[weightIndex, sizeIndex] = BuildHandle(path, SizeMultipliers[sizeIndex], scale);
            }
        }

        return built;
    }

    private IFontHandle BuildHandle(string path, float multiplier, float scale)
    {
        var pixels = baseSize * multiplier * scale;
        var tracking = multiplier >= TrackingThreshold ? pixels * TrackingRatio : 0f;
        return atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var primary = tk.AddFontFromFile(path, new SafeFontConfig
            {
                SizePx = pixels,
                GlyphRanges = glyphRanges,
                GlyphExtraSpacing = new Vector2(tracking, 0f),
            });

            tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular, new SafeFontConfig
            {
                SizePx = pixels,
                GlyphRanges = glyphRanges,
                MergeFont = primary,
            });
        }));
    }

    private static int NearestSize(float scale)
    {
        var best = 0;
        var bestDelta = float.MaxValue;
        for (var index = 0; index < SizeMultipliers.Length; index++)
        {
            var delta = MathF.Abs(SizeMultipliers[index] - scale);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = index;
            }
        }

        return best;
    }

    private static ushort[] ComposeRanges()
    {
        var extraLength = 0;
        for (var index = 0; index < Languages.All.Length; index++)
        {
            var extra = Languages.All[index].ExtraGlyphRanges;
            if (extra is not null)
            {
                extraLength += extra.Length;
            }
        }

        var combined = new ushort[BaseGlyphRanges.Length + extraLength + 1];
        Array.Copy(BaseGlyphRanges, 0, combined, 0, BaseGlyphRanges.Length);

        var offset = BaseGlyphRanges.Length;
        for (var index = 0; index < Languages.All.Length; index++)
        {
            var extra = Languages.All[index].ExtraGlyphRanges;
            if (extra is null)
            {
                continue;
            }

            Array.Copy(extra, 0, combined, offset, extra.Length);
            offset += extra.Length;
        }

        return combined;
    }

    public void Dispose() => DisposeHandles(handles);

    private static void DisposeHandles(IFontHandle[,] target)
    {
        for (var weightIndex = 0; weightIndex < target.GetLength(0); weightIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < target.GetLength(1); sizeIndex++)
            {
                target[weightIndex, sizeIndex].Dispose();
            }
        }
    }
}
