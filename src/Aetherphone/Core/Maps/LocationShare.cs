using System.Collections.Concurrent;
using System.Globalization;
using Aetherphone.Core.Game;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal readonly record struct SharedLocation(
    uint TerritoryId,
    uint MapId,
    float MapX,
    float MapY,
    uint WorldId,
    short Ward,
    short Plot,
    short Room);

internal static class LocationShare
{
    private const string TokenPrefix = "[aep.loc.v1:";
    private const char TokenSuffix = ']';
    private const char FieldSeparator = ';';
    private const int FieldCount = 8;
    private const int ParseCacheCapacity = 512;
    private const int MaxWardIndex = 30;
    private const int MaxPlotIndex = 60;

    private static readonly ConcurrentDictionary<string, (bool Ok, SharedLocation Location)> ParseCache =
        new(StringComparer.Ordinal);

    public static SharedLocation? Capture()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return null;
        }

        var player = Plugin.ObjectTable.LocalPlayer;
        var currentTerritoryId = Plugin.ClientState.TerritoryType;
        if (player is null || currentTerritoryId == 0)
        {
            return null;
        }

        var (ward, plot, room) = ReadHousing();
        var worldId = player.CurrentWorld.RowId;
        if (TryCaptureHousePlot(ward, plot, room, worldId, out var housePlot))
        {
            return housePlot;
        }

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(currentTerritoryId, out var territory))
        {
            return null;
        }

        var mapId = territory.Map.RowId;
        var mapX = 0f;
        var mapY = 0f;
        if (mapId != 0 && Plugin.DataManager.GetExcelSheet<Map>().TryGetRow(mapId, out var map))
        {
            var position = player.Position;
            mapX = ToMapCoordinate(position.X, map.SizeFactor, map.OffsetX);
            mapY = ToMapCoordinate(position.Z, map.SizeFactor, map.OffsetY);
        }

        return new SharedLocation(currentTerritoryId, mapId, mapX, mapY, worldId, ward, plot, room);
    }

    // Since patch 7.1 a house can wear any district's interior, so the territory it loads into names the
    // design's district (a classic design) or nothing at all (a newer one); only the house address knows
    // where it stands and only the plot outside has a map.
    private static bool TryCaptureHousePlot(short ward, short plot, short room, uint worldId,
        out SharedLocation location)
    {
        location = default;
        var districtId = ReadIndoorHouseDistrict();
        if (districtId == 0 || ZoneName(districtId).Length == 0)
        {
            return false;
        }

        var (mapId, mapX, mapY) = PlotMarker(districtId, plot);
        location = new SharedLocation(districtId, mapId, mapX, mapY, worldId, ward, plot, room);
        return true;
    }

    private static uint ReadIndoorHouseDistrict()
    {
        try
        {
            unsafe
            {
                var housing = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
                if (housing == null || housing->IndoorTerritory == null)
                {
                    return 0;
                }

                var house = housing->GetCurrentIndoorHouseId();
                if (house.IsApartment || house.IsWorkshop)
                {
                    return 0;
                }

                return house.TerritoryTypeId;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[LocationShare] house district read failed");
            return 0;
        }
    }

    private static (uint MapId, float MapX, float MapY) PlotMarker(uint districtId, short plot)
    {
        if (plot <= 0 || plot > HousingDistricts.PlotsPerWard)
        {
            return (0, 0f, 0f);
        }

        var plotIndex = (ushort)(plot - 1);
        if (!Plugin.DataManager.GetSubrowExcelSheet<HousingMapMarkerInfo>()
                .TryGetSubrow(districtId, plotIndex, out var marker)
            || marker.Map.RowId == 0
            || !Plugin.DataManager.GetExcelSheet<Map>().TryGetRow(marker.Map.RowId, out var map))
        {
            return (0, 0f, 0f);
        }

        return (marker.Map.RowId,
            ToMapCoordinate(marker.X, map.SizeFactor, map.OffsetX),
            ToMapCoordinate(marker.Z, map.SizeFactor, map.OffsetY));
    }

    public static uint CurrentWorldId()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return 0;
        }

        return Plugin.ObjectTable.LocalPlayer?.CurrentWorld.RowId ?? 0;
    }

    public static (short Ward, short Plot, short Room) CurrentHousing() => ReadHousing();

    public static string Compose(in SharedLocation location)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{TokenPrefix}{location.TerritoryId};{location.MapId};{location.MapX:0.0};{location.MapY:0.0};{location.WorldId};{location.Ward};{location.Plot};{location.Room}{TokenSuffix}");
    }

    public static bool IsToken(string? body) => TryParse(body, out _);

    public static bool TryParse(string? body, out SharedLocation location)
    {
        location = default;
        if (body is null || body.Length < TokenPrefix.Length + FieldCount * 2 - 1)
        {
            return false;
        }

        var text = body.AsSpan().Trim();
        if (text.Length != body.Length)
        {
            return TryParseExact(text.ToString(), out location);
        }

        return TryParseExact(body, out location);
    }

    private static bool TryParseExact(string body, out SharedLocation location)
    {
        location = default;
        var text = body.AsSpan();
        if (!text.StartsWith(TokenPrefix, StringComparison.Ordinal) || text[^1] != TokenSuffix)
        {
            return false;
        }

        if (ParseCache.TryGetValue(body, out var cached))
        {
            location = cached.Location;
            return cached.Ok;
        }

        var ok = TryParseFields(text[TokenPrefix.Length..^1], out location);
        if (ParseCache.Count >= ParseCacheCapacity)
        {
            ParseCache.Clear();
        }

        ParseCache[body] = (ok, location);
        return ok;
    }

    private static bool TryParseFields(ReadOnlySpan<char> inner, out SharedLocation location)
    {
        location = default;
        Span<Range> fieldRanges = stackalloc Range[FieldCount + 1];
        var fieldTotal = inner.Split(fieldRanges, FieldSeparator);
        if (fieldTotal != FieldCount)
        {
            return false;
        }

        var invariant = CultureInfo.InvariantCulture;
        if (!uint.TryParse(inner[fieldRanges[0]], NumberStyles.None, invariant, out var territoryId)
            || !uint.TryParse(inner[fieldRanges[1]], NumberStyles.None, invariant, out var mapId)
            || !float.TryParse(inner[fieldRanges[2]], NumberStyles.Float, invariant, out var mapX)
            || !float.TryParse(inner[fieldRanges[3]], NumberStyles.Float, invariant, out var mapY)
            || !uint.TryParse(inner[fieldRanges[4]], NumberStyles.None, invariant, out var worldId)
            || !short.TryParse(inner[fieldRanges[5]], NumberStyles.None, invariant, out var ward)
            || !short.TryParse(inner[fieldRanges[6]], NumberStyles.None, invariant, out var plot)
            || !short.TryParse(inner[fieldRanges[7]], NumberStyles.None, invariant, out var room))
        {
            return false;
        }

        if (territoryId == 0 || !float.IsFinite(mapX) || !float.IsFinite(mapY))
        {
            return false;
        }

        location = new SharedLocation(territoryId, mapId, mapX, mapY, worldId, ward, plot, room);
        return true;
    }

    public static void OpenMap(in SharedLocation location)
    {
        if (location.MapId == 0)
        {
            return;
        }

        try
        {
            var payload = new MapLinkPayload(location.TerritoryId, location.MapId, location.MapX, location.MapY);
            Plugin.GameGui.OpenMapWithMapLink(payload);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[LocationShare] open map failed");
        }
    }

    public static string ZoneName(uint territoryId)
    {
        if (territoryId == 0 ||
            !Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory) ||
            territory.PlaceName.RowId == 0)
        {
            return string.Empty;
        }

        return Plugin.DataManager.GetLocalizedSheet<PlaceName>().TryGetRow(territory.PlaceName.RowId, out var placeName)
            ? placeName.Name.ExtractText()
            : string.Empty;
    }

    public static string WorldName(uint worldId)
    {
        if (worldId != 0 && Plugin.DataManager.GetExcelSheet<World>().TryGetRow(worldId, out var world))
        {
            return world.Name.ExtractText();
        }

        return string.Empty;
    }

    public static string DataCenterName(uint worldId)
    {
        if (worldId != 0 && Plugin.DataManager.GetExcelSheet<World>().TryGetRow(worldId, out var world)
            && world.DataCenter.RowId != 0)
        {
            return world.DataCenter.Value.Name.ExtractText();
        }

        return string.Empty;
    }

    public static string WorldLine(in SharedLocation location)
    {
        var world = WorldName(location.WorldId);
        var dataCenter = DataCenterName(location.WorldId);
        if (world.Length > 0 && dataCenter.Length > 0)
        {
            return $"{world} · {dataCenter}";
        }

        return world.Length > 0 ? world : dataCenter;
    }

    public static string HousingLine(in SharedLocation location)
    {
        var line = Loc.T(L.DirectMessages.LocationWard, location.Ward);
        if (location.Plot > 0)
        {
            line = $"{line} · {Loc.T(L.DirectMessages.LocationPlot, location.Plot)}";
        }

        if (location.Room > 0)
        {
            line = $"{line} · {Loc.T(L.DirectMessages.LocationRoom, location.Room)}";
        }

        return line;
    }

    public static string Summary(in SharedLocation location)
    {
        var zone = ZoneName(location.TerritoryId);
        var worldLine = WorldLine(location);
        var headline = zone.Length > 0 && worldLine.Length > 0
            ? $"{zone} · {worldLine}"
            : zone.Length > 0 ? zone : worldLine;
        var detail = location.Ward > 0 ? HousingLine(location) : CoordinateText(location);
        if (headline.Length > 0 && detail.Length > 0)
        {
            return $"{headline}\n{detail}";
        }

        return headline.Length > 0 ? headline : detail;
    }

    public static string CoordinateText(in SharedLocation location)
    {
        if (location.MapId == 0)
        {
            return string.Empty;
        }

        return string.Create(CultureInfo.InvariantCulture, $"X: {location.MapX:0.0}  Y: {location.MapY:0.0}");
    }

    private static float ToMapCoordinate(float worldCoordinate, ushort sizeFactor, short offset)
    {
        var scale = (sizeFactor == 0 ? (ushort)100 : sizeFactor) / 100f;
        var scaled = (worldCoordinate + offset) * scale;
        return 41f / scale * ((scaled + 1024f) / 2048f) + 1f;
    }

    private static (short Ward, short Plot, short Room) ReadHousing()
    {
        try
        {
            unsafe
            {
                var housing = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
                if (housing == null)
                {
                    return (0, 0, 0);
                }

                var ward = OneBased(housing->GetCurrentWard(), MaxWardIndex);
                var plot = OneBased(housing->GetCurrentPlot(), MaxPlotIndex);
                var room = PositiveRoom(housing->GetCurrentRoom());
                if ((ward != 0 && plot != 0) || housing->IndoorTerritory == null)
                {
                    return (ward, plot, room);
                }

                var indoor = housing->GetCurrentIndoorHouseId();
                var indoorWard = OneBased(indoor.WardIndex, MaxWardIndex);
                if (indoorWard == 0)
                {
                    return (ward, plot, room);
                }

                return (indoorWard, plot != 0 ? plot : OneBased(indoor.PlotIndex, MaxPlotIndex),
                    room != 0 ? room : PositiveRoom(indoor.RoomNumber));
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[LocationShare] housing read failed");
            return (0, 0, 0);
        }
    }

    private static short OneBased(int index, int limit) =>
        index >= 0 && index < limit ? (short)(index + 1) : (short)0;

    private static short PositiveRoom(int roomNumber) => roomNumber > 0 ? (short)roomNumber : (short)0;
}
