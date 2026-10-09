using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino;

internal sealed partial class CasinoApp
{
    private LaunchMorph launch;
    private CasinoRoute launchRoute;

    private bool LaunchActive => launch.Active;

    private void BeginLaunch(Rect source)
    {
        if (source.Width <= 0f || source.Height <= 0f)
        {
            return;
        }

        launchRoute = router.Current;
        launch.Begin(new Rect(source.Min - frameArea.Min, source.Max - frameArea.Min));
    }

    private bool TryDismissLaunch()
    {
        if (!launch.HasSource || router.IsTransitioning || router.Depth < 2
            || !CasinoNavigation.Same(router.Current, launchRoute))
        {
            return false;
        }

        return launch.BeginDismiss();
    }

    private void ResetLaunch()
    {
        launch.Clear();
        launchRoute = default;
    }

    private bool DrawLaunchLayer(Rect area)
    {
        if (!launch.Active)
        {
            if (launch.HasSource && !CasinoNavigation.Same(router.Current, launchRoute))
            {
                ResetLaunch();
            }

            return false;
        }

        if (router.Depth < 2 || !CasinoNavigation.Same(router.Current, launchRoute))
        {
            ResetLaunch();
            return false;
        }

        var step = launch.Advance(ImGui.GetIO().DeltaTime);
        if (step == LaunchStep.Dismissed)
        {
            launchRoute = default;
            PopNow(false);
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
        var source = launch.Source.Translate(area.Min);
        var underIndex = router.Depth - 2;
        if (router.TryGetView(underIndex, out var under))
        {
            using var underLayer = ScreenLayer.Begin(router.LayerId(underIndex), area, true);
            drawView(under, area, underIndex + 1);
            underLayer.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, LaunchMorph.Veil(progress))));
        }

        var transform = LaunchMorph.Transform(source, area, progress, false);
        var interactive = !launch.Dismissing && progress >= TransitionTiming.InteractiveProgress;
        using var playing = interactive
            ? ScreenLayer.BeginWarped(router.LayerId(router.Depth - 1), area, in transform)
            : ScreenLayer.Begin(router.LayerId(router.Depth - 1), area, true);
        drawView(router.Current, area, router.Depth);
        playing.Transform(in transform);
    }
}
