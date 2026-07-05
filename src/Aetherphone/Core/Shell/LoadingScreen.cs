using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Shell;

internal sealed class LoadingScreen
{
    private readonly BootSequence sequence = new();

    public bool IsActive => sequence.IsActive;

    public void BeginSession() => sequence.Begin(!Plugin.Cfg.WelcomeShown);

    public void Show()
    {
        if (sequence.IsActive)
        {
            return;
        }

        sequence.Begin(false);
    }

    public void Advance(float deltaSeconds) => sequence.Advance(deltaSeconds);

    public void Cancel() => sequence.Cancel();

    public void Draw(Rect screen, PhoneTheme theme) => BootScreen.Draw(screen, theme, sequence);
}
