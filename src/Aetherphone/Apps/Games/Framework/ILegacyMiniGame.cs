using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Games.Framework;

internal interface ILegacyMiniGame : IDisposable
{
    string Id { get; }
    string Title { get; }
    GameGenre Genre { get; }
    Vector4 Accent => AppAccents.For(Id);
    bool RunsOnAClock => false;
    bool WantsLandscape => false;
    void Open();
    void Close();
    void Draw(in GameContext context);
}
