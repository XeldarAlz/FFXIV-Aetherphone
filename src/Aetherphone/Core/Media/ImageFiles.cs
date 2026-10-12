namespace Aetherphone.Core.Media;

internal static class ImageFiles
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" };

    public static bool IsImage(ReadOnlySpan<char> path)
    {
        var extension = Path.GetExtension(path);
        for (var index = 0; index < Extensions.Length; index++)
        {
            if (extension.Equals(Extensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
