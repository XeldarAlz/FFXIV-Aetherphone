using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float PickPaneFraction = 0.5f;

    private volatile int entryOutcome;

    private PhotoComposeStyle PickStyle => new(ui.Accent, ui.MutedInk, ui.FieldSurface, ui.Hairline, false);

    private void StartPick()
    {
        pickSession.Open(true);
        pickIntake.Discard();
        router.Push(PrismScreen.Pick);
    }

    private void DrawPick(Rect body)
    {
        ConsumeEntryOutcome();
        pickSession.ConsumePendingImport();
        while (pickIntake.TryTake(out var taken))
        {
            pickSession.TakeImported(taken);
        }

        if (UiInteract.Hover(body.Min, body.Max) && GameFocus.Active && ImageIntake.PasteRequested())
        {
            pickIntake.Paste(library);
        }

        pickIntake.AcceptDrop(body, library, ui.Accent);
        var scale = UiScale.Current;
        var style = PickStyle;
        var paneHeight = MathF.Min(body.Width, body.Height * PickPaneFraction);
        var pane = new Rect(body.Min, new Vector2(body.Max.X, body.Min.Y + paneHeight));
        pickSession.DrawPickPane(pane, scale, style, PrismStore.EntryRatio, PrismStore.EntryRevealsWholeImage,
            !store.Busy);

        var gap = Metrics.Space.Md * scale;
        var side = Metrics.Space.Lg * scale;
        var buttonTop = pane.Max.Y + gap;
        var button = new Rect(new Vector2(body.Min.X + side, buttonTop),
            new Vector2(body.Max.X - side, buttonTop + Button.Height(ButtonSize.Large) * scale));
        var label = store.Busy ? Loc.T(L.Prism.Uploading) : Loc.T(L.Prism.EnterThisLook);
        if (Button.Draw(button, label, ui.Ink, enabled: pickSession.HasSelection && !store.Busy))
        {
            store.Enter(pickSession.FirstSelected, pickSession.CropsArray()[0], entrySettled);
        }

        var gridTop = button.Max.Y + gap;
        if (store.ActionFailed && !store.Busy)
        {
            var reason = store.ActionFailureText;
            gridTop += Typography.DrawWrappedCentered(new Vector2(body.Center.X, gridTop),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), ui.MutedInk, TextStyles.Footnote,
                body.Width - side * 2f) + gap;
        }

        var gridRect = new Rect(new Vector2(body.Min.X, gridTop), body.Max);
        using (AppSurface.BeginEdgeToEdge(gridRect))
        {
            if (pickSession.DrawPickGrid(gridRect, scale, style, false, Loc.T(L.Common.ImportFromPc),
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
            router.Pop();
        }
    }
}
