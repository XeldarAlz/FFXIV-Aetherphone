using System.Collections.Concurrent;

namespace Aetherphone.Harness.Rendering;

internal sealed class TextureStore
{
    private readonly ConcurrentDictionary<nint, CpuTexture> textures = new();
    private long nextHandle;

    public nint Register(CpuTexture texture)
    {
        var handle = (nint)Interlocked.Increment(ref nextHandle);
        textures[handle] = texture;
        return handle;
    }

    public void Replace(nint handle, CpuTexture texture) => textures[handle] = texture;

    public bool Remove(nint handle) => textures.TryRemove(handle, out _);

    public bool TryGet(nint handle, out CpuTexture texture) => textures.TryGetValue(handle, out texture!);
}
