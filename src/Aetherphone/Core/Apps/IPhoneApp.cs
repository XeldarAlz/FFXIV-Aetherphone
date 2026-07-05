using System.Numerics;

namespace Aetherphone.Core.Apps;

internal interface IPhoneApp : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Glyph { get; }
    Vector4 Accent { get; }
    int BadgeCount { get; }
    bool WantsTransparentScreen => false;
    void OnOpened();
    void OnClosed();
    void Draw(in PhoneContext context);
}
