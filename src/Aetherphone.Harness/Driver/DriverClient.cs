using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Aetherphone.Harness.Driver;

internal static class DriverClient
{
    private const int DefaultPort = 47821;

    public static int Run(string[] arguments, string cacheDirectory)
    {
        var command = arguments[0];
        var rest = new List<string>();
        var port = ResolvePort(cacheDirectory);
        for (var index = 1; index < arguments.Length; index++)
        {
            if (arguments[index] == "--port" && index + 1 < arguments.Length)
            {
                port = int.Parse(arguments[index + 1], CultureInfo.InvariantCulture);
                index += 1;
                continue;
            }

            rest.Add(arguments[index]);
        }

        var query = new List<KeyValuePair<string, string>>();
        string? outputPath = null;
        switch (command)
        {
            case "step":
                query.Add(new("frames", rest.Count > 0 ? rest[0] : "1"));
                break;
            case "shot":
                for (var index = 0; index < rest.Count; index++)
                {
                    if (rest[index] == "--full")
                    {
                        query.Add(new("full", "1"));
                    }
                    else if (rest[index] == "--frames" && index + 1 < rest.Count)
                    {
                        query.Add(new("frames", rest[index + 1]));
                        index += 1;
                    }
                    else
                    {
                        outputPath = rest[index];
                    }
                }

                outputPath ??= Path.Combine(Environment.CurrentDirectory, "phone.png");
                break;
            case "tap":
                if (rest.Count == 0)
                {
                    return Usage("tap X Y | tap ANCHOR");
                }

                if (float.TryParse(rest[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    query.Add(new("x", rest[0]));
                    query.Add(new("y", rest.Count > 1 ? rest[1] : "0"));
                    AddOptions(rest, 2, query);
                }
                else
                {
                    query.Add(new("anchor", rest[0]));
                    AddOptions(rest, 1, query);
                }

                break;
            case "drag":
                if (rest.Count < 4)
                {
                    return Usage("drag X1 Y1 X2 Y2 [--frames N]");
                }

                query.Add(new("x1", rest[0]));
                query.Add(new("y1", rest[1]));
                query.Add(new("x2", rest[2]));
                query.Add(new("y2", rest[3]));
                AddOptions(rest, 4, query);
                break;
            case "scroll":
                if (rest.Count < 3)
                {
                    return Usage("scroll X Y DY");
                }

                query.Add(new("x", rest[0]));
                query.Add(new("y", rest[1]));
                query.Add(new("dy", rest[2]));
                AddOptions(rest, 3, query);
                break;
            case "type":
                query.Add(new("text", string.Join(' ', rest)));
                break;
            case "key":
                query.Add(new("name", rest.Count > 0 ? rest[0] : string.Empty));
                break;
            case "open":
                if (rest.Count > 0)
                {
                    query.Add(new("app", rest[0]));
                }

                break;
            case "command":
                query.Add(new("text", string.Join(' ', rest)));
                break;
            case "log":
                AddOptions(rest, 0, query);
                break;
            case "url":
                Console.WriteLine($"http://127.0.0.1:{port}/");
                return 0;
            case "state":
            case "anchors":
            case "settings":
            case "home":
            case "login":
            case "logout":
            case "quit":
                break;
            default:
                return Usage("url | state | step [N] | shot [PATH] [--full] | tap X Y | tap ANCHOR | drag X1 Y1 X2 Y2 | scroll X Y DY | type TEXT | key NAME | open [APP] | home | settings | anchors | log [--since N] | command TEXT | login | logout | quit");
        }

        var url = BuildUrl(port, command, query);
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        HttpResponseMessage response;
        try
        {
            response = client.GetAsync(url).GetAwaiter().GetResult();
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine($"No harness driver on port {port} ({exception.Message}). Start one with: serve");
            return 3;
        }

        var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        if (outputPath is not null && response.IsSuccessStatusCode)
        {
            File.WriteAllBytes(outputPath, body);
            Console.WriteLine(outputPath);
            return 0;
        }

        Console.WriteLine(System.Text.Encoding.UTF8.GetString(body));
        return response.IsSuccessStatusCode ? 0 : 1;
    }

    private static void AddOptions(List<string> rest, int start, List<KeyValuePair<string, string>> query)
    {
        for (var index = start; index < rest.Count; index++)
        {
            if (!rest[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= rest.Count)
            {
                continue;
            }

            query.Add(new(rest[index][2..], rest[index + 1]));
            index += 1;
        }
    }

    private static string BuildUrl(int port, string command, List<KeyValuePair<string, string>> query)
    {
        var builder = new System.Text.StringBuilder($"http://127.0.0.1:{port}/{command}");
        for (var index = 0; index < query.Count; index++)
        {
            builder.Append(index == 0 ? '?' : '&');
            builder.Append(WebUtility.UrlEncode(query[index].Key));
            builder.Append('=');
            builder.Append(WebUtility.UrlEncode(query[index].Value));
        }

        return builder.ToString();
    }

    private static int ResolvePort(string cacheDirectory)
    {
        var infoPath = Path.Combine(cacheDirectory, "driver.json");
        if (!File.Exists(infoPath))
        {
            return DefaultPort;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(infoPath));
            return document.RootElement.GetProperty("port").GetInt32();
        }
        catch (JsonException)
        {
            return DefaultPort;
        }
    }

    private static int Usage(string usage)
    {
        Console.Error.WriteLine("Usage: " + usage);
        return 2;
    }
}
