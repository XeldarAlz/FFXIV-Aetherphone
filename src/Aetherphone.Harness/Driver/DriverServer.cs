using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Aetherphone.Core;
using Aetherphone.Harness.Fakes;
using Aetherphone.Harness.Host;

namespace Aetherphone.Harness.Driver;

internal sealed class DriverServer
{
    private const int DefaultSettleFrames = 12;
    private const int DefaultDragFrames = 12;
    private const float MinimumLiveDelta = 1f / 90f;
    private const float MaximumLiveDelta = 1f / 10f;
    private const float FirstLiveDelta = 1f / 60f;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly PhoneHost host;
    private readonly HttpListener listener = new();
    private readonly BlockingCollection<Job> jobs = new();
    private readonly string infoPath;
    private readonly int port;
    private long lastLiveTimestamp;
    private byte[]? latestFrame;
    private bool latestFrameRaw;
    private int latestOriginX;
    private int latestOriginY;
    private int latestWidth;
    private int latestHeight;

    public DriverServer(PhoneHost host, int port, string cacheDirectory)
    {
        this.host = host;
        this.port = port;
        infoPath = Path.Combine(cacheDirectory, "driver.json");
    }

    public void Run()
    {
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        File.WriteAllText(infoPath, JsonSerializer.Serialize(new { port, pid = Environment.ProcessId }, JsonOptions));
        var accept = new Thread(AcceptLoop) { IsBackground = true, Name = "driver-accept" };
        accept.Start();
        HarnessLog.Note($"driver listening on http://127.0.0.1:{port}/");
        foreach (var job in jobs.GetConsumingEnumerable())
        {
            Response response;
            try
            {
                response = Execute(job.Route, job.Query);
            }
            catch (Exception exception)
            {
                HarnessLog.Failure($"driver {job.Route}", exception);
                response = Json(500, new { ok = false, error = exception.Message });
            }

            job.Completion.TrySetResult(response);
            if (job.Route == "/quit")
            {
                break;
            }
        }

        listener.Stop();
        File.Delete(infoPath);
    }

    private void AcceptLoop()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ => Handle(context));
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var route = context.Request.Url?.AbsolutePath ?? "/";
        var query = context.Request.QueryString;
        var job = new Job(route, query, new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously));
        Response response;
        if (jobs.IsAddingCompleted)
        {
            response = Json(503, new { ok = false, error = "driver is shutting down" });
        }
        else
        {
            jobs.Add(job);
            response = job.Completion.Task.GetAwaiter().GetResult();
        }

        try
        {
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = response.ContentType;
            if (response.Headers is not null)
            {
                foreach (var header in response.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
            }

            context.Response.ContentLength64 = response.Body.Length;
            context.Response.OutputStream.Write(response.Body, 0, response.Body.Length);
            context.Response.Close();
        }
        catch (Exception exception)
        {
            HarnessLog.Note($"driver response failed: {exception.Message}");
        }
    }

    private Response Execute(string route, System.Collections.Specialized.NameValueCollection query)
    {
        switch (route)
        {
            case "/":
                return new Response(200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(ViewerPage.Html));
            case "/frame":
                var raw = query["format"] == "raw";
                var frame = LiveFrame(query["full"] is null, raw);
                return new Response(200, raw ? "application/octet-stream" : "image/png", frame, new Dictionary<string, string>
                {
                    ["X-Phone-X"] = latestOriginX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["X-Phone-Y"] = latestOriginY.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["X-Width"] = latestWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["X-Height"] = latestHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
            case "/event":
                return ApplyEvent(query);
            case "/home":
                host.GoHome();
                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/state":
                return Json(200, State());
            case "/step":
                host.Step(Int(query, "frames", 1));
                return Json(200, State());
            case "/shot":
                host.Step(Int(query, "frames", 1));
                return new Response(200, "image/png", host.ScreenshotPng(query["full"] is null));
            case "/tap":
                return Tap(query);
            case "/drag":
                host.Drag(Point(query, "x1", "y1"), Point(query, "x2", "y2"), Int(query, "frames", DefaultDragFrames), Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/scroll":
                host.Scroll(Point(query, "x", "y"), Float(query, "dy", -1f), Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/type":
                host.TypeText(query["text"] ?? string.Empty, Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/key":
                return host.PressKey(query["name"] ?? string.Empty, Int(query, "settle", DefaultSettleFrames))
                    ? Json(200, State())
                    : Json(400, new { ok = false, error = $"unknown key '{query["name"]}'" });
            case "/open":
                host.OpenPhone();
                if (query["app"] is { Length: > 0 } app)
                {
                    host.OpenApp(app);
                }

                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/settings":
                host.OpenSettings();
                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/anchors":
                return Json(200, Anchors());
            case "/log":
                return Json(200, new { ok = true, latest = HarnessLog.LatestSequence, lines = HarnessLog.Since(Long(query, "since", 0)) });
            case "/login":
                host.Login();
                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/logout":
                host.Logout();
                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(200, State());
            case "/command":
                var handled = host.RunCommand(query["text"] ?? string.Empty);
                host.Step(Int(query, "settle", DefaultSettleFrames));
                return Json(handled ? 200 : 404, new { ok = handled, state = State() });
            case "/quit":
                jobs.CompleteAdding();
                return Json(200, new { ok = true });
            default:
                return Json(404, new { ok = false, error = $"unknown route {route}" });
        }
    }

    private byte[] LiveFrame(bool cropToPhone, bool raw)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = lastLiveTimestamp == 0 ? FirstLiveDelta : (float)((now - lastLiveTimestamp) / (double)Stopwatch.Frequency);
        if (latestFrame is null || latestFrameRaw != raw || elapsed >= MinimumLiveDelta)
        {
            host.Step(Math.Clamp(elapsed, MinimumLiveDelta, MaximumLiveDelta));
            lastLiveTimestamp = now;
            latestFrameRaw = raw;
            latestFrame = raw
                ? host.ScreenshotRaw(cropToPhone, out latestWidth, out latestHeight, out latestOriginX, out latestOriginY)
                : host.ScreenshotPng(cropToPhone, true, out latestOriginX, out latestOriginY);
            if (!raw)
            {
                latestWidth = 0;
                latestHeight = 0;
            }
        }

        return latestFrame;
    }

    private Response ApplyEvent(System.Collections.Specialized.NameValueCollection query)
    {
        switch (query["kind"])
        {
            case "move":
                host.MouseMove(Point(query, "x", "y"));
                break;
            case "leave":
                host.MouseLeave();
                break;
            case "button":
                host.MouseButton(Int(query, "button", 0), Int(query, "down", 0) != 0);
                break;
            case "wheel":
                host.MouseWheel(Float(query, "dx", 0f), Float(query, "dy", 0f));
                break;
            case "key":
                host.KeyEvent(query["name"] ?? string.Empty, Int(query, "down", 0) != 0);
                break;
            case "text":
                host.TextInput(query["text"] ?? string.Empty);
                break;
            default:
                return Json(400, new { ok = false, error = $"unknown event kind '{query["kind"]}'" });
        }

        return Json(200, new { ok = true });
    }

    private Response Tap(System.Collections.Specialized.NameValueCollection query)
    {
        Vector2 screen;
        if (query["anchor"] is { Length: > 0 } anchor)
        {
            if (!host.TryFindAnchor(anchor, out var rect))
            {
                return Json(404, new { ok = false, error = $"anchor '{anchor}' is not on screen", anchors = AnchorNames() });
            }

            screen = rect.Center;
        }
        else
        {
            screen = Point(query, "x", "y");
        }

        host.Tap(screen, Int(query, "button", 0), Int(query, "settle", DefaultSettleFrames));
        return Json(200, State());
    }

    private Vector2 Point(System.Collections.Specialized.NameValueCollection query, string xName, string yName)
    {
        var point = new Vector2(Float(query, xName, 0f), Float(query, yName, 0f));
        return query["space"] == "screen" ? point : point + host.PhoneRect.Min;
    }

    private object State()
    {
        var rect = host.PhoneRect;
        return new
        {
            ok = true,
            frame = host.FrameIndex,
            phoneOpen = host.PhoneOpen,
            currentApp = host.CurrentAppId,
            minimizePhase = host.MinimizePhase,
            homeEditing = host.HomeEditing,
            loggedIn = host.IsLoggedIn,
            gameData = host.HasGameData,
            phone = new { x = rect.Min.X, y = rect.Min.Y, width = rect.Width, height = rect.Height },
            display = new { width = host.Width, height = host.Height },
            latestLog = HarnessLog.LatestSequence,
        };
    }

    private object Anchors()
    {
        var origin = host.PhoneRect.Min;
        var entries = host.Anchors();
        var list = new List<object>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var rect = entries[index].Value;
            var local = rect.Translate(-origin);
            list.Add(new
            {
                name = entries[index].Key,
                x = local.Min.X,
                y = local.Min.Y,
                width = local.Width,
                height = local.Height,
                centerX = local.Center.X,
                centerY = local.Center.Y,
            });
        }

        return new { ok = true, anchors = list };
    }

    private List<string> AnchorNames()
    {
        var entries = host.Anchors();
        var names = new List<string>(entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            names.Add(entries[index].Key);
        }

        return names;
    }

    private static int Int(System.Collections.Specialized.NameValueCollection query, string name, int fallback) =>
        int.TryParse(query[name], out var value) ? value : fallback;

    private static long Long(System.Collections.Specialized.NameValueCollection query, string name, long fallback) =>
        long.TryParse(query[name], out var value) ? value : fallback;

    private static float Float(System.Collections.Specialized.NameValueCollection query, string name, float fallback) =>
        float.TryParse(query[name], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static Response Json(int status, object payload) =>
        new(status, "application/json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions)));

    private readonly record struct Job(string Route, System.Collections.Specialized.NameValueCollection Query, TaskCompletionSource<Response> Completion);

    private readonly record struct Response(int Status, string ContentType, byte[] Body, Dictionary<string, string>? Headers = null);
}
