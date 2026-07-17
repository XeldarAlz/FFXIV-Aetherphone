using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings;

internal interface ISettingsPage
{
    string Title { get; }
    string Summary { get; }
    FontAwesomeIcon Icon { get; }
    Vector4 Tint { get; }
    bool ShowsBadge => false;
    bool OwnsChrome => false;
    string? GuideAnchor => null;
    void Draw(in PhoneContext context, Rect body);
}
