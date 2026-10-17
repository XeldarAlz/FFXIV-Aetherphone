using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float PickPaneFraction = 0.52f;

    private static readonly Vector4 PickFill = new(1f, 1f, 1f, 0.08f);
    private static readonly PhotoComposeStyle PickStyle =
        new(PrismArt.White, PrismArt.Muted, PickFill, PrismArt.Faint, false);

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

        pickIntake.AcceptDrop(content, library, PrismArt.White);
        var top = DrawHeader(drawList, content, string.Empty, PrismType.Upper(Loc.T(L.Prism.EnterLook)), true, scale);
        var gutter = PrismArt.Gutter(scale);
        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        pickSession.DrawPickPane(pane, scale, PickStyle, PrismStore.EntryRatio, PrismStore.EntryRevealsWholeImage,
            !store.Busy);

        var stripTop = pane.Max.Y + Metrics.Space.Md * scale;
        var strip = new Rect(new Vector2(content.Min.X + gutter, stripTop),
            new Vector2(content.Max.X - gutter, stripTop + PrismArt.StripUnits * scale));
        var label = PrismType.Upper(Loc.T(store.Busy ? L.Prism.Uploading : L.Prism.EnterThisLook));
        if (PrismArt.Strip(drawList, strip, label, "→", true, pickSession.HasSelection && !store.Busy,
                UiSound.Shutter))
        {
            store.Enter(pickSession.FirstSelected, pickSession.CropsArray()[0], entrySettled);
        }

        var gridTop = strip.Max.Y + Metrics.Space.Md * scale;
        if (store.ActionFailed && !store.Busy)
        {
            var reason = store.ActionFailureText;
            gridTop += Typography.DrawWrappedLeft(new Vector2(strip.Min.X, gridTop),
                reason.Length > 0 ? reason : Loc.T(L.Common.LoadFailedHint), PrismArt.Muted, TextStyles.Subheadline,
                strip.Width) + Metrics.Space.Md * scale;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, gridTop), content.Max);
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
