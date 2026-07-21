namespace Aetherphone.Core;

internal static class AepLog
{
    public static void Debug(string message) => Plugin.Log.Debug(message);

    public static void Info(string message) => Plugin.Log.Information(message);

    public static void Warning(string message) => Plugin.Log.Warning(message);

    public static void Error(string message) => Plugin.Log.Error(message);
}
