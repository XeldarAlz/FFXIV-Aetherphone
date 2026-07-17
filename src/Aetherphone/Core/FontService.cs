using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
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

internal readonly struct FontToken : IDisposable
{
    private readonly FontService owner;
    private readonly IDisposable inner;

    internal FontToken(FontService owner, IDisposable inner)
    {
        this.owner = owner;
        this.inner = inner;
    }

    public void Dispose()
    {
        inner.Dispose();
        owner.PopBucket();
    }
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
    private const int LedgerCapPerBucket = 2500;
    private const long LedgerRebuildDebounceMs = 600;
    private const int PushStackCapacity = 64;
    private readonly Configuration configuration;
    private readonly LoadingScreen loading;
    private readonly IFontAtlas atlas;
    private readonly string fontDirectory;
    private readonly float baseSize;
    private readonly int bucketCount;
    private readonly int defaultBucket;
    private readonly HashSet<ushort>[] ledger;
    private readonly ushort[][] bucketRanges;
    private readonly int[] pushedBuckets = new int[PushStackCapacity];
    private readonly ulong[] baseCoverage = new ulong[(char.MaxValue + 1) / 64];
    private ushort[] glyphRanges;
    private IFontHandle[,] handles;
    private float zoom;
    private float renderScale;
    private int pushDepth;
    private long ledgerDirtySince;
    private volatile bool ledgerRebuildInFlight;
    private int generation;

    public FontService(IDalamudPluginInterface pluginInterface, Configuration configuration, LoadingScreen loading,
        float zoom)
    {
        this.configuration = configuration;
        this.loading = loading;
        atlas = pluginInterface.UiBuilder.FontAtlas;
        fontDirectory = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Fonts");
        baseSize = UiBuilder.DefaultFontSizePx;
        this.zoom = zoom;
        renderScale = zoom / MaxZoom;
        bucketCount = WeightFiles.Length * SizeMultipliers.Length;
        defaultBucket = BucketIndex(FontWeight.Regular, NearestSize(1f));
        ledger = new HashSet<ushort>[bucketCount];
        bucketRanges = new ushort[bucketCount][];
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            ledger[bucket] = new HashSet<ushort>();
        }

        glyphRanges = ComposeRanges(Loc.Current);
        RebuildBaseCoverage();
        SeedLedgerFromConfig();
        SnapshotBucketRanges();
        handles = Build();
    }

    public float Zoom => zoom;

    public int Generation => Volatile.Read(ref generation);

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

        loading.Show();
        var previous = handles;
        glyphRanges = next;
        RebuildBaseCoverage();
        SnapshotBucketRanges();
        using (atlas.SuppressAutoRebuild())
        {
            handles = Build();
            DisposeHandles(previous);
        }

        Interlocked.Increment(ref generation);
    }

    public FontToken Push(float scale) => Push(scale, FontWeight.Regular);

    public FontToken Push(float scale, FontWeight weight)
    {
        MaybeRebuildLedger();
        var sizeIndex = NearestSize(scale);
        if (pushDepth < PushStackCapacity)
        {
            pushedBuckets[pushDepth] = BucketIndex(weight, sizeIndex);
        }

        pushDepth++;
        return new FontToken(this, handles[(int)weight, sizeIndex].Push());
    }

    internal void PopBucket()
    {
        if (pushDepth > 0)
        {
            pushDepth--;
        }
    }

    // Emulates OS-level font fallback on ImGui's static atlas: any character drawn through the phone
    // that no baked range covers is remembered per weight/size bucket, baked into the atlas on the
    // next debounced rebuild, and persisted so it renders instantly in later sessions.
    public void NoticeText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var top = pushDepth > 0 ? Math.Min(pushDepth, PushStackCapacity) - 1 : -1;
        var bucket = top >= 0 ? pushedBuckets[top] : defaultBucket;
        var set = ledger[bucket];
        var added = false;
        for (var index = 0; index < text.Length; index++)
        {
            var codepoint = text[index];
            if (codepoint < 0x0080)
            {
                continue;
            }

            if (char.IsSurrogate(codepoint))
            {
                continue;
            }

            if (codepoint is >= (char)0xE000 and <= (char)0xF8FF)
            {
                continue;
            }

            if (IsBaseCovered(codepoint))
            {
                continue;
            }

            if (set.Count >= LedgerCapPerBucket)
            {
                continue;
            }

            if (set.Add(codepoint))
            {
                added = true;
            }
        }

        if (added)
        {
            ledgerDirtySince = Environment.TickCount64;
        }
    }

    private void MaybeRebuildLedger()
    {
        if (ledgerDirtySince == 0 || ledgerRebuildInFlight)
        {
            return;
        }

        if (Environment.TickCount64 - ledgerDirtySince < LedgerRebuildDebounceMs)
        {
            return;
        }

        ledgerDirtySince = 0;
        ledgerRebuildInFlight = true;
        SnapshotBucketRanges();
        PersistLedger();
        _ = atlas.BuildFontsAsync().ContinueWith(_ =>
        {
            ledgerRebuildInFlight = false;
            Interlocked.Increment(ref generation);
        }, TaskScheduler.Default);
    }

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
                    built[weightIndex, sizeIndex] = BuildHandle(path, weightIndex, sizeIndex);
                }
            }
        }

        return built;
    }

    private IFontHandle BuildHandle(string path, int weightIndex, int sizeIndex)
    {
        var pixels = baseSize * SizeMultipliers[sizeIndex] * MaxZoom;
        var tracking = SizeMultipliers[sizeIndex] >= TrackingThreshold ? pixels * TrackingRatio : 0f;
        var bucket = weightIndex * SizeMultipliers.Length + sizeIndex;
        var primary = default(ImFontPtr);
        return atlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                var ranges = bucketRanges[bucket] ?? glyphRanges;
                primary = tk.AddFontFromFile(path,
                    new SafeFontConfig
                    {
                        SizePx = pixels, GlyphRanges = ranges, GlyphExtraSpacing = new Vector2(tracking, 0f),
                    });
                tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular,
                    new SafeFontConfig { SizePx = pixels, GlyphRanges = ranges, MergeFont = primary, });
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

    private static int BucketIndex(FontWeight weight, int sizeIndex) =>
        (int)weight * SizeMultipliers.Length + sizeIndex;

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

    private bool IsBaseCovered(int codepoint) =>
        (baseCoverage[codepoint >> 6] & (1UL << (codepoint & 63))) != 0;

    private void RebuildBaseCoverage()
    {
        Array.Clear(baseCoverage, 0, baseCoverage.Length);
        for (var index = 0; index + 1 < glyphRanges.Length; index += 2)
        {
            var first = glyphRanges[index];
            if (first == 0)
            {
                break;
            }

            var last = glyphRanges[index + 1];
            for (int codepoint = first; codepoint <= last; codepoint++)
            {
                baseCoverage[codepoint >> 6] |= 1UL << (codepoint & 63);
            }
        }
    }

    private void SeedLedgerFromConfig()
    {
        var stored = configuration.FontGlyphLedger;
        if (stored == null)
        {
            return;
        }

        var limit = Math.Min(stored.Count, bucketCount);
        for (var bucket = 0; bucket < limit; bucket++)
        {
            var chars = stored[bucket];
            if (string.IsNullOrEmpty(chars))
            {
                continue;
            }

            var set = ledger[bucket];
            for (var index = 0; index < chars.Length && set.Count < LedgerCapPerBucket; index++)
            {
                var codepoint = chars[index];
                if (char.IsSurrogate(codepoint) || codepoint < 0x0080)
                {
                    continue;
                }

                set.Add(codepoint);
            }
        }
    }

    private void PersistLedger()
    {
        var stored = new List<string>(bucketCount);
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var set = ledger[bucket];
            if (set.Count == 0)
            {
                stored.Add(string.Empty);
                continue;
            }

            var sorted = new ushort[set.Count];
            set.CopyTo(sorted);
            Array.Sort(sorted);
            var chars = new char[sorted.Length];
            for (var index = 0; index < sorted.Length; index++)
            {
                chars[index] = (char)sorted[index];
            }

            stored.Add(new string(chars));
        }

        configuration.FontGlyphLedger = stored;
        configuration.Save();
    }

    private void SnapshotBucketRanges()
    {
        for (var bucket = 0; bucket < bucketCount; bucket++)
        {
            var set = ledger[bucket];
            if (set.Count == 0)
            {
                bucketRanges[bucket] = null!;
                continue;
            }

            bucketRanges[bucket] = ComposeBucketRanges(set);
        }
    }

    private ushort[] ComposeBucketRanges(HashSet<ushort> set)
    {
        var sorted = new ushort[set.Count];
        set.CopyTo(sorted);
        Array.Sort(sorted);
        var runCount = 1;
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] != sorted[index - 1] + 1)
            {
                runCount++;
            }
        }

        var baseLength = glyphRanges.Length - 1;
        var combined = new ushort[baseLength + runCount * 2 + 1];
        Array.Copy(glyphRanges, 0, combined, 0, baseLength);
        var offset = baseLength;
        var runStart = sorted[0];
        var runEnd = sorted[0];
        for (var index = 1; index < sorted.Length; index++)
        {
            if (sorted[index] == runEnd + 1)
            {
                runEnd = sorted[index];
                continue;
            }

            combined[offset++] = runStart;
            combined[offset++] = runEnd;
            runStart = sorted[index];
            runEnd = sorted[index];
        }

        combined[offset++] = runStart;
        combined[offset] = runEnd;
        return combined;
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
