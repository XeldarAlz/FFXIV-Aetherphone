using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Hunts;

internal static class HuntMobPortraitTextures
{
    private static readonly FileTextureCache Cache = new(Path.Combine("Hunts", "Mobs"), ".jpg");

    public static IDalamudTextureWrap? Resolve(string mobId) => Cache.Resolve(mobId);
}
