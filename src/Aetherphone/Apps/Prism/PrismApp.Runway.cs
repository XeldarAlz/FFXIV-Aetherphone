using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float DoneNumeralUnits = 120f;
    private const float DoneNumeralMinUnits = 48f;
    private const float QuestionUnits = 24f;
    private const float ScoreRowUnits = 52f;
    private const float ScoreDone = 0.97f;
    private const float TurnShift = 0.35f;
    private const float TurnIdle = 10f;
    private const float DoneTextDelay = 0.12f;
    private const float InspectZoom = 2.2f;
    private const float InspectHoldSeconds = 0.18f;
    private const string MoreGlyph = "•••";
    private const int OutcomePending = 0;
    private const int OutcomeSent = 1;
    private const int OutcomeFailed = 2;

    private static readonly string[] Padded = BuildDigits("00");
    private static readonly string[] Plain = BuildDigits("0");

    private PrismStarDto[] stars = Array.Empty<PrismStarDto>();
    private PrismBallotDto? ratingBallot;
    private int lookIndex;
    private int chosen;
    private float lookSeconds;
    private bool heatDone;
    private volatile int ballotOutcome;
    private string previousLookUrl = string.Empty;
    private float turnAge = TurnIdle;
    private Spring inspectZoom = new(0f);
    private float inspectHold;
    private Vector2 inspectFocus;
    private Spring scoreFill = new(0f);
    private long heatCoins;
    private CachedText lookCounter;
    private CachedText heatTitle;
    private CachedText doneCounter;
    private CachedText coinCounter;
    private CachedText rewardText;

    private bool HeatInProgress => store.Ballot is { } ballot && ReferenceEquals(ratingBallot, ballot) &&
                                   lookIndex > 0 && lookIndex < stars.Length;

    private void StartJudging()
    {
        heatDone = false;
        chosen = 0;
        if (!HeatInProgress)
        {
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
        }

        Go(PrismScreen.Runway);
    }

    private void DrawRunway(ImDrawListPtr drawList, Rect content, float scale)
    {
        ConsumeBallotOutcome();
        if (heatDone)
        {
            DrawHeatDone(drawList, content, scale);
            return;
        }

        if (store.Ballot is not { } ballot)
        {
            DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
            DrawBallotPending(drawList, content, scale);
            return;
        }

        var looks = ballot.Looks ?? Array.Empty<PrismLookDto>();
        if (looks.Length == 0)
        {
            DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
            DrawNotice(drawList, content, Loc.T(L.Prism.NothingLeftTitle), Loc.T(L.Prism.NothingLeftHint), scale);
            if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.Back), "→", false))
            {
                Go(PrismScreen.Cover);
            }

            return;
        }

        if (!ReferenceEquals(ratingBallot, ballot))
        {
            ratingBallot = ballot;
            stars = new PrismStarDto[looks.Length];
            lookIndex = 0;
            chosen = 0;
            lookSeconds = 0f;
            previousLookUrl = string.Empty;
            turnAge = TurnIdle;
        }

        if (lookIndex >= looks.Length)
        {
            DrawHeader(drawList, content, string.Empty, string.Empty, false, scale);
            DrawCenteredLabel(drawList, content, Loc.T(L.Prism.Submitting));
            return;
        }

        DrawLookToRate(drawList, content, ballot, looks, scale);
    }

    private void DrawLookToRate(ImDrawListPtr drawList, Rect content, PrismBallotDto ballot, PrismLookDto[] looks,
        float scale)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        lookSeconds += delta;
        turnAge += delta;
        var turn = PrismMotion.At(turnAge, 0f, Motion.PageSettle);
        var look = looks[lookIndex];
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var width = content.Width - gutter * 2f;
        var top = DrawRunwayHeader(drawList, content, ballot, looks.Length, look.Id, scale);
        var labelPixels = PrismArt.LabelPixels();
        var labelLine = PrismType.LineHeight(PrismFace.Label, labelPixels);
        var anchorTop = content.Max.Y - PrismArt.BottomMargin(scale) - labelLine;
        var rowBottom = anchorTop - Metrics.Space.Xs * scale;
        var row = new Rect(new Vector2(left, rowBottom - ScoreRowUnits * scale), new Vector2(left + width, rowBottom));
        var submitFailed = store.Failed(PrismAction.Submit);
        var failure = submitFailed ? store.ActionFailureText : string.Empty;
        var prompt = submitFailed
            ? failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint)
            : Loc.T(L.Prism.RateHint);
        var pixels = PrismType.Fit(prompt, PrismFace.Headline, width, QuestionUnits * scale,
            QuestionUnits * scale, 2, out var lines);
        var questionTop = row.Min.Y - Metrics.Space.Xl * scale -
                          lines.Length * PrismType.LineHeight(PrismFace.Headline, pixels);
        var countTop = questionTop - Metrics.Space.Sm * scale - labelLine;
        var photo = new Rect(new Vector2(content.Min.X, top),
            new Vector2(content.Max.X, countTop - Metrics.Space.Xl * scale));
        DrawTurningLook(drawList, photo, look, turn, delta);
        var counter = LookCounter(lookIndex + 1, looks.Length);
        PrismType.Draw(drawList, new Vector2(left, countTop), counter, PrismArt.Muted, PrismFace.Label, labelPixels);
        var aside = lookIndex == 0 && chosen == 0 ? Loc.T(L.Prism.InspectHint) : store.Week?.Title ?? string.Empty;
        var room = width - PrismType.Measure(counter, PrismFace.Label, labelPixels).X - Metrics.Space.Lg * scale;
        PrismType.DrawRight(drawList, new Vector2(left + width, countTop), PrismType.FitLabel(aside, labelPixels, room),
            PrismArt.Muted, PrismFace.Label, labelPixels);

        PrismType.DrawLines(drawList, new Vector2(left, questionTop), lines, PrismArt.Fore, PrismFace.Headline,
            pixels);
        var fill = chosen > 0 ? scoreFill.Step(1f, Motion.Release, delta) : 0f;
        var picked = PrismArt.Score(drawList, row, chosen, fill, !store.Busy, scale);
        if (picked > 0 && picked != chosen)
        {
            chosen = picked;
            scoreFill.SnapTo(0f);
        }

        PrismType.Draw(drawList, new Vector2(left, anchorTop), Loc.T(L.Prism.ScoreLow), PrismArt.ScoreLowInk,
            PrismFace.Label, labelPixels);
        PrismType.DrawRight(drawList, new Vector2(left + width, anchorTop), Loc.T(L.Prism.ScoreHigh),
            PrismArt.ScoreHighInk, PrismFace.Label, labelPixels);

        if (chosen > 0 && fill >= ScoreDone)
        {
            Rate(looks, look, chosen);
        }
    }

    private float DrawRunwayHeader(ImDrawListPtr drawList, Rect content, PrismBallotDto ballot, int total,
        string lookId, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var left = content.Min.X + gutter;
        var right = content.Max.X - gutter;
        var top = content.Min.Y + HeaderGapUnits * scale;
        PrismArt.StoryBars(drawList, left, right, top, total, lookIndex, 1f, scale);
        var row = top + HeaderGapUnits * scale;
        var labelPixels = PrismArt.LabelPixels();
        if (PrismArt.Link(drawList, new Vector2(left, row), Loc.T(L.Prism.Back), PrismArt.Muted, scale))
        {
            Go(PrismScreen.Cover);
        }

        var title = HeatTitle(ballot);
        if (title.Length > 0)
        {
            var titleWidth = PrismType.Measure(title, PrismFace.Label, labelPixels).X;
            PrismType.Draw(drawList, new Vector2(content.Center.X - titleWidth * 0.5f, row), title, PrismArt.Fore,
                PrismFace.Label, labelPixels);
        }

        var moreWidth = PrismType.Measure(MoreGlyph, PrismFace.Label, labelPixels).X;
        if (PrismArt.Link(drawList, new Vector2(right - moreWidth, row), MoreGlyph, PrismArt.Muted, scale))
        {
            OpenReport(lookId);
        }

        return row + PrismType.LineHeight(PrismFace.Label, labelPixels) + HeaderGapUnits * scale;
    }

    private void DrawTurningLook(ImDrawListPtr drawList, Rect photo, PrismLookDto look, float turn, float delta)
    {
        var shift = new Vector2(photo.Width * TurnShift, 0f);
        drawList.PushClipRect(photo.Min, photo.Max, true);
        if (turn < PrismMotion.Settled && previousLookUrl.Length > 0)
        {
            DrawLook(drawList, new Rect(photo.Min - shift * turn, photo.Max - shift * turn), previousLookUrl, true,
                1f - turn);
        }

        var holding = turn >= PrismMotion.Settled && ImGui.IsMouseDown(ImGuiMouseButton.Left) &&
                      UiInteract.Hover(photo.Min, photo.Max);
        inspectHold = holding ? inspectHold + delta : 0f;
        if (inspectHold >= InspectHoldSeconds)
        {
            inspectFocus = Vector2.Clamp(ImGui.GetMousePos(), photo.Min, photo.Max);
        }

        var inspect = inspectZoom.Step(inspectHold >= InspectHoldSeconds ? 1f : 0f, Motion.PageSettle, delta);
        var incoming = shift * (1f - turn);
        DrawLook(drawList, new Rect(photo.Min + incoming, photo.Max + incoming), look.ImageUrl, true, turn,
            1f + (InspectZoom - 1f) * inspect, inspectFocus);
        drawList.PopClipRect();
    }
    private void Rate(PrismLookDto[] looks, PrismLookDto look, int value)
    {
        stars[lookIndex] = new PrismStarDto(look.Id, value, (int)(lookSeconds * 1000f));
        lookIndex++;
        chosen = 0;
        lookSeconds = 0f;
        previousLookUrl = look.ImageUrl;
        turnAge = 0f;
        if (lookIndex < looks.Length)
        {
            return;
        }

        ballotOutcome = OutcomePending;
        heatCoins = 0;
        store.SubmitBallot(stars, ballotSettled);
    }

    private void SettleBallot(bool succeeded)
    {
        ballotOutcome = succeeded ? OutcomeSent : OutcomeFailed;
    }

    private void ConsumeBallotOutcome()
    {
        var outcome = ballotOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        ballotOutcome = OutcomePending;
        if (outcome == OutcomeSent)
        {
            heatCoins = store.LastBallotCoins;
            heatDone = true;
            UiFeedback.Play(heatCoins > 0 ? UiSound.Payout : UiSound.Success);
            Go(PrismScreen.Runway, 0);
            return;
        }

        lookIndex = Math.Max(0, stars.Length - 1);
    }

    private void DrawBallotPending(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (store.Busy || !store.Failed(PrismAction.Ballot))
        {
            DrawCenteredLabel(drawList, content, Loc.T(L.Common.Loading));
            return;
        }

        var reason = store.ActionFailureText;
        DrawNotice(drawList, content, Loc.T(L.Common.LoadFailed),
            reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), scale);
        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Common.Retry), "→", true))
        {
            store.RequestBallot();
        }
    }

    private void DrawHeatDone(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var canJudge = store.Week is { } week && PrismPhases.Judgeable(week.Phase);
        var strips = canJudge ? 2 : 1;
        var notice = PrismMotion.Mark(drawList);
        DrawNotice(drawList, content, Loc.T(L.Prism.HeatDoneTitle), Loc.T(L.Prism.HeatDoneHint), scale, strips);
        PrismMotion.Rise(drawList, notice, PrismMotion.At(screenAge, DoneTextDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var count = heatCoins > 0 ? CoinCounter(heatCoins) : DoneCounter(stars.Length);
        var pixels = PrismType.Fit(count, PrismFace.Numerals, width, DoneNumeralUnits * scale,
            DoneNumeralMinUnits * scale, 1, out _);
        var reveal = PrismMotion.At(screenAge, 0f, Motion.Sheet);
        var left = content.Min.X + gutter;
        DrawWipe(drawList, new Vector2(left, top), count, PrismFace.Numerals, pixels, reveal, PrismArt.Fore);
        var cursor = top + PrismType.Snap(PrismFace.Numerals, pixels);
        if (heatCoins > 0)
        {
            var labelPixels = PrismArt.LabelPixels();
            DrawWipe(drawList, new Vector2(left, cursor), Loc.T(L.Prism.JudgingCoinLabel), PrismFace.Label,
                labelPixels, reveal, PrismArt.Muted);
            cursor += PrismType.LineHeight(PrismFace.Label, labelPixels);
        }

        var reward = RewardLine(store.Week?.JudgeReward);
        if (reward.Length > 0)
        {
            var rewardMark = PrismMotion.Mark(drawList);
            Typography.DrawWrappedLeft(new Vector2(left, cursor + Metrics.Space.Sm * scale), reward, PrismArt.Muted,
                TextStyles.Subheadline, width);
            PrismMotion.Rise(drawList, rewardMark, PrismMotion.At(screenAge, DoneTextDelay, Motion.PageSettle),
                PrismMotion.RiseUnits * scale);
        }

        var strip = PrismMotion.Mark(drawList);
        if (canJudge && PrismArt.Strip(drawList, StripRect(content, scale, 1), Loc.T(L.Prism.JudgeAnother),
                "→", true))
        {
            heatDone = false;
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
            Go(PrismScreen.Runway);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), Loc.T(L.Prism.Done), string.Empty,
                strips == 1))
        {
            Go(PrismScreen.Cover, -1);
        }

        PrismMotion.Rise(drawList, strip, PrismMotion.At(screenAge, DoneTextDelay + PrismMotion.Stagger,
            Motion.PageSettle), PrismMotion.RiseUnits * scale);
    }

    private void DrawCenteredLabel(ImDrawListPtr drawList, Rect content, string text)
    {
        var upper = text;
        var labelPixels = PrismArt.LabelPixels();
        var size = PrismType.Measure(upper, PrismFace.Label, labelPixels);
        PrismType.Draw(drawList, content.Center - size * 0.5f, upper, PrismArt.Muted, PrismFace.Label, labelPixels);
    }

    private string LookCounter(int current, int total)
    {
        var key = HashCode.Combine(current, total, Loc.Current.Code);
        return lookCounter.IsCurrent(key)
            ? lookCounter.Value
            : lookCounter.Store(key, Loc.T(L.Prism.LookCount, Plain[Math.Min(current, Plain.Length - 1)],
                Plain[Math.Min(total, Plain.Length - 1)]));
    }

    private string CoinCounter(long coins)
    {
        var key = HashCode.Combine(coins, Loc.Current.Code);
        return coinCounter.IsCurrent(key)
            ? coinCounter.Value
            : coinCounter.Store(key, string.Concat("+", coins.ToString("N0", Loc.Culture)));
    }

    private string RewardLine(PrismJudgeRewardDto? reward)
    {
        var line = PrismRewards.LineFor(reward);
        var amount = reward?.Amount ?? 0;
        var left = reward?.LeftThisWeek ?? 0;
        var key = HashCode.Combine(line, amount, left, Loc.Current.Code);
        if (rewardText.IsCurrent(key))
        {
            return rewardText.Value;
        }

        var coins = amount.ToString("N0", Loc.Culture);
        var text = line switch
        {
            PrismRewardLine.Each => Loc.T(L.Prism.JudgeRewardEach, coins),
            PrismRewardLine.Left => Loc.T(L.Prism.JudgeRewardLeft, coins, left.ToString("N0", Loc.Culture)),
            PrismRewardLine.Spent => Loc.T(L.Prism.JudgeRewardSpent),
            _ => string.Empty,
        };
        return rewardText.Store(key, text);
    }

    private string DoneCounter(int total)
    {
        var key = HashCode.Combine(total);
        var number = Padded[Math.Min(total, Padded.Length - 1)];
        return doneCounter.IsCurrent(key) ? doneCounter.Value : doneCounter.Store(key, number + "/" + number);
    }

    private string HeatTitle(PrismBallotDto ballot)
    {
        var key = HashCode.Combine(ballot.Stage, ballot.HeatLabel, Loc.Current.Code);
        if (heatTitle.IsCurrent(key))
        {
            return heatTitle.Value;
        }

        var text = ballot.Stage == PrismPhases.StageFinal ? Loc.T(L.Prism.FinalTitle)
            : ballot.HeatLabel.Length > 0 ? Loc.T(L.Prism.HeatName, ballot.HeatLabel) : string.Empty;
        return heatTitle.Store(key, text);
    }

    private static string[] BuildDigits(string format)
    {
        var digits = new string[100];
        for (var index = 0; index < digits.Length; index++)
        {
            digits[index] = index.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        return digits;
    }
}
