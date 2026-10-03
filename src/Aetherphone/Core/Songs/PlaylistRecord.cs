namespace Aetherphone.Core.Songs;

[Serializable]
internal sealed class PlaylistRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public long CreatedUnix { get; set; }
    public long UpdatedUnix { get; set; }
    public List<SongRecord> Songs { get; set; } = new();
}
