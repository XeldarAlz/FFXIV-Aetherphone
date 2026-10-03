namespace Aetherphone.Apps.Activity;

internal enum ActivityViewKind : byte
{
    Summary,
    Day,
    Goals,
}

internal readonly record struct ActivityView(ActivityViewKind Kind)
{
    public static ActivityView Summary() => new(ActivityViewKind.Summary);

    public static ActivityView Day() => new(ActivityViewKind.Day);

    public static ActivityView Goals() => new(ActivityViewKind.Goals);
}
