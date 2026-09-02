namespace Aetherphone.Harness.Rendering;

internal sealed class TextureStore
{
    private readonly Dictionary<nint, CpuTexture> textures = new();
    private nint nextHandle = 1;

    public nint Register(CpuTexture texture)
    {
        var handle = nextHandle;
        nextHandle += 1;
        textures[handle] = texture;
        return handle;
    }

    public void Replace(nint handle, CpuTexture texture) => textures[handle] = texture;

    public bool Remove(nint handle) => textures.Remove(handle);

    public bool TryGet(nint handle, out CpuTexture texture) => textures.TryGetValue(handle, out texture!);
}
