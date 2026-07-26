using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Conduct;

internal enum ConductTone
{
    Encouraged,
    Prohibited,
    Neutral,
}

internal readonly record struct ConductSection(ConductTone Tone, LocString? Heading, LocString? Lead, LocString[] Items);

internal sealed class ConductGate
{
    public required string AppId { get; init; }
    public required int Version { get; init; }
    public required float CountdownSeconds { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public required LocString Title { get; init; }
    public required LocString Intro { get; init; }
    public required ConductSection[] Sections { get; init; }
}

internal static class ConductRules
{
    private static readonly ConductSection[] PlatformStandards =
    {
        new(ConductTone.Neutral, L.Conduct.PlatformTitle, L.Conduct.PlatformLead, Array.Empty<LocString>()),
        new(ConductTone.Neutral, L.Conduct.RespectTitle, L.Conduct.RespectBody, Array.Empty<LocString>()),
        new(ConductTone.Neutral, L.Conduct.PrivacyTitle, L.Conduct.PrivacyLead, L.Conduct.PrivacyItems),
        new(ConductTone.Neutral, L.Conduct.SpamTitle, L.Conduct.SpamLead, L.Conduct.SpamItems),
        new(ConductTone.Neutral, L.Conduct.IpTitle, L.Conduct.IpBody, Array.Empty<LocString>()),
        new(ConductTone.Neutral, L.Conduct.EnforcementTitle, L.Conduct.EnforcementLead, L.Conduct.EnforcementItems),
        new(ConductTone.Neutral, null, L.Conduct.EnforcementNote, Array.Empty<LocString>()),
        new(ConductTone.Neutral, L.Conduct.AppealsTitle, L.Conduct.AppealsBody, Array.Empty<LocString>()),
    };

    public static readonly ConductGate Chirper = new()
    {
        AppId = "chirper",
        Version = 2,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Comments,
        Title = L.Conduct.ChirperTitle,
        Intro = L.Conduct.ChirperIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.ChirperAllowedTitle, L.Conduct.ChirperAllowedLead,
                L.Conduct.ChirperAllowedItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperAppropriateTitle,
                L.Conduct.ChirperAppropriateLead, L.Conduct.ChirperAppropriateItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperRespectTitle, L.Conduct.ChirperRespectLead,
                L.Conduct.ChirperRespectItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperSpamTitle, L.Conduct.ChirperSpamLead,
                L.Conduct.ChirperSpamItems),
            new ConductSection(ConductTone.Neutral, L.Conduct.ChirperPrivacyTitle, L.Conduct.ChirperPrivacyBody,
                Array.Empty<LocString>()),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperCreatorsTitle, L.Conduct.ChirperCreatorsLead,
                L.Conduct.ChirperCreatorsItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperChildSafetyTitle,
                L.Conduct.ChirperChildSafetyBody, Array.Empty<LocString>()),
            new ConductSection(ConductTone.Neutral, L.Conduct.ChirperDiscretionTitle, L.Conduct.ChirperDiscretionBody,
                Array.Empty<LocString>()),
        },
    };

    public static readonly ConductGate Aethergram = new()
    {
        AppId = "aethergram",
        Version = 2,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Camera,
        Title = L.Conduct.AethergramTitle,
        Intro = L.Conduct.AethergramIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.AethergramAllowedTitle,
                L.Conduct.AethergramAllowedLead, L.Conduct.AethergramAllowedItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramSfwTitle, L.Conduct.AethergramSfwLead,
                L.Conduct.AethergramSfwItems),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramContextTitle, L.Conduct.AethergramContextLead,
                L.Conduct.AethergramContextItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramChildlikeTitle,
                L.Conduct.AethergramChildlikeBody, Array.Empty<LocString>()),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramIrlTitle, L.Conduct.AethergramIrlBody,
                Array.Empty<LocString>()),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramRespectTitle,
                L.Conduct.AethergramRespectLead, L.Conduct.AethergramRespectItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramSpamTitle, L.Conduct.AethergramSpamLead,
                L.Conduct.AethergramSpamItems),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramPrivacyTitle, L.Conduct.AethergramPrivacyBody,
                Array.Empty<LocString>()),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramCreatorsTitle,
                L.Conduct.AethergramCreatorsLead, L.Conduct.AethergramCreatorsItems),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramDiscretionTitle,
                L.Conduct.AethergramDiscretionBody, Array.Empty<LocString>()),
        },
    };

    public static readonly ConductGate Velvet = new()
    {
        AppId = "velvet",
        Version = 2,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Heart,
        Title = L.Conduct.VelvetTitle,
        Intro = L.Conduct.VelvetIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetAdultsTitle, L.Conduct.VelvetAdultsBody,
                Array.Empty<LocString>()),
            new ConductSection(ConductTone.Encouraged, L.Conduct.VelvetAllowedTitle, L.Conduct.VelvetAllowedLead,
                L.Conduct.VelvetAllowedItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetConsentTitle, L.Conduct.VelvetConsentLead,
                L.Conduct.VelvetConsentItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetBoundariesTitle, L.Conduct.VelvetBoundariesLead,
                L.Conduct.VelvetBoundariesItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetIllegalTitle, L.Conduct.VelvetIllegalLead,
                L.Conduct.VelvetIllegalItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetPrivacyTitle, L.Conduct.VelvetPrivacyLead,
                L.Conduct.VelvetPrivacyItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetCreatorsTitle, L.Conduct.VelvetCreatorsLead,
                L.Conduct.VelvetCreatorsItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetSpamTitle, L.Conduct.VelvetSpamLead,
                L.Conduct.VelvetSpamItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetRespectTitle, L.Conduct.VelvetRespectLead,
                L.Conduct.VelvetRespectItems),
            new ConductSection(ConductTone.Neutral, L.Conduct.VelvetModerationTitle, L.Conduct.VelvetModerationBody,
                Array.Empty<LocString>()),
        },
    };

    public static readonly ConductGate Muster = new()
    {
        AppId = "muster",
        Version = 1,
        CountdownSeconds = 30f,
        Icon = FontAwesomeIcon.Bullhorn,
        Title = L.Conduct.MusterTitle,
        Intro = L.Conduct.MusterIntro,
        Sections = WithPlatform(
            new ConductSection(ConductTone.Encouraged, L.Conduct.SectionEncouraged, null, L.Conduct.MusterEncouraged),
            new ConductSection(ConductTone.Prohibited, L.Conduct.SectionNotAllowed, null, L.Conduct.MusterNotAllowed)),
    };

    public static readonly ConductGate YellowPages = new()
    {
        AppId = "yellowpages",
        Version = 1,
        CountdownSeconds = 30f,
        Icon = FontAwesomeIcon.AddressBook,
        Title = L.Conduct.YellowPagesTitle,
        Intro = L.Conduct.YellowPagesIntro,
        Sections = WithPlatform(
            new ConductSection(ConductTone.Encouraged, L.Conduct.SectionEncouraged, null,
                L.Conduct.YellowPagesEncouraged),
            new ConductSection(ConductTone.Prohibited, L.Conduct.SectionNotAllowed, null,
                L.Conduct.YellowPagesNotAllowed)),
    };

    private static readonly ConductGate[] All = { Chirper, Aethergram, Velvet, Muster, YellowPages };

    public static ConductGate? For(string appId)
    {
        for (var index = 0; index < All.Length; index++)
        {
            if (All[index].AppId == appId)
            {
                return All[index];
            }
        }

        return null;
    }

    private static ConductSection[] WithPlatform(params ConductSection[] appSections)
    {
        var combined = new ConductSection[appSections.Length + PlatformStandards.Length];
        Array.Copy(appSections, combined, appSections.Length);
        Array.Copy(PlatformStandards, 0, combined, appSections.Length, PlatformStandards.Length);
        return combined;
    }
}
