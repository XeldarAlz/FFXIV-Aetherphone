using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal enum EntryChoice : byte
{
    Done,
    ChangePhoto,
    EditOutfit,
    Withdraw,
}

internal sealed partial class PrismApp
{
    private const int EntryChoiceCount = 3;

    private readonly ActionSheet entrySheet = new();
    private readonly ActionSheet.Item[] entrySheetItems = new ActionSheet.Item[EntryChoiceCount];
    private readonly EntryChoice[] entryChoices = new EntryChoice[EntryChoiceCount];
    private int entrySheetCount;

    private void DrawEntry(ImDrawListPtr drawList, Rect full, Rect content, float scale)
    {
        if (store.Week is not { MyEntry: { } entry } week)
        {
            Go(PrismScreen.Cover);
            return;
        }

        entrySheet.Gate();
        var top = DrawBackHeader(drawList, content, week.Title, scale);
        var editable = week.Phase == PrismPhases.Entering;
        var retry = editable && entry.Status == PrismPhases.StatusRejected;
        var reach = Metrics.Size.TapTarget * scale * 0.5f;
        if (editable && IconButton(drawList, new Vector2(content.Max.X - reach, content.Min.Y + reach), PhoneIcons.Dots,
                Loc.T(L.Prism.More), scale, HoverLabelSide.Below))
        {
            OpenEntrySheet(retry);
        }

        var left = content.Min.X;
        var width = content.Width;
        var actions = ActionRect(content, scale);
        var title = Loc.T(StatusTitle(entry.Status));
        var entryFailed = !store.Busy && (store.Failed(PrismAction.Enter) || store.Failed(PrismAction.Withdraw) ||
                                          store.Failed(PrismAction.Outfit));
        var failure = entryFailed ? store.ActionFailureText : string.Empty;
        var hint = entryFailed
            ? failure.Length > 0 ? failure : Loc.T(L.Common.LoadFailedHint)
            : entry.Status is PrismPhases.StatusRejected or PrismPhases.StatusMoved
                ? entry.RejectReason
                : string.Empty;
        var hintHeight = hint.Length > 0
            ? Typography.MeasureWrappedBlock(hint, PrismType.Body, width).Y + Metrics.Space.Sm * scale
            : 0f;
        var textTop = actions.Min.Y - Metrics.Space.Xl * scale - hintHeight - PrismType.HeroHeight(title, width);
        var photoTop = top + Metrics.Space.Sm * scale;
        var photo = new Rect(new Vector2(full.Min.X, photoTop),
            new Vector2(full.Max.X, MathF.Max(photoTop, textTop - Metrics.Space.Xl * scale)));
        if (store.Busy)
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
        }
        else
        {
            var frame = DrawLook(drawList, photo, entry.ImageUrl, true);
            PrismArt.PhotoPill(drawList, new Vector2(frame.Min.X + Metrics.Space.Md * scale,
                frame.Max.Y - Metrics.Space.Md * scale), frame.Width - Metrics.Space.Md * scale * 2f,
                StatusGlyph(entry.Status), Loc.T(StatusName(entry.Status)), false, scale);
        }

        textTop += PrismType.DrawHero(drawList, new Vector2(left, textTop), title, width, PrismArt.Fore);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, textTop + Metrics.Space.Sm * scale), hint, PrismArt.Muted,
                PrismType.Body, width);
        }

        if (DrawButton(drawList, actions, Loc.T(retry ? L.Common.ChangePhoto : L.Prism.Done), ButtonStyle.Solid,
                scale, enabled: !retry || !store.Busy, id: "prism.entry.primary"))
        {
            ActOnEntry(retry ? EntryChoice.ChangePhoto : EntryChoice.Done);
        }

        DrawEntrySheet(full);
    }

    private void OpenEntrySheet(bool retry)
    {
        entrySheetCount = 0;
        if (!retry)
        {
            AddEntryChoice(EntryChoice.ChangePhoto, Loc.T(L.Common.ChangePhoto), false);
        }

        AddEntryChoice(EntryChoice.EditOutfit, Loc.T(VanillaEntry ? L.Prism.EditGear : L.Prism.EditMods), false);
        AddEntryChoice(EntryChoice.Withdraw, Loc.T(L.Prism.Withdraw), true);
        entrySheet.Open();
    }

    private void AddEntryChoice(EntryChoice choice, string label, bool danger)
    {
        entryChoices[entrySheetCount] = choice;
        entrySheetItems[entrySheetCount] = new ActionSheet.Item(label, string.Empty, danger);
        entrySheetCount++;
    }

    private void DrawEntrySheet(Rect full)
    {
        if (!entrySheet.CapturesPointer)
        {
            return;
        }

        var picked = entrySheet.Draw(full, ActionSheetStyle.From(ui),
            entrySheetItems.AsSpan(0, entrySheetCount), Loc.T(L.Common.Cancel), false);
        if (picked >= 0 && !store.Busy)
        {
            ActOnEntry(entryChoices[picked]);
        }
    }

    private void ActOnEntry(EntryChoice choice)
    {
        switch (choice)
        {
            case EntryChoice.ChangePhoto:
                StartPick();
                break;
            case EntryChoice.EditOutfit:
                OpenOutfit();
                break;
            case EntryChoice.Withdraw:
                confirm.Ask(new ConfirmRequest
                {
                    Title = Loc.T(L.Prism.WithdrawTitle),
                    Message = Loc.T(L.Prism.WithdrawBody),
                    ConfirmLabel = Loc.T(L.Prism.Withdraw),
                    CancelLabel = Loc.T(L.Common.Cancel),
                    Sheet = true,
                    Confirm = withdraw,
                });
                break;
            default:
                Go(PrismScreen.Cover, -1);
                break;
        }
    }

    private static string StatusGlyph(string status) => status switch
    {
        PrismPhases.StatusRejected => PhoneIcons.Ban,
        PrismPhases.StatusMoved => PhoneIcons.ArrowForwardUp,
        _ => PhoneIcons.CircleCheck,
    };

    private static LocString StatusTitle(string status) => status switch
    {
        PrismPhases.StatusRejected => L.Prism.StatusRejectedTitle,
        PrismPhases.StatusMoved => L.Prism.StatusMovedTitle,
        _ => L.Prism.StatusLiveTitle,
    };
}
