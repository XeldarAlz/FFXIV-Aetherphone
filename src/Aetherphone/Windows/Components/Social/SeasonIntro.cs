using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal sealed class SeasonIntro
{
    private const float ContentDelaySeconds = 0.18f;
    private const float Settled = 0.995f;
    private const float MinimumContentAlpha = 0.02f;
    private const double Idle = -1000.0;

    private static readonly Dictionary<string, DateTime> PlayedOn = new(StringComparer.Ordinal);

    private double startedAt = Idle;

    public bool Begin(string appId)
    {
        if (!SeasonalTheme.Halloween)
        {
            return false;
        }

        var today = DateTime.Today;
        if (PlayedOn.TryGetValue(appId, out var playedDay) && playedDay == today)
        {
            return false;
        }

        PlayedOn[appId] = today;
        startedAt = ImGui.GetTime();
        return true;
    }

    public float Reveal => Spring.Settle(Elapsed, Motion.Sheet);

    public NightView ViewFor(Rect frame) => NightView.Now(frame, Reveal);

    public ContentFade FadeContent()
    {
        var alpha = Spring.Settle(Elapsed - ContentDelaySeconds, Motion.Sheet);
        if (alpha >= Settled)
        {
            return default;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * MathF.Max(MinimumContentAlpha, alpha));
        return new ContentFade(true);
    }

    private float Elapsed => (float)(ImGui.GetTime() - startedAt);

    public readonly struct ContentFade(bool pushed) : IDisposable
    {
        public void Dispose()
        {
            if (pushed)
            {
                ImGui.PopStyleVar();
            }
        }
    }
}
