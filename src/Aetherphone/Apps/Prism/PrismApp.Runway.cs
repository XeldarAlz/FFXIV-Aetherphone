using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float LookNumeralUnits = 96f;
    private const float DoneNumeralUnits = 120f;
    private const float DoneNumeralMinUnits = 48f;
    private const float CircleDone = 0.97f;
    private const float NumeralShadowUnits = 2f;
    private const float NumeralOverlap = 0.55f;
    private const float NumeralRoll = 0.6f;
    private const float TurnShift = 0.35f;
    private const float TurnIdle = 10f;
    private const float DoneTextDelay = 0.12f;
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
    private string previousLookUrl = string.Empty;
    private float turnAge = TurnIdle;
    private Spring progressLine = new(0f);
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
            previousLookUrl = string.Empty;
            turnAge = TurnIdle;
            progressLine.SnapTo(0f);
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
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        lookSeconds += delta;
        turnAge += delta;
        var turn = PrismMotion.At(turnAge, 0f, Motion.PageSettle);
        var look = looks[lookIndex];
        var gutter = PrismArt.Gutter(scale);
        var top = DrawRunwayHeader(drawList, content, ballot, looks.Length, look.Id, scale, delta);
        var labelHeight = PrismArt.LabelHeight(scale);
        var label = new Rect(new Vector2(content.Min.X + gutter, content.Max.Y - gutter - labelHeight),
            new Vector2(content.Max.X - gutter, content.Max.Y - gutter));
        var numeralPixels = LookNumeralUnits * scale;
        var photoBottom = label.Min.Y - Metrics.Space.Lg * scale - numeralPixels * (1f - NumeralOverlap);
        var photo = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, photoBottom));
        DrawTurningLook(drawList, photo, look, turn);
        DrawLookNumeral(drawList, new Vector2(photo.Min.X + gutter, photo.Max.Y - numeralPixels * NumeralOverlap),
            numeralPixels, turn, scale);

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
        string lookId, float scale, float delta)
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
        var filled = progressLine.Step((lookIndex + 1f) / total, Motion.PageSettle, delta);
        PrismArt.Hairline(drawList, content.Min.X + gutter, content.Min.X + gutter + span * filled, rule,
            PrismArt.White, scale);
        return rule + HeaderGapUnits * scale;
    }

    private void DrawTurningLook(ImDrawListPtr drawList, Rect photo, PrismLookDto look, float turn)
    {
        var shift = new Vector2(photo.Width * TurnShift, 0f);
        drawList.PushClipRect(photo.Min, photo.Max, true);
        if (turn < PrismMotion.Settled && previousLookUrl.Length > 0)
        {
            DrawLook(drawList, new Rect(photo.Min - shift * turn, photo.Max - shift * turn), previousLookUrl, true,
                1f - turn);
        }

        var incoming = shift * (1f - turn);
        DrawLook(drawList, new Rect(photo.Min + incoming, photo.Max + incoming), look.ImageUrl, true, turn);
        drawList.PopClipRect();
    }

    private void DrawLookNumeral(ImDrawListPtr drawList, Vector2 position, float pixels, float turn, float scale)
    {
        var current = Padded[Math.Min(lookIndex + 1, Padded.Length - 1)];
        var width = PrismType.Measure(Padded[Padded.Length - 1], EditorialFace.Numerals, pixels).X;
        drawList.PushClipRect(position, position + new Vector2(width * 1.5f, pixels), true);
        var roll = pixels * NumeralRoll;
        if (turn < PrismMotion.Settled && lookIndex > 0)
        {
            DrawShadowedNumeral(drawList, position - new Vector2(0f, roll * turn), Padded[lookIndex], 1f - turn,
                pixels, scale);
        }

        DrawShadowedNumeral(drawList, position + new Vector2(0f, roll * (1f - turn)), current, turn, pixels, scale);
        drawList.PopClipRect();
    }

    private static void DrawShadowedNumeral(ImDrawListPtr drawList, Vector2 position, string numeral, float alpha,
        float pixels, float scale)
    {
        var shadow = NumeralShadowUnits * scale;
        PrismType.Draw(drawList, position + new Vector2(shadow, shadow), numeral,
            Palette.WithAlpha(NumeralShadow, NumeralShadow.W * alpha), EditorialFace.Numerals, pixels);
        PrismType.Draw(drawList, position, numeral, Palette.WithAlpha(PrismArt.White, alpha), EditorialFace.Numerals,
            pixels);
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
            Go(PrismScreen.Runway, 0);
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
        var notice = PrismMotion.Mark(drawList);
        DrawNotice(drawList, content, Loc.T(L.Prism.HeatDoneTitle), Loc.T(L.Prism.HeatDoneHint), scale, strips);
        PrismMotion.Rise(drawList, notice, PrismMotion.At(screenAge, DoneTextDelay, Motion.PageSettle),
            PrismMotion.RiseUnits * scale);
        var gutter = PrismArt.Gutter(scale);
        var count = DoneCounter(stars.Length);
        var pixels = PrismType.Fit(count, EditorialFace.Numerals, content.Width - gutter * 2f,
            DoneNumeralUnits * scale, DoneNumeralMinUnits * scale, 1, out _);
        DrawWipe(drawList, new Vector2(content.Min.X + gutter, top), count, EditorialFace.Numerals, pixels,
            PrismMotion.At(screenAge, 0f, Motion.Sheet), PrismArt.White);
        var strip = PrismMotion.Mark(drawList);
        if (canJudge && PrismArt.Strip(drawList, StripRect(content, scale, 1), PrismType.Upper(Loc.T(L.Prism.JudgeAnother)),
                "→", true))
        {
            heatDone = false;
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
            Go(PrismScreen.Runway);
        }

        if (PrismArt.Strip(drawList, StripRect(content, scale), PrismType.Upper(Loc.T(L.Prism.Back)), string.Empty,
                strips == 1))
        {
            Go(PrismScreen.Cover, -1);
        }

        PrismMotion.Rise(drawList, strip, PrismMotion.At(screenAge, DoneTextDelay + PrismMotion.Stagger,
            Motion.PageSettle), PrismMotion.RiseUnits * scale);
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
