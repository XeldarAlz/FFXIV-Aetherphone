using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Core.Shell;

internal sealed class LoadingScreen
{
    private readonly Configuration configuration;
    private readonly BootSequence sequence;

    public LoadingScreen(Configuration configuration)
    {
        this.configuration = configuration;
        sequence = new BootSequence(configuration);
    }

    public bool IsActive => sequence.IsActive;

    public void BeginSession() => sequence.Begin(!configuration.WelcomeShown);

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
