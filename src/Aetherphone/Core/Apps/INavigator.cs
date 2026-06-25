namespace Aetherphone.Core.Apps;

internal interface INavigator
{
    bool AtHome { get; }

    void OpenApp(IPhoneApp app);

    void OpenApp(IPhoneApp app, Rect origin);

    void Open(string appId);

    void Back();

    void GoHome();
}
