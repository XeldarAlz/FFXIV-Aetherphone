using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class NearbyTablesCard
{
    public const int MaxRows = 3;

    private const float HeaderGap = 8f;
    private const float CardGap = 10f;

    private readonly CasinoVenueStore venue;
    private readonly TableRowView[] views = new TableRowView[MaxRows];
    private CasinoTableRowDto[] source = Array.Empty<CasinoTableRowDto>();
    private LanguageInfo? language;
    private int count;

    public NearbyTablesCard(CasinoVenueStore venue)
    {
        this.venue = venue;
    }

    public bool HasTables => venue.Nearby.Length > 0;

    public float Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, Action<CasinoTableRowDto> open)
    {
        venue.EnsureNearby();
        Refresh();
        if (count == 0)
        {
            return origin.Y;
        }

        var scale = UiScale.Current;
        Typography.Draw(drawList, origin, Typography.FitText(Loc.T(L.Venue.AtThisVenue), width, TextStyles.Title3),
            ui.TitleInk, TextStyles.Title3);
        var top = origin.Y + Typography.LineHeight(TextStyles.Title3) + HeaderGap * scale;
        var gap = CardGap * scale;
        for (var index = 0; index < count; index++)
        {
            var height = TableRow.HeightOf(views[index]) * scale;
            var card = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + height));
            using (ImRaii.PushId(index))
            {
                if (TableRow.Draw(drawList, card, ui, views[index], scale))
                {
                    open(source[index]);
                }
            }

            top = card.Max.Y + gap;
        }

        return top - gap;
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
            views[index] = Tables.TableBrowser.ViewOf(rows[index], string.Empty);
        }
    }
}
