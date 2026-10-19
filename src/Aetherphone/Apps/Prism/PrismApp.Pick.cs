using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float PickPaneFraction = 0.52f;

    private const float PickFillAlpha = 0.08f;

    private static PhotoComposeStyle PickStyle =>
        new(PrismArt.Fore, PrismArt.Muted, Palette.WithAlpha(PrismArt.Fore, PickFillAlpha), PrismArt.Faint, false);

    private volatile int entryOutcome;

    private void StartPick()
    {
        pickSession.Open(true);
        pickIntake.Discard();
        pickReturn = screen == PrismScreen.Entry ? PrismScreen.Entry : PrismScreen.Cover;
        Go(PrismScreen.Pick);
    }

    private void DrawPick(ImDrawListPtr drawList, Rect content, float scale)
    {
        ConsumeEntryOutcome();
        pickSession.ConsumePendingImport();
        while (pickIntake.TryTake(out var taken))
        {
            pickSession.TakeImported(taken);
        }

        if (UiInteract.Hover(content.Min, content.Max) && GameFocus.Active && ImageIntake.PasteRequested())
        {
            pickIntake.Paste(library);
        }

        pickIntake.AcceptDrop(content, library, PrismArt.Fore);
        var week = store.Week;
        var top = DrawHeader(drawList, content, string.Empty,
            week is null ? Loc.T(L.Prism.EnterLook) : week.Title, true, scale);
        if (week is { Brief.Length: > 0 })
        {
            var gutter = PrismArt.Gutter(scale);
            top += Typography.DrawWrappedLeft(new Vector2(content.Min.X + gutter, top), week.Brief, PrismArt.Muted,
                TextStyles.Subheadline,
                PrismType.BalancedWidth(week.Brief, TextStyles.Subheadline, content.Width - gutter * 2f)) +
                   Metrics.Space.Lg * scale;
        }

        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        pickSession.DrawPickPane(pane, scale, PickStyle, PrismStore.EntryRatio, PrismStore.EntryRevealsWholeImage,
            !store.Busy, Loc.T(L.Prism.EnterLook));

        var strip = StripRect(content, scale);
        var label = Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.EnterThisLook);
        if (PrismArt.Strip(drawList, strip, label, "→", true, pickSession.HasSelection && !store.Busy,
                UiSound.Shutter))
        {
            store.Enter(pickSession.FirstSelected, pickSession.CropsArray()[0], entrySettled);
        }

        var gridBottom = strip.Min.Y - Metrics.Space.Lg * scale;
        if (store.Failed(PrismAction.Enter) && !store.Busy)
        {
            var reason = store.ActionFailureText;
            var text = reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint);
            gridBottom -= Typography.MeasureWrappedBlock(text, TextStyles.Subheadline, strip.Width).Y;
            Typography.DrawWrappedLeft(new Vector2(strip.Min.X, gridBottom), text, PrismArt.Muted,
                TextStyles.Subheadline, strip.Width);
            gridBottom -= Metrics.Space.Md * scale;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, gridBottom));
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            if (pickSession.DrawPickGrid(gridRect, scale, PickStyle, false, Loc.T(L.Common.ImportFromPc),
                    Loc.T(L.Prism.EnterLook), pickIntake.ClipboardHasImage() ? Loc.T(L.Common.PasteImage) : null))
            {
                pickIntake.Paste(library);
            }
        }
    }

    private void SettleEntry(bool succeeded)
    {
        entryOutcome = succeeded ? OutcomeSent : OutcomeFailed;
    }

    private void ConsumeEntryOutcome()
    {
        var outcome = entryOutcome;
        if (outcome == OutcomePending)
        {
            return;
        }

        entryOutcome = OutcomePending;
        if (outcome == OutcomeSent)
        {
            pickIntake.Discard();
            Go(PrismScreen.Entry);
        }
    }
}
