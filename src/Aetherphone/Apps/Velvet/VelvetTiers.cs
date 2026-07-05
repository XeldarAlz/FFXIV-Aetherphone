using System.Numerics;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Velvet;

internal static class VelvetVisibility
{
    public const int Connections = 0;
    public const int Public = 1;
    public const int Unlockable = 2;

    public static string Label(int visibility) =>
        visibility switch
        {
            Public => Loc.T(L.Velvet.VisibilityPublic),
            Unlockable => Loc.T(L.Velvet.VisibilityUnlockable),
            _ => Loc.T(L.Velvet.VisibilityConnections),
        };
}

internal static class VelvetLookingFor
{
    public const int Any = 0;
    public const int Collab = 1;
    public const int Erp = 2;
    public const int Gpose = 3;
    public const int Sharing = 4;
    public const int Relationship = 5;
    public const int Friends = 6;
    public const int Wandering = 7;
    public static readonly int[] All = { Any, Erp, Gpose, Relationship, Collab, Friends, Sharing, Wandering };

    public static string Label(int value) =>
        value switch
        {
            Collab => Loc.T(L.Velvet.LookingCollab),
            Erp => Loc.T(L.Velvet.LookingErp),
            Gpose => Loc.T(L.Velvet.LookingGpose),
            Sharing => Loc.T(L.Velvet.LookingSharing),
            Relationship => Loc.T(L.Velvet.LookingRelationship),
            Friends => Loc.T(L.Velvet.LookingFriends),
            Wandering => Loc.T(L.Velvet.LookingWandering),
            _ => Loc.T(L.Velvet.LookingAny),
        };
}

internal static class VelvetPresence
{
    public const int Offline = 0;
    public const int Online = 1;
    public const int Away = 2;
    public const int Dnd = 3;

    public static string Label(int value) =>
        value switch
        {
            Online => Loc.T(L.Velvet.PresenceOnline),
            Away => Loc.T(L.Velvet.PresenceAway),
            Dnd => Loc.T(L.Velvet.PresenceDnd),
            _ => Loc.T(L.Velvet.PresenceOffline),
        };

    public static Vector4 Color(int value) =>
        value switch
        {
            Online => new Vector4(0.30f, 0.82f, 0.44f, 1f),
            Away => new Vector4(0.95f, 0.72f, 0.25f, 1f),
            Dnd => new Vector4(0.90f, 0.30f, 0.34f, 1f),
            _ => new Vector4(0.45f, 0.45f, 0.50f, 1f),
        };
}

internal static class VelvetRelationship
{
    public const int NotSaying = 0;
    public const int Single = 1;
    public const int Taken = 2;
    public const int Open = 3;
    public const int Complicated = 4;
    public static readonly int[] All = { NotSaying, Single, Taken, Open, Complicated };

    public static string Label(int value) =>
        value switch
        {
            Single => Loc.T(L.Velvet.RelSingle),
            Taken => Loc.T(L.Velvet.RelTaken),
            Open => Loc.T(L.Velvet.RelOpen),
            Complicated => Loc.T(L.Velvet.RelComplicated),
            _ => Loc.T(L.Velvet.RelNotSaying),
        };
}

internal static class VelvetConnectionState
{
    public const int None = 0;
    public const int OutgoingRequest = 1;
    public const int IncomingRequest = 2;
    public const int Connected = 3;
    public const int Blocked = 4;
}

internal static class VelvetTags
{
    public static string[] Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(parts.Length);
        for (var index = 0; index < parts.Length; index++)
        {
            var tag = parts[index].ToLowerInvariant();
            if (tag.Length > 0 && !result.Contains(tag))
            {
                result.Add(tag);
            }
        }

        return result.ToArray();
    }

    public static string Join(string[] tags) => tags.Length == 0 ? string.Empty : string.Join(", ", tags);
}
