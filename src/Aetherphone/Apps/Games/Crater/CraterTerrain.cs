using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Crater;

internal sealed class CraterTerrain : IDisposable
{
    private const string TextureName = "Aetherphone.Games.Crater.Terrain";
    private const float ScorchReach = 1.45f;
    private const float ScorchExtraCells = 4f;
    private const float ScorchStrength = 0.6f;
    private const int AlphaOffset = 3;
    private static readonly Vector3 Soot = new(26f, 20f, 18f);

    private readonly ITextureProvider textures;
    private readonly TerrainMask mask;
    private readonly byte[] pixels;
    private readonly int[] nearby = new int[CraterBoard.MaxCraters];
    private readonly float[] nearbyOuter = new float[CraterBoard.MaxCraters];
    private TerrainPainter? painter;
    private IDalamudTextureWrap? current;
    private IDalamudTextureWrap? retired;
    private int retiredFrame;
    private int uploadFrame = -1;
    private int scorched;
    private bool uploadPending;

    public CraterTerrain(ITextureProvider textures, TerrainMask mask)
    {
        this.textures = textures;
        this.mask = mask;
        pixels = new byte[mask.Width * mask.Height * TerrainPainter.BytesPerPixel];
    }

    public void Reset(TerrainMaterial material)
    {
        mask.TakeDirty(out _);
        if (painter is null)
        {
            painter = new TerrainPainter(mask, material);
        }
        else
        {
            painter.SetMaterial(material);
        }

        painter.Pixels.CopyTo(pixels);
        scorched = 0;
        uploadPending = true;
    }

    public void Draw(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<CraterMark> craters)
    {
        Refresh(ImGui.GetFrameCount(), craters);
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

    private void Refresh(int frame, ReadOnlySpan<CraterMark> craters)
    {
        if (retired is not null && frame != retiredFrame)
        {
            retired.Dispose();
            retired = null;
        }

        if (painter is null)
        {
            return;
        }

        var changed = mask.TakeDirty(out var region);
        if (changed)
        {
            painter.Paint(region);
        }

        if (craters.Length < scorched)
        {
            scorched = 0;
        }

        for (var index = scorched; index < craters.Length; index++)
        {
            var scorch = ScorchRegion(craters[index]);
            region = changed ? Union(region, scorch) : scorch;
            changed = true;
        }

        scorched = craters.Length;
        var expanded = Expand(region);
        if (changed && expanded.MinColumn <= expanded.MaxColumn && expanded.MinRow <= expanded.MaxRow)
        {
            Repaint(expanded, craters);
            uploadPending = true;
        }

        if (!uploadPending || frame == uploadFrame)
        {
            return;
        }

        Upload(frame);
    }

    private void Repaint(in CellRegion region, ReadOnlySpan<CraterMark> craters)
    {
        var source = painter!.Pixels;
        var rowBytes = region.Columns * TerrainPainter.BytesPerPixel;
        for (var row = region.MinRow; row <= region.MaxRow; row++)
        {
            var offset = (row * mask.Width + region.MinColumn) * TerrainPainter.BytesPerPixel;
            source.Slice(offset, rowBytes).CopyTo(pixels.AsSpan(offset, rowBytes));
        }

        Scorch(region, craters);
    }

    private void Scorch(in CellRegion region, ReadOnlySpan<CraterMark> craters)
    {
        var cell = mask.MetresPerCell;
        var count = 0;
        for (var index = 0; index < craters.Length; index++)
        {
            var reach = ScorchRegion(craters[index]);
            if (reach.MaxColumn < region.MinColumn || reach.MinColumn > region.MaxColumn ||
                reach.MaxRow < region.MinRow || reach.MinRow > region.MaxRow)
            {
                continue;
            }

            nearby[count] = index;
            nearbyOuter[count] = Outer(craters[index]);
            count++;
        }

        if (count == 0)
        {
            return;
        }

        for (var row = region.MinRow; row <= region.MaxRow; row++)
        {
            var y = (row + 0.5f) * cell;
            for (var column = region.MinColumn; column <= region.MaxColumn; column++)
            {
                var offset = (row * mask.Width + column) * TerrainPainter.BytesPerPixel;
                if (pixels[offset + AlphaOffset] == 0)
                {
                    continue;
                }

                var x = (column + 0.5f) * cell;
                var darkest = 0f;
                for (var entry = 0; entry < count; entry++)
                {
                    ref readonly var crater = ref craters[nearby[entry]];
                    var outer = nearbyOuter[entry];
                    var dx = x - crater.Center.X;
                    var dy = y - crater.Center.Y;
                    var distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= outer * outer)
                    {
                        continue;
                    }

                    var along = (MathF.Sqrt(distanceSquared) - crater.Radius) / (outer - crater.Radius);
                    var amount = along <= 0f ? 1f : 1f - along;
                    darkest = MathF.Max(darkest, amount * amount);
                }

                if (darkest <= 0f)
                {
                    continue;
                }

                var strength = darkest * ScorchStrength;
                pixels[offset] = Blend(pixels[offset], Soot.X, strength);
                pixels[offset + 1] = Blend(pixels[offset + 1], Soot.Y, strength);
                pixels[offset + 2] = Blend(pixels[offset + 2], Soot.Z, strength);
            }
        }
    }

    private CellRegion ScorchRegion(in CraterMark crater)
    {
        var outer = Outer(crater);
        return new CellRegion(mask.ColumnOf(crater.Center.X - outer), mask.RowOf(crater.Center.Y - outer),
            mask.ColumnOf(crater.Center.X + outer), mask.RowOf(crater.Center.Y + outer));
    }

    private float Outer(in CraterMark crater) => crater.Radius * ScorchReach + ScorchExtraCells * mask.MetresPerCell;

    private CellRegion Expand(in CellRegion region) => new(Math.Max(0, region.MinColumn), Math.Max(0, region.MinRow - 1),
        Math.Min(mask.Width - 1, region.MaxColumn), Math.Min(mask.Height - 1, region.MaxRow + TerrainPainter.TopsoilCells));

    private static CellRegion Union(in CellRegion first, in CellRegion second) => new(
        Math.Min(first.MinColumn, second.MinColumn), Math.Min(first.MinRow, second.MinRow),
        Math.Max(first.MaxColumn, second.MaxColumn), Math.Max(first.MaxRow, second.MaxRow));

    private static byte Blend(byte value, float target, float strength) =>
        (byte)Math.Clamp((int)MathF.Round(value + (target - value) * strength), 0, 255);

    private void Upload(int frame)
    {
        IDalamudTextureWrap next;
        try
        {
            next = textures.CreateFromRaw(RawImageSpecification.Rgba32(mask.Width, mask.Height), pixels, TextureName);
        }
        catch (Exception exception)
        {
            uploadPending = false;
            AepLog.Warning(exception, "[Games] crater terrain upload failed");
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
