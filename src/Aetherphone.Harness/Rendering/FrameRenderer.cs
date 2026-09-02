using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness.Rendering;

internal sealed unsafe class FrameRenderer
{
    private static readonly CpuTexture WhiteTexture = CpuTexture.Solid(1, 1, 255, 255, 255, 255);
    private readonly SoftwareRasterizer rasterizer;
    private readonly TextureStore textures;

    public FrameRenderer(int width, int height, TextureStore textures)
    {
        rasterizer = new SoftwareRasterizer(width, height);
        this.textures = textures;
    }

    public int Width => rasterizer.Width;

    public int Height => rasterizer.Height;

    public int TrianglesDrawn => rasterizer.TrianglesDrawn;

    public void Render(ImDrawDataPtr drawData, byte red, byte green, byte blue)
    {
        rasterizer.Clear(red, green, blue, 255);
        var displayPos = drawData.DisplayPos;
        var listCount = drawData.CmdListsCount;
        var lists = drawData.CmdLists;
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
                    (int)command.ElemCount, command.ClipRect, displayPos, texture);
            }
        }
    }

    public byte[] Resolve() => rasterizer.Resolve();
}
