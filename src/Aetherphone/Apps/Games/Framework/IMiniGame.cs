using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Games.Framework;

internal interface IMiniGame : IDisposable
{
    string Id { get; }
    string Title { get; }
    string Genre { get; }
    Vector4 Accent => AppAccents.For(Id);
    bool WantsLandscape => false;
    bool UsesCompactHeader => false;
    bool WantsImmersiveContent => false;
    bool WantsStatusBarInImmersiveContent => false;
    void Open();
    void Close();
    bool HandleBack() => false;
    void Draw(in GameContext context);
}
