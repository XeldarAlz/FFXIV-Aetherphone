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
    private const float StatusPillUnits = 28f;
    private const float StatusGlyphUnits = 13f;
    private const string EntryPrimaryId = "prism.entry.primary";
    private const int EntryChoiceCount = 3;

    private static readonly Vector4 StatusPillFill = new(0.07f, 0.07f, 0.08f, 0.55f);

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
        if (editable && DrawEntryMore(drawList, content, scale))
        {
            OpenEntrySheet(retry);
        }

        var left = content.Min.X;
        var width = content.Width;
        var bottom = content.Max.Y - PrismArt.BottomMargin(scale);
        var actions = new Rect(new Vector2(left, bottom - Button.LargeHeight * scale),
            new Vector2(content.Max.X, bottom));
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
        var photo = new Rect(new Vector2(full.Min.X, top + Metrics.Space.Sm * scale),
            new Vector2(full.Max.X, textTop - Metrics.Space.Xl * scale));
        if (store.Busy)
        {
            PrismArt.Placeholder(drawList, photo, false, scale);
        }
        else
        {
            var frame = DrawLook(drawList, photo, entry.ImageUrl, true);
            DrawStatusPill(drawList, new Vector2(frame.Min.X + Metrics.Space.Md * scale,
                frame.Max.Y - Metrics.Space.Md * scale), frame.Width - Metrics.Space.Md * scale * 2f,
                StatusGlyph(entry.Status), Loc.T(StatusName(entry.Status)), scale);
        }

        textTop += PrismType.DrawHero(drawList, new Vector2(left, textTop), title, width, PrismArt.Fore);
        if (hint.Length > 0)
        {
            Typography.DrawWrappedLeft(new Vector2(left, textTop + Metrics.Space.Sm * scale), hint, PrismArt.Muted,
                PrismType.Body, width);
        }

        if (DrawButton(drawList, actions, Loc.T(retry ? L.Common.ChangePhoto : L.Prism.Done), ButtonStyle.Solid,
                scale, enabled: !retry || !store.Busy, id: EntryPrimaryId))
        {
            ActOnEntry(retry ? EntryChoice.ChangePhoto : EntryChoice.Done);
        }

        DrawEntrySheet(full);
    }

    private static bool DrawEntryMore(ImDrawListPtr drawList, Rect content, float scale)
    {
        var tap = Metrics.Size.TapTarget * scale;
        var hit = new Rect(new Vector2(content.Max.X - tap, content.Min.Y),
            new Vector2(content.Max.X, content.Min.Y + tap));
        var hovered = UiInteract.Hover(hit.Min, hit.Max);
        PhoneIcon.Draw(drawList, hit.Center, PhoneIcons.Dots, PrismArt.Fore, PhotoGlyphUnits * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(hit.Min, hit.Max, hovered);
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

        var picked = entrySheet.Draw(full, ActionSheetStyle.From(followedTheme!),
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

    private static void DrawStatusPill(ImDrawListPtr drawList, Vector2 bottomLeft, float maxWidth, string glyph,
        string label, float scale)
    {
        var pixels = PrismArt.LabelPixels();
        var height = StatusPillUnits * scale;
        var pad = Metrics.Space.Md * scale;
        var glyphSize = StatusGlyphUnits * scale;
        var chrome = pad * 2f + glyphSize + Metrics.Space.Xxs * scale;
        var fitted = PrismType.FitLabel(label, pixels, MathF.Max(1f, maxWidth - chrome));
        var min = new Vector2(bottomLeft.X, bottomLeft.Y - height);
        var max = new Vector2(min.X + chrome + PrismType.Measure(fitted, PrismFace.Label, pixels).X, bottomLeft.Y);
        var middle = (min.Y + max.Y) * 0.5f;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(StatusPillFill), height * 0.5f);
        PhoneIcon.Draw(drawList, new Vector2(min.X + pad + glyphSize * 0.5f, middle), glyph, PrismArt.PhotoFore,
            glyphSize);
        PrismType.Draw(drawList,
            new Vector2(min.X + pad + glyphSize + Metrics.Space.Xxs * scale,
                middle - PrismType.Snap(PrismFace.Label, pixels) * 0.5f), fitted, PrismArt.PhotoFore,
            PrismFace.Label, pixels);
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
