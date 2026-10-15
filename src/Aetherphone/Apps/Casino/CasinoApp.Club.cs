using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private const string FloorFameBoard = CasinoFameBoards.Win;
    private const float BadgeLift = 0.45f;
    private const float TierBadgeLift = 0.25f;

    private readonly MissionsSheet missionsSheet;
    private readonly ClubSheet clubSheet = new();
    private readonly FloorShortcuts shortcuts = new();
    private readonly FloorShortcut[] shortcutItems = new FloorShortcut[FloorShortcuts.Capacity];
    private readonly FameView fameView;
    private readonly FameConsentSheet fameConsent;
    private readonly CasinoFameChoiceStore fameChoice;

    private float DrawShortcuts(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var count = CollectShortcuts();
        var tapped = shortcuts.Draw(drawList, ui, shortcutItems.AsSpan(0, count), origin, width, scale,
            out var bottom);
        if (tapped >= 0)
        {
            OpenShortcut(shortcutItems[tapped].Kind);
        }

        return count == 0 ? origin.Y : bottom + FloorBlockGap * scale;
    }

    private int CollectShortcuts()
    {
        var count = 0;
        var missions = floor.Missions;
        if (casino.HasFeature(CasinoFeatures.Missions) && MissionsCard.Count(missions) > 0)
        {
            var tally = FloorShortcuts.Tally(missions);
            shortcutItems[count++] = new FloorShortcut(FloorShortcutKind.Missions, FontAwesomeIcon.Bullseye,
                AccentRing.Azure, Loc.T(L.Floor.MissionsTitle),
                texts.Counts(L.FloorPage.MissionsBadge, tally.Done, tally.Total),
                tally.Ready > 0 ? CasinoColors.Money : Palette.Mix(AccentRing.Azure, CasinoArt.White, BadgeLift));
        }

        var club = casino.HasFeature(CasinoFeatures.Club) ? casino.Club : null;
        if (club is not null)
        {
            var tier = CasinoClubTiers.Clamp(club.Tier);
            shortcutItems[count++] = new FloorShortcut(FloorShortcutKind.Club, FontAwesomeIcon.Crown, AccentRing.Gold,
                Loc.T(L.FloorPage.ShortcutClub), Loc.T(ClubTierArt.TierName(tier)),
                Palette.Mix(ClubTierArt.TierTint(tier), CasinoArt.White, TierBadgeLift));
        }

        if (casino.HasFeature(CasinoFeatures.Fame))
        {
            shortcutItems[count++] = new FloorShortcut(FloorShortcutKind.Fame, FontAwesomeIcon.Trophy, AccentRing.Rose,
                Loc.T(L.Club.FameTitle), FameBadge(), Palette.Mix(AccentRing.Rose, CasinoArt.White, BadgeLift));
        }

        shortcutItems[count++] = new FloorShortcut(FloorShortcutKind.History, FontAwesomeIcon.Receipt,
            AccentRing.Indigo, Loc.T(L.FloorPage.ShortcutHistory), string.Empty, AccentRing.Indigo);
        return count;
    }

    private string FameBadge()
    {
        floor.EnsureFame(FloorFameBoard, CasinoFameSpans.Week);
        var me = floor.Fame(FloorFameBoard, CasinoFameSpans.Week)?.Me;
        return me is { OptedIn: true, Rank: > 0 } ? texts.Count(L.FloorPage.RankBadge, me.Rank) : string.Empty;
    }

    private void OpenShortcut(FloorShortcutKind kind)
    {
        switch (kind)
        {
            case FloorShortcutKind.Missions:
                missionsSheet.Open();
                break;
            case FloorShortcutKind.Club:
                if (casino.Club is { } club)
                {
                    clubSheet.Open(club.Tier);
                }

                break;
            case FloorShortcutKind.Fame:
                OpenFame();
                break;
            default:
                OpenHistory();
                break;
        }
    }

    private void OpenFame()
    {
        if (router.Current.Screen == CasinoScreen.Fame)
        {
            return;
        }

        fameView.Enter(FloorFameBoard);
        routes.Push(new CasinoRoute(CasinoScreen.Fame));
    }

    private void ConsumeFameConsentRequest()
    {
        if (fameView.TakeConsentRequest())
        {
            fameConsent.Open();
        }
    }

    private void OfferFameConsent()
    {
        if (fameConsent.IsOpen || !FloorIdle() || !fameChoice.Needed)
        {
            return;
        }

        fameConsent.Open();
    }

    private bool FloorIdle() =>
        casino.State is not null && preferences.IntroSeen && routes.OnRoot && routes.Tab == CasinoTab.Floor
        && !router.IsTransitioning && !LaunchActive && !rulesSheet.IsOpen && !clubSheet.IsOpen && !cashier.IsOpen
        && !missionsSheet.IsOpen && !venueSheet.IsOpen && !tradeCard.Visible && conduct.Active is null
        && confirm.Active is null;

    private void HandleMissionsSheet()
    {
        var claim = missionsSheet.TakeClaim();
        if (claim.Length > 0)
        {
            floor.ClaimMission(claim);
        }
    }

    private void ConsumeMissionNotes()
    {
        if (floor.TakeClaimFailure())
        {
            ShellToast.Show(Loc.T(CasinoReasons.MessageFor(CasinoReasons.Unreachable)));
        }

        var claim = floor.TakeMissionClaim();
        if (claim is not null)
        {
            if (claim.Granted)
            {
                bonusShelf.Shower(missionsSheet.ClaimCenter, UiScale.Current);
                ShellToast.Show(texts.Compact(L.Strip.BonusLanded, claim.Amount));
            }
            else
            {
                ShellToast.Show(Loc.T(CasinoReasons.MessageFor(claim.Reason)));
            }
        }

        var done = floor.TakeCompletedMission();
        if (done.Length > 0)
        {
            ShellToast.Show(Loc.T(L.Club.MissionCompleteToast));
            UiFeedback.Play(UiSound.CasinoChips);
        }
    }
}
