using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Video;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Aetherphone.Windows;

internal sealed class AetherStreamScreenWindow : Window
{
    private const float SourceAspect = (float)VideoEngine.ScreenWidth / VideoEngine.ScreenHeight;

    private readonly VideoSuite suite;

    internal AetherStreamScreenWindow(VideoSuite suite)
        : base("MogCast Screen###AetherStreamScreenWindow")
    {
        this.suite = suite;
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
        var handle = suite.Screen.Engine.ScreenViewHandle;
        if (handle == nint.Zero || !suite.Player.HasMedia)
        {
            ImGui.TextDisabled(Loc.T(L.AetherStream.NothingPlaying));
            return;
        }

        var available = ImGui.GetContentRegionAvail();
        var drawWidth = available.X;
        var drawHeight = drawWidth / SourceAspect;
        if (drawHeight > available.Y && available.Y > 0f)
        {
            drawHeight = available.Y;
            drawWidth = drawHeight * SourceAspect;
        }

        var offsetX = (available.X - drawWidth) * 0.5f;
        if (offsetX > 0f)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        }

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Image(new ImTextureID(handle), new Vector2(drawWidth, drawHeight));

        var stage = new Rect(origin, origin + new Vector2(drawWidth, drawHeight));
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Global;
        VideoStageOverlay.DrawBubbles(drawList, stage, suite.ChatFeed, scale);
        VideoStageOverlay.DrawReactions(drawList, stage, suite.WatchAlong.Reactions, scale);
    }
}
