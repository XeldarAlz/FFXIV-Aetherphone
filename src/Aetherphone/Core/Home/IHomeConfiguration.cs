namespace Aetherphone.Core.Home;

internal interface IHomeConfiguration
{
    HomeLayout? Home { get; set; }
    int HomeGridRows { get; }
    void Save();
}
