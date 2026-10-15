using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal enum HostTableAction : byte
{
    None,
    GoToTable,
    Manage,
}

internal sealed class HostTableCard
{
    private const float Radius = 22f;
    private const float WashAlpha = 0.22f;
    private const float WashFloorAlpha = 0.03f;
    private const float RimAlpha = 0.6f;
    private const float RimWidth = 1.5f;
    private const float LineGap = 3f;
    private const float NoteGap = 10f;
    private const float NoteInset = 4f;

    private readonly CodeTiles codeTiles = new();

    private CasinoTableRowDto? viewRow;
    private CasinoTableDoorDto? viewDoor;
    private CasinoTableLedgerDto? viewLedger;
    private LanguageInfo? viewLanguage;
    private string name = string.Empty;
    private string line = string.Empty;
    private string crowd = string.Empty;
    private string gameId = CasinoGames.Blackjack;
    private bool book;

    public HostTableAction Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width,
        CasinoTableRowDto row, CasinoTableDoorDto? door, CasinoTableLedgerDto? ledger, bool enabled, float scale,
        out float bottom)
    {
        Refresh(row, door, ledger);
        var pill = TableStatusPill.Size(row.Paused, scale);
        var textWidth = HostHomeLayout.TableTextWidth(width, pill.X, scale);
        var nameHeight = Typography.LineHeight(TextStyles.Headline);
        var lineHeight = PitText.Height(line, TextStyles.Footnote, textWidth);
        var textBlock = nameHeight + LineGap * scale + lineHeight;
        var dot = CasinoArt.LiveDotRadius * scale;
        var crowdText = Typography.Measure(crowd, TextStyles.FootnoteEmphasized);
        var crowdWidth = book ? crowdText.X : crowdText.X + dot * 2f + Metrics.Space.Xs * scale;
        var layout = HostHomeLayout.Table(origin, width, scale, textBlock, pill, crowdWidth, crowdText.Y);
        var card = layout.Card;
        var radius = Radius * scale;
        ui.Card(drawList, card.Min, card.Max, radius);
        Squircle.FillVerticalGradient(drawList, card.Min, card.Max, radius,
            ImGui.GetColorU32(ui.Accent with { W = WashAlpha }), ImGui.GetColorU32(ui.Accent with { W = WashFloorAlpha }));
        Squircle.Stroke(drawList, card.Min, card.Max, radius, ImGui.GetColorU32(ui.Accent with { W = RimAlpha }),
            RimWidth * scale);
        CasinoArt.GameTile(drawList, gameId, layout.TileCenter, HostHomeLayout.Tile * scale);
        Typography.Draw(drawList, new Vector2(layout.TextLeft, layout.TextTop),
            Typography.FitText(name, textWidth, TextStyles.Headline), ui.TitleInk, TextStyles.Headline);
        PitText.Draw(drawList, line, TextStyles.Footnote, ui.BodyInk, layout.TextLeft,
            layout.TextTop + nameHeight + LineGap * scale, textWidth, false);
        TableStatusPill.Draw(drawList, layout.PillMin, row.Paused, scale);
        codeTiles.Draw(drawList, layout.CodeOrigin, layout.CodeTileWidth, layout.CodeTileHeight,
            HostHomeLayout.CodeGap * scale, CodeOf(row, door), TextStyles.Title3, scale);
        DrawCrowd(drawList, ui, layout, crowdText, crowdWidth, width, scale);
        var action = HostTableAction.None;
        if (Button.Draw(drawList, layout.Go, Loc.T(book ? L.Book.OpenLedger : L.HostDesk.GoToTable), ui.Ink,
                ButtonStyle.Prominent, enabled: enabled, id: "casino.host.go"))
        {
            action = HostTableAction.GoToTable;
        }

        if (Button.Draw(drawList, layout.Manage, Loc.T(L.HostDesk.Manage), ui.Ink, ButtonStyle.Gray, enabled: enabled,
                id: "casino.host.manage"))
        {
            action = HostTableAction.Manage;
        }

        var noteTop = card.Max.Y + NoteGap * scale;
        bottom = PitText.Draw(drawList, Loc.T(L.HostDesk.OneTable), TextStyles.Footnote, ui.MutedInk,
            origin.X + NoteInset * scale, noteTop, width - NoteInset * 2f * scale, false);
        return action;
    }

    internal static int CrowdOf(CasinoTableRowDto row, CasinoTableDoorDto? door)
    {
        if (door is null)
        {
            return VenueKinds.Of(row.GameKind) == VenueRoomKind.None ? row.SeatedCount : row.Occupancy;
        }

        var seated = door.Seated ?? Array.Empty<CasinoTableSeatedDto>();
        var count = seated.Length;
        var present = door.Present;
        if (present is null)
        {
            return count;
        }

        for (var index = 0; index < present.Length; index++)
        {
            var userId = present[index].UserId;
            if (string.Equals(userId, row.OwnerUserId, StringComparison.Ordinal) || Seated(seated, userId))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool Seated(CasinoTableSeatedDto[] seated, string userId)
    {
        for (var index = 0; index < seated.Length; index++)
        {
            if (string.Equals(seated[index].UserId, userId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string CodeOf(CasinoTableRowDto row, CasinoTableDoorDto? door)
    {
        var code = TableDoor.JoinCode(door, row);
        return code.Length == CasinoRoomCodes.Length ? code : string.Empty;
    }

    private void DrawCrowd(ImDrawListPtr drawList, AppSkin ui, in HostTableLayout layout, Vector2 crowdText,
        float crowdWidth, float width, float scale)
    {
        var left = layout.CrowdLeftCenter.X;
        var center = layout.CrowdLeftCenter.Y;
        var room = layout.CrowdInline ? crowdWidth : width - HostHomeLayout.Pad * 2f * scale;
        if (!book)
        {
            var dot = CasinoArt.LiveDotRadius * scale;
            CasinoArt.LiveDot(drawList, new Vector2(left + dot, center), scale, CasinoColors.LightA, true);
            left += dot * 2f + Metrics.Space.Xs * scale;
            room -= dot * 2f + Metrics.Space.Xs * scale;
        }

        Typography.Draw(drawList, new Vector2(left, center - crowdText.Y * 0.5f),
            Typography.FitText(crowd, MathF.Max(1f, room), TextStyles.FootnoteEmphasized), ui.TitleInk,
            TextStyles.FootnoteEmphasized);
    }

    private void Refresh(CasinoTableRowDto row, CasinoTableDoorDto? door, CasinoTableLedgerDto? ledger)
    {
        if (ReferenceEquals(row, viewRow) && ReferenceEquals(door, viewDoor) && ReferenceEquals(ledger, viewLedger)
            && ReferenceEquals(viewLanguage, Loc.Current))
        {
            return;
        }

        viewRow = row;
        viewDoor = door;
        viewLedger = ledger;
        viewLanguage = Loc.Current;
        name = TableRowViews.NameOf(row);
        line = TableRowViews.HostLine(row);
        gameId = TableRowViews.GameIdOf(row.GameKind);
        book = CasinoBookRules.IsBook(row.GameKind);
        if (book)
        {
            crowd = TableRowViews.BookLine(TableRowViews.BookEntriesOf(row, ledger), string.Empty);
            return;
        }

        var people = CrowdOf(row, door).ToString(Loc.Culture);
        var knocks = door?.Knocks?.Length ?? 0;
        crowd = knocks > 0
            ? Loc.T(L.HostDesk.HereKnocking, people, knocks.ToString(Loc.Culture))
            : Loc.T(L.HostDesk.Here, people);
    }
}
