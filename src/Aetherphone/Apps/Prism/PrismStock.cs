using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismStock
{
    public const int Count = 13;

    private static readonly string[] Paths = BuildPaths();

    public static bool Draw(ImDrawListPtr drawList, Rect rect, int index, float alpha)
    {
        var texture = Plugin.TextureProvider.GetFromFile(Paths[(index % Count + Count) % Count]).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return false;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        drawList.AddImage(texture.Handle, rect.Min, rect.Max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)));
        return true;
    }

    private static string[] BuildPaths()
    {
        var folder = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Images",
            "Prism");
        var paths = new string[Count];
        for (var index = 0; index < Count; index++)
        {
            paths[index] = Path.Combine(folder, $"glam-{index + 1:D2}.jpg");
        }

        return paths;
    }
}
