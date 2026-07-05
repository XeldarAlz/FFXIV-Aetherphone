using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetSession
{
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private volatile bool tokenRejected;

    public AethernetSession(Configuration configuration, IFramework framework)
    {
        this.configuration = configuration;
        this.framework = framework;
    }

    public string BaseUrl =>
        string.IsNullOrWhiteSpace(configuration.AethernetBaseUrl)
            ? Configuration.DefaultAethernetBaseUrl
            : configuration.AethernetBaseUrl;

    public string? Token => string.IsNullOrEmpty(configuration.AethernetToken) ? null : configuration.AethernetToken;
    public bool IsSignedIn => Token is not null && !tokenRejected;
    public bool TokenRejected => tokenRejected;
    public UserDto? CurrentUser { get; private set; }
    public event Action? Changed;

    public void SignIn(string token, UserDto user)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            tokenRejected = false;
            configuration.AethernetToken = token;
            CurrentUser = user;
            configuration.Save();
            Changed?.Invoke();
        });
    }

    public void SetUser(UserDto user)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            CurrentUser = user;
            Changed?.Invoke();
        });
    }

    public void SignOut()
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            tokenRejected = false;
            configuration.AethernetToken = string.Empty;
            CurrentUser = null;
            configuration.Save();
            Changed?.Invoke();
        });
    }

    public void ReportAuthStatus(int statusCode)
    {
        if (statusCode != 401 || tokenRejected)
        {
            return;
        }

        tokenRejected = true;
        AepLog.Warning("Aethernet token was rejected; sign in again to reconnect.");
        _ = framework.RunOnFrameworkThread(() =>
        {
            CurrentUser = null;
            Changed?.Invoke();
        });
    }
}
