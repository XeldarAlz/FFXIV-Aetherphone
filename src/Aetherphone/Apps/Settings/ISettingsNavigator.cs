namespace Aetherphone.Apps.Settings;

internal interface ISettingsNavigator
{
    void Open(ISettingsPage page);

    void Back();
}
