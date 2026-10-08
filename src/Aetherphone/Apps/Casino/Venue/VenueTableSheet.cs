using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Report;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal enum VenueSheetRequest : byte
{
    None,
    Broadcast,
}

internal readonly record struct VenueSheetTarget(string TableId, bool Gil, bool CanBroadcast);

internal sealed class VenueTableSheet
{
    private const float PanelHeightShare = 0.66f;

    private readonly SheetSurface sheet = new("casino.venue.table");
    private readonly Action<Rect> drawSheetBody;
    private readonly CasinoVenueStore venue;
    private readonly CasinoTradeSync trade;
    private readonly ReportService report;

    private AppSkin skin = null!;
    private VenueSheetTarget target;
    private VenueSheetRequest request;

    public VenueTableSheet(CasinoVenueStore venue, CasinoTradeSync trade, ReportService report)
    {
        this.venue = venue;
        this.trade = trade;
        this.report = report;
        drawSheetBody = DrawSheetBody;
    }

    public bool IsOpen => sheet.IsOpen;

    public void Open(in VenueSheetTarget next)
    {
        target = next;
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

    public VenueSheetRequest TakeRequest()
    {
        var taken = request;
        request = VenueSheetRequest.None;
        return taken;
    }

    public void Draw(Rect screen, AppSkin ui)
    {
        skin = ui;
        sheet.Draw(screen, ui.Theme, Loc.T(L.Venue.TableSheet), PanelHeightShare, drawSheetBody);
    }

    internal static bool TryLastLine(VenueRoomView view, out long seq, out long bound, out long value,
        out bool deathroll)
    {
        seq = 0;
        bound = 0;
        value = 0;
        deathroll = false;
        if (view.Kind == VenueRoomKind.Dice)
        {
            var rolls = view.Dice?.Rolls;
            if (rolls is null || rolls.Length == 0)
            {
                return false;
            }

            seq = rolls[0].Seq;
            bound = rolls[0].Bound;
            value = rolls[0].Value;
            return true;
        }

        if (view.Kind != VenueRoomKind.Deathroll || view.Deathroll is null)
        {
            return false;
        }

        var duel = view.Deathroll.Duel ?? (view.Deathroll.Recent is { Length: > 0 } recent ? recent[0] : null);
        var lines = duel?.Rolls;
        if (lines is null || lines.Length == 0)
        {
            return false;
        }

        var newest = lines[0];
        for (var index = 1; index < lines.Length; index++)
        {
            if (lines[index].Seq > newest.Seq)
            {
                newest = lines[index];
            }
        }

        seq = newest.Seq;
        bound = newest.Bound;
        value = newest.Value;
        deathroll = true;
        return true;
    }

    private void DrawSheetBody(Rect content)
    {
        ImGui.SetCursorScreenPos(content.Min);
        using (ImRaii.Child("##venueTableBody", content.Size, false, ImGuiWindowFlags.NoBackground))
        {
            DrawBody(skin, UiScale.Current);
        }
    }

    private void DrawBody(AppSkin ui, float scale)
    {
        var view = venue.ViewFor(target.TableId);
        if (view is not null)
        {
            DrawVerify(ui, view, scale);
        }

        if (target.Gil)
        {
            DrawTradeSync(ui, scale);
        }

        if (target.CanBroadcast)
        {
            ActionRow(ui, Loc.T(L.Venue.BroadcastOpen), Loc.T(L.Venue.BroadcastHint), ButtonStyle.Tinted,
                ButtonRole.Normal, "venue.sheet.broadcast", out var pressed, scale);
            if (pressed)
            {
                request = VenueSheetRequest.Broadcast;
                sheet.Close();
            }
        }

        ActionRow(ui, Loc.T(L.Venue.ReportTable), Loc.T(L.Venue.ReportHint), ButtonStyle.Gray,
            ButtonRole.Destructive, "venue.sheet.report", out var reportPressed, scale);
        if (reportPressed)
        {
            OpenReport();
        }
    }

    private void DrawVerify(AppSkin ui, VenueRoomView view, float scale)
    {
        var result = venue.Verification;
        var status = result is null || !string.Equals(result.RoomId, view.RoomId, StringComparison.Ordinal)
            ? Loc.T(L.Venue.VerifyHint)
            : !result.Done
                ? Loc.T(L.Venue.VerifyChecking)
                : result.Verdict switch
                {
                    CasinoRoundVerdict.Match => Loc.T(L.Venue.VerifyMatch),
                    CasinoRoundVerdict.Mismatch => Loc.T(L.Venue.VerifyMismatch),
                    _ => Loc.T(L.Venue.VerifyPending),
                };
        ActionRow(ui, Loc.T(L.Venue.VerifyLast), status, ButtonStyle.Tinted, ButtonRole.Normal, "venue.sheet.verify",
            out var pressed, scale);
        if (!pressed)
        {
            return;
        }

        if (view.Kind == VenueRoomKind.Raffle)
        {
            var last = view.Raffle?.Last;
            if (last is not null && last.Drawn)
            {
                venue.VerifyRaffle(view.RoomId, last);
            }

            return;
        }

        if (TryLastLine(view, out var seq, out var bound, out var value, out var deathroll))
        {
            venue.VerifyRoll(view.RoomId, seq, bound, value, deathroll);
        }
    }

    private void DrawTradeSync(AppSkin ui, float scale)
    {
        ui.SectionHeading(Loc.T(L.Venue.TradeSync), Metrics.Space.Sm);
        var card = GroupCard.Begin(ui, 2, VenueFields.RowUnits);
        var enabled = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), "venue.trade.sync",
            Loc.T(L.Venue.TradeSyncToggle), trade.Enabled, scale);
        if (enabled != trade.Enabled)
        {
            trade.Enabled = enabled;
        }

        var auto = VenueFields.ToggleRow(ui, card.NextRow(VenueFields.RowUnits), "venue.trade.auto",
            Loc.T(L.Venue.TradeAutoConfirm), trade.AutoConfirm, scale);
        if (auto != trade.AutoConfirm)
        {
            trade.AutoConfirm = auto;
        }

        card.End();
        VenueFields.Hint(ui, Loc.T(L.Venue.TradeSyncHint), scale);
    }

    private static void ActionRow(AppSkin ui, string label, string hint, ButtonStyle style, ButtonRole role,
        string id, out bool pressed, float scale)
    {
        var width = ScrollLayout.NativeScrollContentWidth();
        var origin = ImGui.GetCursorScreenPos();
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + Button.LargeHeight * scale));
        pressed = Button.Draw(rect, label, ui.Ink, style, role, id: id);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Button.LargeHeight * scale));
        var hintOrigin = ImGui.GetCursorScreenPos();
        var height = Typography.DrawWrappedLeft(new Vector2(hintOrigin.X + Metrics.Space.Sm * scale,
                hintOrigin.Y + Metrics.Space.Xs * scale), hint, ui.BodyInk, TextStyles.Footnote,
            width - Metrics.Space.Sm * 2f * scale);
        ImGui.SetCursorScreenPos(hintOrigin);
        ImGui.Dummy(new Vector2(width, height + Metrics.Space.Md * scale));
    }

    private void OpenReport()
    {
        var tableId = target.TableId;
        if (tableId.Length == 0)
        {
            return;
        }

        sheet.Close();
        report.Open(new ReportPrompt
        {
            Title = Loc.T(L.Venue.ReportTable),
            Submit = (reason, done) => venue.Report(tableId, reason, done),
        });
    }
}
