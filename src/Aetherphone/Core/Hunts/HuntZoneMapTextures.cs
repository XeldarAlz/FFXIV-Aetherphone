using Aetherphone.Core.Maps;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntZoneMapTextures
{
    private readonly IDataManager data;
    private readonly ITextureProvider textures;
    private readonly Dictionary<uint, string?> texturePathByTerritory = new();

    public HuntZoneMapTextures(IDataManager data, ITextureProvider textures)
    {
        this.data = data;
        this.textures = textures;
    }

    public IDalamudTextureWrap? Resolve(uint territoryId)
    {
        var path = ResolveTexturePath(territoryId);
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
            AepLog.Debug(exception, $"Hunt zone map texture '{path}' failed to load");
            return null;
        }
    }

    private string? ResolveTexturePath(uint territoryId)
    {
        if (territoryId == 0)
        {
            return null;
        }

        if (texturePathByTerritory.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var path = BuildTexturePath(territoryId);
        texturePathByTerritory[territoryId] = path;
        return path;
    }

    private string? BuildTexturePath(uint territoryId)
    {
        if (data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is not { } territory
            || territory.Map.ValueNullable is not { } map)
        {
            return null;
        }

        var mapId = map.Id.ExtractText();
        return mapId.Length == 0 ? null : MapTextures.ResolveTexturePath(data, mapId, "Hunts");
    }
}
