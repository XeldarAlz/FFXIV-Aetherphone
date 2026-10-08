using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Venue;

internal readonly record struct NearbyRowText(string Name, string Detail);

internal sealed class NearbyTablesCard
{
    public const int MaxRows = 3;

    private const float RowHeight = 60f;
    private const float Pad = 16f;
    private const float Tile = 36f;
    private const float HeaderGap = 8f;

    private readonly CasinoVenueStore venue;
    private readonly NearbyRowText[] texts = new NearbyRowText[MaxRows];
    private CasinoTableRowDto[] source = Array.Empty<CasinoTableRowDto>();
    private LanguageInfo? language;
    private int count;

    public NearbyTablesCard(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public bool HasTables => venue.Nearby.Length > 0;

    public float Height(float scale)
    {
        Refresh();
        if (count == 0)
        {
            return 0f;
        }

        return Typography.LineHeight(TextStyles.Headline) + HeaderGap * scale + RowHeight * scale * count;
    }

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, Action<CasinoTableRowDto> open)
    {
        venue.EnsureNearby();
        Refresh();
        if (count == 0)
        {
            return origin.Y;
        }

        var scale = UiScale.Current;
        Typography.Draw(drawList, origin, Typography.FitText(Loc.T(L.Venue.AtThisVenue), width, TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        var top = origin.Y + Typography.LineHeight(TextStyles.Headline) + HeaderGap * scale;
        var rowHeight = RowHeight * scale;
        var card = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + rowHeight * count));
        var rounding = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, card.Min, card.Max, rounding);
        for (var index = 0; index < count; index++)
        {
            var row = new Rect(new Vector2(card.Min.X, top + index * rowHeight),
                new Vector2(card.Max.X, top + (index + 1) * rowHeight));
            if (DrawRow(drawList, ui, row, texts[index], index > 0, scale))
            {
                open(source[index]);
            }
        }

        return card.Max.Y;
    }

    internal static NearbyRowText TextOf(CasinoTableRowDto row)
    {
        var name = row.Name.Length > 0 ? row.Name : Loc.T(L.Casino.TableHostedBy, row.OwnerName);
        var kind = VenueKinds.Of(row.GameKind);
        var game = kind == VenueRoomKind.None ? Loc.T(L.Casino.GameBlackjack) : Loc.T(VenueCabinet.NameOf(kind));
        var detail = row.MaxSeats > 0
            ? Loc.T(L.Venue.NearbySeats, game, row.SeatedCount.ToString(Loc.Culture), row.MaxSeats.ToString(Loc.Culture))
            : Loc.T(L.Venue.NearbyRoom, game, row.Occupancy.ToString(Loc.Culture));
        return new NearbyRowText(name, detail);
    }

    private void Refresh()
    {
        var rows = venue.Nearby;
        if (ReferenceEquals(rows, source) && ReferenceEquals(language, Loc.Current))
        {
            return;
        }

        source = rows;
        language = Loc.Current;
        count = Math.Min(rows.Length, MaxRows);
        for (var index = 0; index < count; index++)
        {
            texts[index] = TextOf(rows[index]);
        }
    }

    private static bool DrawRow(ImDrawListPtr drawList, AppSkin ui, Rect row, in NearbyRowText text, bool hairline,
        float scale)
    {
        var pad = Pad * scale;
        if (hairline)
        {
            drawList.AddLine(new Vector2(row.Min.X + pad, row.Min.Y), new Vector2(row.Max.X, row.Min.Y),
                ImGui.GetColorU32(ui.Hairline), MathF.Max(1f, scale * 0.5f));
        }

        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var tile = Tile * scale;
        var tileCenter = new Vector2(row.Min.X + pad + tile * 0.5f, row.Center.Y);
        CasinoArt.IconTileAt(drawList, tileCenter, tile, ui.Accent, FontAwesomeIcon.MapMarkerAlt);
        var chevron = new Vector2(row.Max.X - pad, row.Center.Y);
        CasinoArt.Chevron(drawList, chevron, ui.MutedInk);
        var left = tileCenter.X + tile * 0.5f + Metrics.Space.Md * scale;
        var width = chevron.X - Metrics.Space.Md * scale - left;
        var nameHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var detailHeight = Typography.LineHeight(TextStyles.Footnote);
        var top = row.Center.Y - (nameHeight + detailHeight) * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(text.Name, width, TextStyles.SubheadlineEmphasized), ui.TitleInk,
            TextStyles.SubheadlineEmphasized);
        Typography.Draw(drawList, new Vector2(left, top + nameHeight),
            Typography.FitText(text.Detail, width, TextStyles.Footnote), ui.BodyInk, TextStyles.Footnote);
        return UiInteract.Click(row.Min, row.Max, hovered);
    }
}
