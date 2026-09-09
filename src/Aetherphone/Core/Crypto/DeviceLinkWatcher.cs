using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Crypto;

internal sealed class DeviceLinkWatcher : IDisposable
{
    private readonly KeyVault vault;
    private readonly AethernetSession session;
    private readonly ConfirmService confirm;
    private readonly RealtimeSignalBus signals;
    private readonly CancellationTokenSource cancellation = new();
    private readonly HashSet<string> handled = new(StringComparer.Ordinal);
    private float sincePoll;
    private volatile bool polling;
    private volatile bool prompting;
    private volatile bool probeRequested;
    private bool wasEligible;

    private const float OfflinePollSeconds = 300f;

    public DeviceLinkWatcher(KeyVault vault, AethernetSession session, ConfirmService confirm,
        RealtimeSignalBus signals)
    {
        this.vault = vault;
        this.session = session;
        this.confirm = confirm;
        this.signals = signals;
        signals.DeviceLinkRequested += OnDeviceLinkRequested;
        signals.ConnectedChanged += OnRealtimeConnectedChanged;
    }

    public void Tick(float deltaSeconds)
    {
        var eligible = session.IsSignedIn && vault.State == KeyVaultState.Unlocked;
        if (!eligible)
        {
            wasEligible = false;
            return;
        }

        if (!wasEligible)
        {
            wasEligible = true;
            probeRequested = true;
        }

        if (polling || prompting)
        {
            return;
        }

        sincePoll += deltaSeconds;
        if (!probeRequested && (signals.RealtimeActive || sincePoll < OfflinePollSeconds))
        {
            return;
        }

        probeRequested = false;
        sincePoll = 0f;
        polling = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var pending = await vault.PendingDeviceLinksAsync(cancellation.Token).ConfigureAwait(false);
                if (pending is null || pending.Items.Length == 0)
                {
                    return;
                }

                for (var index = 0; index < pending.Items.Length; index++)
                {
                    var item = pending.Items[index];
                    if (handled.Contains(item.Id))
                    {
                        continue;
                    }

                    handled.Add(item.Id);
                    Prompt(item.Id, item.VerificationCode, item.EphemeralPublicKey);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Checking for device link requests failed");
            }
            finally
            {
                polling = false;
            }
        });
    }

    private void OnDeviceLinkRequested()
    {
        probeRequested = true;
    }

    private void OnRealtimeConnectedChanged(bool connected)
    {
        if (connected)
        {
            probeRequested = true;
        }
    }

    private void Prompt(string requestId, string verificationCode, string ephemeralPublicKey)
    {
        prompting = true;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Encryption.LinkApproveTitle),
            Message = Loc.T(L.Encryption.LinkApproveBody, verificationCode),
            ConfirmLabel = Loc.T(L.Encryption.LinkApproveConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            ConfirmAsync = done =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await vault.ApproveDeviceLinkAsync(requestId, ephemeralPublicKey, cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        AepLog.Warning(exception, "Approving a device link failed");
                    }
                    finally
                    {
                        prompting = false;
                        probeRequested = true;
                        done(true);
                    }
                });
            },
            Danger = false,
            Cancel = () =>
            {
                prompting = false;
                probeRequested = true;
                vault.CancelDeviceLink(requestId);
            },
        });
    }

    public void Dispose()
    {
        signals.DeviceLinkRequested -= OnDeviceLinkRequested;
        signals.ConnectedChanged -= OnRealtimeConnectedChanged;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
