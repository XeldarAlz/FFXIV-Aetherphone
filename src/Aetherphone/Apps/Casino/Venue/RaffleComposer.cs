using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class RaffleComposer
{
    private const float PanelHeightShare = 0.78f;
    private const int MinutesDigits = 4;
    private const int CountDigits = 3;

    private readonly SheetSurface sheet = new("casino.venue.raffle");
    private readonly Action<Rect> drawSheetBody;
    private readonly Action<VenueActDraft> act;

    private AppSkin skin = null!;
    private bool gil;
    private string title = string.Empty;
    private string tickets = string.Empty;
    private string winners = string.Empty;
    private string minutes = string.Empty;
    private string ticketPrice = string.Empty;
    private string prize = string.Empty;
    private bool invalid;

    public RaffleComposer(Action<VenueActDraft> act)
    {
        this.act = act;
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open(bool gilRoom)
    {
        gil = gilRoom;
        invalid = false;
        sheet.Open();
    }

    public void Close()
    {
        sheet.Close();
    }

    public void Gate()
    {
        if (sheet.IsOpen)
        {
            UiInteract.BlockThisFrame();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Venue.RaffleOpenAction), PanelHeightShare, drawSheetBody);
    }

    internal static VenueActDraft Build(string title, int ticketsPerPerson, int winners, int minutes, long ticketPrice,
        long prize, bool gil)
    {
        return new VenueActDraft(VenueActions.RaffleOpen, ticketsPerPerson, title.Trim(), winners, minutes * 60,
            TicketPrice: gil ? ticketPrice : 0, Prize: gil ? prize : 0);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##venueRaffleBody", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(skin, UiScale.Current);
        }
    }

    private void DrawBody(AppSkin ui, float scale)
    {
        var rows = gil ? 6 : 4;
        var card = GroupCard.Begin(ui, rows, VenueFields.RowUnits);
        VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##raffleTitle", Loc.T(L.Venue.RaffleTitle),
            Loc.T(L.Venue.RaffleTitleHint), ref title, VenueRules.TitleMaxLength, false, scale);
        VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##raffleTickets", Loc.T(L.Venue.TicketsEach),
            "1", ref tickets, CountDigits, true, scale);
        VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##raffleWinners", Loc.T(L.Venue.WinnerCount),
            "1", ref winners, CountDigits, true, scale);
        VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##raffleMinutes", Loc.T(L.Venue.RunsMinutes),
            "10", ref minutes, MinutesDigits, true, scale);
        if (gil)
        {
            VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##raffleTicketPrice",
                Loc.T(L.Venue.TicketPrice), Loc.T(L.Venue.GilOptional), ref ticketPrice, VenueFields.AmountDigits,
                true, scale);
            VenueFields.TextRow(ui, card.NextRow(VenueFields.RowUnits), "##rafflePrize", Loc.T(L.Venue.Prize),
                Loc.T(L.Venue.GilOptional), ref prize, VenueFields.AmountDigits, true, scale);
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(gil ? L.Venue.RaffleGilHint : L.Venue.RaffleHint), scale);
        if (invalid)
        {
            VenueFields.Hint(ui, Loc.T(L.Tables.ReasonConfigInvalid), scale);
        }

        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + Button.LargeHeight * scale));
        if (Button.Draw(rect, Loc.T(L.Venue.RaffleStart), ui.Ink, ButtonStyle.Prominent, id: "venue.raffle.start"))
        {
            Submit();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Button.LargeHeight * scale + Metrics.Space.Lg * scale));
    }

    private void Submit()
    {
        var perPerson = VenueFields.ParseInt(tickets, 1);
        var winnerCount = VenueFields.ParseInt(winners, 1);
        var runMinutes = VenueFields.ParseInt(minutes, VenueRules.DefaultRaffleSeconds / 60);
        if (!VenueRules.IsRaffle(title, perPerson, winnerCount, runMinutes * 60))
        {
            invalid = true;
            return;
        }

        invalid = false;
        act(Build(title, perPerson, winnerCount, runMinutes, VenueFields.Parse(ticketPrice), VenueFields.Parse(prize),
            gil));
        sheet.Close();
    }
}
