using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum PrismPicture : byte
{
    None,
    TrackModded,
    TrackVanilla,
    GuideJudging,
    GuidePodiumFirst,
    GuidePodiumSecond,
    GuidePodiumThird,
    GuideTryOn,
    GuideLibraryFirst,
    GuideLibrarySecond,
    GuideLibraryThird,
    GuideLibraryFourth,
    GuideLibraryFifth,
    GuideLibrarySixth,
    GuideWardrobe,
    GuideExploreFirst,
    GuideExploreSecond,
    GuideExploreThird,
}

internal static class PrismStock
{
    private static readonly string[] Files =
    {
        string.Empty, "track-modded.png", "track-vanilla.png", "guide-judging.png", "guide-podium-first.png",
        "guide-podium-second.png", "guide-podium-third.png", "guide-try-on.png", "guide-library-1.jpg",
        "guide-library-2.jpg", "guide-library-3.jpg", "guide-library-4.png", "guide-library-5.png",
        "guide-library-6.png", "guide-wardrobe.png", "guide-explore-first.png", "guide-explore-second.png",
        "guide-explore-third.png",
    };
    private static readonly string[] Paths = BuildPaths();

    public static bool DrawRounded(ImDrawListPtr drawList, Rect rect, PrismPicture picture, float alpha,
        float rounding)
    {
        var path = Paths[(int)picture];
        if (path.Length == 0)
        {
            return false;
        }

        var texture = Plugin.TextureProvider.GetFromFile(path).GetWrapOrDefault();
        if (texture is null || texture.Handle == nint.Zero)
        {
            return false;
        }

        var (uv0, uv1) = ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        drawList.AddImageRounded(texture.Handle, rect.Min, rect.Max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding);
        return true;
    }

    private static string[] BuildPaths()
    {
        var folder = Path.Combine(Plugin.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty, "Images",
            "Prism");
        var paths = new string[Files.Length];
        for (var index = 0; index < Files.Length; index++)
        {
            paths[index] = Files[index].Length == 0 ? string.Empty : Path.Combine(folder, Files[index]);
        }

        return paths;
    }
}
