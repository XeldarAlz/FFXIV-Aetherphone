namespace Aetherphone.Core.Games;

internal sealed class NullScoreSink : IScoreSink
{
    public static readonly NullScoreSink Instance = new();

    public void Submit(in ScoreSubmission submission)
    {
    }
}
