using System.Net.WebSockets;
using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Telephony;

internal sealed class RealtimeConnection : IDisposable
{
    private const int ReceiveBufferBytes = 16 * 1024;
    private static readonly TimeSpan HealthyConnectionThreshold = TimeSpan.FromSeconds(60);
    private readonly AethernetSession session;
    private readonly object gate = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private CancellationTokenSource? lifetime;
    private ClientWebSocket? socket;
    private volatile bool connected;

    public RealtimeConnection(AethernetSession session)
    {
        this.session = session;
    }

    public bool Connected => connected;
    public event Action<CallControl>? ControlReceived;
    public event Action<byte[]>? MediaReceived;
    public event Action<bool>? ConnectedChanged;

    public void Start()
    {
        lock (gate)
        {
            if (lifetime is not null)
            {
                return;
            }

            lifetime = new CancellationTokenSource();
            var token = lifetime.Token;
            _ = Task.Run(() => RunAsync(token));
        }
    }

    public void Stop()
    {
        CancellationTokenSource? toCancel;
        ClientWebSocket? toAbort;
        lock (gate)
        {
            toCancel = lifetime;
            lifetime = null;
            toAbort = socket;
            socket = null;
        }

        toCancel?.Cancel();
        try
        {
            toAbort?.Abort();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Realtime abort failed: {exception.Message}");
        }

        toAbort?.Dispose();
        toCancel?.Dispose();
        SetConnected(false);
    }

    private async Task RunAsync(CancellationToken token)
    {
        var attempt = 0;
        while (!token.IsCancellationRequested)
        {
            var connectedAtUtc = DateTime.MinValue;
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
                ws.Options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
                var bearer = session.Token;
                if (!string.IsNullOrEmpty(bearer))
                {
                    ws.Options.SetRequestHeader("Authorization", $"Bearer {bearer}");
                }

                await ws.ConnectAsync(BuildUri(session.BaseUrl), token).ConfigureAwait(false);
                lock (gate)
                {
                    socket = ws;
                }

                connectedAtUtc = DateTime.UtcNow;
                SetConnected(true);
                await ReceiveLoopAsync(ws, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Realtime connection error: {exception.Message}");
            }
            finally
            {
                lock (gate)
                {
                    socket = null;
                }

                SetConnected(false);
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            var heldLongEnough = connectedAtUtc != DateTime.MinValue
                && DateTime.UtcNow - connectedAtUtc >= HealthyConnectionThreshold;
            attempt = heldLongEnough ? 1 : attempt + 1;
            var seconds = attempt == 1 ? 0.5d : Math.Min(15d, Math.Pow(2, Math.Min(attempt - 1, 4)));
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[ReceiveBufferBytes];
        using var message = new MemoryStream();
        while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            message.SetLength(0);
            ValueWebSocketReceiveResult chunk;
            do
            {
                chunk = await ws.ReceiveAsync(buffer.AsMemory(), token).ConfigureAwait(false);
                if (chunk.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                message.Write(buffer, 0, chunk.Count);
            } while (!chunk.EndOfMessage);

            if (chunk.MessageType == WebSocketMessageType.Text)
            {
                DispatchControl(message.GetBuffer(), (int)message.Length);
            }
            else
            {
                MediaReceived?.Invoke(message.ToArray());
            }
        }
    }

    private void DispatchControl(byte[] data, int length)
    {
        try
        {
            var control = JsonSerializer.Deserialize(new ReadOnlySpan<byte>(data, 0, length),
                TelephonyJsonContext.Default.CallControl);
            if (control is not null)
            {
                ControlReceived?.Invoke(control);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Realtime control parse failed: {exception.Message}");
        }
    }

    public Task SendControlAsync(CallControl control)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(control, TelephonyJsonContext.Default.CallControl);
        return SendAsync(payload, WebSocketMessageType.Text);
    }

    public Task SendMediaAsync(byte[] frame)
    {
        return SendAsync(frame, WebSocketMessageType.Binary);
    }

    private async Task SendAsync(byte[] payload, WebSocketMessageType type)
    {
        ClientWebSocket? ws;
        lock (gate)
        {
            ws = socket;
        }

        if (ws is null || ws.State != WebSocketState.Open)
        {
            if (type == WebSocketMessageType.Text)
            {
                AepLog.Warning($"[realtime] send-dropped socket={(ws is null ? "none" : ws.State.ToString())}");
            }

            return;
        }

        await sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await ws.SendAsync(payload, type, true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Realtime send failed: {exception.Message}");
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void SetConnected(bool value)
    {
        if (connected == value)
        {
            return;
        }

        connected = value;
        ConnectedChanged?.Invoke(value);
    }

    private static Uri BuildUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "wss://" + trimmed.Substring("https://".Length);
        }
        else if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "ws://" + trimmed.Substring("http://".Length);
        }

        return new Uri(trimmed + "/rt");
    }

    public void Dispose()
    {
        Stop();
        sendLock.Dispose();
    }
}
