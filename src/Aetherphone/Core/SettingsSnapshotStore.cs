namespace Aetherphone.Core;

internal sealed class SettingsSnapshotStore<TSnapshot> where TSnapshot : class
{
    private readonly Configuration configuration;
    private readonly Func<Configuration, TSnapshot?> read;
    private readonly Action<Configuration, TSnapshot> write;

    public SettingsSnapshotStore(Configuration configuration, Func<Configuration, TSnapshot?> read,
        Action<Configuration, TSnapshot> write)
    {
        this.configuration = configuration;
        this.read = read;
        this.write = write;
    }

    public TSnapshot? Load() => read(configuration);

    public void Save(TSnapshot snapshot)
    {
        write(configuration, snapshot);
        configuration.Save();
    }
}
