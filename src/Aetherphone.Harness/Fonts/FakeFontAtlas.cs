using System.Runtime.InteropServices;
using Aetherphone.Harness.Fakes;
using Aetherphone.Harness.Rendering;
using Dalamud;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace Aetherphone.Harness.Fonts;

internal sealed unsafe class FakeFontAtlas : IFontAtlas
{
    private readonly TextureStore textures;
    private readonly DalamudAssetFiles assets;
    private readonly List<FakeFontHandle> handles = new();
    private readonly List<GCHandle> pinned = new();
    private readonly List<Action> disposeWithAtlas = new();
    private readonly List<nint> uploaded = new();
    private int suppressDepth;
    private bool dirty;
    private TaskCompletionSource? buildCompletion;

    public FakeFontAtlas(TextureStore textures, DalamudAssetFiles assets, string name)
    {
        this.textures = textures;
        this.assets = assets;
        Name = name;
    }

    public event FontAtlasBuildStepDelegate? BuildStepChange { add { } remove { } }

    public event Action? RebuildRecommend { add { } remove { } }

    public string Name { get; }

    public FontAtlasAutoRebuildMode AutoRebuildMode => FontAtlasAutoRebuildMode.OnNewFrame;

    public ImFontAtlasPtr ImAtlas => ImGui.GetIO().Fonts;

    public Task BuildTask => buildCompletion?.Task ?? Task.CompletedTask;

    public bool HasBuiltAtlas { get; private set; }

    public bool IsGlobalScaled => true;

    public int Generation { get; private set; }

    public IDisposable SuppressAutoRebuild()
    {
        suppressDepth += 1;
        return new SuppressionToken(this);
    }

    public IFontHandle NewGameFontHandle(GameFontStyle style)
    {
        var sizePx = style.SizePx;
        return NewDelegateFontHandle(toolkit => toolkit.OnPreBuild(preBuild =>
            preBuild.AddDalamudAssetFont(DalamudAsset.NotoSansCjkMedium, new SafeFontConfig { SizePx = sizePx })));
    }

    public IFontHandle NewDelegateFontHandle(FontAtlasBuildStepDelegate buildStepDelegate)
    {
        var handle = new FakeFontHandle(this, buildStepDelegate);
        handles.Add(handle);
        dirty = true;
        return handle;
    }

    public void BuildFontsOnNextFrame() => dirty = true;

    public void BuildFontsImmediately() => Rebuild();

    public Task BuildFontsAsync()
    {
        dirty = true;
        buildCompletion ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return buildCompletion.Task;
    }

    public void RebuildIfDirty()
    {
        if (!dirty || suppressDepth > 0)
        {
            return;
        }

        Rebuild();
    }

    public void Dispose()
    {
        ReleasePinned();
        RunDisposeWithAtlas();
    }

    internal void Remove(FakeFontHandle handle)
    {
        if (handles.Remove(handle))
        {
            dirty = true;
        }
    }

    internal nint Pin(ushort[] ranges)
    {
        var handle = GCHandle.Alloc(ranges, GCHandleType.Pinned);
        pinned.Add(handle);
        return handle.AddrOfPinnedObject();
    }

    internal void DisposeWithAtlas(IDisposable disposable) => disposeWithAtlas.Add(disposable.Dispose);

    internal void DisposeWithAtlas(GCHandle handle) => disposeWithAtlas.Add(handle.Free);

    internal void DisposeWithAtlas(Action action) => disposeWithAtlas.Add(action);

    private void Rebuild()
    {
        dirty = false;
        var atlas = ImGui.GetIO().Fonts;
        atlas.Clear();
        ReleasePinned();
        var toolkit = new FakeBuildToolkit(this, atlas, assets) { BuildStep = FontAtlasBuildStep.PreBuild };
        var snapshot = handles.ToArray();
        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index].RunPreBuild(toolkit);
        }

        if (atlas.Fonts.Size == 0)
        {
            atlas.AddFontDefault();
        }

        atlas.Build();
        toolkit.BuildStep = FontAtlasBuildStep.PostBuild;
        for (var index = 0; index < snapshot.Length; index++)
        {
            snapshot[index].RunPostBuild(toolkit);
        }

        var postBuild = toolkit.PostBuildActions;
        for (var index = 0; index < postBuild.Count; index++)
        {
            Invoke(postBuild[index], "post-build");
        }

        Upload(atlas);
        var afterBuild = toolkit.AfterBuildActions;
        for (var index = 0; index < afterBuild.Count; index++)
        {
            Invoke(afterBuild[index], "after-build");
        }

        HasBuiltAtlas = true;
        Generation += 1;
        var completion = buildCompletion;
        buildCompletion = null;
        completion?.TrySetResult();
        HarnessLog.Note($"font atlas built: {atlas.Fonts.Size} fonts, {atlas.Textures.Size} textures, generation {Generation}");
    }

    private void Upload(ImFontAtlasPtr atlas)
    {
        for (var index = 0; index < uploaded.Count; index++)
        {
            textures.Remove(uploaded[index]);
        }

        uploaded.Clear();
        var textureCount = atlas.Textures.Size;
        for (var textureIndex = 0; textureIndex < textureCount; textureIndex++)
        {
            byte* pixels;
            int width;
            int height;
            int bytesPerPixel;
            atlas.GetTexDataAsRGBA32(textureIndex, &pixels, &width, &height, &bytesPerPixel);
            var rgba = new byte[width * height * 4];
            new ReadOnlySpan<byte>(pixels, rgba.Length).CopyTo(rgba);
            var handle = textures.Register(new CpuTexture(width, height, rgba));
            uploaded.Add(handle);
            atlas.SetTexID(textureIndex, (ulong)handle);
        }

        atlas.ClearTexData();
    }

    private void ReleasePinned()
    {
        for (var index = 0; index < pinned.Count; index++)
        {
            pinned[index].Free();
        }

        pinned.Clear();
    }

    private void RunDisposeWithAtlas()
    {
        for (var index = 0; index < disposeWithAtlas.Count; index++)
        {
            Invoke(disposeWithAtlas[index], "dispose-with-atlas");
        }

        disposeWithAtlas.Clear();
    }

    private static void Invoke(Action action, string stage)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            HarnessLog.Note($"font atlas {stage} action failed: {exception.Message}");
        }
    }

    private sealed class SuppressionToken : IDisposable
    {
        private FakeFontAtlas? owner;

        public SuppressionToken(FakeFontAtlas owner)
        {
            this.owner = owner;
        }

        public void Dispose()
        {
            if (owner is null)
            {
                return;
            }

            owner.suppressDepth -= 1;
            owner = null;
        }
    }
}
