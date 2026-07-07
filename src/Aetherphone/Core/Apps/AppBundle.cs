using Aetherphone.Core.Home;

namespace Aetherphone.Core.Apps;

internal sealed class AppBundle
{
    public required IReadOnlyList<IPhoneApp> Apps { get; init; }
    public required WidgetRegistry Widgets { get; init; }
}
