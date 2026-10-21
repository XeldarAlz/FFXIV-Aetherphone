using Dalamud.Plugin;

namespace Aetherphone.Core.Glamour;

internal static class GlamourPlugins
{
    public static bool IsLoaded(IDalamudPluginInterface pluginInterface, string internalName)
    {
        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (plugin.IsLoaded && string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
