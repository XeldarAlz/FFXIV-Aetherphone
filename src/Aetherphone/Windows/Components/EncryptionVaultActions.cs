using Aetherphone.Core;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Crypto;
using Aetherphone.Core.Localization;

namespace Aetherphone.Windows.Components;

internal sealed class EncryptionVaultActions : IDisposable
{
    private readonly KeyVault vault;
    private readonly ConfirmService confirm;
    private readonly CancellationTokenSource cancellation = new();
    private volatile string status = string.Empty;
    private volatile bool busy;
    private volatile string generatedCode = string.Empty;

    public string CodeEntry = string.Empty;

    public EncryptionVaultActions(KeyVault vault, ConfirmService confirm)
    {
        this.vault = vault;
        this.confirm = confirm;
    }

    public KeyVault Vault => vault;

    public bool Busy => busy;

    public string GeneratedCode => generatedCode;

    public string Status
    {
        get => status;
        set => status = value;
    }

    public void AcknowledgeGeneratedCode()
    {
        generatedCode = string.Empty;
        status = string.Empty;
    }

    public void AskReset()
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Encryption.ForgotBody),
            ConfirmLabel = Loc.T(L.Encryption.ForgotConfirm),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = true,
            ConfirmAsync = done =>
            {
                Reset();
                done(true);
            },
        });
    }

    public void BeginCreateRecoveryCode()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var code = await vault.CreateRecoveryCodeAsync(cancellation.Token).ConfigureAwait(false);
                if (code is not null)
                {
                    generatedCode = code;
                    status = string.Empty;
                }
                else
                {
                    status = Loc.T(L.Encryption.Failed);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Recovery code setup failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void BeginRecover()
    {
        var code = CodeEntry;
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var recovered = await vault.RecoverWithCodeAsync(code, cancellation.Token).ConfigureAwait(false);
                if (recovered)
                {
                    CodeEntry = string.Empty;
                    status = string.Empty;
                }
                else
                {
                    status = Loc.T(L.Encryption.RecoveryWrongCode);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Recovery failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    private void Reset()
    {
        busy = true;
        status = Loc.T(L.Encryption.Working);
        _ = Task.Run(async () =>
        {
            try
            {
                var succeeded = await vault.ResetAsync(cancellation.Token).ConfigureAwait(false);
                status = succeeded ? string.Empty : Loc.T(L.Encryption.Failed);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Encryption reset failed: {exception.Message}");
                status = Loc.T(L.Encryption.Failed);
            }
            finally
            {
                busy = false;
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
