using Aetherphone.Core.Aethernet.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetSession
{
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private volatile bool tokenRejected;
    private volatile bool hasDevAccess;
    private volatile bool banned;
    private volatile string? banReason;
    private ulong activeContentId;

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
    public bool HasDevAccess => hasDevAccess && IsSignedIn;
    public bool IsBanned => banned;
    public string? BanReason => banReason;
    public UserDto? CurrentUser { get; private set; }

    public ulong ActiveContentId => activeContentId;
    public bool LegacyClaimPending { get; private set; }

    public event Action? Changed;

    public void SetDevAccess(bool granted)
    {
        if (hasDevAccess == granted)
        {
            return;
        }

        hasDevAccess = granted;
        _ = framework.RunOnFrameworkThread(() => Changed?.Invoke());
    }

    public void SignIn(string token, UserDto user)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            tokenRejected = false;
            banned = false;
            banReason = null;
            LegacyClaimPending = false;
            configuration.AethernetToken = token;
            CurrentUser = user;
            StashActive();
            configuration.Save();
            Changed?.Invoke();
        });
    }

    public void SetUser(UserDto user)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            CurrentUser = user;
            StashActive();
            configuration.Save();
            Changed?.Invoke();
        });
    }

    public void SignOut()
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            tokenRejected = false;
            hasDevAccess = false;
            banned = false;
            banReason = null;
            LegacyClaimPending = false;
            if (activeContentId != 0)
            {
                configuration.CharacterSessions.Remove(activeContentId);
            }

            ClearFlat();
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
        hasDevAccess = false;
        AepLog.Warning("Aethernet token was rejected; sign in again to reconnect.");
        _ = framework.RunOnFrameworkThread(() =>
        {
            CurrentUser = null;
            Changed?.Invoke();
        });
    }

    public void ReportBanned(string? reason)
    {
        banned = true;
        banReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        tokenRejected = true;
        hasDevAccess = false;
        _ = framework.RunOnFrameworkThread(() =>
        {
            CurrentUser = null;
            Changed?.Invoke();
        });
    }

    public void SwitchTo(ulong contentId)
    {
        if (contentId == activeContentId)
        {
            return;
        }

        StashActive();
        activeContentId = contentId;
        tokenRejected = false;
        hasDevAccess = false;
        banned = false;
        banReason = null;
        CurrentUser = null;
        LegacyClaimPending = false;
        if (contentId != 0 && configuration.CharacterSessions.TryGetValue(contentId, out var stored))
        {
            LoadFlat(stored);
        }
        else
        {
            ClearFlat();
            LegacyClaimPending = contentId != 0 && configuration.LegacyUnclaimedToken.Length > 0;
        }

        configuration.Save();
        Changed?.Invoke();
    }

    public void AdoptLegacy(ulong contentId, UserDto user)
    {
        if (contentId != activeContentId || !LegacyClaimPending)
        {
            return;
        }

        configuration.AethernetToken = configuration.LegacyUnclaimedToken;
        configuration.EncryptionKeyCache = configuration.LegacyUnclaimedEncryptionKey;
        configuration.EncryptionKeyCacheUserId = configuration.LegacyUnclaimedEncryptionUserId;
        configuration.LegacyUnclaimedToken = string.Empty;
        configuration.LegacyUnclaimedEncryptionKey = string.Empty;
        configuration.LegacyUnclaimedEncryptionUserId = string.Empty;
        LegacyClaimPending = false;
        tokenRejected = false;
        banned = false;
        banReason = null;
        CurrentUser = user;
        StashActive();
        configuration.Save();
        Changed?.Invoke();
    }

    public void DiscardLegacyClaim()
    {
        LegacyClaimPending = false;
    }

    public void PersistActiveKeyCache()
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            StashActive();
            configuration.Save();
        });
    }

    private void StashActive()
    {
        if (activeContentId == 0)
        {
            return;
        }

        var token = configuration.AethernetToken;
        if (string.IsNullOrEmpty(token))
        {
            configuration.CharacterSessions.Remove(activeContentId);
            return;
        }

        if (!configuration.CharacterSessions.TryGetValue(activeContentId, out var snapshot))
        {
            snapshot = new CharacterSession();
            configuration.CharacterSessions[activeContentId] = snapshot;
        }

        snapshot.Token = token;
        snapshot.EncryptionKeyCache = configuration.EncryptionKeyCache;
        snapshot.EncryptionKeyCacheUserId = configuration.EncryptionKeyCacheUserId;
        var user = CurrentUser;
        if (user is not null)
        {
            snapshot.AccountId = user.Id;
            snapshot.Handle = user.Handle;
            snapshot.DisplayName = user.DisplayName;
            snapshot.CharacterName = user.Name;
            snapshot.World = user.World;
        }
    }

    private void LoadFlat(CharacterSession snapshot)
    {
        configuration.AethernetToken = snapshot.Token;
        configuration.EncryptionKeyCache = snapshot.EncryptionKeyCache;
        configuration.EncryptionKeyCacheUserId = snapshot.EncryptionKeyCacheUserId;
    }

    private void ClearFlat()
    {
        configuration.AethernetToken = string.Empty;
        configuration.EncryptionKeyCache = string.Empty;
        configuration.EncryptionKeyCacheUserId = string.Empty;
    }
}
