using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Framework.World;

internal sealed class TerrainTexture : IDisposable
{
    private const string TextureName = "Aetherphone.Games.Terrain";

    private readonly ITextureProvider textures;
    private readonly TerrainMask mask;
    private readonly TerrainPainter painter;
    private IDalamudTextureWrap? current;
    private IDalamudTextureWrap? retired;
    private int retiredFrame;
    private int uploadFrame = -1;
    private bool uploadPending;

    public TerrainTexture(ITextureProvider textures, TerrainMask mask, TerrainMaterial material)
    {
        this.textures = textures;
        this.mask = mask;
        mask.TakeDirty(out _);
        painter = new TerrainPainter(mask, material);
        uploadPending = true;
    }

    public TerrainMaterial Material => painter.Material;

    public void SetMaterial(TerrainMaterial material)
    {
        mask.TakeDirty(out _);
        painter.SetMaterial(material);
        uploadPending = true;
    }

    public void Draw(ImDrawListPtr drawList, in Camera2D camera)
    {
        Refresh(ImGui.GetFrameCount());
        if (current is null)
        {
            return;
        }

        var worldSize = new Vector2(mask.WorldWidth, mask.WorldHeight);
        var visible = camera.VisibleWorld;
        var min = Vector2.Max(Vector2.Zero, visible.Min);
        var max = Vector2.Min(worldSize, visible.Max);
        if (min.X >= max.X || min.Y >= max.Y)
        {
            return;
        }

        drawList.AddImage(current.Handle, camera.ToScreen(min), camera.ToScreen(max), min / worldSize, max / worldSize);
    }

    public void Dispose()
    {
        current?.Dispose();
        current = null;
        retired?.Dispose();
        retired = null;
    }

    private void Refresh(int frame)
    {
        if (retired is not null && frame != retiredFrame)
        {
            retired.Dispose();
            retired = null;
        }

        if (mask.TakeDirty(out var region))
        {
            painter.Paint(region);
            uploadPending = true;
        }

        if (!uploadPending || frame == uploadFrame)
        {
            return;
        }

        Upload(frame);
    }

    private void Upload(int frame)
    {
        IDalamudTextureWrap next;
        try
        {
            next = textures.CreateFromRaw(RawImageSpecification.Rgba32(mask.Width, mask.Height), painter.Pixels,
                TextureName);
        }
        catch (Exception exception)
        {
            uploadPending = false;
            AepLog.Warning(exception, "[Games] terrain texture upload failed");
            return;
        }

        retired?.Dispose();
        retired = current;
        retiredFrame = frame;
        current = next;
        uploadFrame = frame;
        uploadPending = false;
    }
}
