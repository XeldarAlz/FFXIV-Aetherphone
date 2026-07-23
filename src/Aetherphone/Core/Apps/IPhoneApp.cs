namespace Aetherphone.Core.Apps;

internal interface IPhoneApp : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    string Glyph { get; }
    Vector4 Accent => AppAccents.For(Id);
    int BadgeCount { get; }
    bool BadgeAsDot => false;
    bool WantsLandscape => false;
    bool WantsImmersiveContent => false;
    bool WantsStatusBarInImmersiveContent => false;
    bool WantsTransparentScreen => false;
    Rect? TransparentViewport(Rect screen, float scale) => null;
    bool IsAvailable => true;
    void OnOpened();
    void OnClosed();
    void Draw(in PhoneContext context);
}
