using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Hunts;

internal static class HuntZoneMapTextures
{
    private static readonly FileTextureCache Cache = new(Path.Combine("Hunts", "Maps"), ".jpg");

    public static IDalamudTextureWrap? Resolve(string zoneId) => Cache.Resolve(zoneId);
}
