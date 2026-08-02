using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

// A resizable, movable window that shows AetherStream's video directly - an alternative render
// target to the in-game TV model, for players without a Penumbra-modded companion (or who just
// don't want video projected onto a minion). Reuses VideoDebugWindow's own frame-to-texture
// technique (TryGetFrame -> CreateFromRaw -> ImGui.Image) as a real user-facing feature instead
// of a debug spike. No playback controls of its own - that's what the phone's Player tab is for;
// this only ever displays whatever VideoPlayer is already doing.
internal sealed class AetherStreamScreenWindow : Window, IDisposable
{
    private readonly VideoPlayer video;
    private IDalamudTextureWrap? texture;

    public AetherStreamScreenWindow(VideoPlayer video)
        : base("AetherStream Screen###AetherStreamScreenWindow")
    {
        this.video = video;
        Size = new Vector2(640f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(160f, 90f), MaximumSize = new Vector2(4096f, 4096f),
        };
        IsOpen = false;
    }

    public override void Draw()
    {
        var frame = video.TryGetFrame(out var width, out var height);
        if (frame is null || width <= 0 || height <= 0)
        {
            ImGui.TextDisabled("Nothing playing.");
            return;
        }

        texture?.Dispose();
        texture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Bgra32(width, height), frame,
            "Aetherphone.AetherStream.ScreenWindow");

        var avail = ImGui.GetContentRegionAvail();
        var aspect = (float)width / height;
        var drawWidth = avail.X;
        var drawHeight = drawWidth / aspect;
        if (drawHeight > avail.Y && avail.Y > 0f)
        {
            drawHeight = avail.Y;
            drawWidth = drawHeight * aspect;
        }

        var offsetX = (avail.X - drawWidth) * 0.5f;
        if (offsetX > 0f)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        }

        ImGui.Image(texture.Handle, new Vector2(drawWidth, drawHeight));
    }

    public void Dispose()
    {
        texture?.Dispose();
        texture = null;
    }
}
