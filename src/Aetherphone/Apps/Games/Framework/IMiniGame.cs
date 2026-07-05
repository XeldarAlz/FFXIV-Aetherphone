using System.Numerics;

namespace Aetherphone.Apps.Games.Framework;

internal interface IMiniGame : IDisposable
{
    string Id { get; }
    string Title { get; }
    string Genre { get; }
    Vector4 Accent { get; }
    void Open();
    void Close();
    void Draw(in GameContext context);
}
