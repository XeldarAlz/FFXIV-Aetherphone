using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class AppIconTextures
{
    private const float GlyphFraction = 0.62f;

    private static readonly string IconDirectory =
        Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Icons");

    private static readonly Dictionary<string, string?> ResolvedPaths = new();

    public static bool TryDraw(ImDrawListPtr drawList, string id, Vector2 center, float size, Vector4 tint)
    {
        var path = ResolvePath(id);
        if (path is null)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return false;
        }

        var half = size * GlyphFraction * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        drawList.AddImage(texture.Handle, min, max, Vector2.Zero, Vector2.One, ImGui.GetColorU32(tint));
        return true;
    }

    private static string? ResolvePath(string id)
    {
        if (ResolvedPaths.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var candidate = Path.Combine(IconDirectory, id + ".png");
        if (!File.Exists(candidate))
        {
            return null;
        }

        Plugin.TextureSubstitution.InvalidatePaths(new[] { candidate });
        ResolvedPaths[id] = candidate;
        return candidate;
    }
}
