namespace Aetherphone.Core.Confirm;

internal sealed class ConfirmRequest
{
    public string? Title;
    public required string Message;
    public required string ConfirmLabel;
    public required string CancelLabel;
    public string? BusyLabel;
    public string? FailedMessage;
    public bool Danger = true;
    public bool Acknowledge;
    public Action<Action<bool>>? ConfirmAsync;
    public Action? Confirm;
    public Action? Cancel;
}

internal sealed class ConfirmService
{
    public ConfirmRequest? Active { get; private set; }
    public volatile bool Busy;
    public string? Status { get; private set; }

    public void Ask(ConfirmRequest request)
    {
        Active = request;
        Busy = false;
        Status = null;
    }

    public void Alert(string? title, string message, string dismissLabel, Action? onDismiss = null)
    {
        Ask(new ConfirmRequest
        {
            Title = title,
            Message = message,
            ConfirmLabel = dismissLabel,
            CancelLabel = dismissLabel,
            Danger = false,
            Acknowledge = true,
            Confirm = onDismiss,
            Cancel = onDismiss,
        });
    }

    public void Proceed()
    {
        if (Active is not { } request || Busy)
        {
            return;
        }

        if (request.ConfirmAsync is { } handler)
        {
            Busy = true;
            Status = null;
            handler(ok =>
            {
                Busy = false;
                if (ok)
                {
                    Active = null;
                }
                else
                {
                    Status = request.FailedMessage;
                }
            });
            return;
        }

        request.Confirm?.Invoke();
        Active = null;
    }

    public void CancelActive()
    {
        if (Busy || Active is not { } request)
        {
            return;
        }

        request.Cancel?.Invoke();
        Active = null;
    }
}
