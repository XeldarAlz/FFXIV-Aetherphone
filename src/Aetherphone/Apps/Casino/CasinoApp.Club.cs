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

    private readonly MissionsCard missionsCard = new();
    private readonly ClubCapsule clubCapsule = new();
    private readonly ClubSheet clubSheet = new();
    private readonly FamePodium famePodium = new();
    private readonly FameView fameView;
    private string challengeTextId = string.Empty;
    private LanguageInfo? challengeLanguage;
    private string challengeLeader = string.Empty;
    private string challengeReward = string.Empty;
    private long challengeLeaderValue = -1;

    private float DrawFloorExtras(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var top = origin.Y;
        var missions = floor.Missions;
        if (casino.HasFeature(CasinoFeatures.Missions) && MissionsCard.Count(missions) > 0)
        {
            var tapped = missionsCard.Draw(drawList, ui, missions!, new Vector2(origin.X, top), width,
                floor.ClaimingMission, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), scale, out var bottom);
            if (tapped.Length > 0)
            {
                floor.ClaimMission(tapped);
            }

            top = bottom + CardGap * scale;
        }

        if (casino.HasFeature(CasinoFeatures.Club) && casino.Club is { } club)
        {
            if (clubCapsule.Draw(drawList, ui, club, new Vector2(origin.X, top), width, scale, out var bottom))
            {
                clubSheet.Open(club.Tier);
            }

            top = bottom + CardGap * scale;
        }

        return top;
    }

    private float DrawFloorTail(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        if (!casino.HasFeature(CasinoFeatures.Fame))
        {
            return origin.Y;
        }

        floor.EnsureFame(CasinoFameBoards.Profit, CasinoFameSpans.Week);
        var board = floor.Fame(CasinoFameBoards.Profit, CasinoFameSpans.Week);
        if (board?.Entries is not { Length: > 0 } && board?.Champion is null)
        {
            return origin.Y;
        }

        if (famePodium.Draw(drawList, ui, board!, origin, width, scale, out var bottom))
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

        fameView.Enter();
        routes.Push(new CasinoRoute(CasinoScreen.Fame));
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
                bonusShelf.Shower(missionsCard.ClaimCenter, UiScale.Current);
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
        var top = card.Min.Y + pad * 1.4f;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, top),
            Typography.FitText(Loc.T(L.Club.ChallengeEyebrow), width, TextStyles.FootnoteEmphasized), CasinoColors.LightB,
            TextStyles.FootnoteEmphasized);
        top += Typography.LineHeight(TextStyles.FootnoteEmphasized);
        Marquee.DrawLeftAuto(drawList, "casino.hero.challenge", challenge.Title, card.Min.X + pad, top, width,
            TextStyles.Title2, CasinoColors.InkTitle);
        top += Typography.LineHeight(TextStyles.Title2) + Metrics.Space.Sm * scale;
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, top),
            Typography.FitText(challengeLeader, width, TextStyles.Subheadline), CasinoColors.InkTitle,
            TextStyles.Subheadline);
        top += Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(card.Min.X + pad, top),
            Typography.FitText(challengeReward, width, TextStyles.SubheadlineEmphasized), CasinoColors.Money,
            TextStyles.SubheadlineEmphasized);
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
