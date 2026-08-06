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
    private readonly Queue<ConfirmRequest> queued = new();

    public ConfirmRequest? Active { get; private set; }
    public volatile bool Busy;
    public string? Status { get; private set; }

    public void Ask(ConfirmRequest request)
    {
        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            _ = Plugin.Framework.RunOnFrameworkThread(() => Ask(request));
            return;
        }

        if (Active is not null)
        {
            queued.Enqueue(request);
            return;
        }

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
                void Finish()
                {
                    Busy = false;
                    if (ok)
                    {
                        Advance();
                    }
                    else
                    {
                        Status = request.FailedMessage;
                    }
                }

                if (!Plugin.Framework.IsInFrameworkUpdateThread)
                {
                    _ = Plugin.Framework.RunOnFrameworkThread(Finish);
                    return;
                }

                Finish();
            });
            return;
        }

        request.Confirm?.Invoke();
        Advance();
    }

    public void CancelActive()
    {
        if (Busy || Active is not { } request)
        {
            return;
        }

        request.Cancel?.Invoke();
        Advance();
    }

    private void Advance()
    {
        Busy = false;
        Status = null;
        Active = queued.Count > 0 ? queued.Dequeue() : null;
    }
}
