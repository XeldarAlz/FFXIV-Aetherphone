using Aetherphone.Core.Animation;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SettingsHighlight
{
    public const float PeakAlpha = 0.18f;
    private const float LifetimeSeconds = 1.6f;
    private const float HoldSeconds = 0.35f;
    private const float FadeSmoothTime = 0.40f;
    private const float MaxFrameSeconds = 0.1f;
    private const string ScrollKey = "##settingsHighlightScroll";
    private static Spring wash;
    private static string? label;
    private static double startedAt;
    private static int steppedFrame = -1;
    private static uint scrolledWindow;

    public static bool IsLive => label is not null && ImGui.GetTime() - startedAt < LifetimeSeconds;

    public static void Begin(string target)
    {
        label = target;
        startedAt = ImGui.GetTime();
        wash.SnapTo(PeakAlpha);
        steppedFrame = -1;
        scrolledWindow = 0;
    }

    public static bool TryClaim(string rowLabel, out float alpha, out bool scrollTo)
    {
        alpha = 0f;
        scrollTo = false;
        if (!IsLive)
        {
            label = null;
            return false;
        }

        if (!string.Equals(rowLabel, label, StringComparison.Ordinal))
        {
            return false;
        }

        Advance();
        alpha = wash.Value;
        var window = ImGui.GetID(ScrollKey);
        scrollTo = window != scrolledWindow;
        scrolledWindow = window;
        return true;
    }

    private static void Advance()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == steppedFrame)
        {
            return;
        }

        steppedFrame = frame;
        if (ImGui.GetTime() - startedAt < HoldSeconds)
        {
            return;
        }

        wash.Step(0f, FadeSmoothTime, MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds));
    }
}
