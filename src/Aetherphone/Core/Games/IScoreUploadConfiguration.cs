namespace Aetherphone.Core.Games;

internal interface IScoreUploadConfiguration
{
    List<PendingScoreUpload> PendingScoreUploads { get; }
    List<GameStatRecord> GameStats { get; }
    List<string> LeaderboardConsentAnswered { get; }
    void Save();
}
