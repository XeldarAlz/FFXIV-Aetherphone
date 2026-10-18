using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.GameFonts;
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
    private readonly IDisposable inner;

    internal FontToken(IDisposable inner)
    {
        this.inner = inner;
    }

    public void Dispose() => inner.Dispose();
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

    private static readonly float[] IconSizeMultipliers =
    {
        0.60f, 1.00f, 1.60f, 2.60f, 4.20f, 6.00f,
    };

    private static readonly float[] TitleMultipliers = { 2.50f, 3.00f };
    private static readonly float[] NumeralMultipliers = { 3.00f, 4.00f, 7.50f, 12.50f };
    private static readonly ushort[] TitleRanges = { 0x0020, 0x017E, 0x0400, 0x045F, 0x2018, 0x2026, 0x0000 };
    private static readonly ushort[] NumeralRanges = { 0x002B, 0x0039, 0x0000 };
    private const float NumeralTracking = -0.05f;

    private static readonly Dalamud.DalamudAsset[] SharedAssets =
    {
        Dalamud.DalamudAsset.NotoSansCjkRegular, Dalamud.DalamudAsset.NotoSansCjkMedium,
    };

    private static readonly ushort[] PlaceholderRanges = { 0x0020, 0x0020, 0x0000 };

    private const float TrackingThreshold = 1.20f;
    private const float TrackingRatio = -0.02f;
    private const float MaxZoom = 1.5f;
    private const int LearnedGlyphCap = 2000;
    private const int LearnedIconCap = 512;
    private const string TablerIconFile = "TablerIcons.ttf";

    private static readonly ushort[] TablerRanges =
    {
        IconPlan.FirstTablerCodepoint, IconPlan.LastTablerCodepoint, 0x0000,
    };

    private const string DisplayFontFile = "PirataOne-Regular.ttf";
    private const float DisplayMultiplier = 1.90f;
    private static readonly ushort[] DisplayRanges = { 0x0020, 0x017E, 0x0000 };
    private const long LearnRebuildDebounceMs = 600;
    private readonly Configuration configuration;
    private readonly LoadingScreen loading;
    private readonly IFontAtlas atlas;
    private readonly string fontDirectory;
    private readonly float baseSize;
    private readonly float sharedSize;
    private readonly HashSet<ushort> learned = new();
    private readonly HashSet<ushort> learnedIcons = new();
    private readonly GlyphCoverage nativeCoverage = new();
    private readonly GlyphCoverage sharedCoverage = new();
    private readonly GlyphCoverage iconCoverage = new();
    private readonly ImFontPtr[,] textFonts = new ImFontPtr[WeightFiles.Length, SizeMultipliers.Length];
    private readonly IFontHandle dalamudIconHandle;
    private IFontHandle? displayHandle;
    private ushort[] nativeRanges;
    private ushort[] sharedRanges;
    private ushort[] fontAwesomeRanges;
    private IFontHandle[,] textHandles;
    private IFontHandle[] sharedHandles;
    private IFontHandle[] iconHandles;
    private IFontHandle[] titleHandles = Array.Empty<IFontHandle>();
    private IFontHandle[] numeralHandles = Array.Empty<IFontHandle>();
    private float zoom;
    private float phoneZoom;
    private float renderScale;
    private long learnDirtySince;
    private volatile bool learnRebuildInFlight;
    private int lastRebuildCheckFrame = -1;
    private int lastReadyCheckFrame = -1;
    private bool buildLanded;
    private int generation;

    public FontService(IDalamudPluginInterface pluginInterface, Configuration configuration, LoadingScreen loading,
        float zoom, float phoneZoom)
    {
        this.configuration = configuration;
        this.loading = loading;
        atlas = pluginInterface.UiBuilder.FontAtlas;
        dalamudIconHandle = pluginInterface.UiBuilder.IconFontHandle;
        fontDirectory = Path.Combine(pluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Fonts");
        baseSize = UiBuilder.DefaultFontSizePx;
        sharedSize = baseSize * SizeMultipliers[SizeMultipliers.Length - 1] * MaxZoom;
        this.zoom = zoom;
        this.phoneZoom = phoneZoom;
        renderScale = zoom * phoneZoom / MaxZoom;
        nativeRanges = PlaceholderRanges;
        sharedRanges = PlaceholderRanges;
        fontAwesomeRanges = PlaceholderRanges;
        textHandles = null!;
        sharedHandles = null!;
        iconHandles = null!;
        ApplyNativeRanges(GlyphPlan.Native(Loc.Current));
        SeedLearned();
        SeedLearnedIcons();
        ComposeSharedRanges();
        ComposeIconRanges();
        atlas.BuildStepChange += MeasureCrests;
        Build();
    }

    public float Zoom => zoom;

    public bool DisplayReady => displayHandle is { Available: true };

    public FontToken PushDisplay() => new(displayHandle is null ? textHandles[0, 0].Push() : displayHandle.Push());

    public int Generation => Volatile.Read(ref generation);

    public bool Ready
    {
        get
        {
            for (var weightIndex = 0; weightIndex < textHandles.GetLength(0); weightIndex++)
            {
                for (var sizeIndex = 0; sizeIndex < textHandles.GetLength(1); sizeIndex++)
                {
                    if (!textHandles[weightIndex, sizeIndex].Available)
                    {
                        return false;
                    }
                }
            }

            for (var sourceIndex = 0; sourceIndex < sharedHandles.Length; sourceIndex++)
            {
                if (!sharedHandles[sourceIndex].Available)
                {
                    return false;
                }
            }

            for (var sizeIndex = 0; sizeIndex < iconHandles.Length; sizeIndex++)
            {
                if (!iconHandles[sizeIndex].Available)
                {
                    return false;
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
        ApplyZoom();
        Interlocked.Increment(ref generation);
    }

    public void SetPhoneZoom(float value)
    {
        if (MathF.Abs(value - phoneZoom) < 0.001f)
        {
            return;
        }

        phoneZoom = value;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        renderScale = zoom * phoneZoom / MaxZoom;
        ApplyRenderScale();
    }

    public void OnLanguageChanged()
    {
        var nextNative = GlyphPlan.Native(Loc.Current);
        var nativeChanged = !RangesEqual(nextNative, nativeRanges);
        if (nativeChanged)
        {
            ApplyNativeRanges(nextNative);
        }

        var previousShared = sharedRanges;
        ComposeSharedRanges();
        if (!nativeChanged && RangesEqual(sharedRanges, previousShared))
        {
            return;
        }

        loading.Show();
        var previousText = textHandles;
        var previousSharedHandles = sharedHandles;
        var previousIconHandles = iconHandles;
        using (atlas.SuppressAutoRebuild())
        {
            Build();
            DisposeHandles(previousText, previousSharedHandles, previousIconHandles);
        }

        Interlocked.Increment(ref generation);
    }

    public FontToken Push(float scale) => Push(scale, FontWeight.Regular);

    public FontToken Push(float scale, FontWeight weight)
    {
        NoticeBuildLanded();
        MaybeRebuildLearned();
        return new FontToken(textHandles[(int)weight, NearestSize(scale)].Push());
    }

    public float NearestScale(float scale) => SizeMultipliers[NearestSize(scale)];

    public static float SmallestTextScale => SizeMultipliers[0];

    public static float TextScaleAtMost(float scale) => AtMost(SizeMultipliers, scale);

    public static float TitleScaleAtMost(float scale) => MathF.Max(AtMost(SizeMultipliers, scale),
        AtMost(TitleMultipliers, scale));

    public static float NumeralScaleAtMost(float scale) => MathF.Max(AtMost(SizeMultipliers, scale),
        AtMost(NumeralMultipliers, scale));

    public FontToken PushTitle(float scale, ReadOnlySpan<char> text) =>
        PushLarge(titleHandles, TitleMultipliers, TitleRanges, FontWeight.Bold, scale, text);

    public FontToken PushNumerals(float scale, ReadOnlySpan<char> text) =>
        PushLarge(numeralHandles, NumeralMultipliers, NumeralRanges, FontWeight.Regular, scale, text);

    private FontToken PushLarge(IFontHandle[] handles, float[] multipliers, ushort[] ranges, FontWeight weight,
        float scale, ReadOnlySpan<char> text)
    {
        var index = Array.IndexOf(multipliers, scale);
        if (index >= 0 && index < handles.Length && handles[index].Available && Covers(ranges, text))
        {
            return new FontToken(handles[index].Push());
        }

        return Push(MathF.Min(scale, SizeMultipliers[^1]), weight);
    }

    private static float AtMost(float[] tiers, float scale)
    {
        var best = 0f;
        for (var index = 0; index < tiers.Length; index++)
        {
            if (tiers[index] <= scale + 0.001f && tiers[index] > best)
            {
                best = tiers[index];
            }
        }

        return best;
    }

    private static bool Covers(ushort[] ranges, ReadOnlySpan<char> text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            var covered = false;
            for (var range = 0; range + 1 < ranges.Length; range += 2)
            {
                if (text[index] >= ranges[range] && text[index] <= ranges[range + 1])
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                return false;
            }
        }

        return true;
    }

    public FontToken PushIcon(float pixelHeight, string glyph)
    {
        NoticeBuildLanded();
        MaybeRebuildLearned();
        if (glyph.Length > 0)
        {
            NoticeIcon(glyph[0]);
            var handle = iconHandles[NearestIconSize(pixelHeight)];
            if (handle.Available)
            {
                var pushed = handle.Push();
                if (HasGlyph(ImGui.GetFont(), glyph[0]))
                {
                    return new FontToken(pushed);
                }

                pushed.Dispose();
            }
        }

        return new FontToken(dalamudIconHandle.Push());
    }

    private static void MeasureCrests(IFontAtlasBuildToolkit toolkit)
    {
        if (toolkit.BuildStep == FontAtlasBuildStep.PostBuild)
        {
            GlyphCrests.Measure(toolkit);
        }
    }

    private static unsafe bool HasGlyph(ImFontPtr font, char codepoint)
    {
        ImFontGlyphPtr found = font.FindGlyphNoFallback(codepoint);
        return !found.IsNull;
    }

    private static int NearestIconSize(float pixelHeight)
    {
        for (var index = 0; index < IconSizeMultipliers.Length - 1; index++)
        {
            if (UiBuilder.DefaultFontSizePx * IconSizeMultipliers[index] >= pixelHeight)
            {
                return index;
            }
        }

        return IconSizeMultipliers.Length - 1;
    }

    private void NoticeIcon(char codepoint)
    {
        if (codepoint < IconPlan.FirstIconCodepoint || codepoint > IconPlan.LastIconCodepoint)
        {
            return;
        }

        if (iconCoverage.Contains(codepoint))
        {
            return;
        }

        if (learnedIcons.Count >= LearnedIconCap)
        {
            return;
        }

        if (learnedIcons.Add(codepoint))
        {
            learnDirtySince = Environment.TickCount64;
        }
    }

    public void NoticeText(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return;
        }

        var added = false;
        for (var index = 0; index < text.Length; index++)
        {
            var codepoint = text[index];
            if (codepoint < GlyphPlan.FirstSharedCodepoint)
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

            if (nativeCoverage.Contains(codepoint) || sharedCoverage.Contains(codepoint))
            {
                continue;
            }

            if (learned.Count >= LearnedGlyphCap)
            {
                break;
            }

            if (learned.Add(codepoint))
            {
                added = true;
            }
        }

        if (added)
        {
            learnDirtySince = Environment.TickCount64;
        }
    }

    private void NoticeBuildLanded()
    {
        if (buildLanded)
        {
            return;
        }

        var frame = ImGui.GetFrameCount();
        if (frame == lastReadyCheckFrame)
        {
            return;
        }

        lastReadyCheckFrame = frame;
        if (!Ready)
        {
            return;
        }

        buildLanded = true;
        Interlocked.Increment(ref generation);
    }

    private void MaybeRebuildLearned()
    {
        if (learnDirtySince == 0 || learnRebuildInFlight)
        {
            return;
        }

        var frame = ImGui.GetFrameCount();
        if (frame == lastRebuildCheckFrame)
        {
            return;
        }

        lastRebuildCheckFrame = frame;
        if (Environment.TickCount64 - learnDirtySince < LearnRebuildDebounceMs)
        {
            return;
        }

        learnDirtySince = 0;
        learnRebuildInFlight = true;
        ComposeSharedRanges();
        ComposeIconRanges();
        PersistLearned();
        _ = atlas.BuildFontsAsync().ContinueWith(_ =>
        {
            learnRebuildInFlight = false;
            Interlocked.Increment(ref generation);
        }, TaskScheduler.Default);
    }

    private void Build()
    {
        using (atlas.SuppressAutoRebuild())
        {
            var text = new IFontHandle[WeightFiles.Length, SizeMultipliers.Length];
            for (var weightIndex = 0; weightIndex < WeightFiles.Length; weightIndex++)
            {
                var path = Path.Combine(fontDirectory, WeightFiles[weightIndex]);
                for (var sizeIndex = 0; sizeIndex < SizeMultipliers.Length; sizeIndex++)
                {
                    text[weightIndex, sizeIndex] = BuildTextHandle(path, weightIndex, sizeIndex);
                }
            }

            var shared = new IFontHandle[SharedAssets.Length];
            for (var sourceIndex = 0; sourceIndex < SharedAssets.Length; sourceIndex++)
            {
                shared[sourceIndex] = BuildSharedHandle(sourceIndex);
            }

            var icons = new IFontHandle[IconSizeMultipliers.Length];
            for (var sizeIndex = 0; sizeIndex < IconSizeMultipliers.Length; sizeIndex++)
            {
                icons[sizeIndex] = BuildIconHandle(sizeIndex);
            }

            textHandles = text;
            sharedHandles = shared;
            iconHandles = icons;
            if (titleHandles.Length == 0)
            {
                titleHandles = BuildLargeHandles(WeightFiles[(int)FontWeight.Bold], TitleMultipliers, TitleRanges,
                    TrackingRatio);
                numeralHandles = BuildLargeHandles(WeightFiles[(int)FontWeight.Regular], NumeralMultipliers,
                    NumeralRanges, NumeralTracking);
            }

            displayHandle ??= SeasonalTheme.ResolveHalloween(configuration, DateTime.Now) ? BuildDisplayHandle() : null;
            buildLanded = false;
        }
    }

    private IFontHandle BuildIconHandle(int sizeIndex)
    {
        var pixels = UiBuilder.DefaultFontSizePx * IconSizeMultipliers[sizeIndex];
        var tablerPath = Path.Combine(fontDirectory, TablerIconFile);
        return atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var primary = tk.AddDalamudAssetFont(Dalamud.DalamudAsset.FontAwesomeFreeSolid,
                new SafeFontConfig { SizePx = pixels, GlyphRanges = fontAwesomeRanges, });
            if (!File.Exists(tablerPath))
            {
                return;
            }

            try
            {
                tk.AddFontFromFile(tablerPath,
                    new SafeFontConfig { SizePx = pixels, GlyphRanges = TablerRanges, MergeFont = primary, });
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, $"[Fonts] skipped merging '{TablerIconFile}' at {pixels}px.");
            }
        }));
    }

    public void SyncDisplay(bool wanted)
    {
        if (wanted == displayHandle is not null)
        {
            return;
        }

        if (wanted)
        {
            displayHandle = BuildDisplayHandle();
            return;
        }

        displayHandle?.Dispose();
        displayHandle = null;
    }

    private IFontHandle BuildDisplayHandle()
    {
        var pixels = baseSize * DisplayMultiplier * MaxZoom;
        var path = Path.Combine(fontDirectory, DisplayFontFile);
        return atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var config = new SafeFontConfig { SizePx = pixels, GlyphRanges = DisplayRanges, };
            if (!File.Exists(path))
            {
                tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular, config);
                return;
            }

            tk.AddFontFromFile(path, config);
        }));
    }

    private IFontHandle[] BuildLargeHandles(string file, float[] multipliers, ushort[] ranges, float tracking)
    {
        var path = Path.Combine(fontDirectory, file);
        var handles = new IFontHandle[multipliers.Length];
        for (var index = 0; index < multipliers.Length; index++)
        {
            var pixels = baseSize * multipliers[index] * MaxZoom;
            var font = default(ImFontPtr);
            handles[index] = atlas.NewDelegateFontHandle(e =>
            {
                e.OnPreBuild(tk =>
                {
                    var config = new SafeFontConfig
                    {
                        SizePx = pixels, GlyphRanges = ranges, GlyphExtraSpacing = new Vector2(pixels * tracking, 0f),
                    };
                    font = File.Exists(path)
                        ? tk.AddFontFromFile(path, config)
                        : tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular, config);
                });
                e.OnPostBuild(_ => font.Scale = renderScale);
            });
        }

        return handles;
    }

    private IFontHandle BuildTextHandle(string path, int weightIndex, int sizeIndex)
    {
        var pixels = baseSize * SizeMultipliers[sizeIndex] * MaxZoom;
        var tracking = SizeMultipliers[sizeIndex] >= TrackingThreshold ? pixels * TrackingRatio : 0f;
        var primary = default(ImFontPtr);
        return atlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                var config = new SafeFontConfig
                {
                    SizePx = pixels, GlyphRanges = nativeRanges, GlyphExtraSpacing = new Vector2(tracking, 0f),
                };
                if (!File.Exists(path))
                {
                    primary = tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular, config);
                    textFonts[weightIndex, sizeIndex] = primary;
                    return;
                }

                primary = tk.AddFontFromFile(path, config);
                tk.AddDalamudAssetFont(Dalamud.DalamudAsset.NotoSansCjkRegular,
                    new SafeFontConfig { SizePx = pixels, GlyphRanges = nativeRanges, MergeFont = primary, });
                textFonts[weightIndex, sizeIndex] = primary;
            });
            e.OnPostBuild(_ => primary.Scale = renderScale);
        });
    }

    private IFontHandle BuildSharedHandle(int sourceIndex)
    {
        var asset = SharedAssets[sourceIndex];
        var source = default(ImFontPtr);
        IFontAtlasBuildToolkitPostBuild? postBuild = null;
        return atlas.NewDelegateFontHandle(e =>
        {
            e.OnPreBuild(tk =>
            {
                postBuild = null;
                source = tk.AddDalamudAssetFont(asset,
                    new SafeFontConfig { SizePx = sharedSize, GlyphRanges = sharedRanges, });
                MergeGameSymbols(tk, source);

                // Dalamud pours the merged game glyphs into the shared font from its own substance, which runs
                // after every handle's post build callback, so the spread has to wait in the later queue.
                tk.RegisterPostBuild(() => SpreadSharedGlyphs(postBuild, source, sourceIndex));
            });
            e.OnPostBuild(tk => postBuild = tk);
        });
    }

    private void SpreadSharedGlyphs(IFontAtlasBuildToolkitPostBuild? toolkit, ImFontPtr source, int sourceIndex)
    {
        if (toolkit is null)
        {
            return;
        }

        for (var weightIndex = 0; weightIndex < WeightFiles.Length; weightIndex++)
        {
            if (SharedSourceFor(weightIndex) != sourceIndex)
            {
                continue;
            }

            for (var sizeIndex = 0; sizeIndex < SizeMultipliers.Length; sizeIndex++)
            {
                toolkit.CopyGlyphsAcrossFonts(source, textFonts[weightIndex, sizeIndex], true, true);
            }
        }
    }

    private void MergeGameSymbols(IFontAtlasBuildToolkitPreBuild toolkit, ImFontPtr primary)
    {
        try
        {
            toolkit.AddGameGlyphs(new GameFontStyle(GameFontFamily.Axis, sharedSize), GlyphPlan.GameSymbols, primary);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Fonts] skipped merging the game symbol glyphs.");
        }
    }

    private static int SharedSourceFor(int weightIndex) => weightIndex == 0 ? 0 : 1;

    private void ApplyRenderScale()
    {
        for (var weightIndex = 0; weightIndex < textHandles.GetLength(0); weightIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < textHandles.GetLength(1); sizeIndex++)
            {
                var handle = textHandles[weightIndex, sizeIndex];
                if (!handle.Available)
                {
                    continue;
                }

                using var locked = handle.Lock();
                locked.ImFont.Scale = renderScale;
            }
        }

        ScaleHandles(titleHandles);
        ScaleHandles(numeralHandles);
    }

    private void ScaleHandles(IFontHandle[] handles)
    {
        for (var index = 0; index < handles.Length; index++)
        {
            if (!handles[index].Available)
            {
                continue;
            }

            using var locked = handles[index].Lock();
            locked.ImFont.Scale = renderScale;
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

    private void ApplyNativeRanges(ushort[] ranges)
    {
        nativeRanges = ranges;
        nativeCoverage.Clear();
        nativeCoverage.AddRanges(ranges);
    }

    private void ComposeSharedRanges()
    {
        sharedCoverage.Clear();
        sharedCoverage.AddRanges(GlyphPlan.SharedBase, nativeCoverage);
        var catalogGlyphs = Loc.CatalogGlyphs;
        for (var index = 0; index < catalogGlyphs.Length; index++)
        {
            var codepoint = catalogGlyphs[index];
            if (nativeCoverage.Contains(codepoint))
            {
                continue;
            }

            sharedCoverage.Add(codepoint);
        }

        foreach (var codepoint in learned)
        {
            if (nativeCoverage.Contains(codepoint))
            {
                continue;
            }

            sharedCoverage.Add(codepoint);
        }

        sharedRanges = sharedCoverage.Count == 0
            ? PlaceholderRanges
            : sharedCoverage.ToRanges(GlyphPlan.FirstSharedCodepoint);
    }

    private void ComposeIconRanges()
    {
        iconCoverage.Clear();
        var fontAwesome = IconPlan.FontAwesome;
        for (var index = 0; index < fontAwesome.Length; index++)
        {
            iconCoverage.Add(fontAwesome[index]);
        }

        foreach (var codepoint in learnedIcons)
        {
            iconCoverage.Add(codepoint);
        }

        // FontAwesome 7 draws its own glyphs inside the Tabler block (U+E678 is a table lock), and the first font
        // in a merge wins, so FontAwesome is never asked for the block and Tabler's glyphs always land.
        fontAwesomeRanges = iconCoverage.ToRanges(IconPlan.FirstIconCodepoint);
        for (var codepoint = IconPlan.FirstTablerCodepoint; codepoint <= IconPlan.LastTablerCodepoint; codepoint++)
        {
            iconCoverage.Add(codepoint);
        }
    }

    private void SeedLearned()
    {
        var stored = configuration.FontGlyphCache;
        if (string.IsNullOrEmpty(stored))
        {
            return;
        }

        for (var index = 0; index < stored.Length && learned.Count < LearnedGlyphCap; index++)
        {
            var codepoint = stored[index];
            if (codepoint < GlyphPlan.FirstSharedCodepoint || char.IsSurrogate(codepoint))
            {
                continue;
            }

            if (GlyphPlan.IsSharedBase(codepoint))
            {
                continue;
            }

            learned.Add(codepoint);
        }
    }

    private void SeedLearnedIcons()
    {
        var stored = configuration.IconGlyphCache;
        for (var index = 0; index < stored.Length && learnedIcons.Count < LearnedIconCap; index++)
        {
            var codepoint = stored[index];
            if (codepoint < IconPlan.FirstIconCodepoint || codepoint > IconPlan.LastIconCodepoint)
            {
                continue;
            }

            if (IconPlan.IsDeclared(codepoint))
            {
                continue;
            }

            learnedIcons.Add(codepoint);
        }
    }

    private void PersistLearned()
    {
        configuration.FontGlyphCache = PackGlyphs(learned);
        configuration.IconGlyphCache = PackGlyphs(learnedIcons);
        configuration.Save();
    }

    private static string PackGlyphs(HashSet<ushort> glyphs)
    {
        if (glyphs.Count == 0)
        {
            return string.Empty;
        }

        var sorted = new ushort[glyphs.Count];
        glyphs.CopyTo(sorted);
        Array.Sort(sorted);
        var chars = new char[sorted.Length];
        for (var index = 0; index < sorted.Length; index++)
        {
            chars[index] = (char)sorted[index];
        }

        return new string(chars);
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

    public void Dispose()
    {
        atlas.BuildStepChange -= MeasureCrests;
        DisposeHandles(textHandles, sharedHandles, iconHandles);
        DisposeHandles(titleHandles);
        DisposeHandles(numeralHandles);
        displayHandle?.Dispose();
    }

    private static void DisposeHandles(IFontHandle[] handles)
    {
        for (var index = 0; index < handles.Length; index++)
        {
            handles[index].Dispose();
        }
    }

    private static void DisposeHandles(IFontHandle[,] text, IFontHandle[] shared, IFontHandle[] icons)
    {
        for (var weightIndex = 0; weightIndex < text.GetLength(0); weightIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < text.GetLength(1); sizeIndex++)
            {
                text[weightIndex, sizeIndex].Dispose();
            }
        }

        for (var sourceIndex = 0; sourceIndex < shared.Length; sourceIndex++)
        {
            shared[sourceIndex].Dispose();
        }

        for (var sizeIndex = 0; sizeIndex < icons.Length; sizeIndex++)
        {
            icons[sizeIndex].Dispose();
        }
    }
}
