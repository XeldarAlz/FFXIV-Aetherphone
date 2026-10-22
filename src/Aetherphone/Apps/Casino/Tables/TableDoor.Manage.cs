using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal sealed partial class TableDoor
{
    private const float FieldHeight = 40f;
    private const float FieldPadY = 10f;
    private const float DisabledAlpha = 0.45f;

    internal static int Controls(bool banked, bool hostDeals, DoorControl[] into)
    {
        var count = 0;
        if (banked)
        {
            into[count++] = DoorControl.Pause;
        }

        if (banked && hostDeals)
        {
            into[count++] = DoorControl.Deal;
        }

        if (banked)
        {
            into[count++] = DoorControl.CoDealers;
        }

        into[count++] = DoorControl.Rename;
        into[count++] = DoorControl.Close;
        return count;
    }

    internal static int ControlsFor(CasinoTableRowDto card, DoorControl[] into)
    {
        if (CasinoBookRules.IsBook(card.GameKind))
        {
            return Controls(false, false, into);
        }

        var banked = CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));
        var hostDeals = card.Config is not null && card.Config.DealerMode == CasinoDealerModes.Host;
        var count = Controls(banked, hostDeals, into);
        return TakesCoDealers(card) ? count : WithoutCoDealers(into, count);
    }

    internal static LocString? NoteFor(CasinoTableRowDto card)
    {
        if (CasinoBookRules.IsBook(card.GameKind))
        {
            return L.Book.DoorNote;
        }

        if (ExplainsHostLimits(card))
        {
            return L.Tables.ChipHostNote;
        }

        return TakesCoDealers(card) ? null : L.Tables.CoDealersHoldemNote;
    }

    internal static bool ControlEnabled(DoorControl control, bool busy, bool paused)
    {
        if (control == DoorControl.Deal && paused)
        {
            return false;
        }

        return !busy || control is DoorControl.CoDealers or DoorControl.Rename;
    }

    internal static bool ExplainsHostLimits(CasinoTableRowDto card)
    {
        return !CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(card));
    }

    internal static LocString CloseTitleFor(CasinoTableRowDto? card) =>
        card is not null && CasinoBookRules.IsBook(card.GameKind) ? L.Book.CloseTitle : L.HostDesk.CloseNamed;

    internal static LocString CloseBodyFor(CasinoTableRowDto? card)
    {
        if (card is not null && CasinoBookRules.IsBook(card.GameKind))
        {
            return L.Book.CloseBody;
        }

        return card is not null && ShowsGilLedger(card) ? L.HostDesk.CloseGilBody : L.HostDesk.CloseBody;
    }

    internal static LocString CloseLineFor(CasinoTableRowDto card)
    {
        if (CasinoBookRules.IsBook(card.GameKind))
        {
            return L.Book.CloseBody;
        }

        return ShowsGilLedger(card) ? L.HostDesk.CloseGilLine : L.HostDesk.CloseLine;
    }

    internal static LocString CloseLabelFor(CasinoTableRowDto? card) =>
        card is not null && CasinoBookRules.IsBook(card.GameKind) ? L.HostDesk.CloseBook : L.HostDesk.CloseTitle;

    private static FontAwesomeIcon IconOf(DoorControl control, CasinoTableRowDto card) => control switch
    {
        DoorControl.Pause => card.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause,
        DoorControl.Deal => FontAwesomeIcon.Clone,
        DoorControl.CoDealers => FontAwesomeIcon.UserTie,
        DoorControl.Rename => FontAwesomeIcon.PencilAlt,
        _ => FontAwesomeIcon.PowerOff,
    };

    private static Vector4 TintOf(DoorControl control) => control switch
    {
        DoorControl.Pause => AccentRing.Orange,
        DoorControl.Deal => AccentRing.Green,
        DoorControl.CoDealers => AccentRing.Indigo,
        DoorControl.Rename => AccentRing.Slate,
        _ => AccentRing.Red,
    };

    private string TitleOf(DoorControl control, CasinoTableRowDto card) => control switch
    {
        DoorControl.Pause => Loc.T(card.Paused ? L.HostDesk.ResumeTable : L.HostDesk.PauseTable),
        DoorControl.Deal => Loc.T(L.HostDesk.DealNext),
        DoorControl.CoDealers => Loc.T(L.Tables.ControlCoDealers),
        DoorControl.Rename => Loc.T(L.HostDesk.RenameTitle),
        _ => Loc.T(CloseLabelFor(card)),
    };

    private string LineOf(DoorControl control, CasinoTableRowDto card) => control switch
    {
        DoorControl.Pause => Loc.T(card.Paused ? L.HostDesk.ResumeLine : L.HostDesk.PauseLine),
        DoorControl.Deal => Loc.T(card.Paused ? L.HostDesk.DealPausedLine : L.HostDesk.DealLine),
        DoorControl.CoDealers => texts.Count(L.HostDesk.CoDealersLine, CasinoHostingRules.MaxCoDealers),
        DoorControl.Rename => Loc.T(L.HostDesk.RenameLine),
        _ => Loc.T(CloseLineFor(card)),
    };

    private string TrailingOf(DoorControl control, CasinoTableRowDto card) =>
        control == DoorControl.CoDealers
            ? texts.Counts(L.HostDesk.CoDealerCount, card.Config?.CoDealers?.Length ?? 0, CasinoHostingRules.MaxCoDealers)
            : string.Empty;

    private void DrawManage(AppSkin ui, CasinoTableRowDto card, float width, float scale)
    {
        DrawTitle(ui, Loc.T(L.HostDesk.ManageTitle), width, false, scale);
        var count = ControlsFor(card, controls);
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var textLeft = DoorLayout.ManageTextLeft(origin.X, scale);
        var total = 0f;
        for (var index = 0; index < count; index++)
        {
            total += RowHeight(controls[index], card, origin.X, width, titleHeight, scale);
            if (controls[index] == DoorControl.Rename && renaming)
            {
                total += (FieldHeight + FieldPadY * 2f) * scale;
            }
        }

        var frame = new Rect(origin, new Vector2(origin.X + width, origin.Y + total));
        ui.Card(drawList, frame.Min, frame.Max, Metrics.Radius.Grouped * scale);
        var top = origin.Y;
        var busy = tables.IntentInFlight;
        for (var index = 0; index < count; index++)
        {
            var control = controls[index];
            var height = RowHeight(control, card, origin.X, width, titleHeight, scale);
            var row = new Rect(new Vector2(origin.X, top), new Vector2(frame.Max.X, top + height));
            if (index > 0)
            {
                drawList.AddLine(new Vector2(textLeft, top), new Vector2(frame.Max.X, top),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            using (UiScope.PushId(index))
            {
                if (DrawRow(drawList, ui, control, card, row, index == 0, index == count - 1 && !(renaming
                        && control == DoorControl.Rename), ControlEnabled(control, busy, card.Paused), scale))
                {
                    Press(control, card);
                }
            }

            top = row.Max.Y;
            if (control == DoorControl.Rename && renaming)
            {
                var field = new Rect(new Vector2(textLeft, top + FieldPadY * scale),
                    new Vector2(frame.Max.X - DoorLayout.Pad * scale, top + (FieldPadY + FieldHeight) * scale));
                DrawRenameRow(ui, field, scale);
                top += (FieldHeight + FieldPadY * 2f) * scale;
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, total));
        if (NoteFor(card) is { } note)
        {
            Gap(Metrics.Space.Sm, scale);
            DrawNote(ui, Loc.T(note), width, scale);
        }
    }

    private float RowHeight(DoorControl control, CasinoTableRowDto card, float left, float width, float titleHeight,
        float scale)
    {
        var textWidth = ManageTextWidth(control, card, left, width, scale);
        return DoorLayout.ManageHeight(scale, titleHeight, PitText.Height(LineOf(control, card), TextStyles.Footnote,
            textWidth));
    }

    private float ManageTextWidth(DoorControl control, CasinoTableRowDto card, float left, float width, float scale)
    {
        var trailing = TrailingOf(control, card);
        var reserve = trailing.Length > 0
            ? Typography.Measure(trailing, TextStyles.FootnoteEmphasized).X + DoorLayout.TextGap * scale
            : 0f;
        return MathF.Max(1f, left + width - DoorLayout.Pad * scale - reserve - DoorLayout.ManageTextLeft(left, scale));
    }

    private bool DrawRow(ImDrawListPtr drawList, AppSkin ui, DoorControl control, CasinoTableRowDto card, in Rect row,
        bool first, bool last, bool enabled, float scale)
    {
        var tappable = enabled;
        var hovered = tappable && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            FillRowHover(drawList, ui, row, first, last, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var alpha = enabled ? 1f : DisabledAlpha;
        var icon = DoorLayout.ManageIcon * scale;
        var center = new Vector2(row.Min.X + DoorLayout.Pad * scale + icon * 0.5f, row.Center.Y);
        var half = new Vector2(icon * 0.5f, icon * 0.5f);
        IconTile.FillShaded(drawList, center - half, center + half, icon * Metrics.Radius.TileFactor,
            IconTile.Surface(TintOf(control)), alpha);
        ProgressRing.CenterIcon(drawList, center, IconOf(control, card), CasinoArt.White with { W = alpha },
            icon * 0.46f);
        var textLeft = DoorLayout.ManageTextLeft(row.Min.X, scale);
        var textWidth = ManageTextWidth(control, card, row.Min.X, row.Width, scale);
        var title = TitleOf(control, card);
        var line = LineOf(control, card);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(line, TextStyles.Footnote, textWidth);
        var top = row.Center.Y - (titleHeight + DoorLayout.ManageLineGap * scale + lineHeight) * 0.5f;
        var titleInk = control == DoorControl.Close ? ui.Theme.Danger : ui.TitleInk;
        Typography.Draw(drawList, new Vector2(textLeft, top), Typography.FitText(title, textWidth, TextStyles.Headline),
            titleInk with { W = titleInk.W * alpha }, TextStyles.Headline);
        PitText.Draw(drawList, line, TextStyles.Footnote, ui.MutedInk with { W = ui.MutedInk.W * alpha }, textLeft,
            top + titleHeight + DoorLayout.ManageLineGap * scale, textWidth, false);
        var trailing = TrailingOf(control, card);
        if (trailing.Length > 0)
        {
            var size = Typography.Measure(trailing, TextStyles.FootnoteEmphasized);
            Typography.Draw(drawList, new Vector2(row.Max.X - DoorLayout.Pad * scale - size.X, row.Center.Y - size.Y * 0.5f),
                trailing, ui.MutedInk, TextStyles.FootnoteEmphasized);
        }

        return tappable && UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void FillRowHover(ImDrawListPtr drawList, AppSkin ui, in Rect row, bool first, bool last,
        float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        var min = new Vector2(row.Min.X, first ? row.Min.Y : row.Min.Y - radius);
        var max = new Vector2(row.Max.X, last ? row.Max.Y : row.Max.Y + radius);
        drawList.PushClipRect(row.Min, row.Max, true);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(ui.HoverTint));
        drawList.PopClipRect();
    }

    private void Press(DoorControl control, CasinoTableRowDto card)
    {
        switch (control)
        {
            case DoorControl.Pause:
                inlineReason = string.Empty;
                tables.Pause(roomId, !card.Paused);
                break;
            case DoorControl.Deal:
                inlineReason = string.Empty;
                tables.Deal(roomId);
                openTable(roomId);
                break;
            case DoorControl.CoDealers:
                scrollToPlayers = true;
                break;
            case DoorControl.Rename:
                renaming = !renaming;
                break;
            case DoorControl.Close:
                AskClose();
                break;
        }
    }

    private void DrawRenameRow(AppSkin ui, in Rect area, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var label = Loc.T(L.Tables.RenameSave);
        var buttonWidth = DoorPill.WidthFor(label, area.Height, scale);
        var field = new Rect(area.Min,
            new Vector2(area.Max.X - buttonWidth - DoorLayout.ButtonGap * scale, area.Max.Y));
        Squircle.Fill(drawList, field.Min, field.Max, field.Height * 0.5f,
            ImGui.GetColorU32(Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary)));
        var submitted = GlassField.Text(field, "##doorName", Loc.T(L.Tables.HostNameHint), ref nameBuffer, ui.Theme,
            scale, CasinoHostingRules.NameMaxLength, false, ImGuiInputTextFlags.EnterReturnsTrue);
        var button = new Rect(new Vector2(area.Max.X - buttonWidth, area.Min.Y), area.Max);
        var tapped = DoorPill.Draw(drawList, button, label, ui.Ink, ButtonStyle.Prominent, null,
            !tables.IntentInFlight, "casino.door.rename", scale);
        if ((tapped || submitted) && !tables.IntentInFlight)
        {
            inlineReason = string.Empty;
            renaming = false;
            tables.Rename(roomId, nameBuffer.Trim());
        }
    }

    private void AskClose()
    {
        var target = roomId;
        var card = tables.CardFor(target);
        var name = card is null ? string.Empty : TableRowViews.NameOf(card);
        confirm.Ask(new ConfirmRequest
        {
            Title = texts.Named(CloseTitleFor(card), name),
            Message = Loc.T(CloseBodyFor(card)),
            ConfirmLabel = Loc.T(CloseLabelFor(card)),
            CancelLabel = Loc.T(L.HostDesk.KeepOpen),
            Sheet = true,
            Danger = true,
            Confirm = () => tables.CloseTable(target),
        });
    }
}
