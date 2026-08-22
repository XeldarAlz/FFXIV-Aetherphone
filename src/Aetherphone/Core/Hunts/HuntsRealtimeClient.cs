using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Aetherphone.Core.Hunts;

internal sealed class HuntsRealtimeClient : IDisposable
{
    private const int ReceiveBufferBytes = 16 * 1024;
    private const int MaxMessageBytes = 1024 * 1024;
    private const string SocketUrl = "wss://faloop.app/comms/socket.io/?EIO=4&transport=websocket";
    private static readonly TimeSpan HealthyConnectionThreshold = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NoSessionRetryDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IdleReceiveTimeout = TimeSpan.FromSeconds(90);

    private readonly HuntsAuthTokenStore tokens;
    private readonly object gate = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private CancellationTokenSource? lifetime;
    private ClientWebSocket? socket;
    private volatile bool connected;

    public HuntsRealtimeClient(HuntsAuthTokenStore tokens)
    {
        this.tokens = tokens;
    }

    public bool Connected => connected;
    public event Action<HuntsSocketMobReport>? MobReportReceived;
    public event Action<HuntLogEntryDto>? MobWorldKillReceived;
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

    public void Restart()
    {
        Stop();
        Start();
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
            AepLog.Warning(exception, "Hunts realtime abort failed");
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
            var sessionId = tokens.SessionId;
            if (string.IsNullOrEmpty(sessionId))
            {
                try
                {
                    await Task.Delay(NoSessionRetryDelay, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            var connectedAtUtc = DateTime.MinValue;
            try
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(SocketUrl), token).ConfigureAwait(false);
                lock (gate)
                {
                    socket = ws;
                }

                await HandshakeAsync(ws, sessionId, token).ConfigureAwait(false);
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
                AepLog.Warning(exception, "Hunts realtime connection error");
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
            var seconds = attempt == 1
                ? 0.5d + Random.Shared.NextDouble() * 4.5d
                : Math.Min(15d, Math.Pow(2, Math.Min(attempt - 1, 4))) * (0.5d + Random.Shared.NextDouble());
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

    private async Task HandshakeAsync(ClientWebSocket ws, string sessionId, CancellationToken token)
    {
        var open = await ReceiveTextAsync(ws, token).ConfigureAwait(false);
        if (open is not { Length: > 0 } || open[0] != '0')
        {
            throw new InvalidOperationException("Hunts realtime handshake did not open");
        }

        var payload = JsonSerializer.Serialize(new HuntsSocketConnectPayload { SessionId = sessionId },
            HuntsJsonContext.Default.HuntsSocketConnectPayload);
        await SendRawAsync(ws, "40" + payload, token).ConfigureAwait(false);

        var ack = await ReceiveTextAsync(ws, token).ConfigureAwait(false);
        if (ack is null || !ack.StartsWith("40", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Hunts realtime handshake was not acknowledged");
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken token)
    {
        while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            var frame = await ReceiveTextAsync(ws, token).ConfigureAwait(false);
            if (frame is null)
            {
                return;
            }

            await HandleFrameAsync(ws, frame, token).ConfigureAwait(false);
        }
    }

    private async Task HandleFrameAsync(ClientWebSocket ws, string frame, CancellationToken token)
    {
        if (frame.Length == 0)
        {
            return;
        }

        if (frame[0] == '2')
        {
            await SendRawAsync(ws, "3", token).ConfigureAwait(false);
            return;
        }

        if (frame[0] != '4' || frame.Length < 2)
        {
            return;
        }

        if (frame[1] != '2')
        {
            return;
        }

        DispatchEvent(frame.AsSpan(2));
    }

    private void DispatchEvent(ReadOnlySpan<char> json)
    {
        try
        {
            using var document = JsonDocument.Parse(json.ToString());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 2)
            {
                return;
            }

            if (!string.Equals(root[0].GetString(), "message", StringComparison.Ordinal))
            {
                return;
            }

            var payload = root[1];
            var type = payload.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            switch (type)
            {
                case "mob":
                    DispatchMobReport(payload);
                    return;
                case "mobworldkill":
                    DispatchMobWorldKill(payload);
                    return;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Hunts realtime event parse failed");
        }
    }

    private void DispatchMobReport(JsonElement payload)
    {
        var message = JsonSerializer.Deserialize(payload.GetRawText(), HuntsJsonContext.Default.HuntsSocketMessage);
        if (message is not { Type: "mob", Data: { } report })
        {
            return;
        }

        MobReportReceived?.Invoke(report);
    }

    private void DispatchMobWorldKill(JsonElement payload)
    {
        if (!payload.TryGetProperty("subType", out var subTypeElement) ||
            !string.Equals(subTypeElement.GetString(), "recentAdd", StringComparison.Ordinal))
        {
            return;
        }

        if (!payload.TryGetProperty("data", out var dataElement))
        {
            return;
        }

        var entry = JsonSerializer.Deserialize(dataElement.GetRawText(), HuntsJsonContext.Default.HuntLogEntryDto);
        if (entry is not null)
        {
            MobWorldKillReceived?.Invoke(entry);
        }
    }

    private async Task<string?> ReceiveTextAsync(ClientWebSocket ws, CancellationToken token)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
        idle.CancelAfter(IdleReceiveTimeout);
        var buffer = new byte[ReceiveBufferBytes];
        using var message = new MemoryStream();
        ValueWebSocketReceiveResult chunk;
        try
        {
            do
            {
                chunk = await ws.ReceiveAsync(buffer.AsMemory(), idle.Token).ConfigureAwait(false);
                if (chunk.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                message.Write(buffer, 0, chunk.Count);
                if (message.Length > MaxMessageBytes)
                {
                    AepLog.Warning("Hunts realtime message exceeded the size limit; dropping the connection.");
                    return null;
                }
            } while (!chunk.EndOfMessage);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            AepLog.Warning("Hunts realtime connection went idle; dropping the connection.");
            return null;
        }

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    private async Task SendRawAsync(ClientWebSocket ws, string text, CancellationToken token)
    {
        var payload = Encoding.UTF8.GetBytes(text);
        await sendLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(SendTimeout);
            await ws.SendAsync(payload, WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            AepLog.Warning("Hunts realtime send timed out; dropping the connection.");
            ws.Abort();
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

    public void Dispose()
    {
        Stop();
        sendLock.Dispose();
    }
}
