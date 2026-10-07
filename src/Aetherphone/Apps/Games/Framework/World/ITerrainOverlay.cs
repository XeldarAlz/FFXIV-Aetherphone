namespace Aetherphone.Apps.Games.Framework.World;

internal interface ITerrainOverlay
{
    bool TakeDirty(out CellRegion region);

    void Paint(Span<byte> pixels, int width, in CellRegion region);
}
