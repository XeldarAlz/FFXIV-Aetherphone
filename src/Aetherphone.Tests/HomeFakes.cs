using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;

namespace Aetherphone.Tests;

internal sealed class FakeApp : IPhoneApp
{
    public FakeApp(string id) => Id = id;

    public string Id { get; }
    public string DisplayName => Id;
    public string Glyph => string.Empty;
    public int BadgeCount => 0;
    public bool IsAvailable { get; set; } = true;

    public void OnOpened() { }
    public void OnClosed() { }
    public void Draw(in PhoneContext context) { }
    public void Dispose() { }
}

internal sealed class FakeHomeConfiguration : IHomeConfiguration
{
    public HomeLayout? Home { get; set; }
    public int HomeGridRows { get; set; }

    public void Save() { }
}
