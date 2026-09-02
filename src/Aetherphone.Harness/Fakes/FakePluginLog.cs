using Dalamud.Plugin.Services;
using Serilog;
using Serilog.Events;

namespace Aetherphone.Harness.Fakes;

internal sealed class FakePluginLog : IPluginLog
{
    public ILogger Logger => Log.Logger;

    public LogEventLevel MinimumLogLevel { get; set; } = LogEventLevel.Debug;

    public void Fatal(string messageTemplate, params object[] values) => Write(LogEventLevel.Fatal, null, messageTemplate, values);

    public void Fatal(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Fatal, exception, messageTemplate, values);

    public void Error(string messageTemplate, params object[] values) => Write(LogEventLevel.Error, null, messageTemplate, values);

    public void Error(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Error, exception, messageTemplate, values);

    public void Warning(string messageTemplate, params object[] values) => Write(LogEventLevel.Warning, null, messageTemplate, values);

    public void Warning(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Warning, exception, messageTemplate, values);

    public void Information(string messageTemplate, params object[] values) => Write(LogEventLevel.Information, null, messageTemplate, values);

    public void Information(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Information, exception, messageTemplate, values);

    public void Info(string messageTemplate, params object[] values) => Write(LogEventLevel.Information, null, messageTemplate, values);

    public void Info(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Information, exception, messageTemplate, values);

    public void Debug(string messageTemplate, params object[] values) => Write(LogEventLevel.Debug, null, messageTemplate, values);

    public void Debug(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Debug, exception, messageTemplate, values);

    public void Verbose(string messageTemplate, params object[] values) => Write(LogEventLevel.Verbose, null, messageTemplate, values);

    public void Verbose(Exception? exception, string messageTemplate, params object[] values) => Write(LogEventLevel.Verbose, exception, messageTemplate, values);

    public void Write(LogEventLevel level, Exception? exception, string messageTemplate, params object[] values)
    {
        if (level < MinimumLogLevel)
        {
            return;
        }

        var text = values.Length == 0 ? messageTemplate : messageTemplate + " " + string.Join(" ", values);
        if (exception is not null)
        {
            text += Environment.NewLine + exception;
        }

        HarnessLog.Plugin(level.ToString(), text);
    }
}
