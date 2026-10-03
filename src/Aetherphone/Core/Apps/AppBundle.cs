using Aetherphone.Core.Home;
using Aetherphone.Core.Message;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Apps;

internal sealed class AppBundle
{
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
    public required WidgetRegistry Widgets { get; init; }
    public required WidgetActions WidgetActions { get; init; }
    public required PhotoLibrary Photos { get; init; }
    public required Telephony.ContactBook Contacts { get; init; }
    public required IMessagePopouts MessagePopouts { get; init; }
    public required VideoSuite Video { get; init; }
}
