namespace Aetherphone.Core.ControlCenter;

internal readonly struct ControlDefault
{
    public readonly string ModuleId;
    public readonly ControlSpan Span;

    public ControlDefault(string moduleId, ControlSpan span)
    {
        ModuleId = moduleId;
        Span = span;
    }
}

internal static class ControlDefaults
{
    public const string ClusterId = "toggles";

    public static readonly string[] ClusterMembers = { "dnd", "silent", "calls", "idle" };

    public static readonly ControlDefault[] Layout =
    {
        new(ClusterId, ControlSpan.Large),
        new("media", ControlSpan.Large),
        new("brightness", ControlSpan.Tall),
        new("volume", ControlSpan.Tall),
        new("lock", ControlSpan.Small),
        new("settings", ControlSpan.Small),
        new("accent", ControlSpan.Wide),
    };
}
