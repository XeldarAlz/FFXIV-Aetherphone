namespace Aetherphone.Core.Lyrics;

internal enum LyricsStatus : byte
{
    Loading,
    Ready,
    Instrumental,
    NotFound,
    Failed,
}

internal readonly struct LyricsState
{
    public static readonly LyricsState Loading = new(LyricsStatus.Loading, LrcDocument.Empty);
    public static readonly LyricsState Instrumental = new(LyricsStatus.Instrumental, LrcDocument.Empty);
    public static readonly LyricsState NotFound = new(LyricsStatus.NotFound, LrcDocument.Empty);
    public static readonly LyricsState Failed = new(LyricsStatus.Failed, LrcDocument.Empty);

    public readonly LyricsStatus Status;
    public readonly LrcDocument Document;

    private LyricsState(LyricsStatus status, LrcDocument document)
    {
        Status = status;
        Document = document;
    }

    public bool IsReady => Status == LyricsStatus.Ready;

    public static LyricsState Ready(LrcDocument document)
    {
        return document.IsEmpty ? NotFound : new LyricsState(LyricsStatus.Ready, document);
    }
}
