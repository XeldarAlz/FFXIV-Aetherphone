using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games;

internal sealed partial class GamesApp
{
    private const string LaunchIconLayerId = "games.launch.icon";
    private const float LaunchIconAlphaFloor = 0.001f;

    private LaunchMorph launch;
    private IMiniGame? launchGame;

    private bool LaunchActive => launch.Active;

    private bool BeginLaunch(IMiniGame game, Rect localSource)
    {
        launchGame = game;
        launch.Begin(localSource);
        return true;
    }

    private bool TryDismissLaunch()
    {
        if (!launch.HasSource || launchGame is null || !ReferenceEquals(launchGame, currentGame)
            || launchGame.Spec.Landscape || router.IsTransitioning || router.Depth < 2
            || router.Current.Screen != GamesScreen.Playing)
        {
            return false;
        }

        return launch.BeginDismiss();
    }

    private void ResetLaunch()
    {
        launch.Clear();
        launchGame = null;
    }

    private bool DrawLaunchLayer(Rect area)
    {
        if (!launch.Active)
        {
            if (launch.HasSource && currentGame is null)
            {
                ResetLaunch();
            }

            return false;
        }

        if (router.Depth < 2 || router.Current.Screen != GamesScreen.Playing
            || !ReferenceEquals(launchGame, currentGame))
        {
            ResetLaunch();
            return false;
        }

        var step = launch.Advance(ImGui.GetIO().DeltaTime);
        if (step == LaunchStep.Dismissed)
        {
            launchGame = null;
            router.Pop(false);
            return false;
        }

        if (step != LaunchStep.Moving)
        {
            return false;
        }

        DrawLaunchFrame(area);
        return true;
    }

    private void DrawLaunchFrame(Rect area)
    {
        var progress = launch.Progress;
        var icon = launch.FromIcon;
        var source = launch.Source.Translate(area.Min);
        var underIndex = router.Depth - 2;
        if (router.TryGetView(underIndex, out var under))
        {
            using var underLayer = ScreenLayer.Begin(router.LayerId(underIndex), area, true);
            drawView(under, area, underIndex + 1);
            underLayer.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, LaunchMorph.Veil(progress))));
        }

        var transform = LaunchMorph.Transform(source, area, progress, icon);
        using (var playing = OpenLaunchLayer(router.LayerId(router.Depth - 1), area, progress, in transform))
        {
            drawView(router.Current, area, router.Depth);
            playing.Transform(in transform);
        }

        if (icon)
        {
            DrawLaunchIcon(area, LaunchMorph.Card(source, area, progress), LaunchMorph.IconAlpha(progress));
        }
    }

    private ScreenLayer OpenLaunchLayer(string id, Rect area, float progress, in LayerTransform transform)
    {
        var interactive = !launch.Dismissing && progress >= TransitionTiming.InteractiveProgress;
        return interactive ? ScreenLayer.BeginWarped(id, area, in transform) : ScreenLayer.Begin(id, area, true);
    }

    private void DrawLaunchIcon(Rect area, Rect card, float alpha)
    {
        if (alpha <= LaunchIconAlphaFloor || launchGame is null)
        {
            return;
        }

        using var layer = ScreenLayer.BeginPassive(LaunchIconLayerId, area);
        var drawList = ImGui.GetWindowDrawList();
        var half = MathF.Min(card.Width, card.Height) * 0.5f;
        var center = card.Center;
        var firstVertex = drawList.VtxBuffer.Size;
        GameIconArt.Draw(drawList, launchGame.Id, launchGame.Accent, new Vector2(center.X - half, center.Y - half),
            new Vector2(center.X + half, center.Y + half), null, false);
        LayerCompositor.Fade(drawList, firstVertex, alpha);
    }
}
