namespace Aetherphone.Core.Onboarding;

internal readonly struct GuideSequence
{
    public readonly string Id;
    public readonly int ContentVersion;
    public readonly string? RequiredAppId;
    public readonly GuideStep[] Steps;

    public GuideSequence(string id, int contentVersion, string? requiredAppId, GuideStep[] steps)
    {
        Id = id;
        ContentVersion = contentVersion;
        RequiredAppId = requiredAppId;
        Steps = steps;
    }

    public bool IsValid => Steps is { Length: > 0 };
}
