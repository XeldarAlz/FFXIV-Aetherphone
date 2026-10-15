using System.Globalization;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class RaffleTicketPicker
{
    public const float DeckHeight = CasinoStageLayout.DeckHeight + DeckActions.SecondaryHeight + DeckActions.Gap;
    public const string FewerLabel = "−";
    public const string MoreLabel = "+";

    private const int BufferLength = VenueRules.TicketDigits + 1;
    private const float FlashSeconds = 0.5f;
    private const string FieldId = "##venue.raffle.count";
    private const string TotalMarquee = "venue.raffle.total";
    private const string FewerId = "venue.raffle.fewer";
    private const string MoreId = "venue.raffle.more";
    private const string MaxId = "venue.raffle.max";

    private string buffer = string.Empty;
    private bool editing;
    private bool focusPending;
    private float flash;
    private int count = 1;
    private long raffleSeq = -1;

    public int Count => count;

    public void Reset()
    {
        buffer = string.Empty;
        editing = false;
        focusPending = false;
        flash = 0f;
        count = 1;
        raffleSeq = -1;
    }

    public static bool Offers(VenueRoomView? view, bool owner, string me)
    {
        if (view is not { Kind: VenueRoomKind.Raffle, Raffle.Raffle: { } live } || (owner && view.Gil))
        {
            return false;
        }

        return VenueRules.TicketsLeft(RaffleRoom.MyTickets(live, me), live.TicketsPerPerson) > 0;
    }

    public static float DeckHeightFor(VenueRoomView? view, bool owner, string me)
    {
        return Offers(view, owner, me) || OwnerRow(view, owner) ? DeckHeight : CasinoStageLayout.DeckHeight;
    }

    public static bool OwnerRow(VenueRoomView? view, bool owner)
    {
        return owner && view is { Kind: VenueRoomKind.Raffle, Gil: true, Raffle.Raffle: not null };
    }

    internal static VenueActDraft Build(int count)
    {
        return new VenueActDraft(VenueActions.RaffleTicket, count);
    }

    internal static int CountFrom(string text, int fallback)
    {
        return VenueFields.ParseInt(text, fallback);
    }

    public bool Draw(in VenueRoomFrame room, CasinoTextCache texts, CasinoRaffleDto live, Rect deck, int held)
    {
        var scale = room.Scale;
        if (live.Seq != raffleSeq)
        {
            raffleSeq = live.Seq;
            count = 1;
            editing = false;
        }

        var left = VenueRules.TicketsLeft(held, live.TicketsPerPerson);
        count = VenueRules.ClampTicketCount(count, held, live.TicketsPerPerson);
        flash = MathF.Max(0f, flash - room.Frame.DeltaSeconds);
        var row = DeckActions.Row(deck, scale);
        if (left == 0)
        {
            DrawCaption(room, texts, live, row);
            room.Stage.PrimaryAction(Loc.T(L.Venue.TakeTicket), false, room.Ui.Ink);
            return false;
        }

        var countRow = DeckActions.Above(row, DeckActions.SecondaryHeight * scale, scale);
        DrawCaption(room, texts, live, countRow);
        DrawCountRow(room.DrawList, room.Ui, countRow, left, room.Enabled, scale);
        var label = count > 1 ? texts.Count(L.Venue.TakeTickets, count) : Loc.T(L.Venue.TakeTicket);
        return room.Stage.PrimaryAction(label, room.Enabled, room.Ui.Ink) && room.Enabled;
    }

    private void DrawCaption(in VenueRoomFrame room, CasinoTextCache texts, CasinoRaffleDto live, Rect below)
    {
        var lineHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var above = DeckActions.Above(below, lineHeight, room.Scale);
        StageText.Label(room.DrawList, above.Center, Caption(texts, live, room.Gil), above.Width, TotalMarquee,
            TextStyles.SubheadlineEmphasized, false);
    }

    private string Caption(CasinoTextCache texts, CasinoRaffleDto live, bool gil)
    {
        if (!gil || live.TicketPrice <= 0)
        {
            return Loc.T(L.Venue.RaffleFree);
        }

        if (count <= 1)
        {
            return texts.Number(L.Venue.TicketPriceGil, live.TicketPrice);
        }

        return texts.Numbers(L.Venue.TicketsTotalGil, count, VenueRules.TicketsTotal(count, live.TicketPrice));
    }

    private void DrawCountRow(ImDrawListPtr drawList, AppSkin ui, Rect row, int left, bool enabled, float scale)
    {
        var style = Button.LabelStyle(row.Height);
        var gap = DeckActions.Gap * scale;
        var maxLabel = Loc.T(L.Casino.BetMax);
        var maxWidth = DeckActions.PillWidth(Typography.Measure(maxLabel, style).X, scale);
        var stepWidth = DeckActions.PillWidth(Typography.Measure(MoreLabel, style).X, scale);
        var fieldMin = DeckAmountRow.FieldMinWidth * scale;
        var showMax = row.Width - maxWidth - gap - (stepWidth + gap) * 2f >= fieldMin;
        var showSteps = row.Width - (stepWidth + gap) * 2f >= fieldMin;
        var right = row.Max.X;
        var maxRect = Place(row, ref right, showMax ? maxWidth : 0f, gap);
        var moreRect = Place(row, ref right, showSteps ? stepWidth : 0f, gap);
        var fewerRect = Place(row, ref right, showSteps ? stepWidth : 0f, gap);
        var field = new Rect(row.Min, new Vector2(MathF.Max(row.Min.X, right), row.Max.Y));
        DrawField(drawList, ui, field, left, enabled, scale);
        var stepping = enabled && left > 1;
        if (fewerRect.Width > 0f
            && DeckActions.DrawPill(fewerRect, FewerLabel, ui.Ink, ButtonStyle.Gray, stepping && count > 1, FewerId))
        {
            count--;
        }

        if (moreRect.Width > 0f
            && DeckActions.DrawPill(moreRect, MoreLabel, ui.Ink, ButtonStyle.Gray, stepping && count < left, MoreId))
        {
            count++;
        }

        if (maxRect.Width > 0f
            && DeckActions.DrawPill(maxRect, maxLabel, ui.Ink, ButtonStyle.Gray, stepping && count < left, MaxId))
        {
            count = left;
        }
    }

    private void DrawField(ImDrawListPtr drawList, AppSkin ui, Rect field, int left, bool enabled, float scale)
    {
        var capsule = DeckAmountRow.FieldSurface(drawList, field, ui.Ink);
        if (flash > 0f)
        {
            Squircle.Stroke(drawList, capsule.Min, capsule.Max, capsule.Height * 0.5f,
                ImGui.GetColorU32(Palette.WithAlpha(CasinoColors.Money, flash / FlashSeconds)),
                Metrics.Stroke.Ring * scale);
        }

        var inset = DeckAmountRow.FieldInset(scale);
        var captionStyle = KnobPicker.CaptionStyle(field.Height);
        var caption = Loc.T(L.Venue.TicketCountLabel);
        var captionWidth = Typography.Measure(caption, captionStyle).X;
        var captionHeight = Typography.LineHeight(captionStyle);
        Typography.Draw(drawList, new Vector2(capsule.Min.X + inset, capsule.Center.Y - captionHeight * 0.5f), caption,
            enabled ? CasinoColors.InkBody : ui.MutedInk, captionStyle);
        var valueLeft = capsule.Min.X + inset + captionWidth + KnobPicker.Gap * scale;
        var valueRoom = MathF.Max(1f, capsule.Max.X - inset - valueLeft);
        if (editing && enabled)
        {
            DrawEditor(field, valueLeft, valueRoom, left);
            return;
        }

        editing = false;
        var valueStyle = TextStyles.Headline;
        var value = Typography.FitText(GameNumber.Label(count), valueRoom, valueStyle);
        var size = Typography.Measure(value, valueStyle);
        Typography.Draw(drawList, new Vector2(valueLeft, capsule.Center.Y - size.Y * 0.5f), value,
            enabled ? CasinoColors.Money : ui.MutedInk, valueStyle);
        var hovered = enabled && UiInteract.Hover(field.Min, field.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.TextInput);
        }

        if (!UiInteract.Click(field.Min, field.Max, hovered))
        {
            return;
        }

        editing = true;
        focusPending = true;
        buffer = count.ToString(CultureInfo.InvariantCulture);
    }

    private void DrawEditor(Rect field, float left, float room, int ticketsLeft)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, field.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(room);
        if (focusPending)
        {
            ImGui.SetKeyboardFocusHere();
        }

        bool committed;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, CasinoColors.InkTitle))
        {
            committed = ImGui.InputText(FieldId, ref buffer, BufferLength,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll |
                ImGuiInputTextFlags.EnterReturnsTrue);
        }

        var active = ImGui.IsItemActive();
        if (focusPending)
        {
            focusPending = false;
            return;
        }

        if (!committed && active)
        {
            return;
        }

        editing = false;
        var typed = CountFrom(buffer, count);
        var clamped = Math.Clamp(typed, 1, Math.Max(1, ticketsLeft));
        if (clamped != typed)
        {
            flash = FlashSeconds;
        }

        count = clamped;
    }

    private static Rect Place(Rect row, ref float right, float width, float gap)
    {
        if (width <= 0f)
        {
            return new Rect(new Vector2(right, row.Min.Y), new Vector2(right, row.Max.Y));
        }

        var rect = new Rect(new Vector2(right - width, row.Min.Y), new Vector2(right, row.Max.Y));
        right = rect.Min.X - gap;
        return rect;
    }
}
