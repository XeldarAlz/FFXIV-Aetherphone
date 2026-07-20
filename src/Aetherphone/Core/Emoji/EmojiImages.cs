using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Emoji;

internal static class EmojiImages
{
    private static readonly string AssetDir =
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Emoji");

    private static readonly Dictionary<string, string?> Paths = new(StringComparer.Ordinal);

    public static bool TryDraw(ImDrawListPtr drawList, string file, Vector2 min, Vector2 max, uint tint)
    {
        var path = Resolve(file);
        if (path is null)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return false;
        }

        drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, tint);
        return true;
    }

    private static string? Resolve(string file)
    {
        if (Paths.TryGetValue(file, out var cached))
        {
            return cached;
        }

        var candidate = Path.Combine(AssetDir, file + ".png");
        var resolved = File.Exists(candidate) ? candidate : null;
        Paths[file] = resolved;
        return resolved;
    }
}
