using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Venue;

internal sealed class VenueCheer
{
    public const float BannerSeconds = 2.6f;

    private const int ConfettiCount = 48;
    private const float Sweep = 0.4f;

    private string text = string.Empty;
    private float progress = 1f;

    public bool Active => progress < 1f;

    public void Show(CasinoStage stage, string message, Vector2 origin, bool instant)
    {
        text = message;
        progress = 0f;
        var scale = UiScale.Current;
        CasinoSfx.Play(UiSound.WinSmall);
        if (instant)
        {
            return;
        }

        stage.Particles.Confetti(origin, ConfettiCount, CasinoColors.Confetti, 260f * scale, 4.5f * scale, 1.6f);
        CasinoLights.LightSweep(stage.Backdrop, Sweep);
    }

    public void Clear()
    {
        progress = 1f;
        text = string.Empty;
    }

    public void Draw(ImDrawListPtr drawList, Rect safe, float deltaSeconds)
    {
        if (!Active)
        {
            return;
        }

        progress = GameBanner.Advance(progress, deltaSeconds, BannerSeconds);
        var style = TextStyles.Title2;
        var fitted = Typography.FitText(text, safe.Width * 0.86f, style);
        GameBanner.Draw(drawList, new Vector2(safe.Center.X, safe.Min.Y + safe.Height * 0.38f), fitted,
            CasinoColors.Money, PhoneTheme.Default, MathF.Max(0.001f, progress), style);
    }
}
