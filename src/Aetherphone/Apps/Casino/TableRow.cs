using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino;

internal readonly record struct TableRowView(
    string Name,
    string Stakes,
    string Seats,
    string Spectators,
    bool Full,
    bool InviteOnly,
    bool Mine,
    bool Draining,
    int Currency,
    string CurrencyLabel,
    string Reputation,
    bool ReputationWarns,
    bool Paused,
    string GameId = "",
    string HostLine = "",
    string Monogram = "",
    int Seated = 0,
    int SeatCount = 0,
    VenueRoomKind Room = VenueRoomKind.None,
    string PhaseLabel = "",
    bool PhaseLive = false);

internal static class TableRow
{
    public const float Height = TableCardLayout.MinHeight;

    private const float PillPadX = 9f;
    private const float PillPadY = 3f;
    private const float PillDotGap = 6f;
    private const float FillAlpha = 0.16f;
    private const float RimAlpha = 0.40f;
    private const float EmptySeatAlpha = 0.22f;
    private const float ChipGap = 8f;
    private const int SeatDotLimit = 9;
    private const int DiscSegments = 28;

    private static readonly Vector4 GilTint = new(1f, 0.788f, 0.290f, 1f);

    public static float HeightOf(in TableRowView view)
    {
        var scale = MathF.Max(UiScale.Current, 0.0001f);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var reputation = view.Reputation.Length > 0 ? footnote + TableCardLayout.LineGap * scale : 0f;
        var row = MathF.Max(footnote, ChipHeight(scale));
        return TableCardLayout.Height(scale, Typography.LineHeight(TextStyles.Headline), footnote, row, reputation)
               / scale;
    }

    public static Vector4 CurrencyTint(int currency, Vector4 accent) => currency switch
    {
        CasinoCurrencies.Practice => CasinoColors.Practice,
        CasinoCurrencies.Gil => GilTint,
        _ => accent,
    };

    public static FontAwesomeIcon IconOf(VenueRoomKind room) => room switch
    {
        VenueRoomKind.Dice => FontAwesomeIcon.Dice,
        VenueRoomKind.Deathroll => FontAwesomeIcon.Skull,
        _ => FontAwesomeIcon.Ticket,
    };

    public static string PhaseOf(in TableRowView view)
    {
        if (view.PhaseLabel.Length > 0)
        {
            return view.PhaseLabel;
        }

        if (view.Draining)
        {
            return Loc.T(L.Casino.TableClosingBadge);
        }

        if (view.Paused)
        {
            return Loc.T(L.Tables.PausedBadge);
        }

        if (view.Mine)
        {
            return Loc.T(L.Casino.TableYoursBadge);
        }

        if (view.InviteOnly)
        {
            return Loc.T(L.Casino.TablePrivateBadge);
        }

        if (view.Room != VenueRoomKind.None)
        {
            return view.Seats;
        }

        return view.Full ? Loc.T(L.Casino.TableFullBadge) : Loc.T(L.Tables.PhaseOpen);
    }

    public static bool Live(in TableRowView view)
    {
        if (view.PhaseLabel.Length > 0)
        {
            return view.PhaseLive;
        }

        return !view.Draining && !view.Paused && (view.Room != VenueRoomKind.None ? view.Seated > 0 : !view.Full);
    }

    public static bool Draw(ImDrawListPtr drawList, in Rect row, AppSkin ui, in TableRowView view, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        ui.Card(drawList, row.Min, row.Max, radius);
        if (hovered)
        {
            Squircle.Fill(drawList, row.Min, row.Max, radius, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (view.Mine)
        {
            Squircle.Stroke(drawList, row.Min, row.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, RimAlpha)), 1.2f * scale);
        }

        var phase = PhaseOf(view);
        var live = Live(view);
        var headline = Typography.LineHeight(TextStyles.Headline);
        var footnote = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = MathF.Max(footnote, ChipHeight(scale));
        var layout = TableCardLayout.Compute(row, scale, headline, footnote, rowHeight, PillWidth(phase, live, scale),
            ChipHeight(scale));
        DrawArt(drawList, ui, view, layout, scale);
        DrawPill(drawList, layout.Phase, phase, live ? ui.Accent : ui.MutedInk, live, scale);
        CasinoArt.Chevron(drawList, layout.Chevron, ui.MutedInk);
        var nameWidth = MathF.Max(1f, layout.NameRight - layout.TextLeft);
        Typography.Draw(drawList, new Vector2(layout.TextLeft, layout.NameTop),
            Typography.FitText(view.Name, nameWidth, TextStyles.Headline), view.Draining ? ui.MutedInk : ui.TitleInk,
            TextStyles.Headline);
        var lineWidth = MathF.Max(1f, layout.LineRight - layout.TextLeft);
        var line = view.HostLine.Length > 0 && view.Stakes.Length == 0 ? view.HostLine : view.Stakes;
        Typography.Draw(drawList, new Vector2(layout.TextLeft, layout.LineTop),
            Typography.FitText(line, lineWidth, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
        DrawSeatRow(drawList, ui, view, layout, rowHeight, scale);
        if (view.Reputation.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(layout.TextLeft, layout.ReputationTop + TableCardLayout.LineGap * scale),
                Typography.FitText(view.Reputation, lineWidth, TextStyles.Footnote),
                view.ReputationWarns ? ui.Accent : GilTint, TextStyles.Footnote);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawArt(ImDrawListPtr drawList, AppSkin ui, in TableRowView view, in TableCardLayout layout,
        float scale)
    {
        var art = layout.Art;
        if (view.Room != VenueRoomKind.None)
        {
            CasinoArt.IconTileAt(drawList, art.Center, art.Width,
                Tables.HostGames.Of(Tables.HostGames.FromVenue(view.Room)).Tint, IconOf(view.Room));
        }
        else
        {
            CasinoArt.GameTile(drawList, view.GameId.Length > 0 ? view.GameId : CasinoGames.Blackjack, art.Center,
                art.Width);
        }

        if (view.Monogram.Length == 0)
        {
            return;
        }

        drawList.AddCircleFilled(layout.Avatar, layout.AvatarRadius + 2f * scale,
            ImGui.GetColorU32(ui.Palette.BackdropBottom), DiscSegments);
        AvatarView.Draw(drawList, layout.Avatar, layout.AvatarRadius, ui.Accent, view.Monogram,
            TextStyles.FootnoteEmphasized.Scale, AvatarHandle.Disabled, DiscSegments);
    }

    private static void DrawSeatRow(ImDrawListPtr drawList, AppSkin ui, in TableRowView view,
        in TableCardLayout layout, float rowHeight, float scale)
    {
        var centerY = layout.RowTop + rowHeight * 0.5f;
        var left = layout.TextLeft;
        var right = layout.LineRight;
        var chipWidth = view.CurrencyLabel.Length > 0 ? ChipWidth(view.CurrencyLabel, scale) : 0f;
        if (chipWidth > 0f)
        {
            var chipMin = new Vector2(right - chipWidth, centerY - ChipHeight(scale) * 0.5f);
            DrawChip(drawList, new Rect(chipMin, chipMin + new Vector2(chipWidth, ChipHeight(scale))), view, ui,
                scale);
            right = chipMin.X - ChipGap * scale;
        }

        if (view.Room == VenueRoomKind.None && view.SeatCount > 0)
        {
            var seatInk = view.Seated > 0 ? CasinoColors.Money : ui.MutedInk;
            var dotsWidth = CasinoArt.SeatDots(drawList, new Vector2(left, centerY), view.Seated,
                Math.Min(view.SeatCount, SeatDotLimit), seatInk, Palette.WithAlpha(ui.TitleInk, EmptySeatAlpha),
                scale);
            left += dotsWidth + Metrics.Space.Sm * scale;
        }

        var text = view.Spectators.Length > 0 ? view.Spectators : view.Room == VenueRoomKind.None ? view.Seats
            : view.HostLine;
        if (text.Length == 0 || right - left <= 0f)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, centerY - Typography.LineHeight(TextStyles.Footnote) * 0.5f),
            Typography.FitText(text, right - left, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
    }

    private static float ChipHeight(float scale) =>
        Typography.LineHeight(TextStyles.FootnoteEmphasized) + PillPadY * 2f * scale;

    private static float ChipWidth(string label, float scale) =>
        Typography.Measure(label, TextStyles.FootnoteEmphasized).X + PillPadX * 2f * scale;

    private static float PillWidth(string label, bool live, float scale) =>
        ChipWidth(label, scale) + (live ? CasinoArt.LiveDotRadius * 2f * scale + PillDotGap * scale : 0f);

    private static void DrawChip(ImDrawListPtr drawList, in Rect chip, in TableRowView view, AppSkin ui, float scale)
    {
        var tint = CurrencyTint(view.Currency, ui.Accent);
        var rounding = chip.Height * 0.5f;
        Squircle.Fill(drawList, chip.Min, chip.Max, rounding, ImGui.GetColorU32(Palette.WithAlpha(tint, FillAlpha)));
        if (view.Currency == CasinoCurrencies.Practice)
        {
            Squircle.Stroke(drawList, chip.Min, chip.Max, rounding,
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.55f)), 1f * scale);
        }

        Typography.DrawCentered(drawList, chip.Center, view.CurrencyLabel, tint, TextStyles.FootnoteEmphasized);
    }

    private static void DrawPill(ImDrawListPtr drawList, in Rect pill, string label, Vector4 ink, bool live,
        float scale)
    {
        var rounding = pill.Height * 0.5f;
        Squircle.Fill(drawList, pill.Min, pill.Max, rounding, ImGui.GetColorU32(Palette.WithAlpha(ink, FillAlpha)));
        var left = pill.Min.X + PillPadX * scale;
        if (live)
        {
            var dot = new Vector2(left + CasinoArt.LiveDotRadius * scale, pill.Center.Y);
            CasinoArt.LiveDot(drawList, dot, scale, ink, true);
            left = dot.X + CasinoArt.LiveDotRadius * scale + PillDotGap * scale;
        }

        Typography.Draw(drawList,
            new Vector2(left, pill.Center.Y - Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f), label, ink,
            TextStyles.FootnoteEmphasized);
    }
}
