using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Polls;

internal sealed partial class PollsApp
{
    private const float MetaGap = 12f;
    private const float QuestionGap = 14f;
    private const float OptionGap = 8f;
    private const float OptionPadY = 11f;
    private const float OptionInsetX = 14f;
    private const float RadioGap = 10f;
    private const float PercentGap = 10f;
    private const float LeaderIconSize = 13f;
    private const float LeaderIconGap = 6f;
    private const float FooterGap = 14f;
    private const float FooterHitHeight = 32f;
    private const float UndoPadX = 10f;
    private const float RevealStaggerSeconds = 0.05f;
    private const float FailureSeconds = 4f;
    private const float FailureFadeSeconds = 0.4f;
    private const float RevealEpsilon = 0.01f;
    private const float TrackAlphaOpen = 0.08f;
    private const float TrackAlphaResults = 0.05f;
    private const float StrokeAlphaOpen = 0.10f;
    private const float FillAlphaMine = 0.92f;
    private const float FillAlphaWinner = 0.70f;
    private const float FillAlphaLeader = 0.20f;
    private const float FillAlphaOther = 0.13f;
    private const float RingAlphaResults = 0.35f;

    private float[] rowHeights = Array.Empty<float>();

    private void DrawCard(PollDto poll, float width, float scale, long nowUnix, bool isTourCard)
    {
        var text = TextFor(poll, nowUnix);
        var origin = ImGui.GetCursorScreenPos();
        var pad = Metrics.Space.Lg * scale;
        var innerLeft = origin.X + pad;
        var innerRight = origin.X + width - pad;
        var innerWidth = innerRight - innerLeft;

        var chipCenterY = origin.Y + pad + PollsArt.ChipHeight * scale * 0.5f;
        var questionTop = origin.Y + pad + (PollsArt.ChipHeight + MetaGap) * scale;
        var questionHeight = BlockHeight(text.Question, TextStyles.Title3, innerWidth);
        var optionsTop = questionTop + questionHeight + QuestionGap * scale;
        var percentReserve = Typography.Measure(WidestPercent(), TextStyles.SubheadlineEmphasized).X
                             + (LeaderIconSize + LeaderIconGap + PercentGap) * scale;
        var labelOffset = (OptionInsetX + PollsArt.RadioRadius * 2f + RadioGap) * scale;
        var labelWidth = MathF.Max(1f, innerWidth - labelOffset - percentReserve - OptionInsetX * scale);

        var heights = EnsureRowBuffer(poll.Options.Length);
        var optionsHeight = 0f;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            var labelHeight = BlockHeight(text.Options[index], TextStyles.BodyEmphasized, labelWidth);
            heights[index] = MathF.Max(Metrics.Size.TapTarget * scale, labelHeight + OptionPadY * 2f * scale);
            optionsHeight += heights[index] + (index > 0 ? OptionGap * scale : 0f);
        }

        var footerTop = optionsTop + optionsHeight + FooterGap * scale;
        var footerHeight = Typography.LineHeight(TextStyles.Footnote);
        var cardMax = new Vector2(origin.X + width, footerTop + footerHeight + pad);
        var motion = MotionFor(poll);

        if (isTourCard && UiAnchors.Recording)
        {
            UiAnchors.Report("polls.card", new Rect(origin, cardMax));
            UiAnchors.Report("polls.options", new Rect(new Vector2(innerLeft, optionsTop),
                new Vector2(innerRight, optionsTop + optionsHeight)));
            UiAnchors.Report("polls.footer", new Rect(new Vector2(innerLeft, footerTop - Metrics.Space.Xs * scale),
                new Vector2(innerRight, footerTop + footerHeight + Metrics.Space.Xs * scale)));
        }

        if (!ImGui.IsRectVisible(origin, cardMax))
        {
            motion.Primed = false;
            Advance(origin, width, cardMax.Y - origin.Y, scale);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Widget * scale, true);
        DrawMeta(drawList, poll, text, innerLeft, innerRight, chipCenterY, scale, nowUnix);
        Typography.DrawWrappedLeft(new Vector2(innerLeft, questionTop), text.Question, ui.TitleInk,
            TextStyles.Title3, innerWidth);

        var deltaSeconds = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        var resultsVisible = PollRules.ResultsVisible(poll, nowUnix);
        var reveal = StepReveal(poll, text, motion, resultsVisible, deltaSeconds);

        var rowTop = optionsTop;
        for (var index = 0; index < poll.Options.Length; index++)
        {
            var row = new Rect(new Vector2(innerLeft, rowTop), new Vector2(innerRight, rowTop + heights[index]));
            DrawOption(drawList, poll, text, motion, index, row, labelOffset, labelWidth, reveal, deltaSeconds,
                scale);
            rowTop += heights[index] + OptionGap * scale;
        }

        DrawFooter(drawList, poll, text, innerLeft, innerRight, footerTop, footerHeight, scale);
        Advance(origin, width, cardMax.Y - origin.Y, scale);
    }

    private static void Advance(Vector2 origin, float width, float height, float scale)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
    }

    private void DrawMeta(ImDrawListPtr drawList, PollDto poll, PollText text, float left, float right,
        float centerY, float scale, long nowUnix)
    {
        var chipInk = text.Status switch
        {
            PollStatus.EndingSoon => PollsArt.EndingSoonInk,
            PollStatus.Ended => ui.MutedInk,
            _ => ui.Accent,
        };
        var chipIcon = text.Status == PollStatus.Ended ? FontAwesomeIcon.Flag : FontAwesomeIcon.HourglassHalf;
        var chipWidth = text.Status == PollStatus.Open
            ? 0f
            : PollsArt.Chip(drawList, new Vector2(left, centerY), text.StatusLabel, chipInk, chipIcon, scale)
              + Metrics.Space.Sm * scale;

        var needsVote = PollRules.NeedsVote(poll, nowUnix);
        var markLabel = needsVote ? Loc.T(L.Polls.NeedsVote) : string.Empty;
        var markWidth = needsVote ? PollsArt.MarkWidth(markLabel, scale) + Metrics.Space.Sm * scale : 0f;
        if (needsVote)
        {
            PollsArt.NeedsVoteMark(drawList, new Vector2(right, centerY), markLabel, ui.Accent, scale);
        }

        if (!PollRules.ResultsVisible(poll, nowUnix))
        {
            return;
        }

        var votesLeft = left + chipWidth;
        var available = right - markWidth - votesLeft;
        if (available <= 0f)
        {
            return;
        }

        var fitted = Typography.FitText(text.VotesLabel, available, TextStyles.Footnote);
        var votesHeight = Typography.Measure(fitted, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(votesLeft, centerY - votesHeight * 0.5f), fitted, ui.MutedInk,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
    }

    private float StepReveal(PollDto poll, PollText text, PollMotion motion, bool resultsVisible, float deltaSeconds)
    {
        var target = resultsVisible ? 1f : 0f;
        if (!motion.Primed)
        {
            motion.Primed = true;
            motion.Reveal.SnapTo(target);
            motion.RevealAge = resultsVisible ? float.MaxValue : 0f;
            for (var index = 0; index < motion.Fills.Length; index++)
            {
                motion.Fills[index].SnapTo(resultsVisible ? text.Fractions[index] : 0f);
                motion.Selection[index].SnapTo(poll.MyVote == index ? 1f : 0f);
            }
        }

        if (resultsVisible && motion.Reveal.Value <= RevealEpsilon && motion.RevealAge >= float.MaxValue)
        {
            motion.RevealAge = 0f;
        }

        if (!resultsVisible)
        {
            motion.RevealAge = float.MaxValue;
        }
        else if (motion.RevealAge < float.MaxValue)
        {
            motion.RevealAge += deltaSeconds;
        }

        return motion.Reveal.Step(target, Motion.Appear, deltaSeconds);
    }

    private void DrawOption(ImDrawListPtr drawList, PollDto poll, PollText text, PollMotion motion, int optionIndex,
        Rect row, float labelOffset, float labelWidth, float reveal, float deltaSeconds, float scale)
    {
        var closed = text.Closed;
        var mine = poll.MyVote == optionIndex;
        var leader = text.Leaders[optionIndex] && poll.TotalVotes > 0;
        var hovered = !closed && UiInteract.Hover(row.Min, row.Max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(text.PressIds[optionIndex], pressed, PressFx.CardPressedScale);
        var half = row.Size * 0.5f * press;
        var min = row.Center - half;
        var max = row.Center + half;
        var radius = MathF.Min((max.Y - min.Y) * 0.5f, Metrics.Size.TapTarget * scale * 0.5f);

        var trackAlpha = TrackAlphaOpen + (TrackAlphaResults - TrackAlphaOpen) * reveal;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(PollsArt.White, trackAlpha)));
        if (reveal < 1f)
        {
            Squircle.Stroke(drawList, min, max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(PollsArt.White, StrokeAlphaOpen * (1f - reveal))), 1f);
        }

        var selection = motion.Selection[optionIndex].Step(mine ? 1f : 0f, Motion.Release, deltaSeconds);
        var fillTarget = reveal > RevealEpsilon && motion.RevealAge >= optionIndex * RevealStaggerSeconds
            ? text.Fractions[optionIndex]
            : 0f;
        var fill = motion.Fills[optionIndex].Step(fillTarget, Motion.Sheet, deltaSeconds);
        if (fill > 0.001f)
        {
            var fillColor = FillColor(closed, leader, selection);
            var fillRight = min.X + (max.X - min.X) * MathF.Min(1f, fill);
            drawList.PushClipRect(min, new Vector2(fillRight, max.Y), true);
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(fillColor with { W = fillColor.W * reveal }));
            drawList.PopClipRect();
        }

        if (hovered)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var emphasized = mine || (closed && leader);
        var style = emphasized ? TextStyles.BodyEmphasized : TextStyles.Body;
        var label = text.Options[optionIndex];
        var labelHeight = BlockHeight(label, style, labelWidth);
        var labelTop = row.Center.Y - labelHeight * 0.5f;
        var firstLineCenterY = labelTop + Typography.Measure(label, style).Y * 0.5f;
        var radioCenter = new Vector2(row.Min.X + (OptionInsetX + PollsArt.RadioRadius) * scale, firstLineCenterY);
        var ringAlpha = 1f + (RingAlphaResults - 1f) * reveal;
        if (!closed || mine)
        {
            PollsArt.RadioRing(drawList, radioCenter, Palette.WithAlpha(ui.MutedInk, ringAlpha * (1f - selection)),
                scale);
        }

        PollsArt.CheckBadge(drawList, radioCenter, selection, PollsArt.White, ui.Accent, scale);
        Typography.DrawWrappedLeft(new Vector2(row.Min.X + labelOffset, labelTop), label, ui.TitleInk, style,
            labelWidth);

        if (reveal > RevealEpsilon)
        {
            DrawPercent(drawList, text, optionIndex, row, firstLineCenterY, leader, closed, emphasized, reveal, scale);
            HoverTooltip.Show(text.TooltipIds[optionIndex], row, text.CountLabels[optionIndex]);
        }

        if (!closed && UiInteract.Click(row.Min, row.Max, hovered))
        {
            ChooseOption(poll, optionIndex);
        }
    }

    private Vector4 FillColor(bool closed, bool leader, float selection)
    {
        var muted = Palette.WithAlpha(PollsArt.White, leader ? FillAlphaLeader : FillAlphaOther);
        if (closed && leader)
        {
            muted = Palette.WithAlpha(ui.Accent, FillAlphaWinner);
        }

        return Vector4.Lerp(muted, Palette.WithAlpha(ui.Accent, FillAlphaMine), closed ? 0f : selection);
    }

    private void DrawPercent(ImDrawListPtr drawList, PollText text, int optionIndex, Rect row, float centerY,
        bool leader, bool closed, bool emphasized, float reveal, float scale)
    {
        var percent = text.Percents[optionIndex];
        var size = Typography.Measure(percent, TextStyles.SubheadlineEmphasized);
        var right = row.Max.X - OptionInsetX * scale;
        var ink = emphasized ? ui.TitleInk : ui.BodyInk;
        Typography.Draw(drawList, new Vector2(right - size.X, centerY - size.Y * 0.5f), percent,
            Palette.WithAlpha(ink, ink.W * reveal), TextStyles.SubheadlineEmphasized.Scale,
            TextStyles.SubheadlineEmphasized.Weight);
        if (!leader)
        {
            return;
        }

        var iconSize = LeaderIconSize * scale;
        var iconCenter = new Vector2(right - size.X - LeaderIconGap * scale - iconSize * 0.5f, centerY);
        var iconInk = closed ? PollsArt.EndingSoonInk : ui.TitleInk;
        PollsArt.Glyph(drawList, iconCenter, closed ? FontAwesomeIcon.Trophy : FontAwesomeIcon.Crown,
            Palette.WithAlpha(iconInk, iconInk.W * reveal), iconSize);
        var iconHalf = new Vector2(iconSize * 0.5f, iconSize * 0.5f);
        HoverTooltip.Show(text.LeaderIds[optionIndex], new Rect(iconCenter - iconHalf, iconCenter + iconHalf),
            Loc.T(closed ? L.Polls.Winner : L.Polls.Leading), HoverLabelSide.Above);
    }

    private void DrawFooter(ImDrawListPtr drawList, PollDto poll, PollText text, float left, float right, float top,
        float height, float scale)
    {
        var centerY = top + height * 0.5f;
        var undoWidth = 0f;
        if (!text.Closed && poll.MyVote >= 0)
        {
            undoWidth = DrawUndo(drawList, poll, right, centerY, scale);
        }

        var failure = store.VoteFailure;
        var failureAge = failure is null ? float.MaxValue : (float)(DateTime.UtcNow - failure.AtUtc).TotalSeconds;
        var failing = failure is not null && failure.PollId == poll.Id && failureAge < FailureSeconds;
        var label = failing ? failure!.Message : text.FooterLabel;
        var ink = failing ? theme.Danger : ui.MutedInk;
        if (failing)
        {
            var fade = Math.Clamp((FailureSeconds - failureAge) / FailureFadeSeconds, 0f, 1f);
            ink = Palette.WithAlpha(ink, ink.W * fade);
        }

        var available = right - undoWidth - left;
        var fitted = Typography.FitText(label, available, TextStyles.Footnote);
        var textHeight = Typography.Measure(fitted, TextStyles.Footnote).Y;
        Typography.Draw(drawList, new Vector2(left, centerY - textHeight * 0.5f), fitted, ink,
            TextStyles.Footnote.Scale, TextStyles.Footnote.Weight);
    }

    private float DrawUndo(ImDrawListPtr drawList, PollDto poll, float right, float centerY, float scale)
    {
        var label = Loc.T(L.Polls.UndoVote);
        var size = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        var width = size.X + UndoPadX * 2f * scale;
        var hitHalf = FooterHitHeight * scale * 0.5f;
        var min = new Vector2(right - width + UndoPadX * scale, centerY - hitHalf);
        var max = new Vector2(right + UndoPadX * scale, centerY + hitHalf);
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID($"polls.undo.{poll.Id}"), pressed, PressFx.ControlPressedScale);
        var pillHalf = new Vector2(width * 0.5f, size.Y * 0.5f + Metrics.Space.Xs * scale) * press;
        var pillCenter = (min + max) * 0.5f;
        if (hovered)
        {
            drawList.AddRectFilled(pillCenter - pillHalf, pillCenter + pillHalf, ImGui.GetColorU32(ui.HoverTint),
                pillHalf.Y);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Typography.Draw(drawList, pillCenter - size * 0.5f, label, ui.Accent, TextStyles.FootnoteEmphasized.Scale,
            TextStyles.FootnoteEmphasized.Weight);
        if (UiInteract.Click(min, max, hovered) && store.ClearVote(poll))
        {
            UiFeedback.Play(UiSound.ToggleOff);
        }

        return width;
    }

    private void ChooseOption(PollDto poll, int optionIndex)
    {
        if (poll.MyVote == optionIndex)
        {
            return;
        }

        if (store.Vote(poll, optionIndex))
        {
            UiFeedback.Play(UiSound.ToggleOn);
        }
    }

    private static float BlockHeight(string text, in TextStyle style, float width) =>
        Typography.MeasureWrappedBlock(text, style, width).Y - ImGui.GetStyle().ItemSpacing.Y;

    private float[] EnsureRowBuffer(int count)
    {
        if (rowHeights.Length < count)
        {
            rowHeights = new float[count];
        }

        return rowHeights;
    }

    private sealed class PollMotion
    {
        public Spring[] Fills = Array.Empty<Spring>();
        public Spring[] Selection = Array.Empty<Spring>();
        public Spring Reveal;
        public float RevealAge = float.MaxValue;
        public bool Primed;
    }
}
