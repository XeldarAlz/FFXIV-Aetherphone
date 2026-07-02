using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetSession
{
    private readonly Configuration configuration;

    public AethernetSession(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public string BaseUrl => string.IsNullOrWhiteSpace(configuration.AethernetBaseUrl) ? Configuration.DefaultAethernetBaseUrl : configuration.AethernetBaseUrl;

    public string? Token => string.IsNullOrEmpty(configuration.AethernetToken) ? null : configuration.AethernetToken;

    public bool IsSignedIn => Token is not null;

    public UserDto? CurrentUser { get; private set; }

    public event Action? Changed;

    public void SignIn(string token, UserDto user)
    {
        configuration.AethernetToken = token;
        CurrentUser = user;
        configuration.Save();
        Changed?.Invoke();
    }

    public void SetUser(UserDto user)
    {
        CurrentUser = user;
        Changed?.Invoke();
    }

    public void SignOut()
    {
        configuration.AethernetToken = string.Empty;
        CurrentUser = null;
        configuration.Save();
        Changed?.Invoke();
    }
}
