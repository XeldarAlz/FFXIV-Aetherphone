namespace Aetherphone.Core.Apps;

internal interface INavigator
{
    bool AtHome { get; }
    void OpenApp(IPhoneApp app);
    void OpenApp(IPhoneApp app, string source);
    void Open(string appId);
    void Open(string appId, string source);
    void Back();
    void GoHome();
}
