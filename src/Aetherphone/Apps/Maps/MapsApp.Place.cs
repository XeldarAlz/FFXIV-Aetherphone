using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Maps;

internal sealed partial class MapsApp
{
    private const float TeleportHeight = 50f;
    private const float PreviewHeight = 172f;
    private const float PreviewSpan = 0.42f;
    private const float DetailRowHeight = 46f;
    private const float BlockGap = 14f;
    private const float NoteGap = 8f;
    private const int DetailRowCount = 3;

    private string teleportNote = string.Empty;
    private string coordinateText = string.Empty;

    private void ReadTeleportInfo(MapAetheryte aetheryte)
    {
        coordinateText = aetheryte.HasPosition
            ? string.Create(CultureInfo.InvariantCulture, $"X {aetheryte.GameX:0.0}  Y {aetheryte.GameY:0.0}")
            : string.Empty;
        if (!lifestreamAvailable)
        {
            teleportNote = Loc.T(L.Maps.LifestreamHint);
            return;
        }

        teleportNote = string.Empty;
        try
        {
            var attuned = Plugin.AetheryteList;
            var count = attuned.Length;
            for (var index = 0; index < count; index++)
            {
                if (attuned[index] is not { SubIndex: 0 } entry || entry.AetheryteId != aetheryte.RowId)
                {
                    continue;
                }

                teleportNote = Loc.T(L.Maps.TeleportCost, entry.GilCost);
                return;
            }

            if (count > 0)
            {
                teleportNote = Loc.T(L.Travel.NotAttuned, aetheryte.Name);
            }
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Maps] aetheryte list read failed");
        }
    }

    private void DrawPlaceHeader(ImDrawListPtr drawList, Rect header, float scale)
    {
        if (openPlace is not { } place)
        {
            return;
        }

        var inset = Metrics.Space.Lg * scale;
        var top = header.Min.Y + SheetMetrics.GrabberZone * scale;
        var radius = MapChrome.CircleSize * 0.5f * scale;
        var closeCenter = new Vector2(header.Max.X - inset - radius, top + radius);
        if (MapChrome.Circle(drawList, "maps.place.close", closeCenter, PhoneIcons.X, theme.TextMuted, theme, scale,
                Loc.T(L.Common.Close), false))
        {
            PopPage();
            return;
        }

        var left = header.Min.X + inset;
        var width = MathF.Max(1f, closeCenter.X - radius - Metrics.Space.Md * scale - left);
        var titleSize = Typography.Measure(place.Name, TextStyles.Title2);
        Marquee.DrawLeftAuto(drawList, "maps.place.title", place.Name, left, top, width, TextStyles.Title2,
            theme.TextStrong);
        Marquee.DrawLeftAuto(drawList, "maps.place.subtitle", place.Subtitle, left,
            top + titleSize.Y + Metrics.Space.Xxs * scale, width, TextStyles.Subheadline, theme.TextMuted);
    }

    private void DrawPlacePage(float scale)
    {
        if (openPlace is not { } place)
        {
            PopPage();
            return;
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xxs * scale));
        DrawTeleport(place, scale);
        DrawPlaceTiles(place, scale);
        DrawPreview(place, scale);
        DrawDetails(place, scale);
    }

    private void DrawTeleport(MapAetheryte place, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var button = new Rect(origin, new Vector2(origin.X + width, origin.Y + TeleportHeight * scale));
        ReportVisible("maps.place.teleport", button);
        var label = lifestreamAvailable ? Loc.T(L.Maps.Teleport) : Loc.T(L.Maps.CopyCommand);
        if (ConfirmDialog.DrawPillButton(button, label, true, theme, 1f, 1f, ConfirmButtonTone.Primary,
                "maps.place.teleport"))
        {
            Teleport(place);
        }

        var bottom = button.Max.Y;
        if (teleportNote.Length > 0)
        {
            bottom = Typography.DrawWrappedCentered(ImGui.GetWindowDrawList(), teleportNote, TextStyles.Footnote,
                theme.TextMuted, new Vector2(button.Center.X, bottom + NoteGap * scale), width);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, bottom - origin.Y + BlockGap * scale));
    }

    private void DrawPlaceTiles(MapAetheryte place, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var onStage = IsOnStage(place);
        var count = onStage ? 3 : 2;
        var gap = Metrics.Space.Sm * scale;
        var tileWidth = (width - gap * (count - 1)) / count;
        var height = MapChrome.TileHeight * scale;
        var favorite = favorites.Contains(place.RowId);
        var tile = new Rect(origin, new Vector2(origin.X + tileWidth, origin.Y + height));
        ReportVisible("maps.place.favorite", tile);
        if (MapChrome.Tile(drawList, "maps.place.favorite", tile, favorite ? PhoneIcons.StarFilled : PhoneIcons.Star,
                Loc.T(L.Maps.Favorite), MapGlyphs.FavoriteStar, true, theme, scale))
        {
            ToggleFavorite(place.RowId);
        }

        if (onStage)
        {
            tile = new Rect(new Vector2(tile.Max.X + gap, origin.Y), new Vector2(tile.Max.X + gap + tileWidth,
                origin.Y + height));
            if (MapChrome.Tile(drawList, "maps.place.show", tile, PhoneIcons.MapPin, Loc.T(L.Maps.ShowOnMap),
                    theme.Accent, true, theme, scale))
            {
                UiFeedback.Play(UiSound.Tap);
                ShowOnMap(place);
            }
        }

        tile = new Rect(new Vector2(tile.Max.X + gap, origin.Y), new Vector2(origin.X + width, origin.Y + height));
        if (MapChrome.Tile(drawList, "maps.place.gameMap", tile, PhoneIcons.Compass, Loc.T(L.Maps.GameMap),
                theme.Accent, place.HasPosition, theme, scale))
        {
            OpenGameMap(place.TerritoryId, place.MapId, place.GameX, place.GameY);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + BlockGap * scale));
    }

    private void DrawPreview(MapAetheryte place, float scale)
    {
        if (!place.HasPosition)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + PreviewHeight * scale));
        var radius = Metrics.Radius.Grouped * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(theme.GroupedCard));
        if (ladder.Get(place.MapId, MapCanvas.PreviewPixels(rect, PreviewSpan)) is { } texture)
        {
            var pin = MapCanvas.Preview(drawList, rect, texture, place.U, place.V, PreviewSpan, radius);
            MapCanvas.Pin(drawList, pin, scale, 1f, true, accent);
            Material.EdgeSquircle(drawList, rect.Min, rect.Max, radius, scale);
            MapChrome.Chip(drawList, new Vector2(rect.Min.X + Metrics.Space.GlassInset * scale,
                rect.Max.Y - Metrics.Space.GlassInset * scale), place.ZoneName, theme, scale);
        }

        var onStage = IsOnStage(place);
        var hovered = onStage && UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            HoverTooltip.Show(rect, Loc.T(L.Maps.ShowOnMap), HoverLabelSide.Above);
        }

        if (onStage && UiInteract.Click(rect.Min, rect.Max, hovered))
        {
            UiFeedback.Play(UiSound.Tap);
            ShowOnMap(place);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rect.Height + BlockGap * scale));
    }

    private void DrawDetails(MapAetheryte place, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var card = GroupCard.Begin(theme, DetailRowCount, DetailRowHeight);
        DrawDetailRow(drawList, card.NextRow(), Loc.T(L.Maps.Zone), place.ZoneName);
        DrawDetailRow(drawList, card.NextRow(), Loc.T(L.Maps.Region), place.RegionName);
        DrawDetailRow(drawList, card.NextRow(), Loc.T(L.Maps.Coordinates), coordinateText);
        card.End();
    }

    private void DrawDetailRow(ImDrawListPtr drawList, Rect row, string label, string value)
    {
        var labelSize = Typography.Measure(label, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, theme.TextStrong,
            TextStyles.Body);
        var valueWidth = MathF.Max(1f, row.Width - labelSize.X - Metrics.Space.Md * UiScale.Current);
        var fitted = Typography.FitText(value, valueWidth, TextStyles.Body);
        var valueSize = Typography.Measure(fitted, TextStyles.Body);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fitted,
            theme.TextMuted, TextStyles.Body);
    }
}
