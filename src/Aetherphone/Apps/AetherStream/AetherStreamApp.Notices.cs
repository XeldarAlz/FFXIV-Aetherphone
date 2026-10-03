using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.AetherStream;

internal sealed partial class AetherStreamApp
{
    private IReadOnlyList<ViewerFailure>? viewerFailureNamesSource;
    private string viewerFailureNames = string.Empty;
    private LocalMediaIdentity? localPromptSource;
    private string localPromptLine = string.Empty;
    private TextCache retryText;
    private TextCache viewerFailureTitle;

    private void DrawPlaybackNotices(float scale)
    {
        if (watchAlong.IsHosting && watchAlong.ViewerFailures.Count > 0)
        {
            DrawViewerFailuresCard(scale);
        }

        if (TryDescribeFailure(out var title, out var body))
        {
            DrawFailureCard(scale, title, body, video.State != VideoPlaybackState.Failed);
            return;
        }

        if (video.RecoveryNotice is { } notice)
        {
            DrawPlainNotice(scale, notice);
        }
    }

    private bool TryDescribeFailure(out string title, out string body)
    {
        if (video.State == VideoPlaybackState.Failed && video.FailureKind == PlaybackFailureKind.BotCheck)
        {
            title = Loc.T(L.AetherStream.FailureBotCheckTitle);
            body = Loc.T(L.AetherStream.FailureBotCheckBody);
            return true;
        }

        if (video.State == VideoPlaybackState.Failed)
        {
            title = Loc.T(L.AetherStream.FailureTitle);
            body = video.LastError ?? Loc.T(L.AetherStream.PlaybackFailed);
            return true;
        }

        if (video.RecoveryExhausted)
        {
            title = Loc.T(L.AetherStream.FailureStalledTitle);
            body = Loc.T(L.AetherStream.FailureStalledBody);
            return true;
        }

        title = string.Empty;
        body = string.Empty;
        return false;
    }

    private void DrawFailureCard(float scale, string title, string body, bool resumeFromPosition)
    {
        var viewing = watchAlong.IsViewing;
        var countdown = viewing ? watchAlong.AutoReplayInSeconds : 0f;
        string? footnote = null;
        if (countdown >= 60f)
        {
            footnote = retryText.Format(Loc.T(L.AetherStream.FailureRetryingInMinutes),
                (int)MathF.Ceiling(countdown / 60f));
        }
        else if (countdown > 0f)
        {
            footnote = retryText.Format(Loc.T(L.AetherStream.FailureRetryingIn), (int)MathF.Ceiling(countdown));
        }

        var canSkip = !viewing && queue.HasNext;
        DrawActionCard(scale, theme.Danger, title, body, footnote, Loc.T(L.AetherStream.FailureRetry),
            canSkip ? Loc.T(L.AetherStream.FailureSkip) : null, out var retry, out var skip);
        if (retry)
        {
            RetryPlayback(resumeFromPosition);
        }

        if (skip)
        {
            queue.Advance();
        }
    }

    private void RetryPlayback(bool resumeFromPosition)
    {
        if (watchAlong.IsViewing)
        {
            watchAlong.RetryNow();
            return;
        }

        var resume = resumeFromPosition ? (double)video.Progress.Position : 0d;
        video.ResetRecoveryBudget();
        queue.Replay(resume);
    }

    private void DrawViewerFailuresCard(float scale)
    {
        var failures = watchAlong.ViewerFailures;
        if (!ReferenceEquals(failures, viewerFailureNamesSource))
        {
            viewerFailureNamesSource = failures;
            viewerFailureNames = JoinViewerNames(failures);
        }

        var watchers = Math.Max(failures.Count, watchAlong.Roster.Count - 1);
        var title = viewerFailureTitle.Format(Loc.T(L.AetherStream.FailureViewersTitle), failures.Count, watchers);
        var canSkip = queue.HasNext;
        DrawActionCard(scale, ui.Accent, title, viewerFailureNames, Loc.T(L.AetherStream.FailureViewersHint),
            canSkip ? Loc.T(L.AetherStream.FailureSkip) : Loc.T(L.AetherStream.FailureDismiss),
            canSkip ? Loc.T(L.AetherStream.FailureDismiss) : null, out var primary, out var secondary);
        if (primary && canSkip)
        {
            watchAlong.DismissViewerFailures();
            queue.Advance();
            return;
        }

        if (primary || secondary)
        {
            watchAlong.DismissViewerFailures();
        }
    }

    private static string JoinViewerNames(IReadOnlyList<ViewerFailure> failures)
    {
        var names = new string[failures.Count];
        for (var index = 0; index < failures.Count; index++)
        {
            names[index] = failures[index].DisplayName;
        }

        return string.Join(", ", names);
    }

    private void DrawActionCard(float scale, Vector4 tint, string title, string body, string? footnote,
        string primaryLabel, string? secondaryLabel, out bool primary, out bool secondary)
    {
        Gap(Metrics.Space.Md);
        var pad = Metrics.Space.Md * scale;
        var textWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Footnote, textWidth).Y;
        var footnoteHeight = footnote is null
            ? 0f
            : Typography.MeasureWrappedBlock(footnote, TextStyles.Footnote, textWidth).Y + Metrics.Space.Xs * scale;
        var buttonHeight = SmallButtonHeight * scale;
        var card = BeginBlock(pad + titleHeight + 3f * scale + bodyHeight + footnoteHeight + Metrics.Space.Md * scale
            + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        TintedCard(drawList, card, tint);

        var textX = card.Min.X + pad;
        var textY = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(textX, textY),
            Typography.FitText(title, textWidth, TextStyles.BodyEmphasized), Ink.TitleInk,
            TextStyles.BodyEmphasized);
        textY += titleHeight + 3f * scale;
        Typography.DrawWrappedLeft(new Vector2(textX, textY), body, Ink.BodyInk, TextStyles.Footnote, textWidth);
        textY += bodyHeight;
        if (footnote is not null)
        {
            Typography.DrawWrappedLeft(new Vector2(textX, textY + Metrics.Space.Xs * scale), footnote, Ink.MutedInk,
                TextStyles.Footnote, textWidth);
        }

        var buttonsTop = card.Max.Y - pad - buttonHeight;
        secondary = false;
        if (secondaryLabel is null)
        {
            primary = SmallButton(new Rect(new Vector2(textX, buttonsTop),
                new Vector2(textX + textWidth, buttonsTop + buttonHeight)), primaryLabel, true);
        }
        else
        {
            var half = (textWidth - Metrics.Space.Sm * scale) * 0.5f;
            primary = SmallButton(new Rect(new Vector2(textX, buttonsTop),
                new Vector2(textX + half, buttonsTop + buttonHeight)), primaryLabel, true);
            secondary = SmallButton(new Rect(new Vector2(textX + half + Metrics.Space.Sm * scale, buttonsTop),
                new Vector2(textX + textWidth, buttonsTop + buttonHeight)), secondaryLabel, false);
        }

        EndBlock();
    }

    private void DrawPlainNotice(float scale, string text)
    {
        Gap(Metrics.Space.Md);
        var pad = Metrics.Space.Md * scale;
        var textWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var textHeight = Typography.MeasureWrappedBlock(text, TextStyles.Footnote, textWidth).Y;
        var card = BeginBlock(textHeight + pad * 2f);
        TintedCard(ImGui.GetWindowDrawList(), card, Ink.MutedInk);
        Typography.DrawWrappedLeft(card.Min + new Vector2(pad, pad), text, Ink.MutedInk, TextStyles.Footnote,
            textWidth);
        EndBlock();
    }

    private void DrawLocalMediaPrompt(float scale)
    {
        if (!watchAlong.IsViewing || watchAlong.PendingLocalMedia is not { } pending)
        {
            return;
        }

        if (!ReferenceEquals(pending, localPromptSource))
        {
            localPromptSource = pending;
            localPromptLine = string.Concat(pending.FileName, "  ·  ", FormatFileSize(pending.SizeBytes));
        }

        Gap(Metrics.Space.Md);
        var pad = Metrics.Space.Md * scale;
        var textWidth = ScrollLayout.StableContentWidth() - PadX * 2f * scale - pad * 2f;
        var titleHeight = Typography.LineHeight(TextStyles.BodyEmphasized);
        var fileLineHeight = Typography.LineHeight(TextStyles.Footnote);
        var hint = Loc.T(L.AetherStream.LocalWatchHint);
        var hintHeight = Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, textWidth).Y;
        var noFileHint = Loc.T(L.AetherStream.LocalWatchNoFileHint);
        var noFileHintHeight = Typography.MeasureWrappedBlock(noFileHint, TextStyles.Footnote, textWidth).Y;
        var mismatch = watchAlong.LocalMediaMismatch;
        var mismatchText = Loc.T(L.AetherStream.LocalWatchMismatch);
        var mismatchHeight = mismatch
            ? Typography.MeasureWrappedBlock(mismatchText, TextStyles.Footnote, textWidth).Y
                + Metrics.Space.Xs * scale
            : 0f;
        var buttonHeight = SmallButtonHeight * scale;
        var gap = Metrics.Space.Xs * scale;
        var card = BeginBlock(pad + titleHeight + 3f * scale + fileLineHeight + gap + hintHeight + gap
            + noFileHintHeight + mismatchHeight + Metrics.Space.Md * scale + buttonHeight + pad);
        var drawList = ImGui.GetWindowDrawList();
        TintedCard(drawList, card, ui.Accent);

        var textX = card.Min.X + pad;
        var textY = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(textX, textY),
            Typography.FitText(Loc.T(L.AetherStream.LocalWatchTitle), textWidth, TextStyles.BodyEmphasized),
            Ink.TitleInk, TextStyles.BodyEmphasized);
        textY += titleHeight + 3f * scale;
        Typography.Draw(drawList, new Vector2(textX, textY),
            Typography.FitText(localPromptLine, textWidth, TextStyles.Footnote), Ink.TitleInk, TextStyles.Footnote);
        textY += fileLineHeight + gap;
        Typography.DrawWrappedLeft(new Vector2(textX, textY), hint, Ink.MutedInk, TextStyles.Footnote, textWidth);
        textY += hintHeight + gap;
        Typography.DrawWrappedLeft(new Vector2(textX, textY), noFileHint, Ink.MutedInk, TextStyles.Footnote,
            textWidth);
        textY += noFileHintHeight;
        if (mismatch)
        {
            Typography.DrawWrappedLeft(new Vector2(textX, textY + gap), mismatchText, Ink.Danger,
                TextStyles.Footnote, textWidth);
        }

        var buttonsTop = card.Max.Y - pad - buttonHeight;
        var locating = watchAlong.IsLocatingLocalMedia;
        var useAnyway = mismatch && watchAlong.HasMismatchCandidate;
        var locateRight = useAnyway ? textX + (textWidth - Metrics.Space.Sm * scale) * 0.5f : textX + textWidth;
        if (SmallButton(new Rect(new Vector2(textX, buttonsTop), new Vector2(locateRight, buttonsTop + buttonHeight)),
                Loc.T(L.AetherStream.LocalWatchLocate), true) && !locating)
        {
            FilePicker.PickVideo(Loc.T(L.AetherStream.LocalWatchLocate),
                path => Interlocked.Exchange(ref pendingLocateFile, path));
        }

        if (useAnyway && SmallButton(new Rect(new Vector2(locateRight + Metrics.Space.Sm * scale, buttonsTop),
                    new Vector2(textX + textWidth, buttonsTop + buttonHeight)),
                Loc.T(L.AetherStream.LocalWatchUseAnyway), false) && !locating)
        {
            watchAlong.AcceptMismatchedLocalMedia();
        }

        EndBlock();
    }

    private static string FormatFileSize(long bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        const double megabyte = 1024d * 1024d;
        if (bytes >= gigabyte)
        {
            return (bytes / gigabyte).ToString("0.0", Loc.Culture) + " GB";
        }

        return Math.Max(1d, Math.Round(bytes / megabyte)).ToString("0", Loc.Culture) + " MB";
    }
}
