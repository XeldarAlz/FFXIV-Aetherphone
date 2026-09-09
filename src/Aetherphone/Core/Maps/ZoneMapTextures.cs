using Aetherphone.Core.Game;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed class ZoneMapTextures
{
    private const string Subsystem = "Maps";

    private readonly IDataManager data;
    private readonly ITextureProvider textures;
    private readonly Dictionary<uint, string?> texturePathByMap = new();
    private readonly Dictionary<uint, uint> mapByTerritory = new();

    public ZoneMapTextures(IDataManager data, ITextureProvider textures)
    {
        this.data = data;
        this.textures = textures;
    }

    public IDalamudTextureWrap? ForTerritory(uint territoryId) => ForMap(MapIdForTerritory(territoryId));

    public IDalamudTextureWrap? ForMap(uint mapRowId)
    {
        var path = ResolveTexturePath(mapRowId);
        if (path is null)
        {
            return null;
        }

        try
        {
            var texture = textures.GetFromGame(path).GetWrapOrDefault();
            return texture is null || texture.Handle == nint.Zero ? null : texture;
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"Zone map texture '{path}' failed to load");
            return null;
        }
    }

    public uint MapIdForTerritory(uint territoryId)
    {
        if (!GameSheets.Available)
        {
            return 0;
        }

        if (territoryId == 0)
        {
            return 0;
        }

        if (mapByTerritory.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var mapRowId = data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is { } territory
            ? territory.Map.RowId
            : 0u;
        mapByTerritory[territoryId] = mapRowId;
        return mapRowId;
    }

    private string? ResolveTexturePath(uint mapRowId)
    {
        if (mapRowId == 0)
        {
            return null;
        }

        if (texturePathByMap.TryGetValue(mapRowId, out var cached))
        {
            return cached;
        }

        var path = BuildTexturePath(mapRowId);
        texturePathByMap[mapRowId] = path;
        return path;
    }

    private string? BuildTexturePath(uint mapRowId)
    {
        if (!GameSheets.Available)
        {
            return null;
        }

        if (data.GetExcelSheet<Map>().GetRowOrDefault(mapRowId) is not { } map)
        {
            return null;
        }

        var mapId = map.Id.ExtractText();
        return mapId.Length == 0 ? null : MapTextures.ResolveTexturePath(data, mapId, Subsystem);
    }
}
