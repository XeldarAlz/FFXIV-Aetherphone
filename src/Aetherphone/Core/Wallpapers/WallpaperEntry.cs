namespace Aetherphone.Core.Wallpapers;

internal enum WallpaperKind
{
    BuiltIn,
    Custom,
}

internal sealed class WallpaperEntry
{
    public required string Id { get; init; }

    public required WallpaperKind Kind { get; init; }

    public required string FilePath { get; init; }

    public required WallpaperCrop Crop { get; init; }
}
