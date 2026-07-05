using System;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Feedback;

internal sealed class FeedbackStore : IDisposable
{
    private readonly AethernetSession session;
    private readonly AethernetClient client;
    private readonly CancellationTokenSource cancellation = new();

    private volatile bool posting;

    public bool IsSignedIn => session.IsSignedIn;
    public bool Posting => posting;
    public UserDto? Me => session.CurrentUser;

    public FeedbackStore(AethernetSession session, AethernetClient client)
    {
        this.session = session;
        this.client = client;
    }

    public void Compose(string text, Action<bool> onComplete)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || posting)
        {
            return;
        }

        posting = true;
        var token = cancellation.Token;
        _ = Task.Run(async () =>
        {
            var succeeded = false;
            try
            {
                var created = await client.CreateFeedbackAsync(trimmed, token).ConfigureAwait(false);
                succeeded = created is not null;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning($"[Feedback] compose failed: {exception.Message}");
            }
            finally
            {
                posting = false;
                onComplete(succeeded);
            }
        });
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
