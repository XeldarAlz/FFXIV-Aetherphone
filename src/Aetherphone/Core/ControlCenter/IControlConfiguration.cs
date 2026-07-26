namespace Aetherphone.Core.ControlCenter;

internal interface IControlConfiguration
{
    ControlLayout? ControlPanel { get; set; }
    void Save();
}
