namespace Aetherphone.Core.Photos;

internal static class PickerPaths
{
    public static string[] WithImported(string[] paths, string path)
    {
        if (Array.IndexOf(paths, path) >= 0)
        {
            return paths;
        }

        var merged = new string[paths.Length + 1];
        merged[0] = path;
        Array.Copy(paths, 0, merged, 1, paths.Length);
        return merged;
    }
}
