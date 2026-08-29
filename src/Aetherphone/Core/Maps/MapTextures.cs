using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Maps;

internal static class MapTextures
{
    public static string[] Candidates(string mapId)
    {
        var flat = mapId.Replace("/", string.Empty);
        var underscored = mapId.Replace('/', '_');
        return
        [
            $"ui/map/{mapId}/{flat}_m.tex",
            $"ui/map/{mapId}/{flat}m_m.tex",
            $"ui/map/{mapId}/{flat}_s.tex",
            $"ui/map/{mapId}/{underscored}_m.tex",
            $"ui/map/{mapId}/{underscored}m_m.tex",
        ];
    }

    public static string? ResolveTexturePath(IDataManager data, string mapId, string subsystem)
    {
        var candidates = Candidates(mapId);
        for (var index = 0; index < candidates.Length; index++)
        {
            if (FileExists(data, candidates[index], subsystem))
            {
                return candidates[index];
            }
        }

        return null;
    }

    public static bool FileExists(IDataManager data, string path, string subsystem)
    {
        try
        {
            return data.FileExists(path);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"{subsystem} could not test '{path}'");
            return false;
        }
    }
}
