using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class PhoneCaseTextures
{
    private const string SkinSuffix = ".png";
    private const string ThumbSuffix = ".thumb.png";

    private static readonly string CaseDirectory =
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Cases");

    private static readonly Dictionary<string, string?> ResolvedPaths = new();

    public static ImTextureID? Skin(string textureId) => Handle(textureId, SkinSuffix);

    public static ImTextureID? Thumb(string textureId) => Handle(textureId, ThumbSuffix) ?? Handle(textureId, SkinSuffix);

    private static ImTextureID? Handle(string textureId, string suffix)
    {
        if (textureId.Length == 0)
        {
            return null;
        }

        var path = ResolvePath(textureId + suffix);
        if (path is null)
        {
            return null;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return null;
        }

        return texture.Handle;
    }

    private static string? ResolvePath(string fileName)
    {
        if (ResolvedPaths.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        var candidate = Path.Combine(CaseDirectory, fileName);
        if (!File.Exists(candidate))
        {
            ResolvedPaths[fileName] = null;
            return null;
        }

        Plugin.TextureSubstitution.InvalidatePaths(new[] { candidate });
        ResolvedPaths[fileName] = candidate;
        return candidate;
    }
}
