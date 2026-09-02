using System.Collections.Concurrent;

namespace Aetherphone.Harness.Rendering;

internal sealed class TextureStore
{
    private readonly ConcurrentDictionary<nint, CpuTexture> textures = new();
    private readonly ConcurrentQueue<nint> removed = new();
    private long nextHandle;

    public nint Register(CpuTexture texture)
    {
        var handle = (nint)Interlocked.Increment(ref nextHandle);
        textures[handle] = texture;
        return handle;
    }

    public void Replace(nint handle, CpuTexture texture)
    {
        textures[handle] = texture;
        removed.Enqueue(handle);
    }

    public bool Remove(nint handle)
    {
        if (!textures.TryRemove(handle, out _))
        {
            return false;
        }

        removed.Enqueue(handle);
        return true;
    }

    public bool TryGet(nint handle, out CpuTexture texture) => textures.TryGetValue(handle, out texture!);

    public void DrainRemoved(List<nint> into)
    {
        while (removed.TryDequeue(out var handle))
        {
            into.Add(handle);
        }
    }
}
