using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shortcuts;

namespace Aetherphone.Tests;

internal sealed class FakeShortcutSource : IShortcutSource
{
    public List<ShortcutEntry> Entries { get; } = new();

    public ShortcutEntry? Find(Guid id)
    {
        for (var index = 0; index < Entries.Count; index++)
        {
            if (Entries[index].Id == id)
            {
                return Entries[index];
            }
        }

        return null;
    }
}

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
