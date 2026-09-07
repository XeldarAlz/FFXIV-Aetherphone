using Aetherphone.Core;
using Aetherphone.Core.Apps;

namespace Aetherphone.Windows;

internal sealed class PopoutNavigator : INavigator
{
    public static readonly PopoutNavigator Instance = new();

    private PopoutNavigator()
    {
    }

    public bool AtHome => false;

    public bool IsAvailable(string appId) => false;

    public void OpenApp(IPhoneApp app)
    {
    }

    public void OpenAppFrom(IPhoneApp app, Rect origin, LaunchOrigin kind)
    {
    }

    public void Open(string appId)
    {
    }

    public void Back()
    {
    }

    public void GoHome()
    {
    }
}
