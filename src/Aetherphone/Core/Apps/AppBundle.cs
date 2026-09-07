using Aetherphone.Core.Home;
using Aetherphone.Core.Photos;

namespace Aetherphone.Core.Apps;

internal sealed class AppBundle
{
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
    public required WidgetRegistry Widgets { get; init; }
    public required PhotoLibrary Photos { get; init; }
    public required Telephony.ContactBook Contacts { get; init; }
    public required Windows.MessagePopouts MessagePopouts { get; init; }
}
