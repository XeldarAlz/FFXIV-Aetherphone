namespace Aetherphone.Core.Casino;

internal enum KnockStage : byte
{
    None,
    Waiting,
    Admitted,
    Denied,
    Refused,
}

internal sealed record CasinoKnockStatus(string RoomId, KnockStage Stage, string Reason)
{
    public static readonly CasinoKnockStatus Idle = new(string.Empty, KnockStage.None, string.Empty);
}

internal static class CasinoKnocks
{
    public static bool AsksToJoin(string reason)
    {
        return Is(reason, CasinoReasons.InviteOnly) || Is(reason, CasinoReasons.NotMember)
            || Is(reason, CasinoReasons.Denied);
    }

    public static bool AtTheDoor(string reason)
    {
        return AsksToJoin(reason) || Is(reason, CasinoReasons.KnockPending);
    }

    public static KnockStage FromDecline(string reason)
    {
        if (Is(reason, CasinoReasons.KnockPending))
        {
            return KnockStage.Waiting;
        }

        if (Is(reason, CasinoReasons.Denied))
        {
            return KnockStage.Denied;
        }

        return Is(reason, CasinoReasons.Kicked) || Is(reason, CasinoReasons.BannedFromTable)
            ? KnockStage.Refused
            : KnockStage.None;
    }

    public static KnockStage FromKnock(KnockStage current, bool granted, string reason)
    {
        if (granted)
        {
            return reason.Length == 0 ? KnockStage.Admitted : KnockStage.Waiting;
        }

        if (Is(reason, CasinoReasons.Denied))
        {
            return KnockStage.Denied;
        }

        if (Is(reason, CasinoReasons.Cooldown))
        {
            return current == KnockStage.Denied ? KnockStage.Denied : KnockStage.Waiting;
        }

        return KnockStage.Refused;
    }

    public static KnockStage FromCard(KnockStage current, bool admitted, string reason)
    {
        if (admitted)
        {
            return KnockStage.Admitted;
        }

        if (Is(reason, CasinoReasons.KnockPending))
        {
            return KnockStage.Waiting;
        }

        if (Is(reason, CasinoReasons.Denied))
        {
            return KnockStage.Denied;
        }

        if (Is(reason, CasinoReasons.InviteOnly) || Is(reason, CasinoReasons.NotMember))
        {
            // A server that predates kept denials deletes a denied knock, so a waiting knock that
            // vanished without a seat was turned away.
            return current == KnockStage.Waiting ? KnockStage.Denied : KnockStage.None;
        }

        return KnockStage.Refused;
    }

    private static bool Is(string reason, string expected)
    {
        return string.Equals(reason, expected, StringComparison.Ordinal);
    }
}
