namespace Aetherphone.Core.Wallpapers;

[Serializable]
internal sealed class CustomWallpaper
{
    public string Id { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public float Zoom { get; set; } = 1f;

    public float CenterX { get; set; } = 0.5f;

    public float CenterY { get; set; } = 0.5f;
}
