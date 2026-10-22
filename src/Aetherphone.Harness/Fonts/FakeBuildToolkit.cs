using System.Globalization;
using System.Runtime.InteropServices;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Harness.Fonts;

internal sealed unsafe class FakeBuildToolkit : IFontAtlasBuildToolkitPreBuild, IFontAtlasBuildToolkitPostBuild
{
    private const float DefaultFontSizePx = 16.5f;
    private readonly FakeFontAtlas owner;
    private readonly ImFontAtlasPtr atlas;
    private readonly DalamudAssetFiles assets;
    private readonly List<Action> postBuild = new();
    private readonly List<Action> afterBuild = new();
    private readonly Dictionary<nint, FontScaleMode> scaleModes = new();

    public FakeBuildToolkit(FakeFontAtlas owner, ImFontAtlasPtr atlas, DalamudAssetFiles assets)
    {
        this.owner = owner;
        this.atlas = atlas;
        this.assets = assets;
    }

    public ImFontPtr Font { get; set; }

    public float Scale => 1f;

    public bool IsAsyncBuildOperation => false;

    public FontAtlasBuildStep BuildStep { get; internal set; }

    public ImFontAtlasPtr NewImAtlas => atlas;

    public ImVectorWrapper<ImFontPtr> Fonts => throw new NotSupportedException();

    public IReadOnlyList<Action> PostBuildActions => postBuild;

    public IReadOnlyList<Action> AfterBuildActions => afterBuild;

    public T DisposeWithAtlas<T>(T disposable)
        where T : IDisposable
    {
        owner.DisposeWithAtlas(disposable);
        return disposable;
    }

    public GCHandle DisposeWithAtlas(GCHandle gcHandle)
    {
        owner.DisposeWithAtlas(gcHandle);
        return gcHandle;
    }

    public void DisposeWithAtlas(Action action) => owner.DisposeWithAtlas(action);

    public ImFontPtr GetFont(IFontHandle fontHandle) => ((FakeFontHandle)fontHandle).Font;

    public T DisposeAfterBuild<T>(T disposable)
        where T : IDisposable
    {
        afterBuild.Add(disposable.Dispose);
        return disposable;
    }

    public GCHandle DisposeAfterBuild(GCHandle gcHandle)
    {
        afterBuild.Add(gcHandle.Free);
        return gcHandle;
    }

    public void DisposeAfterBuild(Action action) => afterBuild.Add(action);

    public ImFontPtr SetFontScaleMode(ImFontPtr fontPtr, FontScaleMode mode)
    {
        scaleModes[(nint)fontPtr.Handle] = mode;
        return fontPtr;
    }

    public FontScaleMode GetFontScaleMode(ImFontPtr fontPtr) =>
        scaleModes.TryGetValue((nint)fontPtr.Handle, out var mode) ? mode : FontScaleMode.Default;

    public void RegisterPostBuild(Action action) => postBuild.Add(action);

    public ImFontPtr AddFontFromImGuiHeapAllocatedMemory(nint dataPointer, int dataSize, in SafeFontConfig fontConfig,
        bool freeOnException, string debugTag) => throw new NotSupportedException();

    public ImFontPtr AddFontFromImGuiHeapAllocatedMemory(void* dataPointer, int dataSize, in SafeFontConfig fontConfig,
        bool freeOnException, string debugTag) => throw new NotSupportedException();

    public ImFontPtr AddFontFromFile(string path, in SafeFontConfig fontConfig) => AddFont(path, in fontConfig);

    public ImFontPtr AddFontFromStream(Stream stream, in SafeFontConfig fontConfig, bool leaveOpen, string debugTag)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        if (!leaveOpen)
        {
            stream.Dispose();
        }

        return AddFontFromMemory(buffer.ToArray(), in fontConfig, debugTag);
    }

    public ImFontPtr AddFontFromMemory(ReadOnlySpan<byte> span, in SafeFontConfig fontConfig, string debugTag)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aetherphone-harness-font-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, span.ToArray());
        afterBuild.Add(() => File.Delete(path));
        return AddFont(path, in fontConfig);
    }

    public ImFontPtr AddDalamudDefaultFont(float sizePx, ushort[]? glyphRanges = null) =>
        AddDalamudAssetFont(DalamudAsset.NotoSansCjkMedium, new SafeFontConfig { SizePx = sizePx, GlyphRanges = glyphRanges });

    public ImFontPtr AddDalamudAssetFont(DalamudAsset asset, in SafeFontConfig fontConfig) =>
        AddFont(assets.Resolve(asset), in fontConfig);

    public ImFontPtr AddFontAwesomeIconFont(in SafeFontConfig fontConfig) =>
        AddDalamudAssetFont(DalamudAsset.FontAwesomeFreeSolid, in fontConfig);

    public ImFontPtr AddGameSymbol(in SafeFontConfig fontConfig) =>
        AddDalamudAssetFont(DalamudAsset.NotoSansCjkMedium, in fontConfig);

    public ImFontPtr AddGameGlyphs(GameFontStyle gameFontStyle, ushort[]? glyphRanges, ImFontPtr mergeFont)
    {
        var config = new SafeFontConfig
        {
            SizePx = gameFontStyle.SizePx > 0 ? gameFontStyle.SizePx : DefaultFontSizePx,
            GlyphRanges = glyphRanges,
            MergeFont = mergeFont,
        };
        return AddDalamudAssetFont(DalamudAsset.NotoSansCjkMedium, in config);
    }

    public void AttachWindowsDefaultFont(CultureInfo cultureInfo, in SafeFontConfig fontConfig, int weight = 400,
        int stretch = 5, int style = 0)
    {
    }

    public void AttachExtraGlyphsForDalamudLanguage(in SafeFontConfig fontConfig)
    {
    }

    public int StoreTexture(IDalamudTextureWrap textureWrap, bool disposeOnError) => throw new NotSupportedException();

    public void FitRatio(ImFontPtr font, bool rebuildLookupTable = true)
    {
    }

    public void CopyGlyphsAcrossFonts(ImFontPtr source, ImFontPtr target, bool missingOnly, bool rebuildLookupTable = true,
        char rangeLow = ' ', char rangeHigh = '￾') =>
        ImGuiHelpers.CopyGlyphsAcrossFonts(source, target, missingOnly, rebuildLookupTable, rangeLow, rangeHigh);

    public void BuildLookupTable(ImFontPtr font) => font.BuildLookupTable();

    private ImFontPtr AddFont(string path, in SafeFontConfig fontConfig)
    {
        var raw = fontConfig.Raw;
        var ranges = fontConfig.GlyphRanges;
        ushort* rangesPointer = null;
        if (ranges is { Length: > 0 })
        {
            rangesPointer = (ushort*)owner.Pin(ranges);
        }

        var mergeFont = fontConfig.MergeFont;
        raw.MergeMode = !mergeFont.IsNull;
        raw.DstFont = mergeFont;
        raw.GlyphRanges = rangesPointer;
        raw.SizePixels = fontConfig.SizePx;
        var font = atlas.AddFontFromFileTTF(path, fontConfig.SizePx, raw, rangesPointer);
        var result = mergeFont.IsNull ? font : mergeFont;
        if (Font.IsNull)
        {
            Font = result;
        }

        return result;
    }
}
