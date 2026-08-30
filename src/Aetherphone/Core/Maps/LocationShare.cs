using System.Collections.Concurrent;
using System.Globalization;
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
        var territoryId = NamedHousingTerritory(currentTerritoryId, ward);
        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
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

        return new SharedLocation(territoryId, mapId, mapX, mapY, player.CurrentWorld.RowId, ward, plot, room);
    }

    // Since patch 7.2 every house interior loads into one shared, nameless territory; the district only
    // survives through HousingManager, so a share from inside a house is stamped with the named territory.
    private static uint NamedHousingTerritory(uint territoryId, short ward)
    {
        if (ward == 0 || ZoneName(territoryId).Length > 0)
        {
            return territoryId;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        var (original, district) = ReadHouseTerritories();
        if (original != 0 && sheet.HasRow(original) && ZoneName(original).Length > 0)
        {
            return original;
        }

        if (district != 0 && sheet.HasRow(district) && ZoneName(district).Length > 0)
        {
            return district;
        }

        return territoryId;
    }

    private static (uint Original, uint District) ReadHouseTerritories()
    {
        try
        {
            unsafe
            {
                var housing = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance();
                if (housing == null)
                {
                    return (0, 0);
                }

                var original = FFXIVClientStructs.FFXIV.Client.Game.HousingManager.GetOriginalHouseTerritoryTypeId();
                var house = housing->IndoorTerritory != null
                    ? housing->GetCurrentIndoorHouseId()
                    : housing->GetCurrentHouseId();
                return (original, house.TerritoryTypeId);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[LocationShare] house territory read failed");
            return (0, 0);
        }
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
        if (territoryId != 0
            && Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territory))
        {
            return territory.PlaceName.Value.Name.ExtractText();
        }

        return string.Empty;
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
