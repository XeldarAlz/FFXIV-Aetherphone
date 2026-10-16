using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const int MaxStars = 5;
    private const float StarSize = 34f;
    private const float StarHitPad = 6f;
    private const float FadeStart = 0.35f;
    private const int OutcomePending = 0;
    private const int OutcomeSent = 1;
    private const int OutcomeFailed = 2;

    private PrismStarDto[] stars = Array.Empty<PrismStarDto>();
    private PrismBallotDto? ratingBallot;
    private int lookIndex;
    private float lookSeconds;
    private bool heatDone;
    private volatile int ballotOutcome;
    private Spring lookFade = new(1f);
    private CachedText lookCounter;

    private void StartJudging()
    {
        heatDone = false;
        ratingBallot = null;
        store.RequestBallot();
        router.Push(PrismScreen.Runway);
    }

    private void DrawRunway(Rect body)
    {
        using var surface = AppSurface.Begin(body);
        ConsumeBallotOutcome();
        if (heatDone)
        {
            DrawHeatDone(body);
            return;
        }

        var ballot = store.Ballot;
        if (ballot is null)
        {
            DrawBallotPending(body);
            return;
        }

        var looks = ballot.Looks ?? Array.Empty<PrismLookDto>();
        if (looks.Length == 0)
        {
            EmptyState.Draw(body, ui, PhoneIcons.StarFilled, Loc.T(L.Prism.NothingLeftTitle),
                Loc.T(L.Prism.NothingLeftHint));
            return;
        }

        if (!ReferenceEquals(ratingBallot, ballot))
        {
            ratingBallot = ballot;
            stars = new PrismStarDto[looks.Length];
            lookIndex = 0;
            lookSeconds = 0f;
            lookFade.SnapTo(0f);
        }

        if (lookIndex >= looks.Length)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Common.Loading), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        DrawLookToRate(looks, body);
    }

    private void DrawBallotPending(Rect body)
    {
        if (store.Busy || !store.ActionFailed)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Common.Loading), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        var reason = store.ActionFailureText;
        if (EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Common.LoadFailed),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
        {
            store.RequestBallot();
        }
    }

    private void DrawLookToRate(PrismLookDto[] looks, Rect body)
    {
        var scale = UiScale.Current;
        var delta = ImGui.GetIO().DeltaTime;
        lookSeconds += delta;
        var fade = lookFade.Step(1f, Motion.Appear, delta);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var look = looks[lookIndex];
        var counterHeight = Typography.LineHeight(TextStyles.Footnote);
        var hintHeight = Typography.MeasureWrappedBlock(Loc.T(L.Prism.RateHint), TextStyles.Footnote, width).Y;
        var starRow = (StarSize + StarHitPad * 2f) * scale;
        var gap = Metrics.Space.Md * scale;
        var available = body.Max.Y - origin.Y - counterHeight - starRow - hintHeight - gap * 3f;
        var imageHeight = MathF.Max(available, width * 0.6f);
        var frame = new Rect(origin, new Vector2(origin.X + width, origin.Y + imageHeight));
        DrawLook(drawList, frame, look.ImageUrl, Metrics.Radius.Card * scale, true, FadeStart + (1f - FadeStart) * fade);
        var top = frame.Max.Y + gap;
        Typography.DrawCentered(drawList, new Vector2(frame.Center.X, top + counterHeight * 0.5f),
            LookCounter(lookIndex + 1, looks.Length), ui.MutedInk, TextStyles.Footnote);
        top += counterHeight + gap;
        var picked = DrawStars(drawList, new Vector2(frame.Center.X, top + starRow * 0.5f), scale);
        top += starRow + gap;
        var failure = store.ActionFailed ? store.ActionFailureText : string.Empty;
        top += Typography.DrawWrappedCentered(new Vector2(frame.Center.X, top),
            store.ActionFailed ? (failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint)) : Loc.T(L.Prism.RateHint),
            ui.MutedInk, TextStyles.Footnote, width);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, top - origin.Y));
        if (picked > 0)
        {
            Rate(looks, look, picked);
        }
    }

    private int DrawStars(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var size = StarSize * scale;
        var cell = size + StarHitPad * 2f * scale;
        var left = center.X - cell * MaxStars * 0.5f;
        var hoveredStars = 0;
        Span<bool> hovered = stackalloc bool[MaxStars];
        for (var index = 0; index < MaxStars; index++)
        {
            var min = new Vector2(left + cell * index, center.Y - cell * 0.5f);
            hovered[index] = !store.Busy && UiInteract.Hover(min, min + new Vector2(cell, cell));
            if (hovered[index])
            {
                hoveredStars = index + 1;
            }
        }

        var picked = 0;
        for (var index = 0; index < MaxStars; index++)
        {
            var min = new Vector2(left + cell * index, center.Y - cell * 0.5f);
            var lit = index < hoveredStars;
            PhoneIcon.Draw(drawList, min + new Vector2(cell * 0.5f, cell * 0.5f),
                lit ? PhoneIcons.StarFilled : PhoneIcons.Star, lit ? AccentRing.Gold : ui.MutedInk, size);
            if (hovered[index])
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, min + new Vector2(cell, cell), hovered[index]))
            {
                picked = index + 1;
            }
        }

        return picked;
    }

    private void Rate(PrismLookDto[] looks, PrismLookDto look, int value)
    {
        UiFeedback.Play(UiSound.Tap);
        stars[lookIndex] = new PrismStarDto(look.Id, value, (int)(lookSeconds * 1000f));
        lookIndex++;
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
            return;
        }

        lookIndex = Math.Max(0, stars.Length - 1);
    }

    private void DrawHeatDone(Rect body)
    {
        var canJudge = store.Week is { } week && PrismPhases.Judgeable(week.Phase);
        if (!canJudge)
        {
            EmptyState.Draw(body, ui, PhoneIcons.StarFilled, Loc.T(L.Prism.HeatDoneTitle), Loc.T(L.Prism.HeatDoneHint));
            return;
        }

        if (EmptyState.Draw(body, ui, PhoneIcons.StarFilled, Loc.T(L.Prism.HeatDoneTitle),
                Loc.T(L.Prism.HeatDoneHint), Loc.T(L.Prism.JudgeAnother)))
        {
            heatDone = false;
            ratingBallot = null;
            store.ClearBallot();
            store.RequestBallot();
        }
    }

    private string LookCounter(int current, int total)
    {
        var key = HashCode.Combine(current, total);
        return lookCounter.IsCurrent(key)
            ? lookCounter.Value
            : lookCounter.Store(key, Loc.T(L.Prism.LookCount, current, total));
    }

    private string? CurrentLookId()
    {
        var looks = store.Ballot?.Looks;
        return heatDone || looks is null || lookIndex >= looks.Length ? null : looks[lookIndex].Id;
    }
}
