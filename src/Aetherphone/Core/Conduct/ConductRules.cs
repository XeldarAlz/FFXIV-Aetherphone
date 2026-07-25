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
        Version = 1,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Comments,
        Title = L.Conduct.ChirperTitle,
        Intro = L.Conduct.ChirperIntro,
        Sections = WithPlatform(
            new ConductSection(ConductTone.Encouraged, L.Conduct.SectionEncouraged, null, L.Conduct.ChirperEncouraged),
            new ConductSection(ConductTone.Prohibited, L.Conduct.SectionNotAllowed, null, L.Conduct.ChirperNotAllowed)),
    };

    public static readonly ConductGate Aethergram = new()
    {
        AppId = "aethergram",
        Version = 1,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Camera,
        Title = L.Conduct.AethergramTitle,
        Intro = L.Conduct.AethergramIntro,
        Sections = WithPlatform(
            new ConductSection(ConductTone.Encouraged, L.Conduct.SectionEncouraged, null,
                L.Conduct.AethergramEncouraged),
            new ConductSection(ConductTone.Prohibited, L.Conduct.SectionNotAllowed, null,
                L.Conduct.AethergramNotAllowed)),
    };

    public static readonly ConductGate Velvet = new()
    {
        AppId = "velvet",
        Version = 1,
        CountdownSeconds = 45f,
        Icon = FontAwesomeIcon.Heart,
        Title = L.Conduct.VelvetTitle,
        Intro = L.Conduct.VelvetIntro,
        Sections = WithPlatform(
            new ConductSection(ConductTone.Encouraged, L.Conduct.SectionPermittedMature, null,
                L.Conduct.VelvetPermitted),
            new ConductSection(ConductTone.Prohibited, L.Conduct.SectionNotAllowed, null, L.Conduct.VelvetNotAllowed)),
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
