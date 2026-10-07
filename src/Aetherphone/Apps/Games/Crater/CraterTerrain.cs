using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Games.Crater;

internal sealed class CraterTerrain : ITerrainOverlay, IDisposable
{
    private const float ScorchReach = 1.45f;
    private const float ScorchExtraCells = 4f;
    private const float ScorchStrength = 0.6f;
    private const int AlphaOffset = 3;
    private static readonly Vector3 Soot = new(26f, 20f, 18f);

    private readonly ITextureProvider textures;
    private readonly TerrainMask mask;
    private readonly CraterMark[] marks = new CraterMark[CraterBoard.MaxCraters];
    private readonly int[] nearby = new int[CraterBoard.MaxCraters];
    private readonly float[] nearbyOuter = new float[CraterBoard.MaxCraters];
    private TerrainTexture? texture;
    private int markCount;
    private int scorched;

    public CraterTerrain(ITextureProvider textures, TerrainMask mask)
    {
        this.textures = textures;
        this.mask = mask;
    }

    public void Reset(TerrainMaterial material)
    {
        markCount = 0;
        scorched = 0;
        if (texture is null)
        {
            texture = new TerrainTexture(textures, mask, material, this);
            return;
        }

        texture.SetMaterial(material);
    }

    public void Draw(ImDrawListPtr drawList, in Camera2D camera, ReadOnlySpan<CraterMark> craters)
    {
        if (texture is null)
        {
            return;
        }

        var count = Math.Min(craters.Length, marks.Length);
        if (count < scorched)
        {
            scorched = 0;
        }

        craters[..count].CopyTo(marks);
        markCount = count;
        texture.Draw(drawList, in camera);
    }

    public void Dispose()
    {
        texture?.Dispose();
        texture = null;
    }

    public bool TakeDirty(out CellRegion region)
    {
        region = default;
        if (scorched >= markCount)
        {
            return false;
        }

        region = ScorchRegion(marks[scorched]);
        for (var index = scorched + 1; index < markCount; index++)
        {
            region = CellRegion.Union(region, ScorchRegion(marks[index]));
        }

        scorched = markCount;
        return true;
    }

    public void Paint(Span<byte> pixels, int width, in CellRegion region)
    {
        var count = 0;
        for (var index = 0; index < markCount; index++)
        {
            if (!ScorchRegion(marks[index]).Overlaps(region))
            {
                continue;
            }

            nearby[count] = index;
            nearbyOuter[count] = Outer(marks[index]);
            count++;
        }

        if (count == 0)
        {
            return;
        }

        var cell = mask.MetresPerCell;
        for (var row = region.MinRow; row <= region.MaxRow; row++)
        {
            var y = (row + 0.5f) * cell;
            for (var column = region.MinColumn; column <= region.MaxColumn; column++)
            {
                var offset = (row * width + column) * TerrainPainter.BytesPerPixel;
                if (pixels[offset + AlphaOffset] == 0)
                {
                    continue;
                }

                var darkest = Darkest((column + 0.5f) * cell, y, count);
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

    private float Darkest(float x, float y, int count)
    {
        var darkest = 0f;
        for (var entry = 0; entry < count; entry++)
        {
            ref readonly var crater = ref marks[nearby[entry]];
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

        return darkest;
    }

    private CellRegion ScorchRegion(in CraterMark crater)
    {
        var outer = Outer(crater);
        return new CellRegion(mask.ColumnOf(crater.Center.X - outer), mask.RowOf(crater.Center.Y - outer),
            mask.ColumnOf(crater.Center.X + outer), mask.RowOf(crater.Center.Y + outer));
    }

    private float Outer(in CraterMark crater) => crater.Radius * ScorchReach + ScorchExtraCells * mask.MetresPerCell;

    private static byte Blend(byte value, float target, float strength) =>
        (byte)Math.Clamp((int)MathF.Round(value + (target - value) * strength), 0, 255);
}
