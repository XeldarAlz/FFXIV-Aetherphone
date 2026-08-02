using Aetherphone.Core.Video;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

// Stage 3/6 spike window: decode a local video file and draw it locally - off the phone shell
// entirely. No URL handling, no phone UI yet - see docs/port-plan.md. The screen is self-drawn
// and world-anchored on the local player (port/alphachannel-engine Stage 4) - playing a video here
// plays it on the in-world screen automatically, there's no separate "send to TV" step or
// companion to summon first.
internal sealed class VideoDebugWindow : Window, IDisposable
{
    private readonly VideoPlayer video;
    private readonly ScreenController screen;
    private string path = string.Empty;
    private IDalamudTextureWrap? texture;

    public VideoDebugWindow(VideoPlayer video, ScreenController screen)
        : base("Aetherphone: Video Decode Debug (Stage 3/6)###AetherphoneVideoDebug")
    {
        this.video = video;
        this.screen = screen;
        Size = new Vector2(760, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawTvSection();
        ImGui.Separator();
        DrawPlaybackSection();
    }

    private void DrawTvSection()
    {
        ImGui.TextUnformatted("Screen");
        if (screen.Engine.IsActive)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), "Active.");
            ImGui.TextUnformatted($"Position: {screen.Engine.ScreenPosition} Yaw: {screen.Engine.ScreenYaw:0.00} Scale: {screen.Engine.ScreenScale:0.00}");
        }
        else
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.3f, 1f), "Idle.");
        }
    }

    private void DrawPlaybackSection()
    {
        ImGui.InputText("Local file path or YouTube URL", ref path, 1000);
        ImGui.SameLine();
        if (ImGui.Button("Play"))
        {
            video.Play(path);
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            video.Stop();
        }

        ImGui.Text($"State: {video.State}");
        if (video.LastError is not null)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), video.LastError);
        }

        var (position, duration, paused) = video.GetProgress();
        ImGui.Text($"Position: {position:F1}s / {duration:F1}s   Paused: {paused}");

        var frame = video.TryGetFrame(out var width, out var height);
        if (frame is null)
        {
            ImGui.TextDisabled("No frame yet.");
            return;
        }

        texture?.Dispose();
        texture = Plugin.TextureProvider.CreateFromRaw(RawImageSpecification.Bgra32(width, height), frame,
            "Aetherphone.VideoDebug.Frame");

        var avail = ImGui.GetContentRegionAvail();
        var aspect = (float)width / height;
        var drawWidth = avail.X;
        var drawHeight = drawWidth / aspect;
        if (drawHeight > avail.Y && avail.Y > 0f)
        {
            drawHeight = avail.Y;
            drawWidth = drawHeight * aspect;
        }

        ImGui.Image(texture.Handle, new Vector2(drawWidth, drawHeight));
    }

    public void Dispose()
    {
        video.Stop();
        texture?.Dispose();
        texture = null;
    }
}
