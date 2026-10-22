using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness.Rendering;

internal sealed unsafe class FrameRenderer
{
    private const int MaxBands = 16;
    private static readonly CpuTexture WhiteTexture = CpuTexture.Solid(1, 1, 255, 255, 255, 255);
    private readonly SoftwareRasterizer rasterizer;
    private readonly TextureStore textures;
    private readonly int bandCount;

    public FrameRenderer(int width, int height, TextureStore textures)
    {
        rasterizer = new SoftwareRasterizer(width, height);
        this.textures = textures;
        bandCount = Math.Clamp(Environment.ProcessorCount, 1, MaxBands);
    }

    public int Width => rasterizer.Width;

    public int Height => rasterizer.Height;

    public int TrianglesDrawn { get; private set; }

    public void Render(ImDrawDataPtr drawData, byte red, byte green, byte blue)
    {
        rasterizer.Clear(red, green, blue, 255);
        TrianglesDrawn = drawData.TotalIdxCount / 3;
        var data = drawData.Handle;
        var rowsPerBand = (Height + bandCount - 1) / bandCount;
        Parallel.For(0, bandCount, band =>
        {
            var bandMinY = band * rowsPerBand;
            var bandMaxY = Math.Min(Height, bandMinY + rowsPerBand) - 1;
            if (bandMinY <= bandMaxY)
            {
                RenderBand(data, bandMinY, bandMaxY);
            }
        });
    }

    public byte[] Resolve(int left, int top, int width, int height)
    {
        var target = new byte[width * height * 4];
        rasterizer.Resolve(left, top, width, height, target);
        return target;
    }

    private void RenderBand(ImDrawData* data, int bandMinY, int bandMaxY)
    {
        var displayPos = data->DisplayPos;
        var listCount = data->CmdListsCount;
        var lists = data->CmdLists;
        for (var listIndex = 0; listIndex < listCount; listIndex++)
        {
            var list = lists[listIndex];
            var vertices = (DrawVertex*)list->VtxBuffer.Data;
            var indices = list->IdxBuffer.Data;
            var commands = list->CmdBuffer.Data;
            var commandCount = list->CmdBuffer.Size;
            for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                var command = commands[commandIndex];
                if (command.UserCallback != null)
                {
                    continue;
                }

                if (!textures.TryGet((nint)command.TextureId.Handle, out var texture))
                {
                    texture = WhiteTexture;
                }

                rasterizer.DrawTriangles(vertices + command.VtxOffset, indices + command.IdxOffset,
                    (int)command.ElemCount, command.ClipRect, displayPos, texture, bandMinY, bandMaxY);
            }
        }
    }
}
