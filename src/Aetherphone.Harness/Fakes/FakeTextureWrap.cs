using Aetherphone.Harness.Rendering;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeTextureWrap : IDalamudTextureWrap
{
    private readonly TextureStore store;
    private readonly nint handle;
    private readonly bool owned;

    public FakeTextureWrap(TextureStore store, nint handle, int width, int height, bool owned)
    {
        this.store = store;
        this.handle = handle;
        this.owned = owned;
        Width = width;
        Height = height;
    }

    public ImTextureID Handle => (ulong)handle;

    public int Width { get; }

    public int Height { get; }

    public Vector2 Size => new(Width, Height);

    public IDalamudTextureWrap CreateWrapSharingLowLevelResource() => new FakeTextureWrap(store, handle, Width, Height, false);

    public void Dispose()
    {
        if (owned)
        {
            store.Remove(handle);
        }
    }

    public static FakeTextureWrap Upload(TextureStore store, CpuTexture texture, bool owned) =>
        new(store, store.Register(texture), texture.Width, texture.Height, owned);
}
