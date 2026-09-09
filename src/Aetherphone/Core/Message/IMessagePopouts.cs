using Dalamud.Interface.Windowing;

namespace Aetherphone.Core.Message;

internal interface IMessagePopouts : IDisposable
{
    IReadOnlyList<Window> Windows { get; }

    Action<string>? OpenInPhone { get; set; }

    void Restore();
}
