using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Sharing;

namespace Aetherphone.Core.Apps;

internal interface IPhoneApp : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Glyph { get; }
    Vector4 Accent => AppAccents.For(Id);
    int BadgeCount { get; }
    bool BadgeAsDot => false;
    bool WantsTransparentScreen => false;
    bool WantsSystemTheme => false;
    Rect? TransparentViewport(Rect screen, float scale) => null;
    bool IsAvailable => AppAvailability.IsEnabled(Id);
    ShareKindSet AcceptedShares => ShareKindSet.None;
    LocString? ShareLabel(ShareKind kind) => null;
    void OnShare(in ShareItem item)
    {
    }

    void OnOpened();
    void OnClosed();
    void Draw(in PhoneContext context);
}
