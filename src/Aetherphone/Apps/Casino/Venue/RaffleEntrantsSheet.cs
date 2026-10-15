using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class RaffleEntrantsSheet
{
    private const float PanelHeightShare = 0.78f;
    private const string RemoveId = "venue.raffle.entrant.remove";

    private readonly SheetSurface sheet = new("casino.venue.raffle.entrants");
    private readonly Action<Rect> drawSheetBody;
    private readonly CasinoTextCache texts;
    private readonly ConfirmService confirm;
    private readonly Action<VenueActDraft> act;

    private AppSkin skin = null!;
    private CasinoRaffleDto? raffle;
    private CasinoRaffleDto? linesSource;
    private LanguageInfo? linesLanguage;
    private string[] ticketLines = Array.Empty<string>();
    private string me = string.Empty;
    private bool gil;
    private bool enabled;

    public RaffleEntrantsSheet(CasinoTextCache texts, ConfirmService confirm, Action<VenueActDraft> act)
    {
        this.texts = texts;
        this.confirm = confirm;
        this.act = act;
        drawSheetBody = DrawSheetBody;
    }

    public void Open()
    {
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

    public void Track(CasinoRaffleDto? live, string myUserId, bool gilRoom, bool canAct)
    {
        raffle = live;
        me = myUserId;
        gil = gilRoom;
        enabled = canAct;
        if (live is null && sheet.IsOpen)
        {
            sheet.Close();
        }
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Venue.EntrantsTitle), PanelHeightShare, drawSheetBody);
    }

    internal static VenueActDraft Build(string userId)
    {
        return new VenueActDraft(VenueActions.RaffleRemove, TargetUserId: userId);
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##venueRaffleEntrants", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(skin, UiScale.Current);
        }
    }

    private void DrawBody(AppSkin ui, float scale)
    {
        VenueFields.Hint(ui, Loc.T(L.Venue.EntrantsHint), scale);
        var entrants = raffle?.Entrants ?? Array.Empty<CasinoRaffleEntrantDto>();
        if (entrants.Length == 0)
        {
            VenueFields.Hint(ui, Loc.T(L.Venue.NoEntrants), scale);
            return;
        }

        RefreshLines(entrants);
        var width = ScrollLayout.NativeScrollContentWidth();
        for (var index = 0; index < entrants.Length; index++)
        {
            using (ImRaii.PushId(index))
            {
                DrawRow(ui, entrants[index], ticketLines[index], width, scale);
            }
        }
    }

    private void RefreshLines(CasinoRaffleEntrantDto[] entrants)
    {
        if (ReferenceEquals(raffle, linesSource) && ReferenceEquals(linesLanguage, Loc.Current)
            && ticketLines.Length == entrants.Length)
        {
            return;
        }

        linesSource = raffle;
        linesLanguage = Loc.Current;
        ticketLines = new string[entrants.Length];
        for (var index = 0; index < entrants.Length; index++)
        {
            ticketLines[index] = Loc.Plural(L.Venue.TicketCount, entrants[index].Tickets);
        }
    }

    private void DrawRow(AppSkin ui, CasinoRaffleEntrantDto entrant, string line, float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var height = DoorLayout.PlayerHeight(scale, nameHeight, lineHeight);
        var row = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        ui.Card(drawList, row.Min, row.Max, Metrics.Radius.Grouped * scale);
        KnockCard.DrawAvatar(drawList, ui, DoorLayout.AvatarCenter(row, scale), entrant.DisplayName, scale);
        var removable = !string.Equals(entrant.UserId, me, StringComparison.Ordinal);
        var label = Loc.T(L.Casino.DoorRemove);
        var button = DoorLayout.TrailingButton(row, Button.WidthFor(label, ButtonSize.Large), scale);
        var textLeft = DoorLayout.TextLeft(row, scale);
        var textRight = removable ? button.Min.X - DoorLayout.TextGap * scale : row.Max.X - DoorLayout.Pad * scale;
        var textWidth = MathF.Max(1f, textRight - textLeft);
        var top = row.Center.Y - (nameHeight + lineHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(entrant.DisplayName, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(textLeft, top + nameHeight),
            Typography.FitText(line, textWidth, TextStyles.Subheadline), ui.BodyInk, TextStyles.Subheadline);
        if (removable && Button.Draw(drawList, button, label, ui.Ink, ButtonStyle.Gray, ButtonRole.Destructive,
                enabled, id: RemoveId))
        {
            AskRemove(entrant);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + DoorLayout.RowGap * scale));
    }

    private void AskRemove(CasinoRaffleEntrantDto entrant)
    {
        var userId = entrant.UserId;
        confirm.Ask(new ConfirmRequest
        {
            Title = texts.Named(L.Casino.DoorRemoveConfirmTitle, entrant.DisplayName),
            Message = Loc.T(gil ? L.Venue.RemoveEntrantBodyGil : L.Venue.RemoveEntrantBody),
            ConfirmLabel = Loc.T(L.Casino.DoorRemove),
            CancelLabel = Loc.T(L.Common.Cancel),
            Sheet = true,
            Danger = true,
            Confirm = () => act(Build(userId)),
        });
    }
}
