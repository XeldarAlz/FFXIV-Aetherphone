using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Internal.Types.Manifest;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.VersionInfo;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakePluginInterface : IDalamudPluginInterface
{
    private const string PluginName = "Aetherphone";
    private readonly Dictionary<string, object> dataSlots = new();
    private readonly Assembly pluginAssembly;
    private readonly JsonSerializerSettings jsonSettings;

    public FakePluginInterface(string configDirectory, string assetDirectory, IUiBuilder uiBuilder, Assembly pluginAssembly)
    {
        this.pluginAssembly = pluginAssembly;
        Directory.CreateDirectory(configDirectory);
        ConfigDirectory = new DirectoryInfo(Path.Combine(configDirectory, PluginName));
        ConfigDirectory.Create();
        ConfigFile = new FileInfo(Path.Combine(configDirectory, PluginName + ".json"));
        DalamudAssetDirectory = new DirectoryInfo(assetDirectory);
        AssemblyLocation = new FileInfo(pluginAssembly.Location);
        UiBuilder = uiBuilder;
        jsonSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            SerializationBinder = new PluginAssemblyBinder(pluginAssembly),
        };
    }

    public event IDalamudPluginInterface.LanguageChangedDelegate? LanguageChanged { add { } remove { } }

    public event IDalamudPluginInterface.ActivePluginsChangedDelegate? ActivePluginsChanged { add { } remove { } }

    public PluginLoadReason Reason => PluginLoadReason.Boot;

    public bool IsAutoUpdateComplete => true;

    public string SourceRepository => string.Empty;

    public string InternalName => PluginName;

    public IPluginManifest Manifest { get; } = NullProxy.Create<IPluginManifest>();

    public bool IsDev => true;

    public bool IsTesting => false;

    public bool IsInProfile => false;

    public DateTime LoadTime { get; } = DateTime.Now;

    public DateTime LoadTimeUTC { get; } = DateTime.UtcNow;

    public TimeSpan LoadTimeDelta => DateTime.Now - LoadTime;

    public DirectoryInfo DalamudAssetDirectory { get; }

    public FileInfo AssemblyLocation { get; }

    public DirectoryInfo ConfigDirectory { get; }

    public FileInfo ConfigFile { get; }

    public IUiBuilder UiBuilder { get; }

    public bool IsDevMenuOpen => false;

    public bool IsDebugging => false;

    public bool AllowSeasonalEvents => true;

    public string UiLanguage => "en";

    public ISanitizer Sanitizer { get; } = new FakeSanitizer();

    public XivChatType GeneralChatType => XivChatType.Say;

    public IEnumerable<IExposedPlugin> InstalledPlugins => Array.Empty<IExposedPlugin>();

    public bool OpenPluginInstallerTo(PluginInstallerOpenKind openTo = PluginInstallerOpenKind.AllPlugins, string? searchText = null) => false;

    public bool OpenDalamudSettingsTo(SettingsOpenKind openTo = SettingsOpenKind.General, string? searchText = null) => false;

    public bool OpenDeveloperMenu() => false;

    public IExposedPlugin? GetPlugin(Assembly assembly) => null;

    public IExposedPlugin? GetPlugin(AssemblyLoadContext context) => null;

    public IDalamudVersionInfo GetDalamudVersion() => NullProxy.Create<IDalamudVersionInfo>();

    public T GetOrCreateData<T>(string tag, Func<T> dataGenerator)
        where T : class
    {
        if (dataSlots.TryGetValue(tag, out var existing))
        {
            return (T)existing;
        }

        var created = dataGenerator();
        dataSlots[tag] = created;
        return created;
    }

    public void RelinquishData(string tag) => dataSlots.Remove(tag);

    public bool TryGetData<T>(string tag, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? data)
        where T : class
    {
        if (dataSlots.TryGetValue(tag, out var existing) && existing is T typed)
        {
            data = typed;
            return true;
        }

        data = null;
        return false;
    }

    public T? GetData<T>(string tag)
        where T : class => dataSlots.TryGetValue(tag, out var existing) ? existing as T : null;

    public ICallGateProvider<TRet> GetIpcProvider<TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, TRet> GetIpcProvider<T1, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, TRet> GetIpcProvider<T1, T2, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, TRet> GetIpcProvider<T1, T2, T3, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, T4, TRet> GetIpcProvider<T1, T2, T3, T4, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, T4, T5, TRet> GetIpcProvider<T1, T2, T3, T4, T5, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, T4, T5, T6, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, T7, TRet>(string name) => throw new NotSupportedException();

    public ICallGateProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet> GetIpcProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(string name) => throw new NotSupportedException();

    public ICallGateSubscriber<TRet> GetIpcSubscriber<TRet>(string name) => NullProxy.Create<ICallGateSubscriber<TRet>>();

    public ICallGateSubscriber<T1, TRet> GetIpcSubscriber<T1, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, TRet>>();

    public ICallGateSubscriber<T1, T2, TRet> GetIpcSubscriber<T1, T2, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, TRet> GetIpcSubscriber<T1, T2, T3, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, T4, TRet> GetIpcSubscriber<T1, T2, T3, T4, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, T4, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, T4, T5, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, T4, T5, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, T4, T5, T6, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet>>();

    public ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet> GetIpcSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(string name) => NullProxy.Create<ICallGateSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet>>();

    public void SavePluginConfig(IPluginConfiguration? currentConfig)
    {
        if (currentConfig is null)
        {
            return;
        }

        File.WriteAllText(ConfigFile.FullName, JsonConvert.SerializeObject(currentConfig, Formatting.Indented, jsonSettings));
    }

    public IPluginConfiguration? GetPluginConfig()
    {
        if (!File.Exists(ConfigFile.FullName))
        {
            return null;
        }

        return JsonConvert.DeserializeObject<IPluginConfiguration>(File.ReadAllText(ConfigFile.FullName), jsonSettings);
    }

    public string GetPluginConfigDirectory() => ConfigDirectory.FullName;

    public string GetPluginLocDirectory() => Path.Combine(ConfigDirectory.FullName, "loc");

    public T Create<T>(params object[] scopedObjects)
        where T : class => throw new NotSupportedException();

    public Task<T> CreateAsync<T>(params object[] scopedObjects)
        where T : class => throw new NotSupportedException();

    public bool Inject(object instance, params object[] scopedObjects) => false;

    public Task InjectAsync(object instance, params object[] scopedObjects) => Task.CompletedTask;

    public Task<PluginUpdate?> CheckForUpdateAsync() => Task.FromResult<PluginUpdate?>(null);

    public object? GetService(Type serviceType) => null;

    private sealed class PluginAssemblyBinder : ISerializationBinder
    {
        private readonly Assembly pluginAssembly;

        public PluginAssemblyBinder(Assembly pluginAssembly)
        {
            this.pluginAssembly = pluginAssembly;
        }

        public Type BindToType(string? assemblyName, string typeName) =>
            pluginAssembly.GetType(typeName) ?? Type.GetType($"{typeName}, {assemblyName}") ??
            throw new JsonSerializationException($"Unknown configuration type {typeName}.");

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = serializedType.Assembly.GetName().Name;
            typeName = serializedType.FullName;
        }
    }
}
