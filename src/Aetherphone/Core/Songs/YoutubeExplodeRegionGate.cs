using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Aetherphone.Core.Songs;

internal static class YoutubeExplodeRegionGate
{
    private const string BypassVariable = "SLAVA_UKRAINI";
    private const string BypassValue = "1";

    [ModuleInitializer]
    [SuppressMessage(
        "Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "YoutubeExplode ships a module initializer that shows a modal dialog and calls Environment.Exit(1), killing the game, whenever CultureInfo.CurrentCulture ends in -ru/-by or the Windows Geo registry key is RU/BY. Its documented bypass variable is read via Environment.GetEnvironmentVariable, so it must be set before any YoutubeExplode type is loaded, which only a module initializer can guarantee.")]
    public static void Disarm()
    {
        Environment.SetEnvironmentVariable(BypassVariable, BypassValue);
    }
}
