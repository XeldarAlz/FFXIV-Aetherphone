namespace Aetherphone.Core.Onboarding;

internal readonly struct GuideSequence
{
    public readonly string Id;
    public readonly int ContentVersion;
    public readonly string? RequiredAppId;
    public readonly GuideStep[] Steps;
    public readonly string[]? CompletesOnFinish;

    public GuideSequence(string id, int contentVersion, string? requiredAppId, GuideStep[] steps,
        string[]? completesOnFinish = null)
    {
        Id = id;
        ContentVersion = contentVersion;
        RequiredAppId = requiredAppId;
        Steps = steps;
        CompletesOnFinish = completesOnFinish;
    }

    public bool IsValid => Steps is { Length: > 0 };
}
