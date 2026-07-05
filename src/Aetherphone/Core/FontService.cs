using System.Numerics;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
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
        "Inter-Regular.ttf", "Inter-Medium.ttf", "Inter-SemiBold.ttf", "Inter-Bold.ttf",
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
    private const float MaxZoom = 1.5f;
    private readonly IFontAtlas atlas;
    private readonly string fontDirectory;
    private readonly float baseSize;
    private ushort[] glyphRanges;
    private IFontHandle[,] handles;
    private float zoom;
    private float renderScale;

    public FontService(IDalamudPluginInterface pluginInterface, float zoom)
    {
        atlas = pluginInterface.UiBuilder.FontAtlas;
        fontDirectory = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Fonts");
        baseSize = UiBuilder.DefaultFontSizePx;
        this.zoom = zoom;
        renderScale = zoom / MaxZoom;
        glyphRanges = ComposeRanges(Loc.Current);
        handles = Build();
    }

    public float Zoom => zoom;

    public bool Ready
    {
        get
        {
            for (var weightIndex = 0; weightIndex < handles.GetLength(0); weightIndex++)
            {
                for (var sizeIndex = 0; sizeIndex < handles.GetLength(1); sizeIndex++)
                {
                    if (!handles[weightIndex, sizeIndex].Available)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public void SetZoom(float value)
    {
        if (MathF.Abs(value - zoom) < 0.001f)
        {
            return;
        }

        zoom = value;
        renderScale = zoom / MaxZoom;
        ApplyRenderScale();
    }

    public void OnLanguageChanged()
    {
        var next = ComposeRanges(Loc.Current);
        if (RangesEqual(next, glyphRanges))
        {
            return;
        }

        Plugin.Loading.Show();
        var previous = handles;
        glyphRanges = next;
        using (atlas.SuppressAutoRebuild())
        {
            handles = Build();
            DisposeHandles(previous);
        }
    }

    public IDisposable Push(float scale) => Push(scale, FontWeight.Regular);
    public IDisposable Push(float scale, FontWeight weight) => handles[(int)weight, NearestSize(scale)].Push();

    private IFontHandle[,] Build()
    {
        var built = new IFontHandle[WeightFiles.Length, SizeMultipliers.Length];
        using (atlas.SuppressAutoRebuild())
        {
            for (var weightIndex = 0; weightIndex < WeightFiles.Length; weightIndex++)
            {
                var path = Path.Combine(fontDirectory, WeightFiles[weightIndex]);
                for (var sizeIndex = 0; sizeIndex < SizeMultipliers.Length; sizeIndex++)
                {
                    built[weightIndex, sizeIndex] = BuildHandle(path, SizeMultipliers[sizeIndex]);
                }
            }
        }

        return built;
    }

    private IFontHandle BuildHandle(string path, float multiplier)
    {
        var pixels = baseSize * multiplier * MaxZoom;
        var tracking = multiplier >= TrackingThreshold ? pixels * TrackingRatio : 0f;
        var primary = default(ImFontPtr);
        return atlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                primary = tk.AddFontFromFile(path,
                    new SafeFontConfig
                    {
                        SizePx = pixels, GlyphRanges = glyphRanges, GlyphExtraSpacing = new Vector2(tracking, 0f),
                    });
                tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular,
                    new SafeFontConfig { SizePx = pixels, GlyphRanges = glyphRanges, MergeFont = primary, });
            });
            e.OnPostBuild(_ => primary.Scale = renderScale);
        });
    }

    private void ApplyRenderScale()
    {
        for (var weightIndex = 0; weightIndex < handles.GetLength(0); weightIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < handles.GetLength(1); sizeIndex++)
            {
                var handle = handles[weightIndex, sizeIndex];
                if (!handle.Available)
                {
                    continue;
                }

                using var locked = handle.Lock();
                locked.ImFont.Scale = renderScale;
            }
        }
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

    private static readonly ushort[] NativeNameGlyphRanges = ComposeNativeNameRanges();

    private static ushort[] ComposeRanges(LanguageInfo language)
    {
        var extra = language.ExtraGlyphRanges;
        var extraLength = extra?.Length ?? 0;
        var combined = new ushort[BaseGlyphRanges.Length + NativeNameGlyphRanges.Length + extraLength + 1];
        var offset = 0;
        Array.Copy(BaseGlyphRanges, 0, combined, offset, BaseGlyphRanges.Length);
        offset += BaseGlyphRanges.Length;
        Array.Copy(NativeNameGlyphRanges, 0, combined, offset, NativeNameGlyphRanges.Length);
        offset += NativeNameGlyphRanges.Length;
        if (extraLength > 0)
        {
            Array.Copy(extra!, 0, combined, offset, extraLength);
        }

        return combined;
    }

    private static ushort[] ComposeNativeNameRanges()
    {
        var seen = new bool[char.MaxValue + 1];
        var count = 0;
        for (var languageIndex = 0; languageIndex < Languages.All.Length; languageIndex++)
        {
            var name = Languages.All[languageIndex].NativeName;
            for (var charIndex = 0; charIndex < name.Length; charIndex++)
            {
                var codepoint = name[charIndex];
                if (seen[codepoint])
                {
                    continue;
                }

                seen[codepoint] = true;
                count++;
            }
        }

        var ranges = new ushort[count * 2];
        var offset = 0;
        for (var codepoint = 0; codepoint <= char.MaxValue; codepoint++)
        {
            if (!seen[codepoint])
            {
                continue;
            }

            ranges[offset++] = (ushort)codepoint;
            ranges[offset++] = (ushort)codepoint;
        }

        return ranges;
    }

    private static bool RangesEqual(ushort[] left, ushort[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
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
