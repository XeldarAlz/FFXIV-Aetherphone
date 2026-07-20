using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Game;
using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Aethernet;

internal sealed class CharacterSessionManager : IDisposable
{
    private readonly IFramework framework;
    private readonly AethernetSession session;
    private readonly AccountClient account;
    private readonly GameData gameData;
    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<ulong> legacyAttempted = new();
    private readonly HashSet<ulong> signInPromptShown = new();
    private ulong lastContentId;
    private ulong pendingPromptContentId;
    private volatile bool claiming;
    private bool hasHadAccount;
    private bool started;

    public CharacterSessionManager(IFramework framework, AethernetSession session, AccountClient account,
        GameData gameData, Configuration configuration, ConfirmService confirm)
    {
        this.framework = framework;
        this.session = session;
        this.account = account;
        this.gameData = gameData;
        this.configuration = configuration;
        this.confirm = confirm;
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        framework.Update += OnTick;
    }

    private void OnTick(IFramework _)
    {
        var contentId = InventoryReader.ReadLocalContentId();
        if (contentId != lastContentId)
        {
            lastContentId = contentId;
            pendingPromptContentId = 0;
            session.SwitchTo(contentId);
            if (contentId != 0)
            {
                if (session.IsSignedIn)
                {
                    hasHadAccount = true;
                    account.EnsureCurrentUser();
                }
                else if (session.LegacyClaimPending)
                {
                    TryClaimLegacy(contentId);
                }
                else
                {
                    pendingPromptContentId = contentId;
                }
            }

            return;
        }

        if (session.LegacyClaimPending)
        {
            TryClaimLegacy(contentId);
        }

        if (pendingPromptContentId != 0)
        {
            FlushSignInPrompt();
        }
    }

    private void FlushSignInPrompt()
    {
        var contentId = pendingPromptContentId;
        if (contentId == 0 || contentId != lastContentId || session.IsSignedIn || session.LegacyClaimPending)
        {
            pendingPromptContentId = 0;
            return;
        }

        if (!hasHadAccount || signInPromptShown.Contains(contentId))
        {
            pendingPromptContentId = 0;
            return;
        }

        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return;
        }

        pendingPromptContentId = 0;
        signInPromptShown.Add(contentId);
        confirm.Alert(Loc.T(L.Account.AltSignInTitle), Loc.T(L.Account.AltSignInBody, player.Name.TextValue),
            Loc.T(L.Account.FailDismiss));
    }

    private void TryClaimLegacy(ulong contentId)
    {
        if (claiming || contentId == 0 || legacyAttempted.Contains(contentId))
        {
            return;
        }

        if (gameData.LocalPlayer is null)
        {
            return;
        }

        var legacyToken = configuration.LegacyUnclaimedToken;
        if (legacyToken.Length == 0)
        {
            session.DiscardLegacyClaim();
            return;
        }

        claiming = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            UserDto? user = null;
            try
            {
                user = await account.MeWithBearerAsync(legacyToken, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                claiming = false;
                return;
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Legacy account claim failed: {exception.Message}");
            }

            var resolved = user;
            await framework.RunOnFrameworkThread(() =>
            {
                claiming = false;
                if (session.ActiveContentId != contentId || !session.LegacyClaimPending)
                {
                    return;
                }

                legacyAttempted.Add(contentId);
                if (resolved is not null && gameData.IsLocalPlayer(resolved.Name, resolved.World))
                {
                    session.AdoptLegacy(contentId, resolved);
                    hasHadAccount = true;
                }
                else
                {
                    session.DiscardLegacyClaim();
                    pendingPromptContentId = contentId;
                }
            }).ConfigureAwait(false);
        });
    }

    public void Dispose()
    {
        if (started)
        {
            framework.Update -= OnTick;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }
}
