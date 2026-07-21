namespace Aetherphone.Core.Apps;

internal interface INavigator
{
    bool AtHome { get; }
    bool IsAvailable(string appId);
    void OpenApp(IPhoneApp app);
    void OpenAppFrom(IPhoneApp app, Rect origin);
    void Open(string appId);
    void Back();
    void GoHome();
}
