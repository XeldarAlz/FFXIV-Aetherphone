namespace Aetherphone.Apps.Photos;

internal enum PhotoRoute : byte
{
    Grid,
    Album,
    Viewer,
}

internal readonly struct PhotoView
{
    public const int RecentsKey = -1;

    public readonly PhotoRoute Route;
    public readonly int AlbumKey;

    private PhotoView(PhotoRoute route, int albumKey)
    {
        Route = route;
        AlbumKey = albumKey;
    }

    public static PhotoView Grid() => new(PhotoRoute.Grid, 0);

    public static PhotoView Album(int albumKey) => new(PhotoRoute.Album, albumKey);

    public static PhotoView Viewer() => new(PhotoRoute.Viewer, 0);
}
