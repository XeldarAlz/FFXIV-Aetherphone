namespace Aetherphone.Apps.Jobs;

internal enum JobsViewKind : byte
{
    Root,
    Detail,
}

internal readonly record struct JobsView(JobsViewKind Kind, uint ClassJobId)
{
    public static JobsView Root() => new(JobsViewKind.Root, 0);

    public static JobsView Detail(uint classJobId) => new(JobsViewKind.Detail, classJobId);
}
