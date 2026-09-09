using System.Globalization;
using Aetherphone.Core.Localization;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Maps;

internal sealed class MinimapReader
{
    private const float MapIntervalSeconds = 0.5f;
    private const float CoordinateIntervalSeconds = 0.2f;

    private readonly ZoneMapTextures textures;
    private CultureInfo? coordinateCulture;
    private float clock;
    private float mapDue = -1f;
    private float coordinateDue;
    private uint mapRowId;
    private int sizeFactor = 100;
    private int offsetX;
    private int offsetY;
    private int coordinateKey = int.MinValue;

    public MinimapReader(ZoneMapTextures textures)
    {
        this.textures = textures;
    }

    public bool HasMap { get; private set; }

    public bool HasPlayer { get; private set; }

    public float PlayerU { get; private set; }

    public float PlayerV { get; private set; }

    public float Facing { get; private set; }

    public float PixelsPerYalm => Math.Max(sizeFactor, 1) / 100f;

    public string ZoneName { get; private set; } = string.Empty;

    public string Coordinates { get; private set; } = string.Empty;

    public IDalamudTextureWrap? Texture => HasMap ? textures.ForMap(mapRowId) : null;

    public void Update(float delta)
    {
        clock += delta;
        RefreshMap();
        RefreshPlayer();
    }

    private void RefreshMap()
    {
        if (clock < mapDue)
        {
            return;
        }

        mapDue = clock + MapIntervalSeconds;
        var resolved = ResolveMapRowId();
        if (resolved == mapRowId)
        {
            return;
        }

        mapRowId = resolved;
        coordinateKey = int.MinValue;
        ReadMapMetrics(resolved);
    }

    private void ReadMapMetrics(uint rowId)
    {
        HasMap = false;
        ZoneName = string.Empty;
        Coordinates = string.Empty;
        if (rowId == 0 || !Plugin.DataManager.GetExcelSheet<Map>().TryGetRow(rowId, out var map))
        {
            return;
        }

        sizeFactor = map.SizeFactor;
        offsetX = map.OffsetX;
        offsetY = map.OffsetY;
        ZoneName = PlaceName(map.PlaceName.RowId);
        HasMap = true;
    }

    private void RefreshPlayer()
    {
        HasPlayer = false;
        if (!HasMap)
        {
            return;
        }

        var player = Plugin.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return;
        }

        var position = player.Position;
        var rawX = MapPixelMath.ToCanvasPixel(position.X, sizeFactor, offsetX);
        var rawY = MapPixelMath.ToCanvasPixel(position.Z, sizeFactor, offsetY);
        PlayerU = rawX / MapPixelMath.FullCanvasSize;
        PlayerV = rawY / MapPixelMath.FullCanvasSize;
        Facing = player.Rotation;
        HasPlayer = true;
        RefreshCoordinates(rawX, rawY);
    }

    private void RefreshCoordinates(float rawX, float rawY)
    {
        var culture = Loc.Culture;
        var stale = !ReferenceEquals(coordinateCulture, culture);
        if (clock < coordinateDue && !stale)
        {
            return;
        }

        coordinateDue = clock + CoordinateIntervalSeconds;
        var (x, y) = MapPixelMath.ToGameCoordinate(rawX, rawY, sizeFactor);
        var key = (int)MathF.Round(x * 10f) * 10000 + (int)MathF.Round(y * 10f);
        if (key == coordinateKey && !stale)
        {
            return;
        }

        coordinateKey = key;
        coordinateCulture = culture;
        Coordinates = string.Create(culture, $"{x:0.0}, {y:0.0}");
    }

    private uint ResolveMapRowId()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return 0;
        }

        var current = CurrentAgentMapId();
        return current != 0 ? current : textures.MapIdForTerritory(Plugin.ClientState.TerritoryType);
    }

    private static unsafe uint CurrentAgentMapId()
    {
        try
        {
            var agent = AgentMap.Instance();
            return agent == null ? 0u : agent->CurrentMapId;
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[Minimap] current map read failed");
            return 0;
        }
    }

    private static string PlaceName(uint placeNameRowId) =>
        placeNameRowId != 0 && Plugin.DataManager.GetExcelSheet<PlaceName>().TryGetRow(placeNameRowId, out var name)
            ? name.Name.ExtractText()
            : string.Empty;
}
