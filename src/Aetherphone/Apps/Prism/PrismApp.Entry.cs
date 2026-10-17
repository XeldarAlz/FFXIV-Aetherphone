using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float StatusMaxUnits = 32f;
    private const float StatusMinUnits = 24f;
    private const float TagDelay = 0.2f;
    private const float TagStamp = 0.35f;

    private void DrawEntry(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (store.Week is not { MyEntry: { } entry } week)
        {
            Go(PrismScreen.Cover);
            return;
        }

        var top = DrawHeader(drawList, content, string.Empty, IssueLine(week.Issue), true, scale);
        var gutter = PrismArt.Gutter(scale);
        var width = content.Width - gutter * 2f;
        var left = content.Min.X + gutter;
        var editable = week.Phase == PrismPhases.Entering;
        var judgeable = PrismPhases.Judgeable(week.Phase);
        var labelPixels = PrismArt.LabelPixels();
        var bottom = content.Max.Y - gutter;
        var linkTop = bottom - PrismType.LineHeight(EditorialFace.Label, labelPixels);
        var stripBottom = editable ? linkTop - Metrics.Space.Lg * scale : bottom;
        var strip = new Rect(new Vector2(left, stripBottom - PrismArt.StripUnits * scale),
            new Vector2(left + width, stripBottom));
        var actionsTop = editable || judgeable ? strip.Min.Y : bottom;
        var pixels = PrismType.Fit(Loc.T(StatusTitle(entry.Status)), EditorialFace.Headline, width,
            StatusMaxUnits * scale, StatusMinUnits * scale, 2, out var lines);
        var hint = entry.Status == PrismPhases.StatusRejected
            ? entry.RejectReason
            : entry.Status == PrismPhases.StatusLive ? string.Empty : Loc.T(L.Prism.StatusReviewHint);
        var hintHeight = hint.Length > 0
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, width).Y + Metrics.Space.Sm * scale
            : 0f;
        var textTop = actionsTop - Metrics.Space.Xl * scale - hintHeight -
                      lines.Length * PrismType.LineHeight(EditorialFace.Headline, pixels);
        var photo = new Rect(new Vector2(left, top), new Vector2(left + width, textTop - Metrics.Space.Xl * scale));
        var frame = DrawLook(drawList, photo, store.Busy ? string.Empty : entry.ImageUrl, true);
        if (!store.Busy)
        {
            var tag = PrismMotion.Mark(drawList);
            var pivot = PrismArt.Tag(drawList, new Vector2(frame.Max.X, frame.Min.Y),
                Loc.T(StatusName(entry.Status)), scale);
            var stamp = PrismMotion.At(screenAge, TagDelay, Motion.PageSettle);
            PrismMotion.Scale(drawList, tag, pivot, 1f + TagStamp * (1f - stamp), stamp);
        }

        textTop += PrismType.DrawLines(drawList, new Vector2(left, textTop), lines, PrismArt.White,
            EditorialFace.Headline, pixels);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, textTop + Metrics.Space.Sm * scale), hint, PrismArt.Muted,
                TextStyles.Subheadline, width);
        }

        if (judgeable && PrismArt.Strip(drawList, strip,
                Loc.T(week.Phase == PrismPhases.Final ? L.Prism.JudgeFinal : L.Prism.JudgeHeat), "→", true,
                !store.Busy))
        {
            StartJudging();
        }

        if (!editable)
        {
            return;
        }

        if (PrismArt.Strip(drawList, strip, Loc.T(L.Common.ChangePhoto), "→", true, !store.Busy))
        {
            StartPick();
        }

        if (!store.Busy && PrismArt.Link(drawList, new Vector2(left, linkTop), Loc.T(L.Prism.Withdraw),
                PrismArt.Muted, scale))
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
    }

    private static LocString StatusTitle(string status) => status switch
    {
        PrismPhases.StatusLive => L.Prism.StatusLiveTitle,
        PrismPhases.StatusRejected => L.Prism.StatusRejectedTitle,
        _ => L.Prism.StatusReviewTitle,
    };
}
