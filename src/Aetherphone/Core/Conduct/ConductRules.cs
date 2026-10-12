using Aetherphone.Core.Localization;
using Dalamud.Interface;

namespace Aetherphone.Core.Conduct;

internal enum ConductTone : byte
{
    Allowed,
    Rule,
    Veiled,
    Removed,
    ZeroTolerance,
    Info,
}

internal enum ConductPill : byte
{
    None,
    Fine,
    Veiled,
    Removed,
    Suspended,
}

internal readonly record struct ConductSection(ConductTone Tone, LocString Heading, LocString? Lead,
    LocString[] Items, FontAwesomeIcon Icon, ConductPill Pill = ConductPill.None, LocString? Note = null);

internal sealed class ConductGate
{
    public required string AppId { get; init; }
    public required int Version { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public required LocString AppName { get; init; }
    public required LocString Title { get; init; }
    public required LocString Intro { get; init; }
    public required ConductSection[] Sections { get; init; }
    public bool GatesOnOpen { get; init; } = true;
}

internal static class ConductRules
{
    private static readonly LocString[] LadderWithVeil = Concat(new[] { L.Conduct.LadderVeil }, L.Conduct.LadderItems);

    public static readonly ConductGate Chirper = new()
    {
        AppId = "chirper",
        Version = 8,
        Icon = FontAwesomeIcon.Comments,
        AppName = L.Apps.Chirper,
        Title = L.Conduct.ChirperTitle,
        Intro = L.Conduct.ChirperIntro,
        Sections = new[]
        {
            Allowed(L.Conduct.ChirperAllowedItems),
            Veiled(L.Conduct.ChirperVeiledNote),
            Removed(L.Conduct.RemovedNudity, L.Conduct.RemovedErp, L.Conduct.RemovedFetish, L.Conduct.RemovedGore,
                L.Conduct.RemovedAi, L.Conduct.RemovedCreators, L.Conduct.RemovedPolitics, L.Conduct.RemovedHarassment,
                L.Conduct.RemovedPrivateChat, L.Conduct.RemovedImpersonation, L.Conduct.ChirperSpam),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(true),
        },
    };

    public static readonly ConductGate Aethergram = new()
    {
        AppId = "aethergram",
        Version = 8,
        Icon = FontAwesomeIcon.Camera,
        AppName = L.Apps.Aethergram,
        Title = L.Conduct.AethergramTitle,
        Intro = L.Conduct.AethergramIntro,
        Sections = new[]
        {
            Allowed(L.Conduct.AethergramAllowedItems),
            Rule(L.Conduct.AethergramInGameTitle, L.Conduct.AethergramInGameLead, L.Conduct.AethergramInGameItems,
                FontAwesomeIcon.Gamepad, ConductPill.Removed),
            Veiled(null),
            Removed(L.Conduct.RemovedNudity, L.Conduct.RemovedErp, L.Conduct.RemovedFetish, L.Conduct.RemovedGore,
                L.Conduct.RemovedAi, L.Conduct.RemovedCreators, L.Conduct.RemovedPolitics, L.Conduct.RemovedHarassment,
                L.Conduct.RemovedPrivateChat, L.Conduct.RemovedImpersonation, L.Conduct.AethergramSpam),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(true),
        },
    };

    public static readonly ConductGate Velvet = new()
    {
        AppId = "velvet",
        Version = 5,
        Icon = FontAwesomeIcon.Heart,
        AppName = L.Apps.Velvet,
        Title = L.Conduct.VelvetTitle,
        Intro = L.Conduct.VelvetIntro,
        Sections = new[]
        {
            Rule(L.Conduct.VelvetAdultsTitle, null, L.Conduct.VelvetAdultsItems, FontAwesomeIcon.IdCard,
                ConductPill.Suspended),
            Allowed(L.Conduct.VelvetAllowedItems),
            Rule(L.Conduct.VelvetConsentTitle, null, L.Conduct.VelvetConsentItems, FontAwesomeIcon.HandPaper,
                ConductPill.Removed),
            Removed(L.Conduct.VelvetAdultCommerce, L.Conduct.RemovedAi, L.Conduct.RemovedGore,
                L.Conduct.RemovedCreators, L.Conduct.RemovedHarassmentStalking, L.Conduct.RemovedPrivateChat,
                L.Conduct.RemovedImpersonation),
            ZeroTolerance(L.Conduct.VelvetZeroItems),
            Moderation(false),
        },
    };

    public static readonly ConductGate Message = new()
    {
        AppId = "message",
        Version = 1,
        Icon = FontAwesomeIcon.Comment,
        AppName = L.Apps.Message,
        Title = L.Conduct.MessageTitle,
        Intro = L.Conduct.MessageIntro,
        Sections = new[]
        {
            Allowed(L.Conduct.MessageAllowedItems),
            Rule(L.Conduct.MessageContactTitle, null, L.Conduct.MessageContactItems, FontAwesomeIcon.HandPaper,
                ConductPill.Removed),
            Removed(L.Conduct.RemovedHarassmentStalking, L.Conduct.MessageSpam, L.Conduct.RemovedImpersonation),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(false),
        },
    };

    public static readonly ConductGate Music = new()
    {
        AppId = "music",
        Version = 1,
        Icon = FontAwesomeIcon.Music,
        AppName = L.Apps.Music,
        Title = L.Conduct.MusicTitle,
        Intro = L.Conduct.MusicIntro,
        GatesOnOpen = false,
        Sections = new[]
        {
            Allowed(L.Conduct.MusicAllowedItems),
            Removed(L.Conduct.MusicExplicit, L.Conduct.RemovedHarassment, L.Conduct.MusicSpam,
                L.Conduct.RemovedImpersonation),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(false),
        },
    };

    public static readonly ConductGate Muster = new()
    {
        AppId = "muster",
        Version = 3,
        Icon = FontAwesomeIcon.Bullhorn,
        AppName = L.Apps.Muster,
        Title = L.Conduct.MusterTitle,
        Intro = L.Conduct.MusterIntro,
        Sections = new[]
        {
            Allowed(L.Conduct.MusterAllowedItems),
            Rule(L.Conduct.MusterHostingTitle, null, L.Conduct.MusterHostingItems, FontAwesomeIcon.CalendarCheck,
                ConductPill.Removed),
            Removed(Concat(L.Conduct.MusterRemovedItems, new[] { L.Conduct.RemovedImpersonation })),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(false),
        },
    };

    public static readonly ConductGate YellowPages = new()
    {
        AppId = "yellowpages",
        Version = 3,
        Icon = FontAwesomeIcon.AddressBook,
        AppName = L.Apps.YellowPages,
        Title = L.Conduct.YellowPagesTitle,
        Intro = L.Conduct.YellowPagesIntro,
        Sections = new[]
        {
            Allowed(L.Conduct.YellowPagesAllowedItems),
            Rule(L.Conduct.YellowPagesPostingTitle, null, L.Conduct.YellowPagesPostingItems, FontAwesomeIcon.Bullhorn,
                ConductPill.None, L.Conduct.YellowPagesPostingNote),
            Removed(L.Conduct.YellowPagesRemovedItems),
            ZeroTolerance(L.Conduct.ZeroChildSafety),
            Moderation(false),
        },
    };

    public static readonly ConductGate Casino = new()
    {
        AppId = "casino",
        Version = 2,
        Icon = FontAwesomeIcon.Dice,
        AppName = L.Apps.Casino,
        Title = L.Conduct.CasinoTitle,
        Intro = L.Conduct.CasinoIntro,
        Sections = new[]
        {
            Info(L.Conduct.CasinoMoneyTitle, L.Conduct.CasinoMoneyItems, FontAwesomeIcon.Coins),
            new ConductSection(ConductTone.Allowed, L.Conduct.CasinoMannersTitle, null, L.Conduct.CasinoMannersItems,
                FontAwesomeIcon.Handshake, ConductPill.Fine),
            Rule(L.Conduct.CasinoFairPlayTitle, null, L.Conduct.CasinoFairPlayItems, FontAwesomeIcon.Users,
                ConductPill.None, L.Conduct.CasinoFairPlayNote),
            Rule(L.Conduct.CasinoGilTablesTitle, null, L.Conduct.CasinoGilTablesItems, FontAwesomeIcon.Landmark),
            ZeroTolerance(L.Conduct.CasinoZeroItems),
            Info(L.Conduct.CasinoSelfCareTitle, L.Conduct.CasinoSelfCareItems, FontAwesomeIcon.Heart),
            Moderation(false),
        },
    };

    public static readonly ConductGate Coin = new()
    {
        AppId = "coin",
        Version = 2,
        Icon = FontAwesomeIcon.Coins,
        AppName = L.Apps.Coin,
        Title = L.Conduct.CoinTitle,
        Intro = L.Conduct.CoinIntro,
        Sections = new[]
        {
            Info(L.Conduct.CoinHowTitle, L.Conduct.CoinHowItems, FontAwesomeIcon.Coins),
            Rule(L.Conduct.CoinFairPlayTitle, null, L.Conduct.CoinFairPlayItems, FontAwesomeIcon.Robot,
                ConductPill.None, L.Conduct.CoinFairPlayNote),
            Info(L.Conduct.CoinScamsTitle, L.Conduct.CoinScamsItems, FontAwesomeIcon.ShieldAlt),
            ZeroTolerance(L.Conduct.CoinZeroItems),
            Moderation(false),
        },
    };

    private static readonly ConductGate[] All =
    {
        Chirper, Aethergram, Velvet, Message, Music, Muster, YellowPages, Casino, Coin,
    };

    public static ReadOnlySpan<ConductGate> Gates => All;

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

    private static ConductSection Allowed(LocString[] items) =>
        new(ConductTone.Allowed, L.Conduct.AllowedTitle, null, items, FontAwesomeIcon.Check, ConductPill.Fine);

    private static ConductSection Veiled(LocString? note) =>
        new(ConductTone.Veiled, L.Conduct.VeiledTitle, L.Conduct.VeiledLead, L.Conduct.VeiledItems,
            FontAwesomeIcon.EyeSlash, ConductPill.Veiled, note);

    private static ConductSection Removed(params LocString[] items) =>
        new(ConductTone.Removed, L.Conduct.RemovedTitle, null, items, FontAwesomeIcon.Ban, ConductPill.Removed);

    private static ConductSection ZeroTolerance(params LocString[] appItems) =>
        new(ConductTone.ZeroTolerance, L.Conduct.ZeroTitle, L.Conduct.ZeroLead, Concat(appItems, L.Conduct.ZeroItems),
            FontAwesomeIcon.ExclamationTriangle, ConductPill.Suspended);

    private static ConductSection Moderation(bool veils) =>
        new(ConductTone.Info, L.Conduct.LadderTitle, L.Conduct.LadderLead, veils ? LadderWithVeil : L.Conduct.LadderItems,
            FontAwesomeIcon.Gavel);

    private static ConductSection Rule(LocString heading, LocString? lead, LocString[] items, FontAwesomeIcon icon,
        ConductPill pill = ConductPill.None, LocString? note = null) =>
        new(ConductTone.Rule, heading, lead, items, icon, pill, note);

    private static ConductSection Info(LocString heading, LocString[] items, FontAwesomeIcon icon) =>
        new(ConductTone.Info, heading, null, items, icon);

    private static LocString[] Concat(LocString[] first, LocString[] second)
    {
        var combined = new LocString[first.Length + second.Length];
        Array.Copy(first, combined, first.Length);
        Array.Copy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
}
