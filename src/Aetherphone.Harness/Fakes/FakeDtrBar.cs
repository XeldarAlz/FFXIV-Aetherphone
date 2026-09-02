using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakeDtrBar : IDtrBar
{
    private readonly List<FakeDtrBarEntry> entries = new();

    public IReadOnlyList<IReadOnlyDtrBarEntry> Entries => entries;

    public IDtrBarEntry Get(string title, SeString? text = null)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].Title == title)
            {
                return entries[index];
            }
        }

        var entry = new FakeDtrBarEntry(this, title) { Text = text };
        entries.Add(entry);
        return entry;
    }

    public void Remove(string title)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            if (entries[index].Title == title)
            {
                entries.RemoveAt(index);
            }
        }
    }

    private sealed class FakeDtrBarEntry : IDtrBarEntry
    {
        private readonly FakeDtrBar owner;

        public FakeDtrBarEntry(FakeDtrBar owner, string title)
        {
            this.owner = owner;
            Title = title;
        }

        public string Title { get; }

        public SeString? Text { get; set; }

        public SeString? Tooltip { get; set; }

        public bool Shown { get; set; } = true;

        public ushort MinimumWidth { get; set; }

        public Action<DtrInteractionEvent>? OnClick { get; set; }

        public bool HasClickAction => OnClick is not null;

        public bool UserHidden => false;

        public (Vector2 Min, Vector2 Max) ScreenBounds => (Vector2.Zero, Vector2.Zero);

        public void Remove() => owner.Remove(Title);
    }
}
