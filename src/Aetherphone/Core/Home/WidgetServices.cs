using Aetherphone.Core.Apps;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Video;

namespace Aetherphone.Core.Home;

internal sealed class WidgetServices
{
    public required PhoneServices Phone { get; init; }
    public required PhotoLibrary Photos { get; init; }
    public required VideoSuite Video { get; init; }
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
}
