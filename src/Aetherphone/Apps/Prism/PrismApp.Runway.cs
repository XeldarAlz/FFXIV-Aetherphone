using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float LookNumeralUnits = 96f;
    private const float DoneNumeralUnits = 120f;
    private const float FadeStart = 0.35f;
    private const float CircleDone = 0.97f;
    private const float NumeralShadowUnits = 2f;
    private const float NumeralOverlap = 0.55f;
    private const int OutcomePending = 0;
    private const int OutcomeSent = 1;
    private const int OutcomeFailed = 2;

    private static readonly Vector4 NumeralShadow = new(0f, 0f, 0f, 0.55f);
    private static readonly string[] Padded = BuildDigits("00");
    private static readonly string[] Plain = BuildDigits("0");

    private PrismStarDto[] stars = Array.Empty<PrismStarDto>();
    private PrismBallotDto? ratingBallot;
    private int lookIndex;
    private int chosen;
    private float lookSeconds;
    private bool heatDone;
    private volatile int ballotOutcome;
    private Spring lookFade = new(1f);
    private Spring circle = new(0f);
    private CachedText lookCounter;
    private CachedText doneCounter;

    private void StartJudging()
    {
        heatDone = false;
        ratingBallot = null;
        chosen = 0;
        store.RequestBallot();
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
            if (PrismArt.Strip(drawList, StripRect(content, scale), PrismType.Upper(Loc.T(L.Prism.Back)), "→", false))
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
            lookFade.SnapTo(0f);
        }

        if (lookIndex >= looks.Length)
        {
            DrawHeader(drawList, content, string.Empty, string.Empty, false, scale);
            DrawCenteredMono(drawList, content, Loc.T(L.Prism.Submitting));
            return;
        }

        DrawLookToRate(drawList, content, ballot, looks, scale);
    }

    private void DrawLookToRate(ImDrawListPtr drawList, Rect content, PrismBallotDto ballot, PrismLookDto[] looks,
        float scale)
    {
        var delta = ImGui.GetIO().DeltaTime;
        lookSeconds += delta;
        var fade = lookFade.Step(1f, Motion.Appear, delta);
        var look = looks[lookIndex];
        var gutter = PrismArt.Gutter(scale);
        var top = DrawRunwayHeader(drawList, content, ballot, looks.Length, look.Id, scale);
        var labelHeight = PrismArt.LabelHeight(scale);
        var label = new Rect(new Vector2(content.Min.X + gutter, content.Max.Y - gutter - labelHeight),
            new Vector2(content.Max.X - gutter, content.Max.Y - gutter));
        var photo = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, label.Min.Y - Metrics.Space.Lg * scale));
        DrawLook(drawList, photo, look.ImageUrl, true, FadeStart + (1f - FadeStart) * fade);
        DrawLookNumeral(drawList, photo, Padded[Math.Min(lookIndex + 1, Padded.Length - 1)], gutter, scale);

        var progress = chosen > 0 ? circle.Step(1f, Motion.Release, delta) : 0f;
        var failure = store.ActionFailed ? store.ActionFailureText : string.Empty;
        var prompt = store.ActionFailed
            ? PrismType.Upper(failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint))
            : PrismType.Upper(Loc.T(L.Prism.RateHint));
        var picked = PrismArt.Label(drawList, label, prompt, chosen, progress, chosen == 0 && !store.Busy, scale);
        if (picked > 0)
        {
            chosen = picked;
            circle.SnapTo(0f);
        }

        if (chosen > 0 && progress >= CircleDone)
        {
            Rate(looks, look, chosen);
        }
    }

    private float DrawRunwayHeader(ImDrawListPtr drawList, Rect content, PrismBallotDto ballot, int total,
        string lookId, float scale)
    {
        var gutter = PrismArt.Gutter(scale);
        var top = content.Min.Y + HeaderGapUnits * scale;
        var mono = PrismArt.MonoPixels();
        if (PrismArt.Link(drawList, new Vector2(content.Min.X + gutter, top), PrismType.Upper(Loc.T(L.Prism.Back)),
                PrismArt.Muted, scale))
        {
            Go(PrismScreen.Cover);
        }

        var counter = LookCounter(lookIndex + 1, total, ballot);
        var counterWidth = PrismType.Measure(counter, EditorialFace.Mono, mono).X;
        PrismType.Draw(drawList, new Vector2(content.Center.X - counterWidth * 0.5f, top), counter, PrismArt.White,
            EditorialFace.Mono, mono);
        var reportLabel = PrismType.Upper(Loc.T(L.Prism.Report));
        var reportWidth = PrismType.Measure(reportLabel, EditorialFace.Mono, mono).X;
        if (PrismArt.Link(drawList, new Vector2(content.Max.X - gutter - reportWidth, top), reportLabel,
                PrismArt.Muted, scale))
        {
            OpenReport(lookId);
        }

        var rule = top + PrismType.LineHeight(EditorialFace.Mono, mono) + HeaderGapUnits * 0.5f * scale;
        var span = content.Width - gutter * 2f;
        PrismArt.Hairline(drawList, content.Min.X + gutter, content.Max.X - gutter, rule, PrismArt.Faint, scale);
        PrismArt.Hairline(drawList, content.Min.X + gutter, content.Min.X + gutter + span * (lookIndex + 1) / total,
            rule, PrismArt.White, scale);
        return rule + HeaderGapUnits * scale;
    }

    private static void DrawLookNumeral(ImDrawListPtr drawList, Rect photo, string numeral, float gutter, float scale)
    {
        var pixels = LookNumeralUnits * scale;
        var position = new Vector2(photo.Min.X + gutter, photo.Max.Y - pixels * NumeralOverlap);
        var shadow = NumeralShadowUnits * scale;
        PrismType.Draw(drawList, position + new Vector2(shadow, shadow), numeral, NumeralShadow,
            EditorialFace.Numerals, pixels);
        PrismType.Draw(drawList, position, numeral, PrismArt.White, EditorialFace.Numerals, pixels);
    }

    private void Rate(PrismLookDto[] looks, PrismLookDto look, int value)
    {
        stars[lookIndex] = new PrismStarDto(look.Id, value, (int)(lookSeconds * 1000f));
        lookIndex++;
        chosen = 0;
        lookSeconds = 0f;
        lookFade.SnapTo(0f);
        if (lookIndex < looks.Length)
        {
            return;
        }

        ballotOutcome = OutcomePending;
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
            heatDone = true;
            screenFade.SnapTo(0f);
            return;
        }

        lookIndex = Math.Max(0, stars.Length - 1);
    }

    private void DrawBallotPending(ImDrawListPtr drawList, Rect content, float scale)
    {
        if (store.Busy || !store.ActionFailed)
        {
            DrawCenteredMono(drawList, content, Loc.T(L.Common.Loading));
            return;
        }

        var reason = store.ActionFailureText;
        DrawNotice(drawList, content, Loc.T(L.Common.LoadFailed),
            reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), scale);
        if (PrismArt.Strip(drawList, StripRect(content, scale), PrismType.Upper(Loc.T(L.Common.Retry)), "→", true))
        {
            store.RequestBallot();
        }
    }

    private void DrawHeatDone(ImDrawListPtr drawList, Rect content, float scale)
    {
        var top = DrawHeader(drawList, content, string.Empty, string.Empty, true, scale);
        var canJudge = store.Week is { } week && PrismPhases.Judgeable(week.Phase);
        var strips = canJudge ? 2 : 1;
        DrawNotice(drawList, content, Loc.T(L.Prism.HeatDoneTitle), Loc.T(L.Prism.HeatDoneHint), scale, strips);
        PrismType.Draw(drawList, new Vector2(content.Min.X + PrismArt.Gutter(scale), top), DoneCounter(stars.Length),
            PrismArt.White, EditorialFace.Numerals, DoneNumeralUnits * scale);
        if (canJudge && PrismArt.Strip(drawList, StripRect(content, scale, 1), PrismType.Upper(Loc.T(L.Prism.JudgeAnother)),
                "→", true))
        {
            heatDone = false;
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
            screenFade.SnapTo(0f);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), PrismType.Upper(Loc.T(L.Prism.Back)), string.Empty,
                strips == 1))
        {
            Go(PrismScreen.Cover);
        }
    }

    private void DrawCenteredMono(ImDrawListPtr drawList, Rect content, string text)
    {
        var upper = PrismType.Upper(text);
        var mono = PrismArt.MonoPixels();
        var size = PrismType.Measure(upper, EditorialFace.Mono, mono);
        PrismType.Draw(drawList, content.Center - size * 0.5f, upper, PrismArt.Muted, EditorialFace.Mono, mono);
    }

    private string LookCounter(int current, int total, PrismBallotDto ballot)
    {
        var key = HashCode.Combine(current, total, ballot.Stage, ballot.HeatLabel, Loc.Current.Code);
        if (lookCounter.IsCurrent(key))
        {
            return lookCounter.Value;
        }

        var counter = PrismType.Upper(Loc.T(L.Prism.LookCount, Padded[Math.Min(current, Padded.Length - 1)],
            Padded[Math.Min(total, Padded.Length - 1)]));
        var stage = HeatTitle(ballot);
        return lookCounter.Store(key, stage.Length > 0 ? counter + " · " + stage : counter);
    }

    private string DoneCounter(int total)
    {
        var key = HashCode.Combine(total);
        var number = Padded[Math.Min(total, Padded.Length - 1)];
        return doneCounter.IsCurrent(key) ? doneCounter.Value : doneCounter.Store(key, number + "/" + number);
    }

    private static string HeatTitle(PrismBallotDto ballot)
    {
        var text = ballot.Stage == PrismPhases.StageFinal ? Loc.T(L.Prism.FinalTitle)
            : ballot.HeatLabel.Length > 0 ? Loc.T(L.Prism.HeatName, ballot.HeatLabel) : string.Empty;
        return PrismType.Upper(text);
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
