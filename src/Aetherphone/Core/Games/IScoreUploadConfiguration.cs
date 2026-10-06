namespace Aetherphone.Core.Games;

internal interface IScoreUploadConfiguration
{
    List<PendingScoreUpload> PendingScoreUploads { get; }
    void Save();
}
