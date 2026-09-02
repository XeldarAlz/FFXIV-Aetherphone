using System.Diagnostics;
using Aetherphone.Harness.Native;
using Aetherphone.Harness.Rendering;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Harness;

internal static unsafe class Program
{
    private const int Width = 480;
    private const int Height = 900;
    private const int FrameCount = 8;
    private const int HoverFrame = 3;
    private const int PressFrame = 4;
    private const int ReleaseFrame = 5;
    private const float FrameSeconds = 1f / 60f;
    private static readonly CpuTexture WhiteTexture = CpuTexture.Solid(1, 1, 255, 255, 255, 255);
    private static int taps;
    private static bool toggled;
    private static float slider = 0.35f;
    private static Vector2 buttonMin;
    private static Vector2 buttonMax;

    public static int Main(string[] arguments)
    {
        var outputPath = arguments.Length > 0 ? arguments[0] : Path.Combine(AppContext.BaseDirectory, "spike.png");
        var fontPath = arguments.Length > 1 ? arguments[1] : null;
        NativeImGuiLoader.Configure();
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(Width, Height);
        io.DeltaTime = FrameSeconds;
        io.IniFilename = null;
        if (fontPath is not null)
        {
            io.Fonts.AddFontFromFileTTF(fontPath, 18f, default, null);
        }

        var textures = new TextureStore();
        UploadFontAtlas(io.Fonts, textures);
        var rasterizer = new SoftwareRasterizer(Width, Height);
        var stopwatch = new Stopwatch();
        for (var frame = 0; frame < FrameCount; frame++)
        {
            DriveInput(io, frame);
            ImGui.NewFrame();
            DrawScene();
            ImGui.Render();
            stopwatch.Restart();
            Rasterize(ImGui.GetDrawData(), rasterizer, textures);
            stopwatch.Stop();
            Console.WriteLine(
                $"frame {frame}: triangles={rasterizer.TrianglesDrawn} raster={stopwatch.Elapsed.TotalMilliseconds:F1}ms taps={taps} assert={NativeImGuiLoader.LastAssert ?? "none"}");
        }

        PngWriter.Write(outputPath, rasterizer.Resolve(), Width, Height);
        Console.WriteLine($"wrote {outputPath}");
        return taps == 1 ? 0 : 1;
    }

    private static void DriveInput(ImGuiIOPtr io, int frame)
    {
        if (frame == HoverFrame)
        {
            var center = (buttonMin + buttonMax) * 0.5f;
            io.AddMousePosEvent(center.X, center.Y);
        }

        if (frame == PressFrame)
        {
            io.AddMouseButtonEvent(0, true);
        }

        if (frame == ReleaseFrame)
        {
            io.AddMouseButtonEvent(0, false);
        }
    }

    private static void DrawScene()
    {
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(new Vector2(Width, Height));
        ImGui.Begin("Aetherphone Harness Spike",
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse);
        ImGui.Text("Software rasterizer over Dalamud.Bindings.ImGui");
        if (ImGui.Button("Tap me", new Vector2(160f, 44f)))
        {
            taps += 1;
        }

        buttonMin = ImGui.GetItemRectMin();
        buttonMax = ImGui.GetItemRectMax();
        ImGui.Text($"Taps: {taps}");
        ImGui.Checkbox("Toggle", ref toggled);
        ImGui.SliderFloat("Slider", ref slider, 0f, 1f, "%.2f", ImGuiSliderFlags.None);
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        drawList.AddRectFilledMultiColor(origin + new Vector2(0f, 20f), origin + new Vector2(440f, 220f),
            PackColor(120, 60, 220, 255), PackColor(40, 160, 255, 255), PackColor(255, 120, 60, 255),
            PackColor(30, 30, 40, 255));
        drawList.AddRectFilled(origin + new Vector2(20f, 40f), origin + new Vector2(220f, 200f),
            PackColor(20, 20, 24, 230), 28f, ImDrawFlags.RoundCornersAll);
        drawList.AddCircleFilled(origin + new Vector2(330f, 120f), 60f, PackColor(255, 255, 255, 200), 48);
        drawList.AddText(origin + new Vector2(24f, 250f), PackColor(255, 255, 255, 255),
            "Rounded rect, gradient, circle, text");
        ImGui.End();
    }

    private static void UploadFontAtlas(ImFontAtlasPtr atlas, TextureStore textures)
    {
        if (!atlas.TexReady)
        {
            atlas.Build();
        }

        var textureCount = atlas.Textures.Size;
        for (var textureIndex = 0; textureIndex < textureCount; textureIndex++)
        {
            byte* pixels;
            int width;
            int height;
            int bytesPerPixel;
            atlas.GetTexDataAsRGBA32(textureIndex, &pixels, &width, &height, &bytesPerPixel);
            var rgba = new byte[width * height * 4];
            new ReadOnlySpan<byte>(pixels, rgba.Length).CopyTo(rgba);
            var handle = textures.Register(new CpuTexture(width, height, rgba));
            atlas.SetTexID(textureIndex, (ulong)handle);
            Console.WriteLine($"font atlas texture {textureIndex}: {width}x{height} handle={handle}");
        }
    }

    private static void Rasterize(ImDrawDataPtr drawData, SoftwareRasterizer rasterizer, TextureStore textures)
    {
        rasterizer.Clear(18, 18, 22, 255);
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

    private static uint PackColor(byte red, byte green, byte blue, byte alpha) =>
        red | ((uint)green << 8) | ((uint)blue << 16) | ((uint)alpha << 24);
}
