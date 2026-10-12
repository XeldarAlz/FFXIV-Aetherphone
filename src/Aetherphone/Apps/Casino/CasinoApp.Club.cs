using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
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
    private const float ChallengeBulbInset = 6f;
    private const string FloorFameBoard = CasinoFameBoards.Win;

    private readonly MissionsCapsule missionsCapsule = new();
    private readonly MissionsSheet missionsSheet;
    private readonly ClubCapsule clubCapsule = new();
    private readonly ClubSheet clubSheet = new();
    private readonly FamePodium famePodium = new();
    private readonly FameView fameView;
    private readonly FameConsentSheet fameConsent;
    private readonly CasinoFameChoiceStore fameChoice;
    private string challengeTextId = string.Empty;
    private LanguageInfo? challengeLanguage;
    private string challengeLeader = string.Empty;
    private string challengeReward = string.Empty;
    private long challengeLeaderValue = -1;

    private float DrawFloorExtras(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var missions = floor.Missions;
        var showsMissions = casino.HasFeature(CasinoFeatures.Missions) && MissionsCard.Count(missions) > 0;
        var club = casino.HasFeature(CasinoFeatures.Club) ? casino.Club : null;
        var gap = CardGap * scale;
        if (showsMissions && club is not null
            && DrawFloorStatusPair(drawList, missions!, club, origin, width, scale, out var pairBottom))
        {
            return pairBottom + gap;
        }

        var top = origin.Y;
        if (showsMissions)
        {
            if (FloorCapsule.Draw(drawList, ui, missionsCapsule.Content(ui, missions!, false),
                    new Vector2(origin.X, top), width, scale, out var bottom))
            {
                missionsSheet.Open();
            }

            top = bottom + gap;
        }

        if (club is not null)
        {
            if (FloorCapsule.Draw(drawList, ui, clubCapsule.Content(ui, club, false), new Vector2(origin.X, top), width,
                    scale, out var bottom))
            {
                clubSheet.Open(club.Tier);
            }

            top = bottom + gap;
        }

        return top;
    }

    private bool DrawFloorStatusPair(ImDrawListPtr drawList, CasinoMissionsDto missions, CasinoClubDto club,
        Vector2 origin, float width, float scale, out float bottom)
    {
        var gap = CardGap * scale;
        var half = (width - gap) * 0.5f;
        var missionsTile = missionsCapsule.Content(ui, missions, true);
        var clubTile = clubCapsule.Content(ui, club, true);
        if (!FloorCapsule.FitsHalf(missionsTile, half, scale) || !FloorCapsule.FitsHalf(clubTile, half, scale))
        {
            bottom = origin.Y;
            return false;
        }

        var height = MathF.Max(FloorCapsule.HalfHeight(missionsTile, scale), FloorCapsule.HalfHeight(clubTile, scale));
        if (FloorCapsule.DrawHalf(drawList, ui, missionsTile, origin, half, height, scale))
        {
            missionsSheet.Open();
        }

        if (FloorCapsule.DrawHalf(drawList, ui, clubTile, new Vector2(origin.X + half + gap, origin.Y), half, height,
                scale))
        {
            clubSheet.Open(club.Tier);
        }

        bottom = origin.Y + height;
        return true;
    }

    private float DrawFloorTail(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!casino.HasFeature(CasinoFeatures.Fame))
        {
            return origin.Y;
        }

        floor.EnsureFame(FloorFameBoard, CasinoFameSpans.Week);
        var board = floor.Fame(FloorFameBoard, CasinoFameSpans.Week);
        if (board is null)
        {
            return origin.Y;
        }

        if (famePodium.Draw(drawList, ui, board, origin, width, scale, out var bottom))
        {
            OpenFame();
        }

        return bottom + CardGap * scale;
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
        && !missionsSheet.IsOpen && !venueSheet.IsOpen && !tradePrompt.Visible && conduct.Active is null
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

    private void DrawChallengePage(ImDrawListPtr drawList, Rect card, float radius, float scale)
    {
        var challenge = floor.LiveChallenge();
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(new Vector4(0.08f, 0.05f, 0.22f, 1f)), ImGui.GetColorU32(JackpotBed));
        CasinoLights.BulbChase(drawList, card.Inset(ChallengeBulbInset * scale), radius - ChallengeBulbInset * scale,
            scale, stage.Phase, CasinoLights.BulbPitch * 1.5f, CasinoColors.LightB, CasinoColors.LightA, 0.7f);
        if (challenge is null)
        {
            return;
        }

        RefreshChallengeText(challenge);
        var pad = HeroOverlayPad * scale;
        var width = card.Width - pad * 2f;
        var left = card.Min.X + pad;
        var top = card.Min.Y + pad * 1.4f;
        top = Tables.PitText.Draw(drawList, Loc.T(L.Club.ChallengeEyebrow), TextStyles.FootnoteEmphasized,
            CasinoColors.LightB, left, top, width, false);
        top = Tables.PitText.Draw(drawList, challenge.Title, TextStyles.Title2, CasinoColors.InkTitle, left, top, width,
            false) + Metrics.Space.Sm * scale;
        top = Tables.PitText.Draw(drawList, challengeLeader, TextStyles.Subheadline, CasinoColors.InkTitle, left, top,
            width, false);
        Tables.PitText.Draw(drawList, challengeReward, TextStyles.SubheadlineEmphasized, CasinoColors.Money, left, top,
            width, false);
        var remaining = Math.Max(0, challenge.EndsAtUnix - floor.ServerNowUnix);
        var ends = texts.Duration(L.Club.ChallengeEndsIn, (int)Math.Min(remaining, int.MaxValue));
        StageText.Plate(drawList, new Vector2(card.Center.X, card.Max.Y - pad - 14f * scale), ends, width,
            CasinoColors.InkTitle, TextStyles.FootnoteEmphasized, scale);
    }

    private void RefreshChallengeText(CasinoChallengeDto challenge)
    {
        if (string.Equals(challengeTextId, challenge.Id, StringComparison.Ordinal)
            && challengeLeaderValue == challenge.LeaderValue && ReferenceEquals(challengeLanguage, Loc.Current))
        {
            return;
        }

        challengeTextId = challenge.Id;
        challengeLeaderValue = challenge.LeaderValue;
        challengeLanguage = Loc.Current;
        challengeReward = Loc.T(L.Club.ChallengeReward, NumberText.Compact(challenge.Reward));
        if (challenge.Leader is null && challenge.LeaderValue <= 0)
        {
            challengeLeader = Loc.T(L.Club.ChallengeNoLeader);
            return;
        }

        var name = WinsTicker.NameOf(challenge.Leader);
        challengeLeader = string.Equals(challenge.Kind, CasinoChallengeKinds.First, StringComparison.Ordinal)
            ? Loc.T(L.Club.ChallengeLeads, name,
                CasinoMultiples.Label((int)Math.Min(challenge.LeaderValue * 10, int.MaxValue)))
            : Loc.T(L.Club.ChallengeLeadsCount, name, NumberText.Group(challenge.LeaderValue));
    }

    private void OpenChallenge(Rect source)
    {
        var challenge = floor.LiveChallenge();
        if (challenge is null || challenge.GameKind.Length == 0)
        {
            OpenFame();
            return;
        }

        OpenGame(ClientGameId(challenge.GameKind), source);
    }
}
