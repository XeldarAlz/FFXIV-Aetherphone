using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal interface IMiniGame : IDisposable
{
    GameSpec Spec { get; }
    string Id => Spec.Id;
    string Title => Loc.T(Spec.Title);
    GameGenre Genre => Spec.Genre;
    Vector4 Accent => AppAccents.For(Spec.Id);
    void Start(in GameStart start);
    void Close();
    void Draw(in GameContext context);

    void DrawIdle(in GameContext context)
    {
    }

    void OnQuit(GameSession session)
    {
    }
}
