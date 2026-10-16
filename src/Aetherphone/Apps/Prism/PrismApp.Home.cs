using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float EntryThumbWidth = 84f;
    private const float BottomPad = 24f;

    private readonly Action withdraw;

    private CachedText deadlineText;
    private CachedText judgedText;

    private void DrawHome(Rect body)
    {
        using var surface = AppSurface.Begin(body);
        if (!store.IsSignedIn)
        {
            if (EmptyState.Draw(body, ui, FontAwesomeIcon.UserLock, Loc.T(L.Prism.SignInTitle),
                    Loc.T(L.Prism.SignInRequired), Loc.T(L.Prism.OpenSettings)))
            {
                navigation.Open("settings");
            }

            return;
        }

        if (store.Week is not { } week)
        {
            DrawWeekMissing(body);
            return;
        }

        if (week.Phase.Length == 0 || week.Id.Length == 0)
        {
            EmptyState.Draw(body, ui, PhoneIcons.Sparkles, Loc.T(L.Prism.NoWeekTitle), Loc.T(L.Prism.NoWeekHint));
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ScrollLayout.StableContentWidth();
        var gap = Metrics.Space.Lg * scale;
        var top = DrawThemeCard(drawList, origin, width, week, scale) + gap;
        top = DrawHomeActions(drawList, new Vector2(origin.X, top), width, week, scale);
        ImGui.Dummy(new Vector2(width, top - origin.Y + BottomPad * scale));
    }

    private void DrawWeekMissing(Rect body)
    {
        if (!store.WeekFailed || store.Loading)
        {
            Typography.DrawCentered(ImGui.GetWindowDrawList(), body.Center, Loc.T(L.Common.Loading), ui.MutedInk,
                TextStyles.Subheadline);
            return;
        }

        var reason = store.WeekFailureText;
        if (!EmptyState.Draw(body, ui, FontAwesomeIcon.ExclamationTriangle, Loc.T(L.Common.LoadFailed),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), Loc.T(L.Common.Retry)))
        {
            return;
        }

        UiFeedback.Play(UiSound.Refresh);
        store.Refresh();
    }

    private float DrawThemeCard(ImDrawListPtr drawList, Vector2 origin, float width, PrismWeekDto week, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var inner = width - pad * 2f;
        var label = Loc.T(L.Prism.ThemeLabel);
        var deadline = DeadlineLine(week);
        var labelHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var titleHeight = Typography.MeasureWrappedBlock(week.Title, TextStyles.Title1, inner).Y;
        var briefHeight = week.Brief.Length > 0
            ? Typography.MeasureWrappedBlock(week.Brief, TextStyles.Body, inner).Y + Metrics.Space.Xs * scale
            : 0f;
        var deadlineHeight = deadline.Length > 0
            ? Typography.MeasureWrappedBlock(deadline, TextStyles.Footnote, inner).Y + Metrics.Space.Md * scale
            : 0f;
        var height = pad * 2f + labelHeight + Metrics.Space.Xxs * scale + titleHeight + briefHeight + deadlineHeight;
        var bottom = origin.Y + height;
        ui.Card(drawList, origin, new Vector2(origin.X + width, bottom), Metrics.Radius.Card * scale);

        var left = origin.X + pad;
        var top = origin.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top), label, ui.Accent, TextStyles.FootnoteEmphasized);
        top += labelHeight + Metrics.Space.Xxs * scale;
        top += Typography.DrawWrappedLeft(new Vector2(left, top), week.Title, ui.TitleInk, TextStyles.Title1, inner);
        if (week.Brief.Length > 0)
        {
            top += Metrics.Space.Xs * scale;
            top += Typography.DrawWrappedLeft(new Vector2(left, top), week.Brief, ui.BodyInk, TextStyles.Body, inner);
        }

        if (deadline.Length > 0)
        {
            top += Metrics.Space.Md * scale;
            Typography.DrawWrappedLeft(new Vector2(left, top), deadline, ui.MutedInk, TextStyles.Footnote, inner);
        }

        return bottom;
    }

    private float DrawHomeActions(ImDrawListPtr drawList, Vector2 origin, float width, PrismWeekDto week,
        float scale)
    {
        var top = origin.Y;
        var gap = Metrics.Space.Md * scale;
        var buttonHeight = Button.Height(ButtonSize.Large) * scale;
        if (week.MyEntry is { } entry)
        {
            top = DrawEntryCard(drawList, new Vector2(origin.X, top), width, entry, week.Phase, scale) + gap;
        }
        else if (week.Phase == PrismPhases.Entering)
        {
            var enter = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + buttonHeight));
            if (Button.Draw(enter, Loc.T(L.Prism.EnterLook), ui.Ink, enabled: !store.Busy))
            {
                StartPick();
            }

            top = enter.Max.Y + gap;
        }

        if (PrismPhases.Judgeable(week.Phase))
        {
            var label = week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal : L.Prism.JudgeHeat;
            var judge = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + buttonHeight));
            if (Button.Draw(judge, Loc.T(label), ui.Ink, enabled: !store.Busy))
            {
                StartJudging();
            }

            top = judge.Max.Y + Metrics.Space.Xs * scale;
            var judged = JudgedLine(week.BallotsCast);
            Typography.DrawCentered(drawList,
                new Vector2(origin.X + width * 0.5f, top + Typography.LineHeight(TextStyles.Footnote) * 0.5f),
                judged, ui.MutedInk, TextStyles.Footnote);
            top += Typography.LineHeight(TextStyles.Footnote) + gap;
        }

        if (store.ActionFailed)
        {
            var reason = store.ActionFailureText;
            top += Typography.DrawWrappedLeft(new Vector2(origin.X, top),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), ui.MutedInk, TextStyles.Footnote,
                width) + gap;
        }

        if (store.Results is not null)
        {
            var results = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + buttonHeight));
            if (Button.Draw(results, Loc.T(L.Prism.LastResults), ui.Ink, ButtonStyle.Gray))
            {
                router.Push(PrismScreen.Results);
            }

            top = results.Max.Y;
        }

        return top;
    }

    private float DrawEntryCard(ImDrawListPtr drawList, Vector2 origin, float width, PrismEntryDto entry,
        string phase, float scale)
    {
        var pad = Metrics.Space.Md * scale;
        var thumbWidth = EntryThumbWidth * scale;
        var thumbHeight = thumbWidth / PrismStore.EntryRatio;
        var left = origin.X + pad + thumbWidth + pad;
        var inner = origin.X + width - pad - left;
        var (status, hint) = StatusText(entry);
        var editable = phase == PrismPhases.Entering;
        var buttonHeight = Button.Height(ButtonSize.Small) * scale;
        var textHeight = Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Xxs * scale;
        textHeight += hint.Length > 0 ? Typography.MeasureWrappedBlock(hint, TextStyles.Footnote, inner).Y : 0f;
        textHeight += editable ? Metrics.Space.Sm * scale + buttonHeight : 0f;
        var bottom = origin.Y + MathF.Max(thumbHeight, textHeight) + pad * 2f;
        ui.Card(drawList, origin, new Vector2(origin.X + width, bottom), Metrics.Radius.Card * scale);
        var thumb = new Rect(new Vector2(origin.X + pad, origin.Y + pad),
            new Vector2(origin.X + pad + thumbWidth, origin.Y + pad + thumbHeight));
        DrawLook(drawList, thumb, entry.ImageUrl, Metrics.Radius.Md * scale, false);

        var top = origin.Y + pad;
        Typography.Draw(drawList, new Vector2(left, top), Typography.FitText(status, inner, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        top += Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Xxs * scale;
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, top), hint, ui.MutedInk, TextStyles.Footnote, inner);
        }

        if (!editable)
        {
            return bottom;
        }

        var buttonTop = bottom - pad - buttonHeight;
        var half = (inner - Metrics.Space.Xs * scale) * 0.5f;
        var change = new Rect(new Vector2(left, buttonTop), new Vector2(left + half, buttonTop + buttonHeight));
        var remove = new Rect(new Vector2(left + inner - half, buttonTop),
            new Vector2(left + inner, buttonTop + buttonHeight));
        if (Button.Draw(change, Loc.T(L.Common.ChangePhoto), ui.Ink, ButtonStyle.Gray, enabled: !store.Busy))
        {
            StartPick();
        }

        if (Button.Draw(remove, Loc.T(L.Prism.Withdraw), ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                enabled: !store.Busy))
        {
            confirm.Ask(new ConfirmRequest
            {
                Title = Loc.T(L.Prism.WithdrawTitle),
                Message = Loc.T(L.Prism.WithdrawBody),
                ConfirmLabel = Loc.T(L.Prism.Withdraw),
                CancelLabel = Loc.T(L.Common.Cancel),
                Sheet = true,
                Confirm = withdraw,
            });
        }

        return bottom;
    }

    private (string Status, string Hint) StatusText(PrismEntryDto entry)
    {
        if (store.Busy)
        {
            return (Loc.T(L.Prism.Uploading), string.Empty);
        }

        return entry.Status switch
        {
            PrismPhases.StatusLive => (Loc.T(L.Prism.StatusLive), string.Empty),
            PrismPhases.StatusRejected => (Loc.T(L.Prism.StatusRejected), entry.RejectReason),
            _ => (Loc.T(L.Prism.StatusReview), Loc.T(L.Prism.StatusReviewHint)),
        };
    }

    private string DeadlineLine(PrismWeekDto week)
    {
        var (format, unix) = week.Phase switch
        {
            PrismPhases.Entering => (L.Prism.EntriesClose, week.EntriesCloseAtUnix),
            PrismPhases.Judging => (L.Prism.JudgingCloses, week.JudgingCloseAtUnix),
            PrismPhases.Final => (L.Prism.FinalCloses, week.FinalCloseAtUnix),
            _ => (L.Prism.ResultsAt, week.ResultsAtUnix),
        };
        if (unix <= 0)
        {
            return string.Empty;
        }

        var key = HashCode.Combine(week.Phase, unix, DateTime.Now.DayOfYear, Loc.Current.Code);
        return deadlineText.IsCurrent(key)
            ? deadlineText.Value
            : deadlineText.Store(key, Loc.T(format, TimeText.FutureMoment(unix)));
    }

    private string JudgedLine(int count)
    {
        var key = HashCode.Combine(count, Loc.Current.Code);
        return judgedText.IsCurrent(key) ? judgedText.Value : judgedText.Store(key, Loc.Plural(L.Prism.HeatsJudged, count));
    }

    private void DrawLook(ImDrawListPtr drawList, Rect rect, string url, float rounding, bool contain,
        float alpha = 1f)
    {
        var texture = images.Sized(url, MathF.Max(rect.Width, rect.Height));
        if (texture is null)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(ui.FieldSurface));
            var placeholder = Loc.T(images.Failed(url) ? L.Common.ImageFailed : L.Common.Loading);
            Typography.DrawCentered(drawList, rect.Center,
                Typography.FitText(placeholder, rect.Width - Metrics.Space.Sm * UiScale.Current, TextStyles.Caption1),
                ui.MutedInk, TextStyles.Caption1);
            return;
        }

        var frame = contain ? ImageFit.CenteredRect(rect, texture.Size.X / MathF.Max(1f, texture.Size.Y)) : rect;
        var (uv0, uv1) = contain
            ? (Vector2.Zero, Vector2.One)
            : ImageFit.Cover(texture.Size.X, texture.Size.Y, rect.Width, rect.Height);
        drawList.AddImageRounded(texture.Handle, frame.Min, frame.Max, uv0, uv1,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding, ImDrawFlags.RoundCornersAll);
    }
}
