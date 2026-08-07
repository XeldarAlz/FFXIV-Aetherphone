using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Conduct;

internal enum ConductTone
{
    Encouraged,
    Prohibited,
    Restricted,
    Neutral,
}

internal readonly record struct ConductSection(ConductTone Tone, LocString? Heading, LocString? Lead, LocString[] Items,
    FontAwesomeIcon? Icon = null);

internal sealed class ConductGate
{
    public required string AppId { get; init; }
    public required int Version { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public required LocString Title { get; init; }
    public required LocString Intro { get; init; }
    public required ConductSection[] Sections { get; init; }
}

internal static class ConductRules
{
    public static readonly ConductGate Chirper = new()
    {
        AppId = "chirper",
        Version = 2,
        Icon = FontAwesomeIcon.Comments,
        Title = L.Conduct.ChirperTitle,
        Intro = L.Conduct.ChirperIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.ChirperAllowedTitle, L.Conduct.ChirperAllowedLead,
                L.Conduct.ChirperAllowedItems),
            new ConductSection(ConductTone.Restricted, L.Conduct.ChirperAppropriateTitle,
                L.Conduct.ChirperAppropriateLead, L.Conduct.ChirperAppropriateItems, FontAwesomeIcon.EyeSlash),
            new ConductSection(ConductTone.Restricted, L.Conduct.ChirperRespectTitle, L.Conduct.ChirperRespectLead,
                L.Conduct.ChirperRespectItems, FontAwesomeIcon.Handshake),
            new ConductSection(ConductTone.Restricted, L.Conduct.ChirperSpamTitle, L.Conduct.ChirperSpamLead,
                L.Conduct.ChirperSpamItems, FontAwesomeIcon.Bullhorn),
            new ConductSection(ConductTone.Neutral, L.Conduct.ChirperPrivacyTitle, L.Conduct.ChirperPrivacyBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Lock),
            new ConductSection(ConductTone.Restricted, L.Conduct.ChirperCreatorsTitle, L.Conduct.ChirperCreatorsLead,
                L.Conduct.ChirperCreatorsItems, FontAwesomeIcon.Copyright),
            new ConductSection(ConductTone.Prohibited, L.Conduct.ChirperChildSafetyTitle,
                L.Conduct.ChirperChildSafetyBody, Array.Empty<LocString>(), FontAwesomeIcon.Child),
            new ConductSection(ConductTone.Neutral, L.Conduct.ChirperDiscretionTitle, L.Conduct.ChirperDiscretionBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Gavel),
        },
    };

    public static readonly ConductGate Aethergram = new()
    {
        AppId = "aethergram",
        Version = 2,
        Icon = FontAwesomeIcon.Camera,
        Title = L.Conduct.AethergramTitle,
        Intro = L.Conduct.AethergramIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.AethergramAllowedTitle,
                L.Conduct.AethergramAllowedLead, L.Conduct.AethergramAllowedItems),
            new ConductSection(ConductTone.Restricted, L.Conduct.AethergramSfwTitle, L.Conduct.AethergramSfwLead,
                L.Conduct.AethergramSfwItems, FontAwesomeIcon.EyeSlash),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramContextTitle, L.Conduct.AethergramContextLead,
                L.Conduct.AethergramContextItems, FontAwesomeIcon.Camera),
            new ConductSection(ConductTone.Prohibited, L.Conduct.AethergramChildlikeTitle,
                L.Conduct.AethergramChildlikeBody, Array.Empty<LocString>(), FontAwesomeIcon.Child),
            new ConductSection(ConductTone.Restricted, L.Conduct.AethergramIrlTitle, L.Conduct.AethergramIrlBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Gamepad),
            new ConductSection(ConductTone.Restricted, L.Conduct.AethergramRespectTitle,
                L.Conduct.AethergramRespectLead, L.Conduct.AethergramRespectItems, FontAwesomeIcon.Handshake),
            new ConductSection(ConductTone.Restricted, L.Conduct.AethergramSpamTitle, L.Conduct.AethergramSpamLead,
                L.Conduct.AethergramSpamItems, FontAwesomeIcon.Bullhorn),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramPrivacyTitle, L.Conduct.AethergramPrivacyBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Lock),
            new ConductSection(ConductTone.Restricted, L.Conduct.AethergramCreatorsTitle,
                L.Conduct.AethergramCreatorsLead, L.Conduct.AethergramCreatorsItems, FontAwesomeIcon.Copyright),
            new ConductSection(ConductTone.Neutral, L.Conduct.AethergramDiscretionTitle,
                L.Conduct.AethergramDiscretionBody, Array.Empty<LocString>(), FontAwesomeIcon.Gavel),
        },
    };

    public static readonly ConductGate Velvet = new()
    {
        AppId = "velvet",
        Version = 2,
        Icon = FontAwesomeIcon.Heart,
        Title = L.Conduct.VelvetTitle,
        Intro = L.Conduct.VelvetIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetAdultsTitle, L.Conduct.VelvetAdultsBody,
                Array.Empty<LocString>(), FontAwesomeIcon.IdCard),
            new ConductSection(ConductTone.Encouraged, L.Conduct.VelvetAllowedTitle, L.Conduct.VelvetAllowedLead,
                L.Conduct.VelvetAllowedItems),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetConsentTitle, L.Conduct.VelvetConsentLead,
                L.Conduct.VelvetConsentItems, FontAwesomeIcon.HandPaper),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetBoundariesTitle, L.Conduct.VelvetBoundariesLead,
                L.Conduct.VelvetBoundariesItems, FontAwesomeIcon.DoorClosed),
            new ConductSection(ConductTone.Prohibited, L.Conduct.VelvetIllegalTitle, L.Conduct.VelvetIllegalLead,
                L.Conduct.VelvetIllegalItems),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetPrivacyTitle, L.Conduct.VelvetPrivacyLead,
                L.Conduct.VelvetPrivacyItems, FontAwesomeIcon.Lock),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetCreatorsTitle, L.Conduct.VelvetCreatorsLead,
                L.Conduct.VelvetCreatorsItems, FontAwesomeIcon.Copyright),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetSpamTitle, L.Conduct.VelvetSpamLead,
                L.Conduct.VelvetSpamItems, FontAwesomeIcon.Bullhorn),
            new ConductSection(ConductTone.Restricted, L.Conduct.VelvetRespectTitle, L.Conduct.VelvetRespectLead,
                L.Conduct.VelvetRespectItems, FontAwesomeIcon.Handshake),
            new ConductSection(ConductTone.Neutral, L.Conduct.VelvetModerationTitle, L.Conduct.VelvetModerationBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Gavel),
        },
    };

    public static readonly ConductGate Muster = new()
    {
        AppId = "muster",
        Version = 2,
        Icon = FontAwesomeIcon.Bullhorn,
        Title = L.Conduct.MusterTitle,
        Intro = L.Conduct.MusterIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.MusterAllowedTitle, L.Conduct.MusterAllowedLead,
                L.Conduct.MusterAllowedItems),
            new ConductSection(ConductTone.Restricted, L.Conduct.MusterHostingTitle, L.Conduct.MusterHostingLead,
                L.Conduct.MusterHostingItems, FontAwesomeIcon.CalendarCheck),
            new ConductSection(ConductTone.Restricted, L.Conduct.MusterAppropriateTitle,
                L.Conduct.MusterAppropriateLead, L.Conduct.MusterAppropriateItems, FontAwesomeIcon.EyeSlash),
            new ConductSection(ConductTone.Prohibited, L.Conduct.MusterChildSafetyTitle,
                L.Conduct.MusterChildSafetyBody, Array.Empty<LocString>(), FontAwesomeIcon.Child),
            new ConductSection(ConductTone.Restricted, L.Conduct.MusterInGameTitle, L.Conduct.MusterInGameBody,
                Array.Empty<LocString>(), FontAwesomeIcon.MapMarkerAlt),
            new ConductSection(ConductTone.Restricted, L.Conduct.MusterRespectTitle, L.Conduct.MusterRespectLead,
                L.Conduct.MusterRespectItems, FontAwesomeIcon.Handshake),
            new ConductSection(ConductTone.Restricted, L.Conduct.MusterSpamTitle, L.Conduct.MusterSpamLead,
                L.Conduct.MusterSpamItems, FontAwesomeIcon.Bullhorn),
            new ConductSection(ConductTone.Neutral, L.Conduct.MusterPrivacyTitle, L.Conduct.MusterPrivacyBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Lock),
            new ConductSection(ConductTone.Neutral, L.Conduct.MusterDiscretionTitle, L.Conduct.MusterDiscretionBody,
                Array.Empty<LocString>(), FontAwesomeIcon.Gavel),
        },
    };

    public static readonly ConductGate YellowPages = new()
    {
        AppId = "yellowpages",
        Version = 2,
        Icon = FontAwesomeIcon.AddressBook,
        Title = L.Conduct.YellowPagesTitle,
        Intro = L.Conduct.YellowPagesIntro,
        Sections = new[]
        {
            new ConductSection(ConductTone.Encouraged, L.Conduct.YellowPagesAllowedTitle,
                L.Conduct.YellowPagesAllowedLead, L.Conduct.YellowPagesAllowedItems),
            new ConductSection(ConductTone.Prohibited, L.Conduct.YellowPagesGilTitle, L.Conduct.YellowPagesGilLead,
                L.Conduct.YellowPagesGilItems, FontAwesomeIcon.Coins),
            new ConductSection(ConductTone.Restricted, L.Conduct.YellowPagesHonestTitle,
                L.Conduct.YellowPagesHonestLead, L.Conduct.YellowPagesHonestItems, FontAwesomeIcon.ClipboardCheck),
            new ConductSection(ConductTone.Restricted, L.Conduct.YellowPagesAppropriateTitle,
                L.Conduct.YellowPagesAppropriateLead, L.Conduct.YellowPagesAppropriateItems,
                FontAwesomeIcon.EyeSlash),
            new ConductSection(ConductTone.Prohibited, L.Conduct.YellowPagesChildSafetyTitle,
                L.Conduct.YellowPagesChildSafetyBody, Array.Empty<LocString>(), FontAwesomeIcon.Child),
            new ConductSection(ConductTone.Restricted, L.Conduct.YellowPagesRespectTitle,
                L.Conduct.YellowPagesRespectLead, L.Conduct.YellowPagesRespectItems, FontAwesomeIcon.Handshake),
            new ConductSection(ConductTone.Restricted, L.Conduct.YellowPagesSpamTitle, L.Conduct.YellowPagesSpamLead,
                L.Conduct.YellowPagesSpamItems, FontAwesomeIcon.Bullhorn),
            new ConductSection(ConductTone.Neutral, L.Conduct.YellowPagesPrivacyTitle,
                L.Conduct.YellowPagesPrivacyBody, Array.Empty<LocString>(), FontAwesomeIcon.Lock),
            new ConductSection(ConductTone.Restricted, L.Conduct.YellowPagesCreatorsTitle,
                L.Conduct.YellowPagesCreatorsLead, L.Conduct.YellowPagesCreatorsItems, FontAwesomeIcon.Copyright),
            new ConductSection(ConductTone.Neutral, L.Conduct.YellowPagesDiscretionTitle,
                L.Conduct.YellowPagesDiscretionBody, Array.Empty<LocString>(), FontAwesomeIcon.Gavel),
        },
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
}
