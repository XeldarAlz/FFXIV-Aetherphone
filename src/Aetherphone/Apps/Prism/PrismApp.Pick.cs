using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Platform;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal sealed partial class PrismApp
{
    private const float PickPaneFraction = 0.52f;

    private const float PickFillAlpha = 0.08f;
    private const string EntryRaceId = "prism.entryRace";
    private const string EntryGenderId = "prism.entryGender";

    private static PhotoComposeStyle PickStyle =>
        new(PrismArt.Fore, PrismArt.Muted, Palette.WithAlpha(PrismArt.Fore, PickFillAlpha), PrismArt.Faint, false);

    private volatile int entryOutcome;
    private CachedText outfitLinkText;
    private CachedText pickHeadingText;

    private void StartPick()
    {
        pickSession.Open(true);
        pickIntake.Discard();
        entryOutcome = OutcomePending;
        entryRace = 0;
        entryGender = NoGender;
        pickReturn = screen == PrismScreen.Entry ? PrismScreen.Entry : PrismScreen.Cover;
        ResetEntryOutfit();
        Go(PrismScreen.Pick);
    }

    private void DrawPick(ImDrawListPtr drawList, Rect content, float scale)
    {
        bodyRaceMenu.Gate();
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
        var bodyChosen = BodyChosen(entryRace, entryGender);
        var ready = pickSession.HasSelection && !store.Busy && (!VanillaEntry || DraftPieces() > 0) && bodyChosen;
        var top = DrawActionHeader(drawList, content, PhoneIcons.X, Loc.T(L.Common.Cancel),
            PickHeading(week), PhoneIcons.Check, Loc.T(L.Prism.Enter), ready,
            store.Busy ? Loc.T(L.Prism.Uploading) : string.Empty, UiSound.Shutter, scale, out var entered) +
                  Metrics.Space.Sm * scale;
        if (entered)
        {
            RememberEntryDrops();
            store.Enter(pickSession.FirstSelected, pickSession.CropsArray()[0], EntryOutfit(), entryRace, entryGender,
                entrySettled);
        }

        if (week is { Brief.Length: > 0 })
        {
            top += Typography.DrawWrappedLeft(new Vector2(content.Min.X, top), week.Brief, PrismArt.Muted,
                PrismType.Body, content.Width) + Metrics.Space.Lg * scale;
        }

        var paneHeight = MathF.Min(content.Width, (content.Max.Y - top) * PickPaneFraction);
        var pane = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + paneHeight));
        pickSession.DrawPickPane(pane, scale, PickStyle, PrismStore.EntryRatio, PrismStore.EntryRevealsWholeImage,
            !store.Busy, Loc.T(L.Prism.EnterLook));

        var row = ActionRect(content, scale);
        var gridBottom = content.Max.Y - bottomChrome - PrismArt.BottomMargin(scale);
        var link = OutfitLink();
        if (link.Length > 0)
        {
            var linkRect = PlainRect(row.Center, link, row.Width, 0.5f, scale);
            if (DrawButton(drawList, linkRect, link, ButtonStyle.Tinted, scale, enabled: !store.Busy))
            {
                OpenOutfit();
            }

            gridBottom = linkRect.Min.Y - Metrics.Space.Lg * scale;
        }

        var tap = Metrics.Size.TapTarget * scale;
        var bodyRow = new Rect(new Vector2(row.Min.X, gridBottom - tap), new Vector2(row.Max.X, gridBottom));
        DrawBodyPicker(drawList, bodyRow, EntryRaceId, EntryGenderId, ref entryRace, ref entryGender, scale);
        gridBottom = bodyRow.Min.Y - Metrics.Space.Md * scale;
        var notice = store.Busy ? Loc.T(L.Prism.Uploading)
            : store.Failed(PrismAction.Enter) ? store.ActionFailureText is { Length: > 0 } reason
                ? reason
                : Loc.T(L.Common.LoadFailedHint)
            : pickSession.HasSelection && !bodyChosen ? Loc.T(L.Prism.BodyNeeded)
            : string.Empty;
        if (notice.Length > 0)
        {
            gridBottom -= Typography.MeasureWrappedBlock(notice, PrismType.Body, row.Width).Y;
            Typography.DrawWrappedLeft(new Vector2(row.Min.X, gridBottom), notice, PrismArt.Muted, PrismType.Body,
                row.Width);
            gridBottom -= Metrics.Space.Md * scale;
        }

        var gridRect = new Rect(new Vector2(content.Min.X, pane.Max.Y + Metrics.Space.Md * scale),
            new Vector2(content.Max.X, gridBottom));
        using (AppSurface.BeginEdgeToEdge(gridRect, smoothWheel: true))
        {
            if (pickSession.DrawPickGrid(gridRect, scale, PickStyle, false, Loc.T(L.Common.ImportFromPc),
                    Loc.T(L.Prism.EnterLook), pickIntake.ClipboardHasImage() ? Loc.T(L.Common.PasteImage) : null))
            {
                pickIntake.Paste(library);
            }
        }

        DrawBodyRaceMenu(content, ref entryRace);
    }

    private string PickHeading(PrismWeekDto? week)
    {
        if (!HasTracks)
        {
            return week?.Title ?? Loc.T(L.Prism.EnterLook);
        }

        var key = HashCode.Combine(store.Track, Loc.Current.Code);
        return pickHeadingText.IsCurrent(key)
            ? pickHeadingText.Value
            : pickHeadingText.Store(key, Loc.T(L.Prism.EnterTrack, PrismTracks.Name(store.Track)));
    }

    private string OutfitLink()
    {
        var count = VanillaEntry ? DraftPieces() : KeptModCount();
        var listed = VanillaEntry || capturedMods.Length > 0;
        var key = HashCode.Combine(VanillaEntry, count, listed, Loc.Current.Code);
        if (outfitLinkText.IsCurrent(key))
        {
            return outfitLinkText.Value;
        }

        var text = VanillaEntry
            ? count == 0 ? Loc.T(L.Prism.AddYourGear) : Loc.Plural(L.Prism.GearSummary, count)
            : listed ? Loc.Plural(L.Prism.ModsSummary, count) : string.Empty;
        return outfitLinkText.Store(key, text);
    }

    private int KeptModCount()
    {
        SyncCapturedMods();
        var count = 0;
        for (var index = 0; index < capturedMods.Length; index++)
        {
            if (!droppedMods.Contains(capturedMods[index].Key))
            {
                count++;
            }
        }

        return count;
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
