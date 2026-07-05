namespace Aetherphone.Core.Apps;

internal interface INavigator
{
    bool AtHome { get; }
    void OpenApp(IPhoneApp app);
    void Open(string appId);
    void Back();
    void GoHome();
}
