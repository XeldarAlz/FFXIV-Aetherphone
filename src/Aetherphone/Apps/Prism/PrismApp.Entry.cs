using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float StatusMaxUnits = PrismType.TitleUnits;
    private const float StatusMinUnits = PrismType.HeadingUnits;
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
        var labelPixels = PrismArt.LabelPixels();
        var stripHeight = PrismArt.StripUnits * scale;
        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        var linkTop = bottom - PrismType.LineHeight(PrismFace.Label, labelPixels);
        var change = new Rect(new Vector2(left, linkTop - Metrics.Space.Lg * scale - stripHeight),
            new Vector2(left + width, linkTop - Metrics.Space.Lg * scale));
        var doneBottom = editable ? change.Min.Y - Metrics.Space.Md * scale : bottom;
        var done = new Rect(new Vector2(left, doneBottom - stripHeight), new Vector2(left + width, doneBottom));
        var actionsTop = done.Min.Y;
        var pixels = PrismType.Fit(Loc.T(StatusTitle(entry.Status)), PrismFace.Headline, width,
            StatusMaxUnits * scale, StatusMinUnits * scale, 2, out var lines);
        var entryFailed = !store.Busy && (store.Failed(PrismAction.Enter) || store.Failed(PrismAction.Withdraw) ||
                                          store.Failed(PrismAction.Outfit));
        var failure = entryFailed ? store.ActionFailureText : string.Empty;
        var hint = entryFailed
            ? failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint)
            : entry.Status is PrismPhases.StatusRejected or PrismPhases.StatusMoved
                ? entry.RejectReason
                : string.Empty;
        hint = PrismType.Sentences(hint, TextStyles.Subheadline, width);
        var hintWidth = PrismType.BalancedWidth(hint, TextStyles.Subheadline, width);
        var hintHeight = hint.Length > 0
            ? Typography.MeasureWrappedBlock(hint, TextStyles.Subheadline, hintWidth).Y + Metrics.Space.Sm * scale
            : 0f;
        var textTop = actionsTop - Metrics.Space.Xl * scale - hintHeight -
                      lines.Length * PrismType.LineHeight(PrismFace.Headline, pixels);
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

        textTop += PrismType.DrawLines(drawList, new Vector2(left, textTop), lines, PrismArt.Fore,
            PrismFace.Headline, pixels);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, textTop + Metrics.Space.Sm * scale), hint, PrismArt.Muted,
                TextStyles.Subheadline, hintWidth);
        }

        var retry = editable && entry.Status == PrismPhases.StatusRejected;
        if (PrismArt.Strip(drawList, retry ? change : done, Loc.T(L.Prism.Done), string.Empty, !retry))
        {
            Go(PrismScreen.Cover, -1);
        }

        if (!editable)
        {
            return;
        }

        if (PrismArt.Strip(drawList, retry ? done : change, Loc.T(L.Common.ChangePhoto), Arrow, retry, !store.Busy))
        {
            StartPick();
        }

        var outfit = Loc.T(VanillaEntry ? L.Prism.EditGear : L.Prism.EditMods);
        var outfitWidth = PrismType.Measure(outfit, PrismFace.Label, labelPixels).X;
        if (!store.Busy && PrismArt.Link(drawList, new Vector2(left + width - outfitWidth, linkTop), outfit,
                PrismArt.Muted, scale))
        {
            OpenOutfit();
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
        PrismPhases.StatusRejected => L.Prism.StatusRejectedTitle,
        PrismPhases.StatusMoved => L.Prism.StatusMovedTitle,
        _ => L.Prism.StatusLiveTitle,
    };
}
