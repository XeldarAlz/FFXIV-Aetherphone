using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class AethernetSession
{
    private readonly Configuration configuration;
    private readonly IFramework framework;
    private volatile bool tokenRejected;
    private volatile bool banned;
    private volatile string? banReason;
    private volatile SuspensionDto? suspension;
    private volatile bool sourceBlocked;
    private volatile bool sourceWarningShown;
    private volatile string? pendingSourceNotice;
    private ulong activeContentId;
    private ulong playingContentId;

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
    public bool IsBanned => banned;
    public string? BanReason => banReason;
    public SuspensionDto? Suspension => suspension;
    public bool IsSourceBlocked => sourceBlocked;
    public UserDto? CurrentUser { get; private set; }

    public ulong ActiveContentId => activeContentId;
    public ulong PlayingContentId => playingContentId;
    public bool FollowsCharacter => configuration.FollowCharacterAccount;
    public bool MatchesPlayedCharacter => playingContentId == 0 || playingContentId == activeContentId;
    public bool LegacyClaimPending { get; private set; }
    public string ManualRegion => ActiveSlot?.ManualRegion ?? string.Empty;

    public string AccountWorld
    {
        get
        {
            var world = CurrentUser?.World;
            return string.IsNullOrEmpty(world) ? ActiveSlot?.World ?? string.Empty : world;
        }
    }

    public event Action? Changed;

    public void SignIn(string token, UserDto user)
    {
        _ = framework.RunOnFrameworkThread(() =>
        {
            UseCharacterSlot();
            tokenRejected = false;
            banned = false;
            sourceBlocked = false;
            banReason = null;
        suspension = null;
            LegacyClaimPending = false;
            configuration.AethernetToken = token;
            CurrentUser = user;
            AdoptLegacyEncryptionKey(user);
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
            AdoptLegacyEncryptionKey(user);
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
            banned = false;
            sourceBlocked = false;
            banReason = null;
        suspension = null;
            LegacyClaimPending = false;
            configuration.AethernetToken = string.Empty;
            StashActive();
            CurrentUser = null;
            var wasPinned = !configuration.FollowCharacterAccount;
            configuration.FollowCharacterAccount = true;
            configuration.PinnedAccountContentId = 0;
            configuration.Save();
            Changed?.Invoke();
            if (wasPinned && playingContentId != activeContentId)
            {
                SwitchTo(playingContentId);
            }
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

    public void ReportSourceStatus(string status)
    {
        if (string.Equals(status, AethernetClientIdentity.StatusBlocked, StringComparison.Ordinal))
        {
            if (sourceBlocked)
            {
                return;
            }

            sourceBlocked = true;
            tokenRejected = true;
            pendingSourceNotice = status;
            AepLog.Warning("Aethernet refused this install source; reinstall Aetherphone from the official repository to sign in again.");
            _ = framework.RunOnFrameworkThread(() =>
            {
                CurrentUser = null;
                Changed?.Invoke();
            });
            return;
        }

        if (string.Equals(status, AethernetClientIdentity.StatusWarned, StringComparison.Ordinal) && !sourceWarningShown)
        {
            sourceWarningShown = true;
            pendingSourceNotice = status;
        }
    }

    public string? ConsumeSourceNotice()
    {
        var notice = pendingSourceNotice;
        if (notice is not null)
        {
            pendingSourceNotice = null;
        }

        return notice;
    }

    public void ReportBanned(string? reason, SuspensionDto? details = null)
    {
        banned = true;
        banReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        suspension = details;
        tokenRejected = true;
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
        banned = false;
        sourceBlocked = false;
        banReason = null;
        suspension = null;
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

    public void ReportPlayingCharacter(ulong contentId)
    {
        playingContentId = contentId;
    }

    public ulong ResolveTarget(ulong playing) =>
        AccountSelection.Target(configuration.CharacterSessions, configuration.FollowCharacterAccount,
            configuration.PinnedAccountContentId, playing);

    public void PinAccount(ulong contentId)
    {
        if (contentId == 0)
        {
            return;
        }

        configuration.FollowCharacterAccount = false;
        configuration.PinnedAccountContentId = contentId;
        if (contentId == activeContentId)
        {
            configuration.Save();
            return;
        }

        SwitchTo(contentId);
    }

    public void UseCharacterAccount()
    {
        configuration.FollowCharacterAccount = true;
        configuration.PinnedAccountContentId = 0;
        if (playingContentId == activeContentId)
        {
            configuration.Save();
            return;
        }

        SwitchTo(playingContentId);
    }

    public void ForgetAccount(ulong contentId)
    {
        if (contentId == 0 || contentId == activeContentId)
        {
            return;
        }

        if (configuration.CharacterSessions.TryGetValue(contentId, out var stored))
        {
            stored.Token = string.Empty;
        }

        if (configuration.PinnedAccountContentId == contentId)
        {
            configuration.PinnedAccountContentId = 0;
            configuration.FollowCharacterAccount = true;
        }

        configuration.Save();
    }

    public void SetManualRegion(string code)
    {
        var slot = ActiveSlot;
        if (slot is null)
        {
            return;
        }

        slot.ManualRegion = code;
        configuration.Save();
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
        suspension = null;
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
        _ = PersistActiveKeyCacheAsync();
    }

    public Task PersistActiveKeyCacheAsync()
    {
        return framework.RunOnFrameworkThread(() =>
        {
            StashActive();
            configuration.SaveNow();
        });
    }

    private CharacterSession? ActiveSlot =>
        activeContentId != 0 && configuration.CharacterSessions.TryGetValue(activeContentId, out var slot)
            ? slot
            : null;

    private void AdoptLegacyEncryptionKey(UserDto user)
    {
        if (configuration.EncryptionKeyCache.Length > 0
            || configuration.LegacyUnclaimedEncryptionKey.Length == 0
            || !string.Equals(configuration.LegacyUnclaimedEncryptionUserId, user.Id, StringComparison.Ordinal))
        {
            return;
        }

        configuration.EncryptionKeyCache = configuration.LegacyUnclaimedEncryptionKey;
        configuration.EncryptionKeyCacheUserId = configuration.LegacyUnclaimedEncryptionUserId;
        configuration.LegacyUnclaimedEncryptionKey = string.Empty;
        configuration.LegacyUnclaimedEncryptionUserId = string.Empty;
    }

    private void UseCharacterSlot()
    {
        configuration.FollowCharacterAccount = true;
        configuration.PinnedAccountContentId = 0;
        if (playingContentId == 0 || playingContentId == activeContentId)
        {
            return;
        }

        StashActive();
        activeContentId = playingContentId;
        if (configuration.CharacterSessions.TryGetValue(playingContentId, out var stored))
        {
            LoadFlat(stored);
        }
        else
        {
            ClearFlat();
        }
    }

    private void StashActive()
    {
        if (activeContentId == 0)
        {
            return;
        }

        var token = configuration.AethernetToken;
        var hasSnapshot = configuration.CharacterSessions.TryGetValue(activeContentId, out var snapshot);
        if (!hasSnapshot)
        {
            if (string.IsNullOrEmpty(token) && configuration.EncryptionKeyCache.Length == 0)
            {
                return;
            }

            snapshot = new CharacterSession();
            configuration.CharacterSessions[activeContentId] = snapshot;
        }

        snapshot!.Token = token;
        if (configuration.EncryptionKeyCache.Length > 0 && KeyBelongsToSnapshot(snapshot))
        {
            snapshot.EncryptionKeyCache = configuration.EncryptionKeyCache;
            snapshot.EncryptionKeyCacheUserId = configuration.EncryptionKeyCacheUserId;
        }

        var user = CurrentUser;
        if (user is not null)
        {
            snapshot.AccountId = user.Id;
            snapshot.Handle = user.Handle;
            snapshot.DisplayName = user.DisplayName;
            snapshot.CharacterName = user.Name;
            snapshot.World = user.World;
            snapshot.AvatarUrl = user.AvatarUrl ?? string.Empty;
        }
    }

    private bool KeyBelongsToSnapshot(CharacterSession snapshot)
    {
        var incomingUserId = configuration.EncryptionKeyCacheUserId;
        if (incomingUserId.Length == 0 || snapshot.EncryptionKeyCache.Length == 0)
        {
            return true;
        }

        var storedUserId = snapshot.EncryptionKeyCacheUserId;
        return storedUserId.Length == 0 || string.Equals(storedUserId, incomingUserId, StringComparison.Ordinal);
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
