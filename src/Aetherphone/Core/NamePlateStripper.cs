using Dalamud.Game.Gui.NamePlate;

namespace Aetherphone.Core;

// Shared by CameraApp (transient full-hide while a shot is captured) and the video screen
// (sustained hide for nameplates near an active TV companion) - the two features trigger the
// hide very differently, but blanking a single nameplate is exactly the same operation either
// way, so it lives in one place.
internal static class NamePlateStripper
{
    public static void Strip(INamePlateUpdateHandler handler)
    {
        handler.RemoveName();
        handler.RemoveTitle();
        handler.RemoveFreeCompanyTag();
        handler.RemoveLevelPrefix();
        handler.RemoveStatusPrefix();
        handler.RemoveTargetSuffix();
        handler.MarkerIconId = 0;
        handler.NameIconId = -1;
    }
}
