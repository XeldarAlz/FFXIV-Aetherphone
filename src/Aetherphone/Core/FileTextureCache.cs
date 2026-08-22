using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core;

internal sealed class FileTextureCache
{
    private readonly string directory;
    private readonly string extension;
    private readonly Dictionary<string, string?> resolvedPaths = new(StringComparer.Ordinal);

    public FileTextureCache(string subdirectory, string extension)
    {
        directory = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, subdirectory);
        this.extension = extension;
    }

    public IDalamudTextureWrap? Resolve(string id)
    {
        var path = ResolvePath(id);
        if (path is null)
        {
            return null;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        return texture is null || texture.Handle == nint.Zero ? null : texture;
    }

    private string? ResolvePath(string id)
    {
        if (resolvedPaths.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var candidate = Path.Combine(directory, id + extension);
        if (!File.Exists(candidate))
        {
            resolvedPaths[id] = null;
            return null;
        }

        Plugin.TextureSubstitution.InvalidatePaths(new[] { candidate });
        resolvedPaths[id] = candidate;
        return candidate;
    }
}
