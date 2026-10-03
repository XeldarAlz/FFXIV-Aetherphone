using Aetherphone.Core.Game;
using Aetherphone.Core.Theme;

namespace Aetherphone.Core.Home;

internal sealed class HomeLookService : IDisposable
{
    private readonly Configuration configuration;
    private readonly ThemeProvider themes;
    private readonly CharacterWatch watch;
    private readonly List<ulong> orphanedCharacters = new();
    private HomeLayoutService? layout;
    private ulong playingContentId;

    public HomeLookService(Configuration configuration, ThemeProvider themes, CharacterWatch watch)
    {
        this.configuration = configuration;
        this.themes = themes;
        this.watch = watch;
        watch.Changed += OnCharacterChanged;
    }

    public IReadOnlyList<HomeLook> Looks => configuration.Looks;
    public Guid ActiveLookId => configuration.ActiveLookId;
    public bool CanDelete => configuration.Looks.Count > 1;

    public HomeLook? Active => Find(configuration.ActiveLookId);

    public void Bind(HomeLayoutService service)
    {
        layout = service;
    }

    public void Choose(Guid id)
    {
        if (Find(id) is null)
        {
            return;
        }

        if (playingContentId != 0)
        {
            configuration.LookByCharacter[playingContentId] = id;
        }

        if (!Activate(id))
        {
            configuration.Save();
        }
    }

    public HomeLook CreateFromCurrent(string name)
    {
        CaptureActive();
        var look = new HomeLook { Id = Guid.NewGuid(), Name = name };
        HomeLookMirror.Capture(configuration, look);
        configuration.Looks.Add(look);
        configuration.Save();
        return look;
    }

    public void Rename(HomeLook look, string name)
    {
        var trimmed = name.Trim();
        if (string.Equals(look.Name, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        look.Name = trimmed;
        configuration.Save();
    }

    public bool Delete(Guid id)
    {
        if (!CanDelete)
        {
            return false;
        }

        var index = HomeLookMirror.IndexOf(configuration.Looks, id);
        if (index < 0)
        {
            return false;
        }

        configuration.Looks.RemoveAt(index);
        ReleaseAssignments(id);
        if (configuration.ActiveLookId == id)
        {
            configuration.ActiveLookId = Guid.Empty;
            Activate(configuration.Looks[0].Id);
            return true;
        }

        configuration.Save();
        return true;
    }

    public void CaptureActive()
    {
        if (Active is { } active)
        {
            HomeLookMirror.Capture(configuration, active);
        }
    }

    private HomeLook? Find(Guid id)
    {
        var index = HomeLookMirror.IndexOf(configuration.Looks, id);
        return index < 0 ? null : configuration.Looks[index];
    }

    private void ReleaseAssignments(Guid id)
    {
        orphanedCharacters.Clear();
        foreach (var pair in configuration.LookByCharacter)
        {
            if (pair.Value == id)
            {
                orphanedCharacters.Add(pair.Key);
            }
        }

        for (var index = 0; index < orphanedCharacters.Count; index++)
        {
            configuration.LookByCharacter.Remove(orphanedCharacters[index]);
        }
    }

    private void OnCharacterChanged(ulong contentId)
    {
        playingContentId = contentId;
        if (contentId == 0)
        {
            return;
        }

        Activate(HomeLookMirror.Resolve(configuration.Looks, configuration.LookByCharacter, contentId));
    }

    private bool Activate(Guid id)
    {
        if (id == configuration.ActiveLookId || Find(id) is not { } target)
        {
            return false;
        }

        CaptureActive();
        HomeLookMirror.Restore(configuration, target);
        configuration.ActiveLookId = id;
        configuration.Save();
        themes.Apply(configuration);
        layout?.Reload();
        return true;
    }

    public void Dispose()
    {
        watch.Changed -= OnCharacterChanged;
    }
}
